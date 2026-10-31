namespace WheelControlSettings;

// All user-visible texts of the Settings app in four languages (RU / EN / UKR / BY).
// Only the Settings UI is localized here; the console app's stable strings are untouched.
internal static partial class Strings
{
    internal enum Key
    {
        Title,
        Language,
        About,
        Save,
        Saved,
        SavedRunning,
        SaveFailed,
        Version,
        ConfigMissing,
        ConsoleMissing,
        BadConfig,
        Demo,
        DemoStarted,
        DemoFailed,
        DemoNoExe,
        DemoNoWindow,
        // Section titles (single-screen rework; keys keep the Tab* prefix).
        TabGeneral,
        TabWheel,
        TabHold,
        TabMove,
        TabSocket,
        // About dialog (UI iteration 1).
        Subtitle,
        ProjectHeader,
        ProjectText,
        LicensesHeader,
        ModTip,
    }

    // Menu order as requested: RU / EN / UKR / BY. Names stay native in every language.
    public static readonly (string Code, string Name)[] Languages =
    {
        ("ru", "Русский"),
        ("en", "English"),
        ("uk", "Українська"),
        ("by", "Беларуская"),
    };

    // Current UI language; persisted in config.json under the "uiLanguage" key.
    public static string Language { get; private set; } = "ru";

    // Per-language data lives in Strings.<lang>.cs (nested Ru/En/Uk/By classes);
    // these registries keep the original by-language lookups unchanged.
    private static readonly Dictionary<string, Dictionary<Key, string>> _table = new()
    {
        ["ru"] = Ru.Table,
        ["en"] = En.Table,
        ["uk"] = Uk.Table,
        ["by"] = By.Table,
    };

    public static string T(Key key) =>
        _table.TryGetValue(Language, out var texts) && texts.TryGetValue(key, out var text)
            ? text
            : _table["en"][key];

    // Per-key field descriptions shown as ToolTips, all four languages —
    // they follow the UI language exactly like the captions (spec: tooltips
    // are localized together with the interface) and are re-applied by
    // MainForm on every language change.
    private static readonly Dictionary<string, Dictionary<string, string>> _tips = new()
    {
        ["ru"] = Ru.Tips,
        ["en"] = En.Tips,
        ["uk"] = Uk.Tips,
        ["by"] = By.Tips,
    };

    // Fallback prefix for a config key without a description (per language).
    private static readonly Dictionary<string, string> _fallbackTipWord = new(StringComparer.Ordinal)
    {
        ["ru"] = Ru.FallbackTipWord,
        ["en"] = En.FallbackTipWord,
        ["uk"] = Uk.FallbackTipWord,
        ["by"] = By.FallbackTipWord,
    };

    // Localized tooltip for a config key; an unknown key falls back to
    // "<word>: key" in the current language (never empty).
    public static string KeyTip(string key) =>
        _tips.TryGetValue(Language, out var byKey) && byKey.TryGetValue(key, out var tip)
            ? tip
            : $"{(_fallbackTipWord.TryGetValue(Language, out var word) ? word : "Setting")}: {key}";

    // Human-readable field captions per language (Step 5, item 4). Keyed by
    // the raw config key; ConfigLabel falls back to the key itself so an
    // unknown/new key never breaks the UI.
    private static readonly Dictionary<string, Dictionary<string, string>> _captions = new()
    {
        ["ru"] = Ru.Captions,
        ["en"] = En.Captions,
        ["uk"] = Uk.Captions,
        ["by"] = By.Captions,
    };

    // Caption for a config field label in the current language; the raw key is
    // the fallback so unknown keys still render.
    public static string ConfigLabel(string key) =>
        _captions.TryGetValue(Language, out var byKey) && byKey.TryGetValue(key, out var label)
            ? label
            : key;

    // Apply the code stored in config.json; unknown or missing values fall back to the default.
    public static void Load(string? code)
    {
        if (code is not null && _table.ContainsKey(code)) Language = code;
    }

    public static void Set(string code)
    {
        if (_table.ContainsKey(code)) Language = code;
    }
}
