using System.Drawing;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// Step 3: empty About dialog — title "About" and an OK button.
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About";
        ClientSize = new Size(360, 180);
    }
}
