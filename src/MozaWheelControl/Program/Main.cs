using System.Diagnostics;
using System.Text;

namespace MozaWheelControl;

// Entry point: CLI args -> focus gate -> fastboot device watch -> connect ->
// demo (per --demo) -> UDP socket. No stdin commands: the console window is a
// log view; closing it exits the app (torque is zeroed in Shutdown).
internal static partial class Program
{
    private static Config _cfg = new();
    private static MozaWheel? _wheel;
    private static SocketServer? _socket;
    private static readonly SemaphoreSlim DispatchGate = new(1, 1); // serialize socket commands

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "MozaWheelControl";

        // Above normal priority: the game (loading/hitches) can preempt the 200 Hz
        // loop — skipped ticks accumulate and the force overshoots the target.
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal; }
        catch { /* not critical — run at normal priority */ }

        // Step 2 of the spec: parse --demo / --demo_time / --demo_amplitude (defaults when absent).
        var cli = ParseArgs(args);
        _cfg = Config.Load();
        InitLog();

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

        Console.WriteLine("Awaiting for device...");

        MozaWheel wheel;
        if (_cfg.Simulate)
        {
            wheel = new MozaWheel(_cfg, true, "SIMULATED");
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
        LogInfo($"Device connected. Socket: {_cfg.SocketHost}:{_cfg.SocketPort}");

        // Spec: start demo right after connect — do not wait for the socket.
        if (cli.DemoEnabled)
            wheel.StartDemo(cli.DemoTime, cli.DemoAmplitude);

        if (_cfg.SocketAutoStart)
            await StartSocketAsync();

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
        if (cmd.Kind is CommandKind.MoveAbsolute or CommandKind.MoveRelative)
            LogDebug($"rotate {line.Trim()}");

        await DispatchGate.WaitAsync();
        try
        {
            var wheel = _wheel;
            if (wheel is null) return;

            switch (cmd.Kind)
            {
                case CommandKind.MoveAbsolute:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    if (_cfg.SocketHandsMode)
                        wheel.HandsTo(cmd.Value, _cfg.HoldForce); // "hold my hands": hard tracking
                    else
                        wheel.SetPosition(cmd.Value, quiet: true);
                    break;
                }

                case CommandKind.MoveRelative:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    float basePos = wheel.GetReferencePosition();
                    if (_cfg.SocketHandsMode)
                        wheel.HandsTo(basePos + cmd.Value, _cfg.HoldForce);
                    else
                        wheel.SetPosition(basePos + cmd.Value, quiet: true);
                    break;
                }

                case CommandKind.Hold:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
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
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("отменена командой");
                    wheel.DisableHold();
                    break;
                }

                case CommandKind.Demo:
                {
                    if (wheel.IsDemoActive) wheel.StopDemo("перезапуск");
                    wheel.StartDemo(cmd.Value, cmd.Value2);
                    break;
                }

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
            DispatchGate.Release();
        }
    }
}
