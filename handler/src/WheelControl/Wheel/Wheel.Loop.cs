using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // ======================== Control loop (200 Hz) ========================

    // Start the background control loop (5 ms / 200 Hz — like DT in the SDK example).
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

        // Set the system timer resolution to 1 ms. Fact (measured in osc_log.csv): without it
        // PeriodicTimer hits the 15.6 ms Windows tick — the loop ran at ~64 Hz instead of
        // the intended 200, and the EMA/damper/log lived on a different time scale.
        TimeBeginPeriod(1);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                long nowMs = _clock.ElapsedMilliseconds;
                float dt = (nowMs - lastTick) / 1000f;
                lastTick = nowMs;
                if (dt <= 0f || dt > 1f) dt = 0.005f; // guard after the system wakes from sleep

                lock (_gate)
                {
                    try { Tick(dt); }
                    catch (Exception ex) { Warn($"ошибка тика: {ex.Message}"); }
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally { TimeEndPeriod(1); }
    }

    private void Tick(float dt)
    {
        if (!_connected) return;

        if (_simulated)
        {
            TickSim(dt);
            WriteLog(_simPos, 0f, 0f); // in sim we do not compute velocity/force — the log is for hardware
            return;
        }

        if (_cf is null) return;

        // Holes in ticks (process/system stalls; from the CSV: 92 in 120 s, up to 218 ms):
        // the error grows silently, and on the first tick after a hole the force jumps toward the stop ->
        // the wheel whips past the target in a bang-bang series — the very jerkiness seen in
        // random spots. After a hole we give the force ~80 ms to ramp smoothly from what was
        // actually applied to the freshly computed value (first tick — no force change).
        long tickMs = _clock.ElapsedMilliseconds;
        bool gap = _lastTickMs != 0 && tickMs - _lastTickMs > GapMs;
        _lastTickMs = tickMs;
        if (gap) _gapT = 0f;
        else if (_gapT < 1f) _gapT = MathF.Min(1f, _gapT + dt / GapRampSec);

        // ---- 1. Read the current wheel state ----
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

            velSdk = hid.fSteeringWheelVelocity;   // primary velocity for control: in the logs
                                                  // it matches the derivative of position (±400 °/s, same sign)
        }
        catch (Exception ex)
        {
            Warn($"getHIDData: {ex.Message}");
        }

        // "Not reading angle" diagnostics: data stalled -> the loop does not know where the wheel is,
        // and cannot return it to zero. Printed at most once per 5 s (Warn throttles it).
        if (_hasPos)
        {
            long ageMs = Environment.TickCount64 - _lastPosMs;
            if (ageMs > 1000)
                Warn($"угол руля не обновляется {ageMs} мс — руль может не вернуться в ноль. Перезапусти программу (закроется предыдущий экземпляр сам).");
        }

        // ---- Velocity: ~40 ms window over position — fallback + velRaw column in the log ----
        // HID position arrives in steps (e.g. 0.2°): an instant difference gave a spike
        // of 40 °/s on EVERY step -> "gear effect". The windowed difference removed the gear, but as
        // the primary velocity it lagged ~58 ms (window + EMA), and on ~6 Hz oscillations
        // the damper arrived ~120° late — it diluted the oscillation (see the demo log):
        // so the primary velocity is now velSdk, with the window as an outlier fallback.
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
            int old = (_velRingI + 1) % VelRingLen;       // oldest sample in the window
            float winSec = (nowMs - _velRingMs[old]) / 1000f;
            vel = winSec > 0.001f ? (pos - _velRingPos[old]) / winSec : 0f;
        }

        _logVelRaw = vel;                                 // windowed velocity BEFORE the filter — into the CSV log
        _logVelSdk = float.IsNaN(velSdk) ? 0f : velSdk;   // raw SDK velocity — into the CSV log

        // Velocity filter: velSdk (fresh) goes into the EMA; on a firmware 0 dropout —
        // the fallback window is used. The dead zone is at the INPUT: the old code zeroed the filter state itself,
        // the filter state itself, so velFilt stuck at 0 while |vel| < 20 °/s (in the demo log:
        // 271 damper ticks killed for up to 70 ms while velRaw was alive) — the damper switched off
        // and on again with a jerk, which fed the jitter.
        float vCtrl = _logVelSdk;
        if (vCtrl == 0f && MathF.Abs(vel) > 10f) vCtrl = vel;    // firmware dropout -> window
        // Hysteresis instead of a flat threshold: we check whether we cross 7/3 °/s,
        // and only then decide (the noise is killed BEFORE the EMA; state is never zeroed).
        if (_velEngaged ? MathF.Abs(vCtrl) < VelReleaseDegPerSec
                        : MathF.Abs(vCtrl) > VelEngageDegPerSec)
            _velEngaged = !_velEngaged;
        if (!_velEngaged) vCtrl = 0f;
        // α=0.5 (~5–8 ms). At the previous 0.25 (~15 ms), on a ~10 Hz wave from the snake log
        // (velFilt swinging 70↔110 — the force tracks these swings ±1000) the filter gave
        // the damper a ~60° phase shift — it managed not to damp but to SUSTAIN the oscillation.
        // The input dead zone kills velSdk noise at rest.
        _velFilt += (vCtrl - _velFilt) * 0.5f;                   // EMA, state never zeroed
        vel = _velFilt;

        // Smooth hold-target tracking — see StepHoldTarget (lead + adaptive τ).
        StepHoldTarget(dt);

        // ---- 1.5. Demo: move the "snake" target (before hold/move mode is chosen) ----
        StepDemo();

        // ---- 2. Compute the required torque and apply it to the wheel ----
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

        // Gap catch-up after a hole: the force ramps smoothly from the last applied value to the fresh one.
        if (_gapT < 1f)
            magnitude = _lastApplied + (magnitude - _lastApplied) * _gapT;

        ApplyMagnitude(magnitude);
        WriteLog(pos, vel, magnitude);
    }

    private void TickSim(float dt)
    {
        StepDemo();

        // Same as in real mode: the hold target is tracked smoothly — shared method (lead + adaptive τ).
        StepHoldTarget(dt);

        float target = _smoothing.Target;

        if (_holdActive)
        {
            // No hands in simulation — we simply hold the position.
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
