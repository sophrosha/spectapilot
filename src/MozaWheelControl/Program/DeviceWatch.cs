namespace MozaWheelControl;

// Fastboot-style device watch: mute native/SDK noise (normal mode only),
// install the SDK (only after the focus gate), poll every 500 ms forever,
// connect with retries. Never exits — the app keeps waiting until the
// console window is closed.
internal static partial class Program
{
    private static volatile bool _connected; // set on successful connect
    private static int _deviceHintShown;     // Interlocked: the hint prints once
    private static int _connectFailsSeen;    // consecutive failed Connect() attempts
    private static int _connectFailReported; // Interlocked: the summary prints once

    // One neutral ERROR after this many consecutive failures so a broken setup
    // is never an endless silent wait in the normal mode. One attempt takes
    // ~6-9 s, so 4 ≈ 30 s — a probabilistic first-attempt SEH is long gone
    // by then, but a real problem is reported early.
    private const int ConnectFailReportAttempts = 4;

    private static async Task<MozaWheel> ConnectLoopAsync()
    {
        // One-shot hint: 20 s after the watch starts, if we are still not
        // connected (device missing OR connect still retrying) — orange, once.
        // It must not live inside the poll loop: when the device IS found while
        // connect fails, the "device not found" branch never runs again.
        _ = StartDeviceHintTimerAsync();

        // Spec: mute BEFORE installMozaSDK() — native printf + stable's Console
        // lines go quiet for the whole install/connect window (debug stays loud).
        MuteNativeOutput();

        // Focus gate has already passed — safe to touch the SDK now.
        if (!MozaWheel.InstallSdk(out string sdkError))
            LogError(Strings.T(Strings.Key.SdkInitFailed, sdkError));

        while (true)
        {
            string id = await WatchAsync();
            var wheel = new MozaWheel(_cfg, false, id);
            if (wheel.Connect(out string error))
            {
                _connected = true;
                RestoreNativeOutput(); // spec: unmute AFTER a successful connect
                return wheel;
            }

            // Probabilistic SEH inside the SDK (E_FAIL on the first effect.start())
            // is fixed by the retry anyway: the per-attempt ERROR with stable's
            // "Проверь: руль в центре..." text is debug-only noise in the normal
            // mode — quiet retry there (spec item 1/4).
            if (_debugEnabled)
                LogError(Strings.T(Strings.Key.ConnectFailed, error));

            // ...but never wait silently forever: one neutral line after N
            // consecutive failures, printed once in either mode (spec item 2).
            if (Interlocked.Increment(ref _connectFailsSeen) >= ConnectFailReportAttempts &&
                Interlocked.Exchange(ref _connectFailReported, 1) == 0)
                LogError(Strings.T(Strings.Key.ConnectSummary));

            wheel.Dispose();
            await Task.Delay(1000); // back to polling — the device may reconnect
        }
    }

    private static async Task StartDeviceHintTimerAsync()
    {
        await Task.Delay(20_000);
        if (!_connected && Interlocked.Exchange(ref _deviceHintShown, 1) == 0)
            PrintOrange(Strings.T(Strings.Key.WaitHint));
    }

    private static async Task<string> WatchAsync()
    {
        bool oddDetailShown = false;
        while (true)
        {
            string? id = MozaWheel.PollDevice(out string detail);
            if (id is not null) return id;

            // Unexpected SDK code (not "no devices") — one ERROR line for hardware debugging.
            if (!oddDetailShown &&
                detail is not ("NODEVICES" or "NOINSTALLSDK" or "NORMAL" or "?"))
            {
                oddDetailShown = true;
                LogError($"getMotorLimitAngle -> {detail} (keep waiting; if the wheel is plugged in, check MOZA Pit House)");
            }

            await Task.Delay(500);
        }
    }
}
