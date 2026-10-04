using System.Drawing;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// Step 3: minimal settings form — toolbar on top (Demo / Language / About),
// config.json fields in the middle, Save button + status line at the bottom.
// All UI is built in code (Layout.cs); no designer files.
internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "MozaWheelControl Settings";
        ClientSize = new Size(720, 520);
    }
}
