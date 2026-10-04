using System.Windows.Forms;

namespace MozaWheelControlSettings;

// App-level init shared by Main: resolve the folder with config.json and
// MozaWheelControl.exe, and load the UI language before the form opens.
// Returns false when the app cannot continue at all.
internal static partial class Program
{
    internal static bool Init()
    {
        PathResolver.Resolve();

        // The language must be known before any message box can be shown.
        Strings.Load(PathResolver.ConfigPath is null
            ? null
            : ConfigBinding.ReadLanguage(PathResolver.ConfigPath));

        if (PathResolver.ConfigPath is null)
        {
            MessageBox.Show(
                Strings.T(Strings.Key.ConfigMissing),
                Strings.T(Strings.Key.Title),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        if (!ConfigBinding.CanRead(PathResolver.ConfigPath))
        {
            MessageBox.Show(
                Strings.T(Strings.Key.BadConfig),
                Strings.T(Strings.Key.Title),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        // Non-fatal: config editing still works; Step 4 needs the exe for Demo.
        if (PathResolver.ConsoleExePath is null)
        {
            MessageBox.Show(
                Strings.T(Strings.Key.ConsoleMissing),
                Strings.T(Strings.Key.Title),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return true;
    }
}
