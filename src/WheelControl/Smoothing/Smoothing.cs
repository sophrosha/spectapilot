namespace WheelControl;

// Math behind the smooth wheel motion.
//
// The task: between input packets (keyboard — rarely, socket — ~30 Hz, i.e. every 33 ms)
// the wheel must not jerk. The solution: the input only writes the TARGET, and this class on every tick
// of the control loop (200 Hz) smoothly brings the current position to the target.
public sealed class Smoothing
{
    private readonly Config _cfg;
    private float _current;
    private float _target;
    private float? _tauMsOverride; // time override (socket: fast reaction instead of 500 ms)

    // Current (smoothed) position, degrees.
    public float Current => _current;

    // Target position, degrees.
    public float Target => _target;

    // Effective smoothing time, ms (the override or config.smoothTimeMs).
    public float EffectiveTauMs => _tauMsOverride ?? _cfg.SmoothTimeMs;

    // Override the smoothing time, ms (null — go back to config.smoothTimeMs).
    public void SetTauMs(float? ms) => _tauMsOverride = ms;

    public Smoothing(Config cfg, float start = 0f)
    {
        _cfg = cfg;
        _current = _target = start;
    }

    // Set a new target position (does not move the wheel instantly — only changes the target).
    public void SetTarget(float target) => _target = target;

    // Jump instantly (used at initialization).
    public void SnapTo(float value) => _current = _target = value;

    // One smoothing step.
    public float Step(float dt)
    {
        if (dt <= 0f) return _current;

        // smooth: false — the target is picked up immediately.
        // Speed and torque are still limited by the move loop (MaxMoveSpeedRpm / MoveForceLimit),
        // so "sharp" mode does not mean "dangerous".
        if (!_cfg.Smooth)
        {
            _current = _target;
            return _current;
        }

        // Exponential approach: ~95% of the way in EffectiveTauMs
        // (equivalent to a critically damped approach, no overshoot past the target).
        float tau = Math.Max(EffectiveTauMs, 1) / 1000f / 3f;
        float k = 1f - MathF.Exp(-dt / tau);
        float next = _current + (_target - _current) * k;

        // Safety speed cap — jerk guard for any config settings.
        next = MoveToward(_current, next, _cfg.MaxDegPerSec * dt);

        _current = next;
        return _current;
    }

    // Move value toward to no faster than maxDelta.
    public static float MoveToward(float from, float to, float maxDelta)
    {
        float delta = to - from;
        if (MathF.Abs(delta) <= maxDelta) return to;
        return from + MathF.Sign(delta) * maxDelta;
    }
}
