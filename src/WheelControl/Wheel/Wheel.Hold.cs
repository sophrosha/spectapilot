namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // Engage hold: the wheel resists attempts to turn it softly but firmly.
    public void EnableHold(float position, float force)
    {
        // Position is in PHYSICAL degrees (console; socket "hold" is converted in Program).
        float clamped = Math.Clamp(position, -_rangeHalf, _rangeHalf);
        float f = Math.Clamp(force, 0f, 100f);
        lock (_gate)
        {
            _consoleOverride = true;           // console took control: the socket does not take over until release
            _holdForcePct = f;
            _holdRamp = 0f;                    // force ramps up smoothly (holdRampMs) — no jerk
            _holdActive = true;
            _lockTarget = MathF.Abs(clamped) >= _rangeHalf - 0.5f;
            _lockDir = clamped >= 0f ? 1f : -1f;

            // The smoothed point goes to the current angle, the target to the hold position:
            // after release the wheel continues from exactly this position, without drifting to the old side.
            float pos = SnapshotPos();
            if (!float.IsNaN(pos)) _smoothing.SnapTo(pos);
            _smoothing.SetTarget(clamped);

            // The spring starts at the CURRENT angle and eases (EMA) to the target — no jerk on engage.
            _holdTarget = clamped;
            _holdPos = float.IsNaN(pos) ? clamped : pos;

            if (_simulated)
                Console.WriteLine($"SIM: EnableHold(pos={clamped:0.#}°, force={f:0.#}%) — руль держит позицию");
        }
    }

    // Disengage hold (force ramps smoothly to 0).
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
            _consoleOverride = false; // release = hand control back to the socket (the game)

            if (_simulated)
                Console.WriteLine("SIM: DisableHold()");
        }
    }

    // Return target control to the socket (the socket command in the console).
    public void AllowSocketControl()
    {
        lock (_gate) { _consoleOverride = false; }
    }

    // Smooth hold-target tracking (shared by hardware and sim). Integer degrees from the game give
    // a 0.5° physical staircase: on slow turns τ doubles (the steps are damped harder),
    // and the input slope adds lead = v×τ — the EMA lag on a slope is compensated
    // and the wheel moves in sync with the game. Sharp moves (|v| ≥ 60 °/s) use plain τ without lead
    // above LeadMaxDeg so we do not run ahead on packet loss.
    private void StepHoldTarget(float dt)
    {
        if (!_holdActive || _demoActive || _cfg.SocketSmoothTimeMs <= 0f) return;

        if (_lastPktMs != 0 && _clock.ElapsedMilliseconds - _lastPktMs > 150)
            _tgtVel = 0f; // packets stopped arriving — no lead is kept

        float vAbs = MathF.Abs(_tgtVel);
        float tau = _cfg.SocketSmoothTimeMs *
                    (2f - MathF.Min(vAbs / FastSlopeDegPerSec, 1f)); // 2×cfg (slow) -> 1×cfg (fast)
        float lead = Math.Clamp(_tgtVel * (tau / 1000f), -LeadMaxDeg, LeadMaxDeg);
        float k = Math.Clamp(dt * 1000f / tau, 0f, 1f);
        // Clamp to the physical stop: target+lead must not exceed rangeHalf — otherwise at the edge
        // the spring winds into the wall (drive log: holdPos 91.7 at ±90 travel), and on the way back
        // the lead drops off within a couple of ticks — the user feels it as a jerk at the end of the range.
        float goal = Math.Clamp(_holdTarget + lead, -_rangeHalf, _rangeHalf);
        _holdPos += (goal - _holdPos) * k;
    }
}
