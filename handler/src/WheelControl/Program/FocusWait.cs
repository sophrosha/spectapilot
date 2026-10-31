using System.Runtime.InteropServices;

namespace WheelControl;

// Focus gate: installMozaSDK() must not run until the console window has the
// foreground (spec: the hint is printed in DarkYellow and we wait in a loop).
internal static partial class Program
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static async Task WaitForFocusAsync()
    {
        if (GetForegroundWindow() == GetConsoleWindow()) return; // already focused

        PrintOrange(Strings.T(Strings.Key.FocusHint));
        while (GetForegroundWindow() != GetConsoleWindow())
            await Task.Delay(250);
    }
}
