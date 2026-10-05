using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// UI iteration 1 (Step 5): standard MenuStrip on top (Demo / Language /
// About, flat renderer), config.json fields in the middle, Save row +
// StatusStrip (status text only) at the bottom. All UI is built in code
// (Layout.cs); no designer files.
internal sealed partial class MainForm : Form
{
    // Starts the console in demo mode. The console raises itself to the
    // foreground (Step 2 focus gate) and handles the already-running instance
    // itself (single instance closes the previous process); Settings stays
    // open in the background and is not blocked. The status is not set here:
    // CheckDemoWindow reports the real outcome after DemoWindowWaitMs.
    private void LaunchDemo()
    {
        try
        {
            if (PathResolver.ConsoleExePath is null)
            {
                SetStatus(
                    $"{Strings.T(Strings.Key.DemoFailed)} {Strings.T(Strings.Key.DemoNoExe)}",
                    Color.Firebrick);
                return;
            }

            Process? started = Process.Start(
                PathResolver.ConsoleExePath!,
                "--demo=enabled --demo_time=100 --demo_amplitude=100");

            // Clear any previous status: a stale "Demo запущен." must not be
            // shown while the verdict is pending. Restarting resets the full
            // interval — a repeated click gets its own check.
            _demoProcess?.Dispose();
            _demoProcess = started;
            SetStatus(string.Empty);
            _demoTimer.Stop();
            _demoTimer.Start();
        }
        catch (Exception ex)
        {
            SetStatus($"{Strings.T(Strings.Key.DemoFailed)} {ex.Message}", Color.Firebrick);
        }
    }

    // Timer tick: the console is alive and has a window -> success; a dead
    // process or a missing window -> the failure text in red (spec item 4).
    private void CheckDemoWindow()
    {
        _demoTimer.Stop();

        bool ok = false;
        try
        {
            if (_demoProcess is { } proc)
            {
                proc.Refresh(); // MainWindowHandle is cached — force a re-read
                ok = !proc.HasExited && proc.MainWindowHandle != IntPtr.Zero;
            }
        }
        catch { /* the process went away between the checks */ }

        if (ok)
            SetStatus(Strings.T(Strings.Key.DemoStarted));
        else
            SetStatus(Strings.T(Strings.Key.DemoNoWindow), Color.Firebrick);
    }
}
