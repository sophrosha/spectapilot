namespace WheelControlSettings;

// Step 3: locates config.json and WheelControl.exe — both apps share
// bin/Release, so they are searched next to the Settings exe first; clear error
// message when either is missing.
internal static class PathResolver
{
    // Absolute path of config.json next to the Settings exe, or null when missing.
    public static string? ConfigPath { get; private set; }

    // Absolute path of the console exe next to the Settings exe, or null when missing.
    public static string? ConsoleExePath { get; private set; }

    public static void Resolve()
    {
        string dir = AppContext.BaseDirectory;
        string config = Path.Combine(dir, "config.json");
        string exe = Path.Combine(dir, "WheelControl.exe");
        ConfigPath = File.Exists(config) ? config : null;
        ConsoleExePath = File.Exists(exe) ? exe : null;
    }
}
