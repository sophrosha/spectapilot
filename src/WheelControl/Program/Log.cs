using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace WheelControl;

// Console log: "[ 0.000 ] LEVEL : text" — seconds since start, invariant culture.
// DEBUG lines print only when "debug": true is set in config.json. The flag is
// read raw from JSON: Config.cs is stable code and has no Debug field.
internal static partial class Program
{
    private static readonly Stopwatch _logClock = Stopwatch.StartNew();
    private static readonly object _logLock = new();
    private static bool _debugEnabled;

    // Real console sinks captured before any mute (see Init.cs): our own lines
    // and colors must bypass the mute window — Console.Out is swapped to
    // TextWriter.Null there, and Console's color API would hit the NUL handle.
    private static TextWriter? _realOut;
    private static TextWriter? _realErr;
    private static IntPtr _realStdOut;

    private static void InitLog()
    {
        // Capture FIRST, before the debug-flag read may return early.
        _realOut ??= Console.Out;
        _realErr ??= Console.Error; // pre-materialized: crash output must never bind NUL
        if (_realStdOut == IntPtr.Zero) _realStdOut = GetStdHandle(StdOutputHandleId);

        try
        {
            if (!File.Exists(Config.FilePath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(Config.FilePath));
            if (doc.RootElement.TryGetProperty("debug", out var flag) &&
                flag.ValueKind == JsonValueKind.True)
                _debugEnabled = true;
        }
        catch { /* unreadable config → no debug output */ }
    }

    private static void LogInfo(string text) => WriteLog("INFO", text);

    private static void LogError(string text) => WriteLog("ERROR", text);

    private static void LogDebug(string text)
    {
        if (_debugEnabled) WriteLog("DEBUG", text);
    }

    private static void WriteLog(string level, string text)
    {
        double sec = _logClock.Elapsed.TotalSeconds;
        string stamp = sec.ToString("0.000", CultureInfo.InvariantCulture);
        lock (_logLock)
        {
            TextWriter w = _realOut ?? Console.Out;
            w.WriteLine($"[ {stamp} ] {level} : {text}");
            w.Flush();
        }
    }

    // Orange hint (focus gate / slow device) — DarkYellow before WriteLine,
    // restore after. The attribute is set on the REAL console handle captured
    // at startup: while muted, Console.ForegroundColor would target the NUL
    // handle we swapped in, and the hint would lose its color.
    private static void PrintOrange(string text)
    {
        lock (_logLock)
        {
            TextWriter w = _realOut ?? Console.Out;
            NativeScreenBufferInfo prev = default;
            bool colored = _realStdOut != IntPtr.Zero &&
                           GetConsoleScreenBufferInfo(_realStdOut, out prev) &&
                           SetConsoleTextAttribute(_realStdOut, AttributeDarkYellow);
            w.WriteLine(text);
            w.Flush();
            if (colored) SetConsoleTextAttribute(_realStdOut, prev.Attributes);
        }
    }
}
