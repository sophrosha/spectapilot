namespace MozaWheelControl;

// Belarusian console texts (format-string table).
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class By
    {
        internal static readonly Dictionary<Key, string> Table = new()
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
        };
    }
}
