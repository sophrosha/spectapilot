namespace WheelControlSettings;

// English UI texts: table entries, field tooltips, captions, fallback word.
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class En
    {
        internal static readonly Dictionary<Key, string> Table = new()
        {
            [Key.Title] = "SpectAPilot Handler",
            [Key.Language] = "Language",
            [Key.About] = "About",
            [Key.Save] = "Save",
            [Key.Saved] = "Saved.",
            [Key.SavedRunning] = "Saved. WheelControl is running — restart it to apply the changes.",
            [Key.SaveFailed] = "Save failed:",
            [Key.Version] = "Version",
            [Key.ConfigMissing] = "config.json was not found in the Assets folder next to WheelControlSettings.exe — the app cannot continue.",
            [Key.ConsoleMissing] = "WheelControl.exe was not found next to WheelControlSettings.exe — the Demo button will be unavailable.",
            [Key.BadConfig] = "config.json is not valid JSON — fix the file manually.",
            [Key.DemoStarted] = "Demo started.",
            [Key.DemoFailed] = "Failed to start WheelControl:",
            [Key.DemoNoExe] = "WheelControl.exe was not found next to WheelControlSettings.exe.",
            [Key.DemoNoWindow] = "WheelControl did not open a window. Check that the exe is not blocked by the antivirus or crashing — start it manually.",
            [Key.Subtitle] = "Steering angle packet handler",
            [Key.ProjectHeader] = "About the project",
            [Key.ProjectText] = "Synchronization of the BeamNG.drive virtual autopilot with a physical steering wheel.",
            [Key.LicensesHeader] = "Licenses",
            [Key.ModTip] = "The mod is being published",
        };

        internal static readonly Dictionary<string, string> Tips = new(StringComparer.Ordinal)
        {
            ["debug"] = "Show debug messages in the console.",
            ["smooth"] = "true — smooth motion toward the target; false — as fast as possible (speed and torque are still limited).",
            ["smoothTimeMs"] = "Higher = smoother, but with more delay. Recommended: 300–800 ms.",
            ["maxTorque"] = "Peak torque, % of maximum (hardware setMotorPeakTorque limit, allowed 50..100).",
            ["holdForce"] = "How hard the wheel holds its position. 100% = maximum.",
            ["holdPosition"] = "Default hold position in degrees (used when the current angle is unknown).",
            ["socketPort"] = "UDP port for receiving angles from BeamNG. Default: 7777.",
            ["socketHost"] = "UDP server address (localhost only — security).",
            ["dataIntervalMs"] = "Expected input interval in ms (~30 Hz from the mod). Informational/debug only.",
            ["socketSmoothTimeMs"] = "Smoothing time for socket commands in ms (small = almost no delay from the mod).",
            ["userFullLockDeg"] = "Full wheel travel in user degrees: 90 = 1:1 with base physics, 180 = twice the range.",
            ["invertSocket"] = "Invert the incoming angle sign if the wheel turns the wrong way.",
            ["socketHandsMode"] = "Engages extra hold mechanisms in the wheel so you can safely hold on to it.",
            ["socketAutoStart"] = "true — the UDP server starts automatically at launch (no need to type socket in the console).",
            ["progressiveHold"] = "Progressive hold: force ramps smoothly up to 100% as resistance on the wheel increases.",
            ["lockNudge"] = "At the steering lock — soft rhythmic nudges (6–12% force, ~1.2 Hz) instead of a hard stop.",
            ["simulate"] = "true — don't touch the SDK, run without a wheel (same as --simulate).",
            ["maxDegPerSec"] = "Anti-jerk safety cap: maximum angle change rate, deg/s.",
            ["holdRampMs"] = "Hold force ramp-up time when hold engages, ms (to avoid a jerk).",
            ["moveForceLimit"] = "Force limit for moving to the target, 0–10000 (DirectInput scale).",
            ["maxMoveSpeedRpm"] = "Motor speed limit while moving to the target, RPM.",
            ["forceInvert"] = "Invert torque sign: set true if the wheel runs to the lock instead of moving to the target.",
            ["kpScale"] = "Hold spring stiffness multiplier (Kp): 1.0 = default, lower = softer, higher = stiffer.",
            ["kdScale"] = "Hold damping multiplier (Kd): 1.0 = default, higher = stronger damping.",
        };

        internal const string FallbackTipWord = "Setting";

        internal static readonly Dictionary<string, string> Captions = new(StringComparer.Ordinal)
        {
            ["debug"] = "Debug",
            ["smooth"] = "Smooth motion",
            ["smoothTimeMs"] = "Smooth time, ms",
            ["maxTorque"] = "Max torque, %",
            ["holdForce"] = "Hold force, %",
            ["holdPosition"] = "Hold position, °",
            ["socketPort"] = "Socket port",
            ["socketHost"] = "Socket host",
            ["dataIntervalMs"] = "Data interval, ms",
            ["socketSmoothTimeMs"] = "Socket smooth, ms",
            ["userFullLockDeg"] = "Full lock, °",
            ["invertSocket"] = "Invert socket",
            ["socketHandsMode"] = "Mode: hold my wheel",
            ["socketAutoStart"] = "Auto-start socket",
            ["progressiveHold"] = "Progressive hold",
            ["lockNudge"] = "Lock nudge",
            ["simulate"] = "Simulate",
            ["maxDegPerSec"] = "Max speed, °/s",
            ["holdRampMs"] = "Hold ramp, ms",
            ["moveForceLimit"] = "Move force limit",
            ["maxMoveSpeedRpm"] = "Max move speed, rpm",
            ["forceInvert"] = "Invert force",
            ["kpScale"] = "Spring stiffness (Kp)",
            ["kdScale"] = "Damping (Kd)",
        };
    }
}
