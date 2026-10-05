namespace MozaWheelControl;

// English console texts (format-string table).
// Entries are copied verbatim from the original combined file.
internal static partial class Strings
{
    private static class En
    {
        internal static readonly Dictionary<Key, string> Table = new()
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
        };
    }
}
