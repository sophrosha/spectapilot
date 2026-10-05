using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // ======================== Demo (автономное «вождение») ========================

    private const float DemoDefaultSec = 50f;   // длительность по умолчанию
    private const float DemoPeriodSec = 5f;     // период «змейки»
    private const float DemoFadeSec = 4f;       // плавный набор/сброс амплитуды

    private volatile bool _demoActive;
    private long _demoStartMs;
    private long _demoDurationMs;
    private float _demoAmp;

    /// <summary>Идёт ли сейчас demo-сценарий.</summary>
    public bool IsDemoActive => _demoActive;

    /// <summary>
    /// Запустить имитацию езды: руль плавно «змейкой» крутит от упора до упора
    /// в режиме hold — сопротивление активно всё время (можно взять в руки).
    /// Останавливается сам (по таймеру) или командой move/hold/release.
    /// </summary>
    /// <param name="seconds">Длительность, с (<=0 → 50).</param>
    /// <param name="amplitude">Амплитуда ±° (<=0 → полный ход руля).</param>
    public void StartDemo(float seconds, float amplitude)
    {
        if (float.IsNaN(seconds) || seconds <= 0f) seconds = DemoDefaultSec;
        float amp = float.IsNaN(amplitude) || amplitude <= 0f
            ? _rangeHalf // по умолчанию — полный ход (физические °)
            : MathF.Abs(amplitude);
        amp = Math.Clamp(amp, 1f, _rangeHalf);

        lock (_gate)
        {
            _demoActive = true;
            _demoStartMs = _clock.ElapsedMilliseconds;
            _demoDurationMs = (long)(seconds * 1000f);
            _demoAmp = amp; // физические градусы (консольная шкала)
            _lockTarget = false; // змейка не у упора — толчки не нужны

            if (!_holdActive)
            {
                // Держим через обычный hold: пружина даёт и слежение за «змейкой»,
                // и сопротивление, если пользователь возьмёт руль в руки.
                _holdPos = 0f;
                _holdForcePct = Math.Clamp(_cfg.HoldForce, 0f, 100f);
                _holdRamp = 0f;
                _holdActive = true;
            }
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(0f);
            _targetSet = false;
        }

        Console.WriteLine($"Demo: {seconds:0.#} с, амплитуда ±{amp:0.#}° (период {DemoPeriodSec:0.#} с, огибающая {DemoFadeSec:0.#} с). " +
                          "Руль сам крутит змейку; можно взять в руки — будет сопротивление. Остановить: любая команда move/hold/release.");
    }

    /// <summary>Остановить demo: цель — в центр, hold выключается (момент сбрасывается, дальше руль едет в 0).</summary>
    /// <param name="reason">Причина (для вывода в консоль).</param>
    public void StopDemo(string reason)
    {
        lock (_gate)
        {
            if (!_demoActive) return;
            _demoActive = false;

            // Сглаженную — на текущий угол, цель — в центр: возврат сразу «вниз», без ухода вверх.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(0f);
            _holdPos = 0f;
            _holdTarget = 0f;
            _holdActive = false;        // освобождаем — move-петля довезёт до 0
            _holdRamp = 0f;
            _lockTarget = false;        // цель — центр, никаких толчков у упора
            _consoleOverride = false;   // демо кончилось — сокет снова может управлять
            _targetSet = true;          // напечатаем «Достигнуто: 0°», когда руль доедет
            if (!_simulated && _cf is not null)
            {
                try { _cf.setMagnitude(0); } catch { }
            }
        }

        if (_simulated) Console.WriteLine("SIM: StopDemo()");
        Console.WriteLine(reason == "конец"
            ? "Demo завершён: руль возвращён в центр, сопротивление снято."
            : $"Demo остановлен ({reason}).");
    }

    /// <summary>Шаг demo — вызывается из Tick под _gate: двигает цель «змейки».</summary>
    private void StepDemo()
    {
        if (!_demoActive) return;

        long t = _clock.ElapsedMilliseconds - _demoStartMs;
        if (t >= _demoDurationMs)
        {
            StopDemo("конец");
            return;
        }

        float sec = t / 1000f;
        float totalSec = _demoDurationMs / 1000f;
        // Огибающая: амплитуда растёт DemoFadeSec и так же гаснет к концу —
        // старт и финиш плавные, руль всегда заканчивает в центре.
        float env = MathF.Min(MathF.Min(sec, totalSec - sec) / DemoFadeSec, 1f);
        if (env < 0f) env = 0f;

        float target = _demoAmp * env * MathF.Sin(2f * MathF.PI * sec / DemoPeriodSec); // физические °
        _holdPos = target;
        _smoothing.SetTarget(target);
        _targetSet = false;
    }
}
