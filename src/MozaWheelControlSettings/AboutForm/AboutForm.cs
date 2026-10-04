using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// Step 3: empty About dialog — title "About" and an OK button.
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = Strings.T(Strings.Key.About);
        ClientSize = new Size(360, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var name = new Label
        {
            Text = "MozaWheelControl",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(20, 24),
        };
        var version = new Label
        {
            Text = $"{Strings.T(Strings.Key.Version)} {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}",
            AutoSize = true,
            Location = new Point(20, 54),
        };
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };
        ok.Location = new Point(
            ClientSize.Width - ok.PreferredSize.Width - 16,
            ClientSize.Height - ok.PreferredSize.Height - 16);
        AcceptButton = ok;

        Controls.Add(name);
        Controls.Add(version);
        Controls.Add(ok);
    }
}
