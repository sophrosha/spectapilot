using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace WheelControlSettings;

// About dialog per the UI spec: product title, subtitle, "About the project"
// and "Licenses" sections with browser links, a row of 32x32 SVG icon buttons
// (github / beamng / bio, rasterized at runtime with Svg.NET; a missing or
// broken file falls back to the round red stub) with a smooth hover shrink,
// the git commit stamp bottom-left and OK bottom-right — all on one bottom
// row (Step 5, item 5). Window chrome is the standard DWM one; every visual
// is a stock WinForms control with standard fonts.
internal sealed partial class AboutForm : Form
{
    // Rasterize an SVG asset (copied next to the exe by the csproj) to a
    // 32x32 bitmap with Svg.NET. A missing or unreadable file returns null —
    // the icon then falls back to the round red stub (see HoverIcon), so the
    // dialog never fails because of an asset.
    private static Image? LoadSvgIcon(string fileName)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) return null;
            Svg.SvgDocument svg = Svg.SvgDocument.Open(path);
            return svg.Draw(IconSize, IconSize);
        }
        catch
        {
            return null;
        }
    }

    // ShellExecute so the default browser handles http links.
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // No browser registered — nothing sensible to show from a dialog.
        }
    }

    // "commit: <hash>" stamped at build time by the GitCommitStamp target
    // (AssemblyInformationalVersionAttribute); falls back to "commit: dev"
    // when the build could not run git.
    private static string ReadCommitStamp()
    {
        string? info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        return info is not null && info.StartsWith("commit:", StringComparison.Ordinal)
            ? info
            : "commit: dev";
    }
}
