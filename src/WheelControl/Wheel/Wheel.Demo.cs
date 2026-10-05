using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // ======================== Demo (autonomous "driving") ========================

    private const float DemoDefaultSec = 50f;   // default duration
    private const float DemoPeriodSec = 5f;     // "snake" period
    private const float DemoFadeSec = 4f;       // smooth fade-in/out of the amplitude

    private volatile bool _demoActive;
    private long _demoStartMs;
    private long _demoDurationMs;
    private float _demoAmp;

    // Whether a demo scenario is running.
    public bool IsDemoActive => _demoActive;

    // Start the driving simulation: the wheel smoothly snakes from stop to stop
    // in hold mode — resistance is active the whole time (you can grab the wheel).
    // Stops by itself (timer) or by a move/hold/release command.
    public void StartDemo(float seconds, float amplitude)
    {
        if (float.IsNaN(seconds) || seconds <= 0f) seconds = DemoDefaultSec;
        float amp = float.IsNaN(amplitude) || amplitude <= 0f
            ? _rangeHalf // default — full lock (physical °)
            : MathF.Abs(amplitude);
        amp = Math.Clamp(amp, 1f, _rangeHalf);

        lock (_gate)
        {
            _demoActive = true;
            _demoStartMs = _clock.ElapsedMilliseconds;
            _demoDurationMs = (long)(seconds * 1000f);
            _demoAmp = amp; // physical degrees (console scale)
            _lockTarget = false; // the snake is not at the stop — no bumps needed

            if (!_holdActive)
            {
                // We hold via a regular hold: the spring both tracks the "snake"
                // and resists if the user grabs the wheel.
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

    // Stop the demo: target to center, hold off (torque zeroed; the wheel then drives to 0).
    public void StopDemo(string reason)
    {
        lock (_gate)
        {
            if (!_demoActive) return;
            _demoActive = false;

            // Smoothed to the current angle, target to center: returns straight "down", never up.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(0f);
            _holdPos = 0f;
            _holdTarget = 0f;
            _holdActive = false;        // release — the move loop carries it to 0
            _holdRamp = 0f;
            _lockTarget = false;        // target — center, no bumps at the stop
            _consoleOverride = false;   // demo over — the socket may control again
            _targetSet = true;          // print the "reached: 0°" line once the wheel arrives
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

    // Demo step — called from Tick under _gate: moves the "snake" target.
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
        // Envelope: the amplitude grows for DemoFadeSec and fades the same way at the end —
        // start and finish are smooth, the wheel always ends in the center.
        float env = MathF.Min(MathF.Min(sec, totalSec - sec) / DemoFadeSec, 1f);
        if (env < 0f) env = 0f;

        float target = _demoAmp * env * MathF.Sin(2f * MathF.PI * sec / DemoPeriodSec); // physical °
        _holdPos = target;
        _smoothing.SetTarget(target);
        _targetSet = false;
    }
}
