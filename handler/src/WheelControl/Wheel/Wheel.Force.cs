namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // Position loop (ported from the official sdk_api_test.cc::moveTo example).
    private float ComputeMoveForce(float pos, float vel, float dt)
    {
        float smoothed = _smoothing.Step(dt);
        float posError = smoothed - pos;

        // Warning only — the force is NOT zeroed: the target must always be reachable.
        // (Before, above 60° the force was reset for good: +100 and "0" never arrived.)
        if (MathF.Abs(posError) > _rangeHalf * PosWarnFactor)
            Warn($"очень большая ошибка позиции {posError:0.#}° — если руль идёт не туда, выставь forceInvert в config.json.");

        // Position -> required speed (rpm).
        // pos_kp is tuned so that tau=SmoothTimeMs yields 1.0 — as in the SDK example.
        // From the socket tau is small (socketSmoothTimeMs) -> the reaction is nearly instant.
        float posKp = _cfg.Smooth
            ? 0.5f / Math.Max(_smoothing.EffectiveTauMs / 1000f, 0.01f)
            : FastMovePosKp;
        float speedLimit = _cfg.Smooth ? _cfg.MaxMoveSpeedRpm : Math.Min(_cfg.MaxMoveSpeedRpm * 2f, 300f);

        float spdRef = posKp * posError;
        spdRef = Math.Clamp(spdRef, -speedLimit, speedLimit);

        // Speed -> torque.
        float currentRpm = vel * DegPerSecToRpm;
        float spdErr = spdRef - currentRpm;

        _posInteg += spdErr * dt * SpdKi;
        // The integrator can reach the FULL torque limit. (Before, we divided by SpdKi:
        // it was 1500/40 = 37.5 ≈ 0.4% — the wheel could not overcome friction and +10 never arrived.)
        float integLimit = Math.Max(_cfg.MoveForceLimit, 100f);
        _posInteg = Math.Clamp(_posInteg, -integLimit, integLimit); // no windup

        float torqueRef = _posInteg + spdErr * SpdKp;

        // Sign: in the official example target_ref = -torque_ref.
        float magnitude = -torqueRef;
        if (_cfg.ForceInvert) magnitude = -magnitude;

        float cap = MathF.Min(_cfg.MoveForceLimit, 10_000f);
        float result = Math.Clamp(magnitude, -cap, cap);

        // ---- Smooth approach to the stop (anti "bouncy ball") ----
        // Target clamped at the mechanical stop: the integral wound up over the whole travel and pushed
        // the wheel into the stop -> bounce -> second hit (it rocks). At the stop: we bleed
        // the integral and slow the feed — the wheel presses softly and stays.
        float toStop = smoothed >= 0f ? _rangeHalf - pos : _rangeHalf + pos; // distance to the stop in the target direction
        bool approachingStop = toStop < LockApproachDeg && (posError >= 0f) == (smoothed >= 0f);
        if (approachingStop)
        {
            _posInteg *= 0.85f; // bleed windup at the stop (every tick)
            if (toStop > 0f)
                result *= 0.3f + 0.7f * Math.Clamp(toStop / LockApproachDeg, 0f, 1f);
        }

        return result;
    }

    // Hold: spring-damper. Resists softly but firmly, without jerks.
    private float ComputeHoldForce(float pos, float vel, float dt)
    {
        // Force ramp on engage (and after each entry into hold) — to avoid a jerk.
        if (_cfg.HoldRampMs <= 0)
            _holdRamp = 1f;
        else
            _holdRamp = Math.Min(1f, _holdRamp + dt * 1000f / _cfg.HoldRampMs);

        float baseCap = Math.Clamp(_holdForcePct, 0f, 100f) / 100f * 10_000f; // % -> DirectInput scale
        if (baseCap <= 0f) return 0f;

        float err = _holdPos - pos;                     // >0 — the wheel is left of the target

        // Progressive boost ("feels my hand"): if the user increased
        // the deviation from the target, the force CAREFULLY rises to 100% — smoothstep, no steps.
        float cap = baseCap;
        if (_cfg.ProgressiveHold && cap < 10_000f)
        {
            float x = Math.Clamp(MathF.Abs(err) / ProgressiveBoostDeg, 0f, 1f);
            x = x * x * (3f - 2f * x);
            cap += (10_000f - cap) * x;
        }

        // KpScale/KdScale from config.json (default 1.0 = previous behavior):
        // kpScale 0.5 = the spring is twice as soft (less fight with the hand, fewer stick-slip clicks),
        // kdScale — the damper (careful: <1 brings back the ~15 Hz wave, see README).
        // The governor does not depend on KpScale (clamped by cap); the equilibrium
        // approach speed = cap/kd = 180/KdScale °/s.
        float kp = cap / HoldSpringFullDeg * _cfg.KpScale;             // spring: full force at 8° of error
        float kd = cap / HoldDampFullDegPerSec * _cfg.KdScale;         // damper: full at 180 °/s

        // Force in the "+ angle" direction: pulls to the target and damps motion (neither way — no jerk).
        // Approach governor: the spring is capped BEFORE being summed with the damper.
        // Otherwise, on a big wind-up (charged at the stop / target jump) the sum kp·err − kd·v
        // gives an equilibrium speed kp·err/kd = 22.5·err — on a −95->0 target jump the wheel
        // flew at 697 °/s with a force step of 6300 (drive log 19:54, event 94.94) — "the
        // table shakes, the wheel wants back fast". With the clamp: v_eq = cap/kd = 180 °/s for
        // ANY error; in typical use |err|<8° the spring is below cap — the formula unchanged,
        // hold force and jerk resistance (err>8, hand speed) are the same.
        float spring = Math.Clamp(kp * err, -cap, cap);
        float forcePlus = spring - kd * vel;

        // At the stop (target clamped, wheel at the mechanical stop): soft rhythmic bumps
        // of 6..12% force at ~1.2 Hz — "careful, not strong" (in game there may be 3 turns,
        // but we have one travel — the stop hints the limit without a hard slam).
        if (_cfg.LockNudge && _lockTarget)
        {
            float dist = _rangeHalf - MathF.Abs(pos);   // how much is left to the stop
            if (dist <= NudgeBlendDeg)
            {
                float prox = 1f - MathF.Max(dist, 0f) / NudgeBlendDeg;      // 0..1 toward the stop
                float phase = Environment.TickCount64 * 0.001f * 2f * MathF.PI * NudgeHz;
                float pulse = 0.06f + 0.06f * (0.5f + 0.5f * MathF.Sin(phase));
                float nudgePlus = cap * pulse * _lockDir;
                forcePlus += (nudgePlus - forcePlus) * prox;   // smooth spring<->bump blend
            }
        }

        float magnitude = _cfg.ForceInvert ? forcePlus : -forcePlus;
        magnitude *= _holdRamp;
        // IMPORTANT: there is NO slew limiter (force rate limit) here — in the feedback loop
        // it caused a phase shift and wheel oscillation up to the stop ("rocked 120..160").
        // Smoothness comes from: the target EMA (_holdPos), the velocity dead zone and holdRampMs.
        return Math.Clamp(magnitude, -cap, cap);
    }

    // Apply torque to the wheel (with effect recovery after a fault).
    private void ApplyMagnitude(float magnitude)
    {
        if (_cf is null) return;
        try
        {
            float c = Math.Clamp(magnitude, -10_000f, 10_000f);
            _lastApplied = c;
            _cf.setMagnitude((long)c);
            if (_effectBroken)
            {
                _effectBroken = false;
                Warn("FFB-эффект восстановлен.");
            }
        }
        catch (Exception ex)
        {
            // The effect may have "died" (e.g. on timeout) — try restarting it once.
            if (!_effectBroken)
            {
                _effectBroken = true;
                try { _cf.stop(); } catch { }
                try { _cf.start(); Warn($"FFB-эффект перезапущен после сбоя: {ex.Message}"); }
                catch (Exception ex2) { Warn($"FFB-эффект не запускается: {ex2.Message}"); }
            }
        }
    }

    private void AnnounceReach(float pos)
    {
        if (!_targetSet || _announcedReach) return;
        float target = _smoothing.Target;
        if (MathF.Abs(target - pos) < 1.0f)
        {
            _announcedReach = true;
            _targetSet = false;
            Console.WriteLine($"Достигнуто: {target:0.#}°");
        }
    }

    // Warning (at most once per 5 seconds, no spam).
    private void Warn(string message)
    {
        LastWarning = message;
        long now = Environment.TickCount64;
        if (now - _lastWarnTick < 5000) return;
        _lastWarnTick = now;
        Console.WriteLine($"! {message}");
    }
}
