using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace MozaWheelControl;

public sealed partial class MozaWheel : IDisposable
{
    /// <summary>Включить удержание: руль мягко, но уверенно сопротивляется попыткам повернуть.</summary>
    /// <param name="position">Позиция для удержания, пользовательские градусы (±полный ход = упор).</param>
    /// <param name="force">Сила, % от максимума (0..100).</param>
    public void EnableHold(float position, float force)
    {
        // Позиция — в ФИЗИЧЕСКИХ градусах (консоль; «hold» из сокета конвертируется в Program).
        float clamped = Math.Clamp(position, -_rangeHalf, _rangeHalf);
        float f = Math.Clamp(force, 0f, 100f);
        lock (_gate)
        {
            _consoleOverride = true;           // консоль взяла управление: сокет не перехватывает до release
            _holdForcePct = f;
            _holdRamp = 0f;                    // силу набираем плавно (holdRampMs) — без рывка
            _holdActive = true;
            _lockTarget = MathF.Abs(clamped) >= _rangeHalf - 0.5f;
            _lockDir = clamped >= 0f ? 1f : -1f;

            // Сглаженную точку — на текущий угол, цель — на удержание:
            // после release руль продолжит именно с этой позиции, без ухода в старую сторону.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(clamped);

            // Пружина стартует с ТЕКУЩЕГО угла и плавно (EMA) идёт к цели — без рывка при включении.
            _holdTarget = clamped;
            _holdPos = float.IsNaN(pos) ? clamped : pos;

            if (_simulated)
                Console.WriteLine($"SIM: EnableHold(pos={clamped:0.#}°, force={f:0.#}%) — руль держит позицию");
        }
    }

    /// <summary>Выключить удержание (сила плавно сбрасывается в 0).</summary>
    public void DisableHold()
    {
        lock (_gate)
        {
            if (_holdActive && !_simulated && _cf is not null)
            {
                try { _cf.setMagnitude(0); } catch { }
            }
            _holdActive = false;
            _holdRamp = 0f;
            _lockTarget = false;
            _consoleOverride = false; // release = вернуть управление сокету (игре)

            if (_simulated)
                Console.WriteLine("SIM: DisableHold()");
        }
    }

    /// <summary>Вернуть управление целью сокету (команда socket в консоли).</summary>
    public void AllowSocketControl()
    {
        lock (_gate) { _consoleOverride = false; }
    }
    /// <summary>
    /// Плавное ведение цели hold (общее для железа и сима). Целые градусы из игры дают
    /// лесенку 0.5° физ: на медленном повороте τ удваивается (ступеньки глушатся сильнее),
    /// а наклон входа добавляет «крен» lead = v×τ — отставание EMA на наклоне компенсируется
    /// и руль идёт синхронно с игрой. Резкие движения (|v| ≥ 60 °/с) — обычная τ без крена
    /// выше LeadMaxDeg, чтобы не «уезжать» вперёд при обрыве пакетов.
    /// </summary>
    private void StepHoldTarget(float dt)
    {
        if (!_holdActive || _demoActive || _cfg.SocketSmoothTimeMs <= 0f) return;

        if (_lastPktMs != 0 && _clock.ElapsedMilliseconds - _lastPktMs > 150)
            _tgtVel = 0f; // пакеты перестали приходить — крен не держим

        float vAbs = MathF.Abs(_tgtVel);
        float tau = _cfg.SocketSmoothTimeMs *
                    (2f - MathF.Min(vAbs / FastSlopeDegPerSec, 1f)); // 2×cfg (медленно) → 1×cfg (быстро)
        float lead = Math.Clamp(_tgtVel * (tau / 1000f), -LeadMaxDeg, LeadMaxDeg);
        float k = Math.Clamp(dt * 1000f / tau, 0f, 1f);
        // Кламп к физ. упору: цель+крен не должны вылезать за rangeHalf — иначе у края
        // пружина ветрается в стенку (лог езды: holdPos 91.7 при ходе ±90), а на развороте
        // крен слетает за пару тактов — юзер это чувствует как дёрганье на конце угла.
        float goal = Math.Clamp(_holdTarget + lead, -_rangeHalf, _rangeHalf);
        _holdPos += (goal - _holdPos) * k;
    }
}
