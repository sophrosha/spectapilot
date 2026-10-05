using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

public sealed partial class Wheel : IDisposable
{
    // ======================== Control (API from the spec) ========================

    // Target position: the wheel goes there smoothly (smooth) or sharply, but within limits.
    public void SetPosition(float degrees, bool quiet = false)
    {
        // Console (quiet=false) — physical degrees; socket (quiet=true) — game scale.
        float clamped;               // target in physical degrees
        float shown;                  // what we print — in the units of its own input
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
            // Command from the console — the socket no longer takes over the target (until release / socket).
            if (!quiet) _consoleOverride = true;

            // Scale diagnostics (see HandsTo): session max |value| from the game.
            else if (MathF.Abs(degrees) > _sockMaxRaw) _sockMaxRaw = MathF.Abs(degrees);

            // User's idea: a repeat of the same value (a "90 90 90" stream)
            // is cut — the target is already set and the wheel tracks/holds it; we wait for a different number.
            if (quiet && !_holdActive &&
                MathF.Abs(clamped - _smoothing.Target) < 1e-3f)
                return;

            // Return without "jumping to the old side": if the smoothed point
            // is on a different side of the wheel than the new target (or target = wheel) — jump
            // to the current angle. Otherwise after "0" the wheel would first go up to the old target.
            float pos = _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);
            if (!float.IsNaN(pos) &&
                (_smoothing.Current - pos) * (clamped - pos) <= 0f &&
                MathF.Abs(_smoothing.Current - pos) > 0.01f)
                _smoothing.SnapTo(pos);

            _smoothing.SetTarget(clamped);
            // Target at the stop? Soft indicator bumps await at the stop (lockNudge).
            _lockTarget = atLock;
            _lockDir = clamped >= 0f ? 1f : -1f;
            // Console: the usual smoothing. Socket: nearly instant (mod latency ~0).
            _smoothing.SetTauMs(quiet ? _cfg.SocketSmoothTimeMs : (float?)_cfg.SmoothTimeMs);
            if (_holdActive) _holdTarget = clamped; // hold "moves" to the new target: +10/-10 work in hold too
            _announcedReach = false;
            _targetSet = true;
            _posInteg = 0;

            if (quiet)
            {
                if (!_simulated)
                    return; // hardware from the socket: stay silent — Console under _gate is expensive (~5-20 ms
                            // of rendering) and skews the 200 Hz tick; the user felt it as a jerk
                // The sim prints at most once a second — the smokes key off these lines.
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

    // Torque limit, % (0 = full zeroing; 50..100 = the hardware limit setMotorPeakTorque).
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
                try { _cf.setMagnitude(0); } catch { /* the effect may already have been stopped */ }
                return;
            }

            try { setMotorPeakTorque((int)Math.Clamp(pct, 50f, 100f)); }
            catch { /* not critical */ }
        }
    }

    // Current wheel angle in PHYSICAL degrees (console scale); NaN if there is no data yet.
    public float GetPosition()
    {
        lock (_gate)
        {
            if (_simulated) return _simPos;
            return _hasPos ? _lastPos : float.NaN;
        }
    }

    // Position for relative commands (phys °): the real angle, or the current target if there is no data.
    public float GetReferencePosition()
    {
        float p = GetPosition();
        return float.IsNaN(p) ? _smoothing.Target : p;
    }

    // Wheel angle straight from HID (for start diagnostics: the SDK only starts from the center).
    // NaN if the data is unavailable — then we treat the angle as unknown and try to start.
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
