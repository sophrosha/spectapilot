using System.Globalization;

namespace WheelControl;

/// <summary>Тип команды пользователя.</summary>
public enum CommandKind
{
    /// <summary>Число без знака — абсолютный угол (0° = центр). Например: 180</summary>
    MoveAbsolute,
    /// <summary>Число со знаком — относительный поворот. Например: +30, -30</summary>
    MoveRelative,
    /// <summary>hold [угол] — включить удержание.</summary>
    Hold,
    /// <summary>release — выключить удержание.</summary>
    Release,
    /// <summary>socket — перейти в socket mode (запустить UDP-сервер).</summary>
    Socket,
    /// <summary>keyboard — вернуться в keyboard mode.</summary>
    Keyboard,
    /// <summary>status — показать состояние.</summary>
    Status,
    /// <summary>sleep N — пауза N мс (удобно для скриптов/тестов).</summary>
    Sleep,
    /// <summary>exit / quit — выход.</summary>
    Exit,
    /// <summary>help / ? — список команд.</summary>
    Help,
    /// <summary>demo [сек] [амплитуда°] — имитация езды в режиме hold.</summary>
    Demo,
    /// <summary>log [сек] — CSV-лог петли 200 Гц (диагностика раскачки).</summary>
    Log,
    /// <summary>Не распознано.</summary>
    Unknown,
}

/// <summary>Распарсенная команда.</summary>
public readonly struct Command
{
    public CommandKind Kind { get; init; }
    /// <summary>Числовое значение (угол для Move*, мс для Sleep).</summary>
    public float Value { get; init; }
    /// <summary>Второе число (амплитуда для demo), NaN если не задано.</summary>
    public float Value2 { get; init; }
    /// <summary>Исходная строка (для вывода ошибок).</summary>
    public string Raw { get; init; }

    public Command(CommandKind kind, float value = 0f, string raw = "", float value2 = float.NaN)
    {
        Kind = kind;
        Value = value;
        Raw = raw;
        Value2 = value2;
    }
}

/// <summary>
/// Разбор команд. Один и тот же парсер для консоли и сокета, но знаки разные:
/// в консоли «-10» — сдвиг (относительный), а мод шлёт ПОЛОЖЕНИЕ «-1»/«-2» —
/// из сокета минус = абсолютный угол. «+10» (явный плюс) — относительный везде.
/// invert=true (invertSocket в config.json) — знак разобранного ЧИСЛА из игры
/// инвертируется: руль в игре крутится не в ту сторону → направление совпадает.
/// </summary>
public static class InputHandler
{
    public static Command Parse(string? line, bool fromSocket = false, bool invert = false)
    {
        string raw = line ?? "";
        // Убираем возможный BOM (файлы/пайпы от PowerShell) и пробелы.
        string s = raw.Trim().Trim((char)0xFEFF).Trim();

        if (s.Length == 0)
            return new Command(CommandKind.Unknown, 0f, raw);

        // --- Числа: абсолютный угол (180, -45, 90.5, 0, допускается запятая: 90,5) ---
        // Относительный: «+30» везде; «-30» — только в консоли (в сокете это позиция).
        if (s[0] == '+' || (s[0] == '-' && s.Length > 1 && !IsWord(s) && !fromSocket))
        {
            if (TryParseNumber(s, out float rel))
                return new Command(CommandKind.MoveRelative, invert ? -rel : rel, raw);
        }
        else if (TryParseNumber(s, out float abs))
        {
            return new Command(CommandKind.MoveAbsolute, invert ? -abs : abs, raw);
        }

        // --- Словесные команды (регистронезависимо) ---
        string[] parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string word = parts[0].ToLowerInvariant();

        switch (word)
        {
            case "hold":
                if (parts.Length >= 2 && TryParseNumber(parts[1], out float hp))
                    return new Command(CommandKind.Hold, hp, raw);
                return new Command(CommandKind.Hold, float.NaN, raw); // NaN = «текущая позиция»

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
                // demo [секунды] [амплитуда°] — по умолчанию 50 с и полный ход руля
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
                // log [секунды] — по умолчанию 10 с
                float sec = 10f;
                if (parts.Length >= 2 && TryParseNumber(parts[1], out float lg)) sec = lg;
                return new Command(CommandKind.Log, sec, raw);
            }

            default:
                return new Command(CommandKind.Unknown, 0f, raw);
        }
    }

    /// <summary>Разбор числа с поддержкой '.' и ',' в качестве разделителя.</summary>
    public static bool TryParseNumber(string s, out float value)
    {
        s = s.Trim();
        if (s.Length == 0) { value = 0; return false; }

        // В русской локали пользователь может писать 90,5 — нормализуем к точке.
        string norm = s.Replace(',', '.');
        return float.TryParse(norm, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsWord(string s)
    {
        // "-30" — число, "-abc" — не число (не трогаем).
        foreach (char c in s)
            if (!char.IsDigit(c) && c != '.' && c != ',' && c != '-' && c != '+' && c != 'e' && c != 'E')
                return true;
        return false;
    }
}
