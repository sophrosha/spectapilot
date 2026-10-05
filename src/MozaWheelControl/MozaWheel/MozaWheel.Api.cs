using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

public sealed partial class MozaWheel : IDisposable
{
    // ======================== Управление (API из ТЗ) ========================

    /// <summary>Целевая позиция: руль пойдёт туда плавно (smooth) или резко, но с ограничениями.</summary>
    /// <param name="degrees">Угол в ПОЛЬЗОВАТЕЛЬСКИХ градусах (0 = центр, ±userFullLockDeg = полный ход).</param>
    /// <param name="quiet">true — приглушить вывод (поток из сокета приходит 30 раз в секунду).</param>
    public void SetPosition(float degrees, bool quiet = false)
    {
        // Консоль (quiet=false) — физические градусы; сокет (quiet=true) — игровая шкала.
        float clamped;               // цель в физических градусах
        float shown;                  // то, что напечатаем — в единицах своего ввода
        bool atLock;
        if (quiet)
        {
            float userClamped = Math.Clamp(degrees, -_userFullLock, _userFullLock);
            clamped = U2P(userClamped);
            shown = userClamped;
            atLock = MathF.Abs(userClamped) >= _userFullLock - 0.5f;
        }
        else
        {
            clamped = Math.Clamp(degrees, -_rangeHalf, _rangeHalf);
            shown = clamped;
            atLock = MathF.Abs(clamped) >= _rangeHalf - 0.5f;
        }
        lock (_gate)
        {
            // Команда из консоли — сокет больше не перехватывает цель (до release / socket).
            if (!quiet) _consoleOverride = true;

            // Диагностика шкалы (см. HandsTo): максимум |числа| из игры за сессию.
            else if (MathF.Abs(degrees) > _sockMaxRaw) _sockMaxRaw = MathF.Abs(degrees);

            // Идея пользователя: повтор одного и того же значения (стрим «90 90 90»)
            // отсекаем — цель уже стоит, руль и так её ведёт/держит; ждём другое число.
            if (quiet && !_holdActive &&
                MathF.Abs(clamped - _smoothing.Target) < 1e-3f)
                return;

            // Возврат без «прыжка в старую сторону»: если сглаженная точка осталась
            // не на той стороне руля, что и новая цель (или цель = руль) — перепрыгиваем
            // на текущий угол. Иначе после «0» руль сначала уезжал бы вверх к старой цели.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos) &&
                (_smoothing.Current - pos) * (clamped - pos) <= 0f &&
                MathF.Abs(_smoothing.Current - pos) > 0.01f)
                _smoothing.SnapTo(pos);

            _smoothing.SetTarget(clamped);
            // Цель на упоре? У упора будут мягкие толчки-индикатор (lockNudge).
            _lockTarget = atLock;
            _lockDir = clamped >= 0f ? 1f : -1f;
            // Консоль: привычное сглаживание. Сокет: почти мгновенно (задержка от мода ~0).
            _smoothing.SetTauMs(quiet ? _cfg.SocketSmoothTimeMs : (float?)_cfg.SmoothTimeMs);
            if (_holdActive) _holdTarget = clamped; // hold «переезжает» к новой цели: +10/-10 работают и в hold
            _announcedReach = false;
            _targetSet = true;
            _posInteg = 0;

            if (quiet)
            {
                if (!_simulated)
                    return; // железо из сокета: молчим — Console под _gate дорог (~5-20 мс
                            // рендера) и сбивает тик 200 Гц, юзер чувствовал рывком
                // Сим печатает не чаще раза в секунду — по этим строкам идут смоуки.
                long now = Environment.TickCount64;
                if (now - _lastQuietPrint < 1000) return;
                _lastQuietPrint = now;
            }

            string posNow = _hasPos ? _lastPos.ToString("0.#") : "?";
            if (_simulated)
                Console.WriteLine($"SIM: SetPosition({shown:0.#}°) smooth={_cfg.Smooth} " +
                                  $"smoothTimeMs={(quiet ? _cfg.SocketSmoothTimeMs : _cfg.SmoothTimeMs)}");
            else
                Console.WriteLine($"-> цель {clamped:0.#}° (сейчас {posNow}°)");
        }
    }

    /// <summary>
    /// Ограничение момента, % (0 = полное обнуление; 50..100 = аппаратный лимит setMotorPeakTorque).
    /// </summary>
    public void SetTorque(float torque)
    {
        lock (_gate)
        {
            float pct = Math.Clamp(torque, 0f, 100f);
            _torquePct = pct;

            if (_simulated)
            {
                Console.WriteLine($"SIM: SetTorque({pct:0.#}%)");
                return;
            }
            if (!_connected || _cf is null) return;

            if (pct <= 0f)
            {
                try { _cf.setMagnitude(0); } catch { /* эффект мог быть уже остановлен */ }
                return;
            }

            try { setMotorPeakTorque((int)Math.Clamp(pct, 50f, 100f)); }
            catch { /* не критично */ }
        }
    }

    /// <summary>Текущий угол руля в ФИЗИЧЕСКИХ градусах (шкала консоли); NaN, если данных ещё не было.</summary>
    public float GetPosition()
    {
        lock (_gate)
        {
            if (_simulated) return _simPos;
            return _hasPos ? _lastPos : float.NaN;
        }
    }

    /// <summary>Позиция для относительных команд (физ. °): реальный угол, а если данных нет — текущая цель.</summary>
    public float GetReferencePosition()
    {
        float p = GetPosition();
        return float.IsNaN(p) ? _smoothing.Target : p;
    }

    /// <summary>
    /// Угол руля напрямую из HID (для диагностики старта: SDK стартует только из центра).
    /// NaN, если данные недоступны — тогда считаем «угол неизвестен» и пробуем стартовать.
    /// </summary>
    private float ReadHidAngle()
    {
        try
        {
            ERRORCODE err = ERRORCODE.NORMAL;
            var hid = getHIDData(ref err);
            if (err != ERRORCODE.NORMAL) return float.NaN;
            return hid.fSteeringWheelAngle;
        }
        catch
        {
            return float.NaN;
        }
    }
}
