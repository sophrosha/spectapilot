namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // "Hold my hand" mode (hands, for the socket): the target is held by a HARD hold spring
    // with the configured force (holdForce = 100% -> hard to push the wheel off target), not a soft move.
    // The first call engages hold (with a smooth holdRampMs ramp-up); later calls just move
    // the target without restarting the ramp, so the 30 Hz stream stays smooth and jerk-free.
    // Turned off by: release / socketHandsMode=false in config.json.
    public void HandsTo(float degrees, float force)
    {
        float userClamped = Math.Clamp(degrees, -_userFullLock, _userFullLock);
        float clamped = U2P(userClamped);

        // All printing happens AFTER _gate: Console.WriteLine is expensive on hardware (5-20 ms render),
        // and under the lock it touches the 200 Hz tick — the user felt it as slight jitter.
        // Flags are set under the lock; printing happens after.
        bool printSkip = false, printEngage = false, printSim = false;

        lock (_gate)
        {
            // Scale diagnostics: session max |value| from the game (before clamping) —
            // userFullLockDeg is tuned by it (see status).
            if (MathF.Abs(degrees) > _sockMaxRaw) _sockMaxRaw = MathF.Abs(degrees);

            // The console took control (hold/angle from the console) — the socket no longer takes over
            // the target until the user runs release and restarts socket themselves.
            if (_consoleOverride)
            {
                _pendingValid = false; // console took the wheel — no need for the socket flicker tail
                long t0 = Environment.TickCount64;
                if (t0 - _lastSkipPrint >= 2000)
                {
                    _lastSkipPrint = t0;
                    printSkip = true;
                }
            }
            // Dedup (user's idea): "90 90 90" -> first accepted, the rest cut,
            // we just wait for a different number; hold keeps holding the same target meanwhile.
            else if (_holdActive && MathF.Abs(clamped - _holdTarget) < 1e-3f)
            {
                _pendingValid = false;   // kill the A↔B flicker tail: back at the target
                UpdateSlope(clamped); // dup = zero slope — decays faster
            }
            else
            {
                // Integer-degree flicker suppression (drive log 19:54: 22 A↔B episodes —
                // force bumps avg 267 / max 716 = "gear effect on small turns").
                // Hold a step for one packet: the next one with the same number -> a real step (commit);
                // return to target -> flicker (target not moved); a third new value ->
                // a real move (committed immediately). The 1-packet delay (33 ms) hides
                // under the EMA τ=100..200 ms — imperceptible by touch.
                bool wait = false;
                if (_holdActive && MathF.Abs(clamped - _holdTarget) > 1e-3f)
                {
                    if (!_pendingValid)
                    {
                        _pendingVal = clamped;
                        _pendingValid = true;
                        wait = true;            // deferred — waiting for confirmation
                    }
                    else
                    {
                        _pendingValid = false;  // repeated or moved on — commit
                    }
                }

                if (!wait)
                {
                    UpdateSlope(clamped); // compute the slope BEFORE writing the new _holdTarget

                    if (!_holdActive)
                    {
                        // First packet: engage hold smoothly (filtered) at the current angle.
                        _holdRamp = 0f;
                        _holdActive = true;
                        _pendingValid = false;
                        float pos0 = SnapshotPos();
                        if (!float.IsNaN(pos0)) _smoothing.SnapTo(pos0);
                        _holdPos = float.IsNaN(pos0) ? clamped : pos0; // start at the current one — the EMA brings it to the target
                        printEngage = !_simulated; // hardware: exactly ONE line when control is taken over
                    }
                    _holdTarget = clamped;
                    _holdForcePct = Math.Clamp(force, 0f, 100f);
                    _lockTarget = MathF.Abs(userClamped) >= _userFullLock - 0.5f;
                    _lockDir = userClamped >= 0f ? 1f : -1f;
                    _smoothing.SetTarget(clamped);
                    _announcedReach = false;
                    _targetSet = true;
                    _posInteg = 0;

                    // The sim still prints "hands ->" (at most 1/s) — the smokes key off these
                    // lines; on hardware the packet-stream chatter is removed (see printEngage).
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
                // wait: UpdateSlope is not fed — during a flicker the slope decays from zero by itself,
                // on a real step one skip is invisible against the target EMA.
            }
        }

        if (printSkip)
            Console.WriteLine($"-> сокет ждёт: руль под консолью (цель {_holdTarget:0.#}° физ) — release, чтобы вернуть игру");
        if (printEngage)
            Console.WriteLine($"hands: руль взял управление → цель {clamped:0.#}° физ (из игры {userClamped:0.#}°, силой {_holdForcePct:0.#}%)");
        if (printSim)
            Console.WriteLine($"SIM: hands → {userClamped:0.#}° (сила {_holdForcePct:0.#}%)");
    }

    // Slope of socket targets (phys °/s) — raw input for lead smoothing of the integer
    // degree staircase. Dups come here too: equal values = zero slope, it decays faster.
    // Called from HandsTo under _gate.
    private void UpdateSlope(float clamped)
    {
        long now = _clock.ElapsedMilliseconds;
        if (_lastPktMs != 0)
        {
            float dtP = (now - _lastPktMs) / 1000f;
            if (dtP > 0.005f && dtP < 0.5f)
            {
                float v = (clamped - _holdTarget) / dtP; // phys °/s (before writing the new _holdTarget)
                _tgtVel += (v - _tgtVel) * 0.4f;          // EMA over packets (~30 Hz)
            }
        }
        _lastPktMs = now;
    }
}
