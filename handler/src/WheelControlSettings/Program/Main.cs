using System.Windows.Forms;

namespace WheelControlSettings;

// Entry point: plain WinForms pump — no designer, no DI, no services.
internal static partial class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (!Init()) return;
        Application.Run(new MainForm());
    }
}
