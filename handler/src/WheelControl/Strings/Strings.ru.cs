namespace WheelControl;

// Russian console texts (format-string table).
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class Ru
    {
        internal static readonly Dictionary<Key, string> Table = new()
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
            // Console window title, localized (Step 9a).
            [Key.WindowTitle] = "Обработчик руля",
        };
    }
}
