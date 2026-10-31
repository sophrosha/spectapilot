using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace WheelControlSettings;

// UI iteration 1 (Step 5): standard MenuStrip on top (Demo / Language /
// About, flat renderer), config.json fields in the middle, Save row with
// the 5 s status text line at the bottom. All UI is built in code
// (Layout.cs); no designer files.
internal sealed partial class MainForm : Form
{
    private readonly LayoutParts _parts;
    private readonly List<ConfigBinding.Entry> _entries;

    // Spec item 4: Process.Start returns immediately — the verdict ("Demo
    // started." vs the failure text) is only known after this delay: the
    // console may die within milliseconds (AV block, crash) and never opens
    // a window. 5 s is the spec's default.
    private const int DemoWindowWaitMs = 5000;

    private readonly System.Windows.Forms.Timer _demoTimer;
    private Process? _demoProcess; // the console started by the last Demo click

    // Shared auto-clear for the status line: every message disappears 5 s
    // after it is shown; a newer SetStatus restarts the countdown.
    private const int StatusClearMs = 5000;

    private readonly System.Windows.Forms.Timer _statusClearTimer;
    private long _statusShownAt; // Environment.TickCount64 of the last SetStatus

    private readonly ToolTip _tips; // field descriptions (Strings.KeyTip)
    private readonly List<(string Key, Label Label, Control Control)> _fields = new(); // for ApplyTexts

    public MainForm()
    {
        // Sections-in-frame: fixed size — width 750 (spec: +20–30 px so the
        // right column with its numeric spinners always fits, uk/by labels
        // included); the height is computed exactly by FitToContent after
        // ApplyTexts (no scrolling).
        ClientSize = new Size(750, 680);

        // Step 9b: fixed-size window — no resize by cursor, no maximize
        // (no MinimumSize: FixedSingle cannot be resized at all).
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        // Window icon: icon.ico in Assets\ next to the exe when present (copied
        // by the csproj); otherwise the default embedded exe icon — no crash either way.
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        // PathResolver.ConfigPath is non-null here: Main runs AppInit.Init()
        // and aborts before this form is constructed when config.json is missing.
        // Layout is qualified: Control.Layout (the inherited event) shadows the class name.
        _parts = WheelControlSettings.Layout.Build(this);
        _entries = ConfigBinding.Build(PathResolver.ConfigPath!);
        _tips = new ToolTip();
        WheelControlSettings.Layout.AddFields(_parts, _entries, _tips, _fields);

        // Every localized string is applied here (and again on each language
        // change); Layout.Build only creates the controls.
        ApplyTexts();

        // Exact height — after ApplyTexts, so the menu and the Save button
        // carry their final texts: two explicit root rows (menu, frames);
        // the client height is last frame bottom + 6 px + button + 6 px.
        // No remainder: no chin, no clipping, nothing below the button —
        // no strip, no band, just the background.
        WheelControlSettings.Layout.FitToContent(this, _parts);

        _parts.SaveButton.Click += (_, _) => SaveConfig();
        _parts.AboutItem.Click += (_, _) =>
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        };
        _parts.DemoItem.Click += (_, _) => LaunchDemo();
        foreach ((string code, ToolStripMenuItem item) in _parts.LanguageItems)
        {
            string selected = code;
            item.Click += (_, _) => ApplyLanguage(selected);
        }

        _demoTimer = new System.Windows.Forms.Timer { Interval = DemoWindowWaitMs };
        _demoTimer.Tick += (_, _) => CheckDemoWindow();

        _statusClearTimer = new System.Windows.Forms.Timer { Interval = StatusClearMs };
        _statusClearTimer.Tick += ClearStatusTick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Final placement pass: the whole chain (form → root → sections →
        // frames) is laid out for real once the handle exists — re-read the
        // last frame's bottom, re-place the Save button and re-check the fit
        // (spec, step 4).
        WheelControlSettings.Layout.PlaceSaveRow(this, _parts);

        // TEMP diagnostics for the Step 9 run — removed after acceptance.
        WheelControlSettings.Layout.DumpLayout(this, _parts);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Spec: cancel pending timers when Settings closes — the demo window
        // check and the status auto-clear.
        _demoTimer.Stop();
        _demoTimer.Dispose();
        _statusClearTimer.Stop();
        _statusClearTimer.Dispose();
        _tips.Dispose();
        _demoProcess?.Dispose(); // releases the handle only; the console keeps running
        base.OnFormClosed(e);
    }

    private void SaveConfig()
    {
        try
        {
            ConfigBinding.Save(_entries, PathResolver.ConfigPath!);
            SetStatus(
                ProcessWatch.IsConsoleRunning()
                    ? Strings.T(Strings.Key.SavedRunning)
                    : Strings.T(Strings.Key.Saved));
        }
        catch (Exception ex)
        {
            SetStatus($"{Strings.T(Strings.Key.SaveFailed)} {ex.Message}", Color.Firebrick);
        }
    }
}
