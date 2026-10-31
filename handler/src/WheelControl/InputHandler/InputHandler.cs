using System.Globalization;

namespace WheelControl;

// A user command kind.
public enum CommandKind
{
    // Unsigned number — absolute angle (0° = center). Example: 180
    MoveAbsolute,
    // Signed number — relative turn. Example: +30, -30
    MoveRelative,
    // hold [angle] — engage hold.
    Hold,
    // release — disengage hold.
    Release,
    // socket — switch to socket mode (start the UDP server).
    Socket,
    // keyboard — return to keyboard mode.
    Keyboard,
    // status — show the state.
    Status,
    // sleep N — pause N ms (handy for scripts/tests).
    Sleep,
    // exit / quit — quit.
    Exit,
    // help / ? — list the commands.
    Help,
    // demo [sec] [amplitude°] — driving simulation in hold mode.
    Demo,
    // log [sec] — CSV log of the 200 Hz loop (oscillation diagnostics).
    Log,
    // Not recognized.
    Unknown,
}

// A parsed command.
public readonly struct Command
{
    public CommandKind Kind { get; init; }
    // Numeric value (angle for Move*, ms for Sleep).
    public float Value { get; init; }
    // Second number (amplitude for demo), NaN if not given.
    public float Value2 { get; init; }
    // The raw line (for error output).
    public string Raw { get; init; }

    public Command(CommandKind kind, float value = 0f, string raw = "", float value2 = float.NaN)
    {
        Kind = kind;
        Value = value;
        Raw = raw;
        Value2 = value2;
    }
}

// Command parsing. One parser for both console and socket, but the signs differ:
// in the console "-10" is a shift (relative), while the mod sends a POSITION "-1"/"-2" —
// from the socket minus = absolute angle. "+10" (explicit plus) is relative everywhere.
// invert=true (invertSocket in config.json) — the sign of the parsed NUMBER from the game
// is flipped: the in-game wheel turns the wrong way -> the direction matches.
public static class InputHandler
{
    public static Command Parse(string? line, bool fromSocket = false, bool invert = false)
    {
        string raw = line ?? "";
        // Strip a possible BOM (files/pipes from PowerShell) and whitespace.
        string s = raw.Trim().Trim((char)0xFEFF).Trim();

        if (s.Length == 0)
            return new Command(CommandKind.Unknown, 0f, raw);

        // --- Numbers: absolute angle (180, -45, 90.5, 0; a comma is allowed: 90,5) ---
        // Relative: "+30" everywhere; "-30" only in the console (in the socket it is a position).
        if (s[0] == '+' || (s[0] == '-' && s.Length > 1 && !IsWord(s) && !fromSocket))
        {
            if (TryParseNumber(s, out float rel))
                return new Command(CommandKind.MoveRelative, invert ? -rel : rel, raw);
        }
        else if (TryParseNumber(s, out float abs))
        {
            return new Command(CommandKind.MoveAbsolute, invert ? -abs : abs, raw);
        }

        // --- Verbal commands (case-insensitive) ---
        string[] parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string word = parts[0].ToLowerInvariant();

        switch (word)
        {
            case "hold":
                if (parts.Length >= 2 && TryParseNumber(parts[1], out float hp))
                    return new Command(CommandKind.Hold, hp, raw);
                return new Command(CommandKind.Hold, float.NaN, raw); // NaN = "current position"

            case "release":
            case "stop":
                return new Command(CommandKind.Release, 0f, raw);

            case "socket":
                return new Command(CommandKind.Socket, 0f, raw);

            case "keyboard":
                return new Command(CommandKind.Keyboard, 0f, raw);

            case "status":
            case "pos":
                return new Command(CommandKind.Status, 0f, raw);

            case "sleep":
            case "waitms":
                if (parts.Length >= 2 && TryParseNumber(parts[1], out float ms))
                    return new Command(CommandKind.Sleep, ms, raw);
                return new Command(CommandKind.Unknown, 0f, raw);

            case "exit":
            case "quit":
            case "q":
                return new Command(CommandKind.Exit, 0f, raw);

            case "demo":
            {
                // demo [seconds] [amplitude°] — defaults: 50 s and full wheel lock
                float sec = 50f;
                float amp = float.NaN;
                if (parts.Length >= 2 && TryParseNumber(parts[1], out float d1)) sec = d1;
                if (parts.Length >= 3 && TryParseNumber(parts[2], out float d2)) amp = d2;
                return new Command(CommandKind.Demo, sec, raw, amp);
            }

            case "help":
            case "?":
                return new Command(CommandKind.Help, 0f, raw);

            case "log":
            {
                // log [seconds] — default 10 s
                float sec = 10f;
                if (parts.Length >= 2 && TryParseNumber(parts[1], out float lg)) sec = lg;
                return new Command(CommandKind.Log, sec, raw);
            }

            default:
                return new Command(CommandKind.Unknown, 0f, raw);
        }
    }

    // Parse a number supporting '.' and ',' as the decimal separator.
    public static bool TryParseNumber(string s, out float value)
    {
        s = s.Trim();
        if (s.Length == 0) { value = 0; return false; }

        // In the Russian locale the user may write 90,5 — normalize it to a dot.
        string norm = s.Replace(',', '.');
        return float.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsWord(string s)
    {
        // "-30" is a number, "-abc" is not (leave it alone).
        foreach (char c in s)
            if (!char.IsDigit(c) && c != '.' && c != ',' && c != '-' && c != '+' && c != 'e' && c != 'E')
                return true;
        return false;
    }
}
