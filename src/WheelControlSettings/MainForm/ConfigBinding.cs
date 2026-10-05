using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace WheelControlSettings;

// Step 3: reads config.json and generates a control per key by JSON type
// (bool -> CheckBox, number -> NumericUpDown, string -> TextBox); writes values
// back on Save. Generated dynamically so new config keys need no UI changes.
// Keys without a supported JSON shape (null, arrays, objects) are skipped, and
// unknown keys (debug, uiLanguage) are preserved on save.
internal static class ConfigBinding
{
    // Key managed by the Language toolbar button instead of a generated field.
    public const string LanguageKey = "uiLanguage";

    // One generated field: the config key and the control editing its value.
    internal sealed class Entry
    {
        public required string Key { get; init; }
        public required Control Control { get; init; }
    }

    // Numeric ranges mirror Config.Normalize() in the console app; keys outside
    // this table get a wide range so an existing value can always be edited.
    private static readonly Dictionary<string, (decimal Min, decimal Max)> Ranges = new(StringComparer.Ordinal)
    {
        ["smoothTimeMs"] = (50, 10_000),
        ["maxTorque"] = (50, 100),
        ["holdForce"] = (0, 100),
        ["holdPosition"] = (-3600, 3600),
        ["socketPort"] = (1, 65_535),
        ["dataIntervalMs"] = (5, 1000),
        ["socketSmoothTimeMs"] = (10, 5000),
        ["userFullLockDeg"] = (10, 720),
        ["maxDegPerSec"] = (30, 5000),
        ["holdRampMs"] = (0, 5000),
        ["moveForceLimit"] = (100, 10_000),
        ["maxMoveSpeedRpm"] = (5, 300),
        ["kpScale"] = (0.1m, 3),
        ["kdScale"] = (0.1m, 3),
    };

    private static readonly JsonDocumentOptions DocOpts = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Two-space indentation matches the hand-written config.json; the relaxed
    // encoder keeps non-ASCII values readable instead of \uXXXX escapes.
    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // True when config.json exists and parses as a JSON object.
    public static bool CanRead(string configPath)
    {
        try
        {
            _ = Parse(configPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Language code stored in config.json, or null when absent or unreadable.
    public static string? ReadLanguage(string configPath)
    {
        try
        {
            JsonObject root = Parse(configPath);
            return root.TryGetPropertyValue(LanguageKey, out JsonNode? node)
                && node is JsonValue value
                && value.TryGetValue<string>(out string? code)
                ? code
                : null;
        }
        catch
        {
            return null;
        }
    }

    // One control per config key, in file order.
    public static List<Entry> Build(string configPath)
    {
        var entries = new List<Entry>();
        foreach (KeyValuePair<string, JsonNode?> pair in Parse(configPath))
        {
            if (pair.Key == LanguageKey) continue;
            Entry? entry = Create(pair.Key, pair.Value);
            if (entry is not null) entries.Add(entry);
        }
        return entries;
    }

    // Write control values back, touching only these keys; everything else in
    // the file (debug, uiLanguage, key order) is preserved as-is.
    public static void Save(IEnumerable<Entry> entries, string configPath)
    {
        JsonObject root = Parse(configPath);
        foreach (Entry entry in entries)
        {
            JsonNode? value = entry.Control switch
            {
                CheckBox check => JsonValue.Create(check.Checked),
                NumericUpDown number => JsonValue.Create(number.Value),
                TextBox text => JsonValue.Create(text.Text),
                _ => null,
            };
            if (value is not null) root[entry.Key] = value;
        }
        File.WriteAllText(configPath, root.ToJsonString(WriteOpts));
    }

    // Persist the language picked in the toolbar (adds the key on first use).
    public static void PersistLanguage(string configPath, string code)
    {
        JsonObject root = Parse(configPath);
        root[LanguageKey] = JsonValue.Create(code);
        File.WriteAllText(configPath, root.ToJsonString(WriteOpts));
    }

    private static Entry? Create(string key, JsonNode? node)
    {
        switch (node?.GetValueKind())
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new Entry
                {
                    Key = key,
                    Control = new CheckBox { Checked = node.GetValue<bool>() },
                };
            case JsonValueKind.Number:
                return new Entry
                {
                    Key = key,
                    Control = CreateNumeric(key, node),
                };
            case JsonValueKind.String:
                return new Entry
                {
                    Key = key,
                    Control = new TextBox { Text = node.GetValue<string>() },
                };
            default:
                return null;
        }
    }

    private static NumericUpDown CreateNumeric(string key, JsonNode node)
    {
        decimal value = node.GetValue<decimal>();
        string raw = node.ToJsonString();

        // Decimal places follow the file: 7777 -> 0, 100.0 -> 1, 0.15 -> 2.
        int decimals = 0;
        int dot = raw.IndexOf('.');
        if (dot >= 0) decimals = Math.Clamp(raw.Length - dot - 1, 1, 2);

        (decimal min, decimal max) = Ranges.TryGetValue(key, out var range)
            ? range
            : (-1_000_000m, 1_000_000m);

        return new NumericUpDown
        {
            // Keep an out-of-range value editable instead of clamping it here —
            // the console applies Config.Normalize() when it loads the file.
            Minimum = Math.Min(min, value),
            Maximum = Math.Max(max, value),
            DecimalPlaces = decimals,
            Increment = decimals switch { 0 => 1m, 1 => 0.1m, _ => 0.01m },
            Value = value,
        };
    }

    private static JsonObject Parse(string configPath)
    {
        JsonNode? node = JsonNode.Parse(File.ReadAllText(configPath), null, DocOpts);
        return node as JsonObject ?? throw new InvalidDataException("config.json is not a JSON object.");
    }
}
