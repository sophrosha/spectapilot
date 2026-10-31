namespace WheelControl;

// Ukrainian console texts (format-string table).
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class Uk
    {
        internal static readonly Dictionary<Key, string> Table = new()
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
            // Console window title, localized (Step 9a).
            [Key.WindowTitle] = "Обробник керма",
        };
    }
}
