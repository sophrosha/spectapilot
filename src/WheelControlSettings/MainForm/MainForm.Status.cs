using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WheelControlSettings;

// UI iteration 1 (Step 5): standard MenuStrip on top (Demo / Language /
// About, flat renderer), config.json fields in the middle, Save row +
// StatusStrip (status text only) at the bottom. All UI is built in code
// (Layout.cs); no designer files.
internal sealed partial class MainForm : Form
{
    // Shows the text (null color = default control color) and arms the shared
    // auto-clear timer: the message disappears StatusClearMs after it was
    // shown; any newer SetStatus cancels the old countdown and starts a new one.
    private void SetStatus(string text, Color? color = null)
    {
        _parts.StatusLabel.Text = text;
        _parts.StatusLabel.ForeColor = color ?? SystemColors.ControlText;
        _statusShownAt = Environment.TickCount64;
        _statusClearTimer.Stop();
        _statusClearTimer.Start();
    }

    private void ClearStatusTick(object? sender, EventArgs e)
    {
        // A stale WM_TIMER may be delivered right after a new SetStatus has
        // re-armed the timer — never wipe a message younger than the interval.
        if (Environment.TickCount64 - _statusShownAt < StatusClearMs - 100) return;
        _statusClearTimer.Stop();
        _parts.StatusLabel.Text = "";
    }
}
