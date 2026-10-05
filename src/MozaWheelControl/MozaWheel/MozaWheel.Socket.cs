using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

public sealed partial class MozaWheel : IDisposable
{
    /// <summary>
    /// Режим «держи меня за руки» (hands, для сокета): цель удерживается ЖЁСТКОЙ пружиной hold
    /// с заданной силой (holdForce = 100% → оттолкнуть руль от цели сложно), а не мягким move.
    /// Первый вызов включает hold (с плавным разгоном holdRampMs), дальше просто переставляет
    /// позицию — без перезапуска разгона, поэтому поток 30 Гц идёт плавно и без рывков.
    /// Выключается: release / socketHandsMode=false в config.json.
    /// </summary>
    /// <param name="degrees">Цель, пользовательские градусы (клампится к ±полному ходу).</param>
    /// <param name="force">Сила удержания, % (0..100).</param>
    public void HandsTo(float degrees, float force)
    {
        float userClamped = Math.Clamp(degrees, -_userFullLock, _userFullLock);
        float clamped = U2P(userClamped);

        // Все печати — ПОСЛЕ _gate: Console.WriteLine на железе дорог (рендер 5-20 мс),
        // а под замком он задевает тик 200 Гц — юзер чувствовал это как мелкое дерганье.
        // Флаги ставятся под замком, пишем после.
        bool printSkip = false, printEngage = false, printSim = false;

        lock (_gate)
        {
            // Диагностика шкалы: максимум |числа| из игры за сессию (до клампа) —
            // по нему настраивают userFullLockDeg (см. status).
            if (MathF.Abs(degrees) > _sockMaxRaw) _sockMaxRaw = MathF.Abs(degrees);

            // Консоль взяла управление (hold/угол из консоли) — сокет больше не перехватывает
            // цель, пока пользователь сам не сделает release и не перезапустит socket.
            if (_consoleOverride)
            {
                _pendingValid = false; // консоль взяла руль — хвост сокет-миганий не нужен
                long t0 = Environment.TickCount64;
                if (t0 - _lastSkipPrint >= 2000)
                {
                    _lastSkipPrint = t0;
                    printSkip = true;
                }
            }
            // Дедуп (идея пользователя): «90 90 90» → первое принято, остальные отсечены,
            // просто ждём другое число. Hold при этом продолжает держать ту же цель.
            else if (_holdActive && MathF.Abs(clamped - _holdTarget) < 1e-3f)
            {
                _pendingValid = false;   // хвост мигания A↔B гасим: вернулись к цели
                UpdateSlope(clamped); // дюп = нулевой наклон — быстрее тухнет
            }
            else
            {
                // Подавление миганий целых градусов (лог езды 19:54: 22 эпизода A↔B —
                // толчки силы средние 267 / макс 716 = «шестерня на малых поворотах»).
                // Шаг держим один пакет: следующий тем же числом → реальный шаг (коммит);
                // возврат к цели → мигание (цель не двигаем); третье новое значение →
                // реальный ход (коммит сразу). Задержка 1 пакет (33 мс) прячется
                // под EMA τ=100..200 мс — на ощупь не отличить.
                bool wait = false;
                if (_holdActive && MathF.Abs(clamped - _holdTarget) > 1e-3f)
                {
                    if (!_pendingValid)
                    {
                        _pendingVal = clamped;
                        _pendingValid = true;
                        wait = true;            // отложили — ждём подтверждения
                    }
                    else
                    {
                        _pendingValid = false;  // повторилось или ушло дальше — коммит
                    }
                }

                if (!wait)
                {
                    UpdateSlope(clamped); // наклон считаем ДО записи нового _holdTarget

                    if (!_holdActive)
                    {
                        // Первый пакет: включаем hold мягко, сглаженную — на текущий угол.
                        _holdRamp = 0f;
                        _holdActive = true;
                        _pendingValid = false;
                        float pos0 = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
                        if (!float.IsNaN(pos0)) _smoothing.SnapTo(pos0);
                        _holdPos = float.IsNaN(pos0) ? clamped : pos0; // старт с текущего — EMA доведёт до цели
                        printEngage = !_simulated; // железо: ровно ОДНА строка при взятии управления
                    }
                    _holdTarget = clamped;
                    _holdForcePct = Math.Clamp(force, 0f, 100f);
                    _lockTarget = MathF.Abs(userClamped) >= _userFullLock - 0.5f;
                    _lockDir = userClamped >= 0f ? 1f : -1f;
                    _smoothing.SetTarget(clamped);
                    _announcedReach = false;
                    _targetSet = true;
                    _posInteg = 0;

                    // Сим печатает «hands →» как раньше (не чаще 1/с) — по этим строкам идут
                    // смоуки; на железе сопровождение потока пакетов убрано (см. printEngage).
                    if (_simulated)
                    {
                        long now = Environment.TickCount64;
                        if (now - _lastQuietPrint >= 1000)
                        {
                            _lastQuietPrint = now;
                            printSim = true;
                        }
                    }
                }
                // wait: UpdateSlope не кормим — на мигании наклон от нуля тухнет сам,
                // на реальном шаге один пропуск не заметен на фоне EMA цели.
            }
        }

        if (printSkip)
            Console.WriteLine($"-> сокет ждёт: руль под консолью (цель {_holdTarget:0.#}° физ) — release, чтобы вернуть игру");
        if (printEngage)
            Console.WriteLine($"hands: руль взял управление → цель {clamped:0.#}° физ (из игры {userClamped:0.#}°, силой {_holdForcePct:0.#}%)");
        if (printSim)
            Console.WriteLine($"SIM: hands → {userClamped:0.#}° (сила {_holdForcePct:0.#}%)");
    }

    /// <summary>
    /// Наклон целей из сокета (°/с физ) — сырьё для lead-сглаживания лесенки целых
    /// градусов. Дюпы тоже идут сюда: равные значения = нулевой наклон, он быстрее тухнет.
    /// Вызывается из HandsTo под _gate.
    /// </summary>
    private void UpdateSlope(float clamped)
    {
        long now = _clock.ElapsedMilliseconds;
        if (_lastPktMs != 0)
        {
            float dtP = (now - _lastPktMs) / 1000f;
            if (dtP > 0.005f && dtP < 0.5f)
            {
                float v = (clamped - _holdTarget) / dtP; // °/с физ (до записи нового _holdTarget)
                _tgtVel += (v - _tgtVel) * 0.4f;          // EMA по пакетам (~30 Гц)
            }
        }
        _lastPktMs = now;
    }
}
