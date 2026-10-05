using System.Globalization;
using System.Text.Json;

namespace WheelControl;

// All user-visible console texts in four languages (ru / en / uk / by) — the
// same "uiLanguage" key and codes as the Settings app. Loaded once at startup:
// the console reads config.json before the first line is printed, so a language
// switch in Settings applies on the next console run (spec item 1).
internal static partial class Strings
{
    internal enum Key
    {
        AwaitingDevice,
        FocusHint,
        WaitHint,
        DeviceConnected,
        DeviceConnectedSocket,
        ConnectFailed,
        ConnectSummary,
        DemoInfo,
        SdkInitFailed,
        SocketListening,
        SocketStartFailed,
        ClosedPrevious,
        HandsCaptured,
    }

    // Current UI language; default per spec — ru.
    public static string Language { get; private set; } = "ru";

    // Per-language data lives in Strings.<lang>.cs (nested Ru/En/Uk/By classes);
    // this registry keeps the original by-language lookup unchanged.
    private static readonly Dictionary<string, Dictionary<Key, string>> Table = new()
    {
        ["ru"] = Ru.Table,
        ["en"] = En.Table,
        ["uk"] = Uk.Table,
        ["by"] = By.Table,
    };

    public static string T(Key key, params object[] args)
    {
        string format = Table.TryGetValue(Language, out var texts) && texts.TryGetValue(key, out var text)
            ? text
            : Table["en"][key];
        // Invariant culture, like the log stamps: "100.5", not "100,5".
        return args.Length == 0 ? format : string.Format(CultureInfo.InvariantCulture, format, args);
    }

    // Read "uiLanguage" from config.json raw: Config.cs is stable code and has
    // no such field — the same approach as the debug flag in Log.InitLog.
    public static void Load()
    {
        try
        {
            if (!File.Exists(Config.FilePath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(Config.FilePath));
            if (doc.RootElement.TryGetProperty("uiLanguage", out var lang) &&
                lang.ValueKind == JsonValueKind.String &&
                lang.GetString() is string code &&
                Table.ContainsKey(code))
                Language = code;
        }
        catch { /* unreadable config → default ru */ }
    }
}
