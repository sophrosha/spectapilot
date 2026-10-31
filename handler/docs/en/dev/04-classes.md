# Classes

The console code lives in `src/WheelControl/`, the settings code in `src/WheelControlSettings/`.

## Wheel (`src/WheelControl/Wheel/`)

A wrapper over the Moza SDK plus the background control loop. A partial class split by role: `Wheel.cs` (SDK install, the ConstantForce effect, degree scales), `Wheel.Connect` (connecting to the base), `Wheel.Loop` (the 200 Hz tick), `Wheel.Api` (position/torque), `Wheel.Hold` (the hold spring-damper), `Wheel.Force` (force computation, kpScale/kdScale), `Wheel.Socket` (packets from the game, hands mode), `Wheel.Demo` (the driving simulation), `Wheel.Log` (CSV logging).

Key members: `InstallSdk()`, `Connect()`, `Start()` (starts the loop), `SetPosition()`, `SetTorque()`, `GetPosition()`, `EnableHold()` / `DisableHold()`, `HandsTo()`, `StartDemo()` / `StopDemo()`, `StartLog()` / `StopLog()`, `SocketToPhysical()`, `Dispose()`.

## Program (`src/WheelControl/Program/`)

The entry point and lifecycle of the console: `Main.cs` (the main thread, reading commands from stdin and from the socket — `HandleSocketLineAsync`), `Init.cs` (DLL directory, silencing SDK noise, `StartSocketAsync`, `CloseOtherInstances`), `Log.cs` (the log file, `debug` mode), `Args.cs` (`--simulate` and other flags), `DeviceWatch` / `FocusWait` / `DemoExit` / `Muted` (base hints, focus waiting, demo shutdown, output muting).

## Config (`src/WheelControl/Config/Config.cs`)

The configuration: properties with defaults right in the code, reading/writing `Assets/config.json` and range clamping. Key members: `Load()`, `Save()`, `Normalize()` (clamps values to their valid ranges), `FilePath`.

## SocketServer (`src/WheelControl/SocketServer/SocketServer.cs`)

The UDP listener for socket mode (no TCP — the mod only sends UDP). One datagram = one command, parsed by the shared `InputHandler`; there is no reply to the client. Key members: `Start()`, `IsRunning`, `Endpoint`, `DisposeAsync()`; the receive loop is private (`ReceiveLoopAsync`).

## InputHandler (`src/WheelControl/InputHandler/InputHandler.cs`)

The command parser — one for both the console and the socket (the signs differ: on the socket `-45` is a position, in the console a shift). Key members: `Parse(line, fromSocket, invert)`, `TryParseNumber()`, the `CommandKind` enum, the `Command` struct.

## Smoothing (`src/WheelControl/Smoothing/Smoothing.cs`)

The math behind the smooth motion: input only writes the target, and `Step(dt)` on every tick brings the current position to it (by `smoothTimeMs`, or by the socket override). Key members: `SetTarget()`, `Step()`, `SnapTo()`, `SetTauMs()`, the properties `Current` / `Target` / `EffectiveTauMs`.

## WheelControlSettings (briefly)

- `MainForm` (plus the partial files `Texts` / `Status` / `Demo`) — the settings window: button events, a status line with 5 s auto-clear, Demo launching.
- `Layout` — the whole window layout is built in code (no designer).
- `ConfigBinding` — table fields are generated from the `config.json` keys, unknown keys are preserved.
- `Strings` — 4 UI languages (ru/en/uk/by), the `Strings.T()` hub.
- `AboutForm`, `AppInit`, `PathResolver`, `ProcessWatch` — the About dialog, initialization, paths, watching the console process.

---
[← Back to contents](../../README.md) | [Next section →](05-faq.md)
