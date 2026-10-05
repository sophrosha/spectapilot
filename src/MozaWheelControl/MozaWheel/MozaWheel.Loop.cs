using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

public sealed partial class MozaWheel : IDisposable
{
    // ======================== Цикл управления (200 Гц) ========================

    /// <summary>Запустить фоновую петлю управления (5 мс / 200 Гц — как DT в примере SDK).</summary>
    public void Start()
    {
        if (_loop != null || _disposed != 0) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(5));
        long lastTick = _clock.ElapsedMilliseconds;

        // Разрешение системного таймера 1 мс. Факт (замер в osc_log.csv): без этого
        // PeriodicTimer упирается в тик Windows 15.6 мс — петля шла на ~64 Гц вместо
        // задуманных 200, и EMA/демпфер/лог жили в другом временном режиме.
        TimeBeginPeriod(1);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                long nowMs = _clock.ElapsedMilliseconds;
                float dt = (nowMs - lastTick) / 1000f;
                lastTick = nowMs;
                if (dt <= 0f || dt > 1f) dt = 0.005f; // защита после «сна» системы

                lock (_gate)
                {
                    try { Tick(dt); }
                    catch (Exception ex) { Warn($"ошибка тика: {ex.Message}"); }
                }
            }
        }
        catch (OperationCanceledException) { /* остановка */ }
        finally { TimeEndPeriod(1); }
    }

    private void Tick(float dt)
    {
        if (!_connected) return;

        if (_simulated)
        {
            TickSim(dt);
            WriteLog(_simPos, 0f, 0f); // в симе скорость/сила не считаем — лог нужен для железа
            return;
        }

        if (_cf is null) return;

        // Дыры в тактах (подвисание процесса/системы; из CSV: 92 за 120 с, до 218 мс):
        // ошибка растёт молча, и на первом же такте после дыры сила прыгает в упор →
        // руль хлестает через цель серией bang-bang — то самое резкое дёрганье в
        // рандомных местах. После дыры даём силе ~80 мс плавно дойти от реально
        // отданной на колесо до свежесчитанной (первый такт — без изменений силы).
        long tickMs = _clock.ElapsedMilliseconds;
        bool gap = _lastTickMs != 0 && tickMs - _lastTickMs > GapMs;
        _lastTickMs = tickMs;
        if (gap) _gapT = 0f;
        else if (_gapT < 1f) _gapT = MathF.Min(1f, _gapT + dt / GapRampSec);

        // ---- 1. Считать текущее состояние руля ----
        float pos = _lastPos;
        float vel = 0f;
        float velSdk = float.NaN;
        try
        {
            ERRORCODE err = ERRORCODE.NORMAL;
            var hid = getHIDData(ref err);
            float angle = hid.fSteeringWheelAngle;
            if (!float.IsNaN(angle))
            {
                pos = angle;
                _lastPos = angle;
                _hasPos = true;
                _lastPosMs = Environment.TickCount64;
            }

            velSdk = hid.fSteeringWheelVelocity;   // основная скорость в контрол: в логах
                                                  // совпадает с производной позиции (±400 °/с, знак тот же)
        }
        catch (Exception ex)
        {
            Warn($"getHIDData: {ex.Message}");
        }

        // Диагностика «не читает угол»: данные встали → петля не знает, где руль,
        // и не сможет вернуть его в ноль. Выводится не чаще раза в 5 с ( Warn троттлит ).
        if (_hasPos)
        {
            long ageMs = Environment.TickCount64 - _lastPosMs;
            if (ageMs > 1000)
                Warn($"угол руля не обновляется {ageMs} мс — руль может не вернуться в ноль. Перезапусти программу (закроется предыдущий экземпляр сам).");
        }

        // ---- Скорость: окно ~40 мс по позиции — запасная + колонка velRaw в лог ----
        // Позиция HID идёт шагами (напр. 0.2°): мгновенная разность давала пик
        // 40 °/с на КАЖДЫЙ шаг → «шестерня». Оконная разность шестерню убрала, но как
        // основная скорость запаздывала на ~58 мс (окно + EMA) и на дрожаниях ~6 Гц
        // демпфер приходил с опозданием ~120° — разбавлял колебания (см. лог demo):
        // поэтому основная скорость теперь velSdk, окно — fallback при выбросах.
        if (_hasPos)
        {
            long nowMs = _clock.ElapsedMilliseconds;
            if (!_hasVelRing)
            {
                for (int i = 0; i < VelRingLen; i++)
                {
                    _velRingPos[i] = pos;
                    _velRingMs[i] = nowMs;
                }
                _velRingI = 0;
                _hasVelRing = true;
            }

            _velRingI = (_velRingI + 1) % VelRingLen;
            _velRingPos[_velRingI] = pos;
            _velRingMs[_velRingI] = nowMs;
            int old = (_velRingI + 1) % VelRingLen;       // самый старый сэмпл окна
            float winSec = (nowMs - _velRingMs[old]) / 1000f;
            vel = winSec > 0.001f ? (pos - _velRingPos[old]) / winSec : 0f;
        }

        _logVelRaw = vel;                                 // оконная скорость ДО фильтра — в CSV-лог
        _logVelSdk = float.IsNaN(velSdk) ? 0f : velSdk;   // сырая SDK-скорость — в CSV-лог

        // Фильтр скорости: в EMA идёт velSdk (свежая), при выбросе 0 от прошивки —
        // запасное окно. Мёртвая зона — на ВХОДЕ: старый код занулял само состояние
        // фильтра, из-за чего velFilt залипал в 0, пока |vel| < 20 °/с (в логе demo:
        // 271 тик демпфера вырублен до 70 мс при живом velRaw) — демпфер выключался
        // и включался рывком, чем и питались дёрганья.
        float vCtrl = _logVelSdk;
        if (vCtrl == 0f && MathF.Abs(vel) > 10f) vCtrl = vel;    // dropout прошивки → окно
        // Гистерезис вместо плоского порога: смотрим, пересекаем ли мы 7/3 °/с,
        // и только потом решаем (шум гасится ДО EMA, состояние не зануляем).
        if (_velEngaged ? MathF.Abs(vCtrl) < VelReleaseDegPerSec
                        : MathF.Abs(vCtrl) > VelEngageDegPerSec)
            _velEngaged = !_velEngaged;
        if (!_velEngaged) vCtrl = 0f;
        // α=0.5 (~5–8 мс). На прежнем 0.25 (~15 мс) на волне ~10 Гц из лога змейки
        // (velFilt качается 70↔110 — сила повторяет эти качки ±1000) фильтр давал
        // демпферу фазу ~60° — тот успевал не гасить, а ПОДДЕРЖИВАТЬ раскачку.
        // Шум velSdk на покое гасит мёртвая зона на входе.
        _velFilt += (vCtrl - _velFilt) * 0.5f;                   // EMA, состояние не зануляем
        vel = _velFilt;

        // Плавное ведение цели hold — см. StepHoldTarget (lead + адаптивная τ).
        StepHoldTarget(dt);

        // ---- 1.5. Demo: подвинуть цель «змейки» (до выбора режима hold/move) ----
        StepDemo();

        // ---- 2. Считать требуемый момент и отдать его на колесо ----
        float magnitude;
        if (_holdActive)
        {
            magnitude = ComputeHoldForce(pos, vel, dt);
        }
        else
        {
            magnitude = ComputeMoveForce(pos, vel, dt);
            AnnounceReach(pos);
        }

        // Дозахват после дыры: сила плавно сходится от последней отданной к свежей.
        if (_gapT < 1f)
            magnitude = _lastApplied + (magnitude - _lastApplied) * _gapT;

        ApplyMagnitude(magnitude);
        WriteLog(pos, vel, magnitude);
    }

    private void TickSim(float dt)
    {
        StepDemo();

        // Как и в реальном режиме: цель hold ведём плавно — общий метод (lead + адаптивная τ).
        StepHoldTarget(dt);

        float target = _smoothing.Target;

        if (_holdActive)
        {
            // Рук в симуляции нет — просто фиксируем позицию.
            _simPos = _holdPos;
            _smoothing.SnapTo(_holdPos);
            return;
        }

        _simPos = _smoothing.Step(dt);

        if (_targetSet && !_announcedReach && MathF.Abs(target - _simPos) < 0.05f)
        {
            _announcedReach = true;
            _targetSet = false;
            Console.WriteLine($"SIM: позиция достигнута ({target:0.#}°)");
        }
    }
}
