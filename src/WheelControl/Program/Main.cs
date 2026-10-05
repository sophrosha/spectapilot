using System.Diagnostics;
using System.Text;

namespace WheelControl;

// Entry point: CLI args -> focus gate -> fastboot device watch -> connect ->
// demo (per --demo; no socket in demo mode) or the UDP socket in the normal
// mode. No stdin commands: the console window is a
// log view; closing it exits the app (torque is zeroed in Shutdown).
internal static partial class Program
{
    private static Config _cfg = new();
    private static Wheel? _wheel;
    private static SocketServer? _socket;
    private static readonly SemaphoreSlim _dispatchGate = new(1, 1); // serialize socket commands

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "WheelControl";

        // Above normal priority: the game (loading/hitches) can preempt the 200 Hz
        // loop — skipped ticks accumulate and the force overshoots the target.
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; }
        catch { /* not critical — run at normal priority */ }

        // Step 2 of the spec: parse --demo / --demo_time / --demo_amplitude (defaults when absent).
        var cli = ParseArgs(args);
        _cfg = Config.Load();
        InitLog();
        Strings.Load(); // console texts follow config.json's "uiLanguage" (default ru)

        // One wheel owner: kill the previous instance first (as in stable).
        CloseOtherInstances();
        // Visible and focused when started from Settings.
        SetForegroundWindow(GetConsoleWindow());

        // Ctrl+C / console window close: zero the moment first, then let the process die.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = false; // let the process terminate AFTER the handler runs
            Shutdown();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        // A crash inside the mute window must still print — unmute first.
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreNativeOutput();

        Console.WriteLine(Strings.T(Strings.Key.AwaitingDevice));

        Wheel wheel;
        if (_cfg.Simulate)
        {
            wheel = new Wheel(_cfg, true, "SIMULATED");
            wheel.Connect(out _);
        }
        else
        {
            // Spec: no installMozaSDK() until the console window is focused.
            await WaitForFocusAsync();
            wheel = await ConnectLoopAsync();
        }

        _wheel = wheel;
        wheel.Start();

        // Startup parking (spec): the wheel may stand anywhere (e.g. full lock
        // at -95°) while the socket is still silent — with stable's default
        // target 0 the move loop would wind up and drag it to the center with
        // overshoot before any packet arrives. Park the target on the CURRENT
        // angle first: zero error, the wheel stands and waits for the socket.
        await ParkTargetAsync(wheel);

        if (cli.DemoEnabled)
        {
            // Spec item 2: demo runs without the UDP socket — SocketServer is
            // never started here, so the log must not name a socket either.
            LogInfo(Strings.T(Strings.Key.DeviceConnected));

            // Spec item 3: stable's StartDemo prints its own long hint — silence
            // Console for the duration of the call (our Log*/PrintOrange lines go
            // to the captured writer and are unaffected). Not editing Wheel.cs.
            Muted(() => wheel.StartDemo(cli.DemoTime, cli.DemoAmplitude));

            // Own short INFO instead. Show what actually runs: mirror stable's
            // StartDemo defaults (DemoDefaultSec is private) and its clamp to
            // ±MoveRangeHalf — same formula as Wheel.StartDemo.
            float seconds = float.IsNaN(cli.DemoTime) || cli.DemoTime <= 0f ? 50f : cli.DemoTime;
            float amp = float.IsNaN(cli.DemoAmplitude) || cli.DemoAmplitude <= 0f
                ? wheel.MoveRangeHalf
                : MathF.Abs(cli.DemoAmplitude);
            amp = Math.Clamp(amp, 1f, wheel.MoveRangeHalf);
            LogInfo(Strings.T(Strings.Key.DemoInfo, seconds, amp));
        }
        else
        {
            // Normal mode: the socket starts exactly as before.
            LogInfo(Strings.T(Strings.Key.DeviceConnectedSocket, _cfg.SocketHost, _cfg.SocketPort));
            if (_cfg.SocketAutoStart)
                await StartSocketAsync();
        }

        // Demo exit: smooth overshoot-free return to 0 after StopDemo —
        // polled edge (stable has no event), see DemoExit.cs.
        _ = DemoExitWatchAsync();

        // No prompt loop: idle until the console window is closed or Ctrl+C.
        await Task.Delay(Timeout.Infinite);
    }

    // ======================== Socket command dispatcher ========================
    // Same semantics as stable's HandleLine, minus the console-only commands
    // (socket/keyboard/help/exit — there is no stdin here).

    private static long _lastUnknownPrint; // throttle "unknown command" spam (30 datagrams/s)

    private static async Task HandleSocketLineAsync(string line)
    {
        var cmd = InputHandler.Parse(line, fromSocket: true, invert: _cfg.InvertSocket);

        // Pause does not hold the shared gate: a 30 Hz socket thread must not
        // queue up behind a sleep.
        if (cmd.Kind == CommandKind.Sleep)
        {
            await Task.Delay((int)Math.Clamp(cmd.Value, 0, 60_000));
            return;
        }

        // Debug mode: echo every accepted value ("[ 0.000 ] DEBUG : rotate 90").
        // The string is only built when debug output is on (30 datagrams/s path).
        if ((cmd.Kind is CommandKind.MoveAbsolute or CommandKind.MoveRelative) && _debugEnabled)
            LogDebug($"rotate {line.Trim()}");

        await _dispatchGate.WaitAsync();
        try
        {
            var wheel = _wheel;
            if (wheel is null) return;

            // Stable's demo must not fight a socket command: stop it first,
            // with the same reason strings the console always printed.
            void StopDemoIfActive(string reason)
            {
                if (wheel.IsDemoActive) wheel.StopDemo(reason);
            }

            // Shared move tail: hands mode tracks hard, the normal mode sets
            // the target quietly — identical in both move cases.
            void ApplyMove(float degrees)
            {
                if (_cfg.SocketHandsMode)
                    HandsToQuiet(wheel, degrees); // "hold my hands": hard tracking
                else
                    wheel.SetPosition(degrees, quiet: true);
            }

            switch (cmd.Kind)
            {
                case CommandKind.MoveAbsolute:
                    StopDemoIfActive("отменена командой");
                    ApplyMove(cmd.Value);
                    break;

                case CommandKind.MoveRelative:
                    StopDemoIfActive("отменена командой");
                    ApplyMove(wheel.GetReferencePosition() + cmd.Value);
                    break;

                case CommandKind.Hold:
                {
                    StopDemoIfActive("отменена командой");
                    float pos = cmd.Value; // NaN = no argument
                    if (float.IsNaN(pos))
                    {
                        pos = wheel.GetPosition(); // physical degrees
                        if (float.IsNaN(pos))
                            pos = _cfg.HoldPosition; // no data yet — from config
                    }
                    // Socket hold comes in the game's scale.
                    pos = wheel.SocketToPhysical(pos);
                    pos = Math.Clamp(pos, -wheel.MoveRangeHalf, wheel.MoveRangeHalf);
                    wheel.EnableHold(pos, _cfg.HoldForce);
                    break;
                }

                case CommandKind.Release:
                    StopDemoIfActive("отменена командой");
                    wheel.DisableHold();
                    break;

                case CommandKind.Demo:
                    StopDemoIfActive("перезапуск");
                    wheel.StartDemo(cmd.Value, cmd.Value2);
                    break;

                case CommandKind.Status:
                {
                    float pos = wheel.GetPosition();
                    LogInfo($"status: pos={(float.IsNaN(pos) ? "n/a" : $"{pos:0.#}°")} " +
                            $"target={wheel.CurrentTarget:0.#}° " +
                            $"hold={(wheel.IsHoldActive ? "ON" : "OFF")} demo={(wheel.IsDemoActive ? "ON" : "OFF")}");
                    break;
                }

                default:
                    if (line.Trim().Length > 0)
                    {
                        // Garbage from the socket must not flood the console at 30 lines/s.
                        long now = Environment.TickCount64;
                        if (now - _lastUnknownPrint >= 2000)
                        {
                            _lastUnknownPrint = now;
                            LogError($"Unknown command from socket: {line.Trim()}");
                        }
                    }
                    break;
            }
        }
        finally
        {
            _dispatchGate.Release();
        }
    }

    // Startup parking (spec): put the move-loop target exactly where the wheel
    // already stands, so ComputeMoveForce sees zero error and the wheel waits
    // for the first socket packet instead of racing to the default 0°.
    private static async Task ParkTargetAsync(Wheel wheel)
    {
        // GetPosition() is NaN until the first HID sample — that read lives in
        // Wheel's own 200 Hz Tick (it starts with wheel.Start()), so poll
        // in short steps for up to ~500 ms before giving up on parking.
        float pos = float.NaN;
        long deadline = Environment.TickCount64 + 500;
        while (Environment.TickCount64 < deadline)
        {
            pos = wheel.GetPosition(); // physical degrees
            if (!float.IsNaN(pos)) break;
            await Task.Delay(20);
        }
        if (float.IsNaN(pos)) return; // still unreadable — leave stable's default

        // EnableHold(pos, 0f) + DisableHold parks _smoothing.Target on the
        // current angle (SnapTo + SetTarget) and releases immediately:
        //  - PHYSICAL degrees (SetPosition can't be used: its quiet argument is
        //    the USER scale — U2P would shift the target, userFullLockDeg=180
        //    vs ±90 physical — and it sets _targetSet, which makes stable
        //    announce the "reached: …°" line right at startup);
        //  - force 0 and DisableHold right away: no force, and DisableHold
        //    clears the console override EnableHold sets;
        //  - _targetSet is not touched, so no reach announcement appears.
        // Mute the calls' own output (SIM prints): the user does not need these
        // technical lines. Demo/socket logic is untouched.
        Muted(() =>
        {
            wheel.EnableHold(pos, 0f);
            wheel.DisableHold();
        });
    }

    // Hands-mode dispatch (spec: capture message format). Stable's hardware
    // engage line ("hands: ...", printed once on the !hold -> hold transition
    // inside HandsTo) is dropped by a filtering writer — Wheel.cs is stable
    // code and must not be edited — and replaced by our localized INFO with the
    // actual physical target. Runs only on the first packet of a capture;
    // subsequent packets and SIM output pass through untouched (smoke scripts
    // match on stable's SIM lines).
    private static void HandsToQuiet(Wheel wheel, float degrees)
    {
        bool capture = !wheel.IsHoldActive; // first packet of this capture
        if (!capture || wheel.IsSimulated)
        {
            wheel.HandsTo(degrees, _cfg.HoldForce);
            return;
        }

        WithOut(new HandsLineFilter(Console.Out), () => wheel.HandsTo(degrees, _cfg.HoldForce));

        // Print only when the engage really happened: otherwise the packet was
        // skipped under a console override (its stable warning went through
        // the filter) and no capture took place.
        if (wheel.IsHoldActive)
            LogInfo(Strings.T(Strings.Key.HandsCaptured, wheel.CurrentTarget));
    }
}

// Pass-through Console writer that drops exactly stable's hands engage line —
// everything else (including the "-> socket waiting: ..." override warning printed
// by the same HandsTo call) reaches the console as before.
internal sealed class HandsLineFilter(TextWriter inner) : TextWriter
{
    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value) => inner.Write(value);

    public override void Write(string? value) => inner.Write(value);

    public override void WriteLine() => inner.WriteLine();

    public override void WriteLine(string? value)
    {
        if (value is not null && value.StartsWith("hands: ", StringComparison.Ordinal)) return;
        inner.WriteLine(value);
    }
}
