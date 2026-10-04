using System.Windows.Forms;

namespace MozaWheelControlSettings;

// Entry point: plain WinForms pump — no designer, no DI, no services.
internal static partial class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
