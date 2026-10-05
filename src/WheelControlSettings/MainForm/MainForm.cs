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
        ClientSize = new Size(720, 520);
        MinimumSize = new Size(720, 520);

        // Window icon: icon.ico next to the exe when present (copied by the
        // csproj); otherwise the default embedded exe icon — no crash either way.
        string iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        // PathResolver.ConfigPath is non-null here: Main runs AppInit.Init()
        // and aborts before this form is constructed when config.json is missing.
        // Layout is qualified: Control.Layout (the inherited event) shadows the class name.
        _parts = WheelControlSettings.Layout.Build(this);
        _entries = ConfigBinding.Build(PathResolver.ConfigPath!);
        _tips = new ToolTip();
        WheelControlSettings.Layout.AddFields(_parts.FieldsTable, _entries, _tips, _fields);

        // Every localized string is applied here (and again on each language
        // change); Layout.Build only creates the controls.
        ApplyTexts();

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
