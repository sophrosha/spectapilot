using System.Text.Json;
using System.Text.Json.Serialization;

namespace WheelControl;

// Application configuration. config.json sits next to the exe.
// Loaded at startup; if the file is missing — created with default values.
public sealed class Config
{
    // ---------- Fields from the spec ----------

    // true — smooth motion to the target; false — as fast as possible (speed and torque are limited anyway).
    public bool Smooth { get; set; } = true;

    // Time to reach the target, ms (~95% of the way in that time when smooth=true).
    public int SmoothTimeMs { get; set; } = 500;

    // Max torque, % of maximum (hardware limit setMotorPeakTorque, allowed 50..100).
    public float MaxTorque { get; set; } = 100.0f;

    // Hold force, % of maximum (0..100 -> DirectInput scale 0..10000).
    public float HoldForce { get; set; } = 50.0f;

    // Default hold position, degrees (used when the current angle is unknown).
    public float HoldPosition { get; set; } = 90.0f;

    // UDP server port (socket mode).
    public int SocketPort { get; set; } = 7777;

    // UDP server address (localhost only — security).
    public string SocketHost { get; set; } = "127.0.0.1";

    // Expected input interval, ms (~30 Hz from the mod). Informational/debug only.
    public int DataIntervalMs { get; set; } = 33;

    // Smoothing time for SOCKET commands, ms (small = almost no latency from the mod).
    public int SocketSmoothTimeMs { get; set; } = 50;

    // Full wheel lock in USER degrees: 90 = 1:1 with the base physics (default),
    // 180 = twice as wide a scale (the old variant, "values up to 180 instead of 90").
    public float UserFullLockDeg { get; set; } = 90.0f;

    // Invert the numbers coming FROM THE GAME (socket): if the in-game wheel turns
    // the wrong way relative to the real one — set true. Does not affect the console.
    public bool InvertSocket { get; set; } = true;

    // "Hold my hand" mode for the socket (hands): game positions are held
    // by a hard hold spring with holdForce (100% = hard to push the wheel away),
    // instead of a soft move. Engages automatically on the first packet.
    public bool SocketHandsMode { get; set; } = true;

    // true — the UDP server comes up by itself at startup (no need to type socket in the console).
    public bool SocketAutoStart { get; set; } = false;

    // Progressive hold boost: if the hand increased the deviation from the target,
    // the force smoothly rises to 100% ("feels the effort — helps carefully").
    public bool ProgressiveHold { get; set; } = true;

    // At the stop (target = full lock): soft rhythmic bumps of 6..12% force at ~1.2 Hz
    // instead of slamming hard into the mechanical stop.
    public bool LockNudge { get; set; } = true;

    // Hold spring stiffness multiplier (Kp): 1.0 = default (full force at 8°),
    // 0.5 = twice as soft (a smoother fight with the hand on micro deviations), 2.0 = stiffer.
    // Tunable without rebuild; demo and hold use the same multiplier.
    public float KpScale { get; set; } = 1.0f;

    // Hold damper multiplier (Kd): 1.0 = default (full at 180 °/s).
    // Below 1 — weaker damping of the ~15 Hz wave (the jitter returns), above 1 —
    // a stiffer damper and a slower approach (max speed = 180/KdScale °/s).
    public float KdScale { get; set; } = 1.0f;

    // ---------- Extra fields (have defaults, not in the spec) ----------

    // true — do not touch the SDK, run without a wheel (equivalent to --simulate).
    public bool Simulate { get; set; } = false;

    // Jerk guard: maximum rate of angle change, °/s.
    public float MaxDegPerSec { get; set; } = 720.0f;

    // Hold force ramp-up time on engage, ms (so it does not jerk).
    public int HoldRampMs { get; set; } = 300;

    // Force limit for moving to the target, 0..10000 (DirectInput scale; the SDK example used 800).
    public float MoveForceLimit { get; set; } = 1500.0f;

    // Motor speed limit while moving to the target, rpm (SDK example: 160).
    public float MaxMoveSpeedRpm { get; set; } = 90.0f;

    // Invert the torque sign. If in the first test the wheel drives to the stop instead of to the target —
    // set true (the motor sign depends on the base model/firmware).
    public bool ForceInvert { get; set; } = false;

    // ---------- File handling ----------

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    // Path to config.json (the exe folder).
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "config.json");

    // Load the config; if missing or broken — create/return the defaults.
    public static Config Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var def = new Config();
                def.Save();
                return def;
            }

            var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath), JsonOpts) ?? new Config();
            cfg.Normalize();
            return cfg;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! Ошибка чтения config.json: {ex.Message}. Используются значения по умолчанию.");
            var def = new Config();
            def.Normalize();
            return def;
        }
    }

    // Save the config to the file.
    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! Не удалось сохранить config.json: {ex.Message}");
        }
    }

    // Clamp the values to valid ranges (guard against a broken file).
    public void Normalize()
    {
        SmoothTimeMs = Math.Clamp(SmoothTimeMs, 50, 10_000);
        MaxTorque = Math.Clamp(MaxTorque, 50f, 100f);      // setMotorPeakTorque range: 50..100
        HoldForce = Math.Clamp(HoldForce, 0f, 100f);
        DataIntervalMs = Math.Clamp(DataIntervalMs, 5, 1000);
        SocketSmoothTimeMs = Math.Clamp(SocketSmoothTimeMs, 10, 5000);
        UserFullLockDeg = Math.Clamp(UserFullLockDeg, 10f, 720f);
        SocketPort = Math.Clamp(SocketPort, 1, 65535);
        SocketHost = string.IsNullOrWhiteSpace(SocketHost) ? "127.0.0.1" : SocketHost.Trim();
        MaxDegPerSec = Math.Clamp(MaxDegPerSec, 30f, 5000f);
        HoldRampMs = Math.Clamp(HoldRampMs, 0, 5000);
        MoveForceLimit = Math.Clamp(MoveForceLimit, 100f, 10_000f);
        MaxMoveSpeedRpm = Math.Clamp(MaxMoveSpeedRpm, 5f, 300f);
        HoldPosition = Math.Clamp(HoldPosition, -3600f, 3600f);
        KpScale = Math.Clamp(KpScale, 0.1f, 3f);   // guard against manually "breaking the loop"
        KdScale = Math.Clamp(KdScale, 0.1f, 3f);
    }
}
