using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using mozaAPI;
using static mozaAPI.mozaAPI; // installMozaSDK, getHIDData, createWheelbaseETConstantForce, ...

namespace WheelControl;

// Wrapper over the Moza SDK + background control loop.
//
// Wheel control (modeled after the official sdk_api_test.cc::moveTo example):
// - a ConstantForce effect is created (needs an HWND — we use the console window);
// - the 200 Hz loop (5 ms, like DT in the example) reads the angle via getHIDData and
// sets setMagnitude: the smoothed target goes out, the position error
// runs through the "position -> velocity -> torque" loop;
// - hold — a spring-damper on the same effect: force = Kp·(target−pos) − Kd·vel.
//
// Simulation mode: the SDK is never touched, position is integrated, commands print as SIM: ...
public sealed partial class Wheel : IDisposable
{
    // ---- Move-loop parameters (taken from the official SDK example) ----
    private const float SpdKp = 5f;              // velocity: proportional (example: 5)
    private const float SpdKi = 40f;             // velocity: integral (example: 40)
    private const float DegPerSecToRpm = 60f / 360f;
    private const float FastMovePosKp = 4.0f;    // position: hard tracking when smooth=false

    // ---- Hold parameters (spring-damper) ----
    private const float HoldSpringFullDeg = 8f;       // error (°) at which the full force engages: smaller = stiffer (user: "the boost is not strong")
    private const float HoldDampFullDegPerSec = 180f; // velocity at which the full damper engages: smaller = stronger damper (kills the tugging on the snake and on sharp jerks). 150→180 (log 18:51): residual tugging = peak of the ~15 Hz wave (an external disturber in the Pit House chain, our efficiency on it ~56% due to the velSdk 5 ms + EMA 5 ms delay); force steps at the peaks ∝ kd — minus 17% on them and on the hum, physical wave amplitude +~10% (0.18°→0.21° — imperceptible)

    // ---- Smoothness and stops ----
    // Dead zone with hysteresis (anti-chatter): a flat 5°/s threshold toggled
    // the damper on/off 3.3 times/s on SMALL moves versus 1.1/s on large ones
    // (drive log 19:54: "large turns smooth, small ones — gear effect") — the force at
    // the boundary yanked the wheel back and forth like a gear. Engage >7°/s, release <3°/s:
    // at rest the firmware noise stays below the threshold (velFilt in a quiet window = 0),
    // on a ±18°/s wave the damping does not change.
    private const float VelEngageDegPerSec = 7f;     // damper engage, °/s
    private const float VelReleaseDegPerSec = 3f;    // damper release, °/s
    private const float HandFlickerPhysDeg = 1.5f;   // integer-degree A↔B flicker suppression, phys °
    private const float ProgressiveBoostDeg = 30f;    // deviation (°) past which the force smoothly rises to 100% (progressiveHold)
    private const float NudgeBlendDeg = 3f;           // zone near the stop for blending the spring with soft bumps
    private const float NudgeHz = 1.2f;               // frequency of the soft bumps at the stop
    private const float LockApproachDeg = 2f;         // approach zone to the stop in the move loop (smooth, no bouncing)

    // ---- Guards ----
    // Threshold for "something is very wrong": error > 1.5 full lock. Warning only —
    // the force is NOT zeroed (before it zeroed at 60°, and long moves like +100 never arrived).
    private const float PosWarnFactor = 1.5f;

    // HWND required to create FFB effects (in the console app — the console window).
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private static IntPtr _fallbackHwnd;

    // Window for DirectInput: the console window; if there is none (hidden/background start) —
    // we create our own invisible window. The native SDK validates hwnd and on null
    // cleanly returns FFBERR (verified), but better to hand it a working window.
    private static IntPtr GetOrCreateWindow()
    {
        IntPtr h = GetConsoleWindow();
        if (h != IntPtr.Zero) return h;
        h = Process.GetCurrentProcess().MainWindowHandle;
        if (h != IntPtr.Zero) return h;
        if (_fallbackHwnd != IntPtr.Zero) return _fallbackHwnd;

        try
        {
            // The STATIC class is registered by the system — the window is hidden, needed only to bind effects.
            _fallbackHwnd = CreateWindowExW(0, "STATIC", "WheelControl", 0,
                0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);
        }
        catch
        {
            _fallbackHwnd = IntPtr.Zero;
        }
        return _fallbackHwnd;
    }

    private readonly Config _cfg;
    private readonly bool _simulated;
    private readonly Smoothing _smoothing;
    private readonly object _gate = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private string _deviceId = "SIMULATED";
    private IntPtr _hwnd;
    private ETConstantForce? _cf;
    private static bool _sdkInstalled; // install/remove — process-wide, the flag is shared by all instances
    private bool _connected;
    private int _disposed;

    // Control (thread-safe under _gate)
    private volatile bool _holdActive;
    private float _holdPos;           // smoothed "spring": EMA toward _holdTarget — no packet jerks
    private float _holdTarget;        // the set hold target — every command writes here
    private float _holdForcePct;
    private float _holdRamp;          // 0..1 — force ramp when hold engages
    private volatile bool _consoleOverride; // console took control — the socket does not take over the target
    private long _lastSkipPrint;      // throttle for the "socket waiting" message
    private StreamWriter? _logWriter; // CSV loop log (log command) — oscillation diagnostics
    private string _logPath = "";
    private long _logEndMs;
    private float _logVelRaw;         // velocity BEFORE the filter — to the CSV log (ripple diagnostics)
    private float _logVelSdk;         // raw velocity from the SDK — to the CSV log (unit check)
    private float _sockMaxRaw;        // session max |value| from the game (before clamp) — the scale

    // Integer-degree staircase from the game (the mod sends round(angle) -> 0.5° phys step):
    // the smoothed input slope "leads" the target EMA (lead) — the steps are damped,
    // but the wheel moves IN SYNC with the game (user request: "smooth and in sync, but not strong").
    private float _tgtVel;                     // target slope from the socket, phys °/s (EMA over packets)
    private long _lastPktMs;                   // last packet from the game (_clock; 0 = none yet)
    private const float LeadMaxDeg = 2.5f;     // max "lead": packets gone — drift is bounded
    private const float FastSlopeDegPerSec = 60f; // |v|≥60 °/s -> plain τ (sharp moves)
    private bool _lockTarget;         // target sits at the stop (clamped to ±full lock)
    private float _lockDir = 1f;      // stop direction (+1 / −1)
    private float _torquePct = 100f;
    private float _rangeHalf = 90f;   // physical ±° (R3: 180° sweep = ±90; sim = same base)
    private Tuple<int, int>? _motorLimits;

    // Sensors
    private float _lastPos;           // last read angle (°)
    private bool _hasPos;
    private long _lastPosMs;          // when the angle last arrived ("not reading angle" diagnostics)
    private float _velFilt;           // filtered velocity (EMA) — noise must not jerk the damper
    private bool _velEngaged;         // dead-zone hysteresis: the damper "sticks" on/off (see VelEngage)
    private float _pendingVal;        // packet candidate (A↔B flicker suppression, see HandsTo)
    private bool _pendingValid;
    private float _simPos;            // position in simulation
    // Velocity window: HID position arrives in quantized steps; an instant
    // finite difference spikes on EVERY step -> the damper pulses ("gear effect").
    // We take the difference over a ~40 ms sliding window — steps are killed 8×.
    private const int VelRingLen = 8;              // 8 × ~5 ms ≈ 40 ms
    private readonly float[] _velRingPos = new float[VelRingLen];
    private readonly long[] _velRingMs = new long[VelRingLen];
    private int _velRingI;
    private bool _hasVelRing;
    private long _lastTickMs;                // tick before the current one — catch a hole after a stall
    private float _gapT = 1f;                // 0 after a hole -> 1 over GapRampSec (force catch-up)
    private float _lastApplied;              // force actually applied to the wheel (for catch-up)
    private const float GapRampSec = 0.08f;  // smooth force ramp after a hole, ~80 ms
    private const int GapMs = 30;            // a tick > 30 ms = a hole (normal tick is 5 ms)
    private float _posInteg;          // velocity-loop integrator

    // User feedback
    private bool _announcedReach;
    private bool _targetSet;          // whether a move was set (to avoid printing "reached" at startup)
    private long _lastQuietPrint;     // output throttle for the socket stream (30 Hz)
    private long _lastWarnTick;
    private bool _effectBroken;

    // Control loop
    private CancellationTokenSource? _cts;
    private Task? _loop;

    // ---- Two scales ----
    // CONSOLE = physical degrees (user requirement: "hold N = exactly N phys.°
    // under ANY Pit House setting"). GAME/SOCKET = its own scale: ±_userFullLock =
    // full wheel (userFullLockDeg in config.json, 90 = 1:1 with the game by default).
    // Socket conversion: phys = game × _rangeHalf / _userFullLock.
    private float _userFullLock = 90f;
    private float U2P(float user) => user * (_rangeHalf / _userFullLock);
    private float P2U(float phys) => phys * (_userFullLock / Math.Max(_rangeHalf, 1f));

    // Current position snapshot (callers hold _gate): the simulated angle, or the last
    // hardware reading — NaN if the wheel has never reported.
    private float SnapshotPos() => _simulated ? _simPos : (_hasPos ? _lastPos : float.NaN);

    // ---- Public state ----

    public bool IsSimulated => _simulated;
    public bool IsConnected => _connected;
    public bool IsHoldActive => _holdActive;
    // Current target in PHYSICAL degrees (console scale).
    public float CurrentTarget => _smoothing.Target;
    // Full lock to the stop, physical ±° (console scale: angles/hold/status).
    public float MoveRangeHalf => _rangeHalf;
    // Full wheel on the GAME scale, ±° (userFullLockDeg) — the scale for socket numbers.
    public float SocketRangeHalf => _userFullLock;
    // Session max |value| from the game (before clamp) — for tuning userFullLockDeg:
    // if it exceeds SocketRangeHalf — the wheel is pinned at the stop and input is cut (see status).
    public float SocketMaxSeen { get { lock (_gate) { return _sockMaxRaw; } } }
    // Convert the game scale to physical degrees (for socket "hold").
    public float SocketToPhysical(float user) => U2P(user);
    public Tuple<int, int>? MotorLimits => _motorLimits;
    public string? LastWarning { get; private set; }

    public Wheel(Config cfg, bool simulate, string deviceId = "SIMULATED")
    {
        _cfg = cfg;
        _simulated = simulate;
        _deviceId = deviceId;
        _userFullLock = Math.Clamp(cfg.UserFullLockDeg, 10f, 720f);
        _smoothing = new Smoothing(cfg, 0f);
    }

    // ======================== Safe shutdown ========================

    // Idempotent: zero the torque, stop the effects, unload the SDK.
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // 1) Stop the control loop
        try { _cts?.Cancel(); } catch { }
        try { _loop?.Wait(700); } catch { }
        try { _cts?.Dispose(); } catch { }

        lock (_gate)
        {
            StopLogInternal(); // flush/close the CSV log if it was running

            if (_simulated)
            {
                Console.WriteLine("SIM: SetTorque(0) — момент обнулён");
                _connected = false;
                return;
            }

            // 2) Zeroing the torque — the main safety rule
            try { _cf?.setMagnitude(0); } catch { }
            try { _cf?.stop(); } catch { }
            try { stopForceFeedback(); } catch { }
            try { motorStopMove(); } catch { }
            try { if (_sdkInstalled) removeMozaSDK(); } catch { }

            _cf = null;
            _sdkInstalled = false;
            _connected = false;
        }
    }
}
