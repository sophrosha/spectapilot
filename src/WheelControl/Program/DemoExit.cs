using System.Globalization;

namespace WheelControl;

// Demo exit (spec, Variant A): after StopDemo — by timer or by any command —
// stable leaves hold off and lets ComputeMoveForce drive the wheel from the
// swing back to 0. On a long path (up to ±90°) that positional loop's
// integrator winds up and overshoots through the center: the wheel rocks back
// and forth until it settles, which yanks the hands. Wrapper around stable
// (Wheel.cs is never edited): re-engage hold at the current angle, walk the
// target to 0 with a bounded lerp (the smoothing EMA carries the wheel), and
// only then release — once the wheel has actually stopped at the center
// (|pos| < 0.5° and |vel| < 5°/s).
internal static partial class Program
{
    // Stable ends demo from its own 200 Hz tick — there is no event to
    // subscribe to, so the active -> inactive edge is polled.
    private const int DemoWatchPollMs = 100;
    private const int ReturnLerpMs = 1500; // target walk: current angle -> 0
    private const int ReturnStepMs = 40;   // lerp step (~37 steps)
    private const int SampleMs = 50;       // velocity estimate window
    private const int SettleTimeoutMs = 5000; // a hand on the wheel must not pin the dispatcher
    private const float SettlePosDeg = 0.5f;
    private const float SettleVelDegPerSec = 5f;

    // Started once from Main. Shares DispatchGate with the socket dispatcher:
    // while the return runs, game packets queue up (last target still wins
    // afterwards) instead of fighting the lerp.
    private static async Task DemoExitWatchAsync()
    {
        var wheel = _wheel;
        if (wheel is null) return;
        bool was = wheel.IsDemoActive;
        while (true)
        {
            await Task.Delay(DemoWatchPollMs);
            bool now = wheel.IsDemoActive;
            if (was && !now)
            {
                await DispatchGate.WaitAsync();
                try
                {
                    // Re-check under the gate: a command may have claimed the
                    // wheel first — hands/hold is ON, or a fresh non-zero move
                    // target — then that command owns the motion, not us.
                    if (!wheel.IsDemoActive && !wheel.IsHoldActive &&
                        MathF.Abs(wheel.CurrentTarget) < SettlePosDeg)
                        await SmoothReturnToZeroAsync(wheel);
                }
                finally
                {
                    DispatchGate.Release();
                }
            }
            was = now;
        }
    }

    // The return itself (spec Variant A). Caller holds DispatchGate.
    private static async Task SmoothReturnToZeroAsync(Wheel wheel)
    {
        // SIM: stable's log lines stay byte-identical for the smoke scripts.
        if (wheel.IsSimulated) return;

        float prev = wheel.GetPosition(); // physical degrees
        if (float.IsNaN(prev)) prev = wheel.CurrentTarget;
        if (float.IsNaN(prev)) return;

        // Already calm at the center? One sample window decides; if yes, don't
        // touch hold or the logs at all (timer end usually happens with the
        // envelope faded to 0 — the wheel is already home and stable's own
        // move loop has no path left to overshoot).
        long tv = Environment.TickCount64;
        await Task.Delay(SampleMs);
        float pos = wheel.GetPosition();
        long tvNow = Environment.TickCount64;
        if (float.IsNaN(pos)) return;
        float vel = MathF.Abs(pos - prev) / MathF.Max((tvNow - tv) / 1000f, 1e-3f);
        if (MathF.Abs(pos) < SettlePosDeg && vel < SettleVelDegPerSec) return;

        LogDebug($"demo exit: smooth return from {F(pos)}° vel {F(vel)}°/s");

        // Hold back ON at the current angle — stable's StopDemo has already
        // released it, and the spring takes the wheel before the move loop can
        // wind up. EnableHold takes PHYSICAL degrees; it also sets the console
        // override, so no socket packet can hijack the return (cleared by the
        // DisableHold at the end).
        wheel.EnableHold(pos, _cfg.HoldForce);

        // Walk the target pos -> 0 over ReturnLerpMs. SetPosition(quiet) is the
        // silent socket path (hardware prints nothing), takes USER degrees
        // (P2U = public inverse of SocketToPhysical), syncs _holdTarget while
        // hold is active and zeroes the move-loop integrator on every step.
        long t0 = Environment.TickCount64;
        while (true)
        {
            await Task.Delay(ReturnStepMs);
            float elapsed = Environment.TickCount64 - t0;
            if (elapsed >= ReturnLerpMs) break;
            wheel.SetPosition(P2U(wheel, pos * (1f - elapsed / ReturnLerpMs)), quiet: true);
        }
        wheel.SetPosition(0f, quiet: true);

        // Wait until the wheel really stops at the center: |pos| < 0.5° and
        // |vel| < 5°/s over consecutive samples (finite difference over
        // SampleMs). The timeout releases anyway if a hand pins the wheel —
        // the path left by then is short, so the move loop can't wind up.
        long deadline = Environment.TickCount64 + SettleTimeoutMs;
        prev = wheel.GetPosition();
        long prevT = Environment.TickCount64;
        while (Environment.TickCount64 < deadline)
        {
            await Task.Delay(SampleMs);
            pos = wheel.GetPosition();
            long t = Environment.TickCount64;
            if (float.IsNaN(pos)) break; // no data — release, stable drives
            float dt = (t - prevT) / 1000f;
            vel = dt > 1e-3f ? MathF.Abs(pos - prev) / dt : SettleVelDegPerSec;
            prev = pos;
            prevT = t;
            if (MathF.Abs(pos) < SettlePosDeg && vel < SettleVelDegPerSec) break;
        }

        // Let go only now: the target is 0 and the wheel sits at 0, so the move
        // loop resumes with no path left to wind up — nothing to overshoot.
        wheel.DisableHold();
        LogDebug($"demo exit: released at {F(pos)}° vel {F(vel)}°/s");
    }

    // Public linear inverse of Wheel's private U2P (SocketToPhysical):
    // physical -> user degrees with the same constants as Wheel.P2U.
    private static float P2U(Wheel wheel, float phys) =>
        phys * (wheel.SocketRangeHalf / Math.Max(wheel.MoveRangeHalf, 1f));

    // Invariant-culture number for debug lines (like the log stamps).
    private static string F(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
