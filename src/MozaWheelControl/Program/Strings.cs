using System.Globalization;
using System.Text.Json;

namespace MozaWheelControl;

// All user-visible console texts in four languages (ru / en / uk / by) — the
// same "uiLanguage" key and codes as the Settings app. Loaded once at startup:
// the console reads config.json before the first line is printed, so a language
// switch in Settings applies on the next console run (spec item 1).
internal static class Strings
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

    private static readonly Dictionary<string, Dictionary<Key, string>> Table = new()
    {
        ["ru"] = new()
        {
            [Key.AwaitingDevice] = "Ожидание устройства...",
            [Key.FocusHint] = "> Пожалуйста, нажмите на это окно, чтобы перевести фокус.",
            [Key.WaitHint] = "> Если устройство включено, нажмите на это окно и подождите.",
            [Key.DeviceConnected] = "Устройство подключено.",
            [Key.DeviceConnectedSocket] = "Устройство подключено. Сокет: {0}:{1}",
            [Key.ConnectFailed] = "Ошибка подключения: {0}",
            [Key.ConnectSummary] = "Устройство найдено, но подключение неоднократно не удаётся. " +
                                   "Проверьте MOZA Pit House (руль по центру, права администратора).",
            [Key.DemoInfo] = "Demo - {0:0.#} с, амплитуда {1:0.#}°. " +
                             "Руль сам крутит змейку, руки можно держать на руле.",
            [Key.SdkInitFailed] = "Ошибка инициализации SDK: {0}",
            [Key.SocketListening] = "Сокет слушает {0}:{1}.",
            [Key.SocketStartFailed] = "Не удалось запустить сокет: {0}",
            [Key.ClosedPrevious] = "Закрыт предыдущий экземпляр [{0} #{1}] — работает только этот.",
            [Key.HandsCaptured] = "Hands - Сокет захватил руль => физически {0:0.#}°",
        },
        ["en"] = new()
        {
            [Key.AwaitingDevice] = "Awaiting for device...",
            [Key.FocusHint] = "> Please click on this window to focus it.",
            [Key.WaitHint] = "> If the device is on, click this window and wait.",
            [Key.DeviceConnected] = "Device connected.",
            [Key.DeviceConnectedSocket] = "Device connected. Socket: {0}:{1}",
            [Key.ConnectFailed] = "Connect failed: {0}",
            [Key.ConnectSummary] = "Device found but connect fails repeatedly. " +
                                   "Check MOZA Pit House (wheel centered, admin rights).",
            [Key.DemoInfo] = "Demo - {0:0.#}s, amplitude {1:0.#}°. Hands will can be in wheel",
            [Key.SdkInitFailed] = "SDK init failed: {0}",
            [Key.SocketListening] = "Socket listening on {0}:{1}.",
            [Key.SocketStartFailed] = "Socket start failed: {0}",
            [Key.ClosedPrevious] = "Closed previous instance [{0} #{1}] — running only this one.",
            [Key.HandsCaptured] = "Hands - Socket took the wheel => physically {0:0.#}°",
        },
        ["uk"] = new()
        {
            [Key.AwaitingDevice] = "Очікування пристрою...",
            [Key.FocusHint] = "> Будь ласка, натисніть на це вікно, щоб перевести фокус.",
            [Key.WaitHint] = "> Якщо пристрій увімкнено, натисніть на це вікно та зачекайте.",
            [Key.DeviceConnected] = "Пристрій підключено.",
            [Key.DeviceConnectedSocket] = "Пристрій підключено. Сокет: {0}:{1}",
            [Key.ConnectFailed] = "Помилка підключення: {0}",
            [Key.ConnectSummary] = "Пристрій знайдено, але підключення неодноразово не вдається. " +
                                   "Перевірте MOZA Pit House (кермо по центру, права адміністратора).",
            [Key.DemoInfo] = "Demo - {0:0.#} с, амплітуда {1:0.#}°. " +
                             "Кермо саме крутить змійку, руки можна тримати на кермі.",
            [Key.SdkInitFailed] = "Помилка ініціалізації SDK: {0}",
            [Key.SocketListening] = "Сокет слухає {0}:{1}.",
            [Key.SocketStartFailed] = "Не вдалося запустити сокет: {0}",
            [Key.ClosedPrevious] = "Закрито попередній екземпляр [{0} #{1}] — працює лише цей.",
            [Key.HandsCaptured] = "Hands - Сокет захопив кермо => фізично {0:0.#}°",
        },
        ["by"] = new()
        {
            [Key.AwaitingDevice] = "Чаканне прылады...",
            [Key.FocusHint] = "> Калі ласка, націсніце на гэта акно, каб перавесці фокус.",
            [Key.WaitHint] = "> Калі прылада ўключана, націсніце на гэта акно і пачакайце.",
            [Key.DeviceConnected] = "Прылада падлучана.",
            [Key.DeviceConnectedSocket] = "Прылада падлучана. Сокет: {0}:{1}",
            [Key.ConnectFailed] = "Памылка падлучэння: {0}",
            [Key.ConnectSummary] = "Прылада знойдзена, але падлучэнне неаднаразова не атрымоўваецца. " +
                                   "Праверце MOZA Pit House (руль у цэнтры, правы адміністратара).",
            [Key.DemoInfo] = "Demo - {0:0.#} с, амплітуда {1:0.#}°. " +
                             "Руль сам круціць змейку, рукі можна трымаць на руле.",
            [Key.SdkInitFailed] = "Памылка ініцыялізацыі SDK: {0}",
            [Key.SocketListening] = "Сокет слухае {0}:{1}.",
            [Key.SocketStartFailed] = "Не атрымалася запусціць сокет: {0}",
            [Key.ClosedPrevious] = "Папярэдні экземпляр [{0} #{1}] спынены — працуе толькі гэты.",
            [Key.HandsCaptured] = "Hands - Сокет захапіў руль => фізічна {0:0.#}°",
        },
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
