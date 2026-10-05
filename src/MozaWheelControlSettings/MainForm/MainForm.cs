using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// UI iteration 1 (Step 5): standard MenuStrip on top (Demo / Language /
// About, flat renderer), config.json fields in the middle, Save row +
// StatusStrip (status text only) at the bottom. All UI is built in code
// (Layout.cs); no designer files.
internal sealed class MainForm : Form
{
    private readonly LayoutParts _parts;
    private readonly List<ConfigBinding.Entry> _entries;

    // Spec item 4: Process.Start returns immediately — the verdict ("Demo
    // запущен." vs the failure text) is only known after this delay: the
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
        Text = Strings.T(Strings.Key.Title);
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
        _parts = MozaWheelControlSettings.Layout.Build(this);
        _entries = ConfigBinding.Build(PathResolver.ConfigPath!);
        _tips = new ToolTip();
        MozaWheelControlSettings.Layout.AddFields(_parts.FieldsTable, _entries, _tips, _fields);

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

    // Starts the console in demo mode. The console raises itself to the
    // foreground (Step 2 focus gate) and handles the already-running instance
    // itself (single instance closes the previous process); Settings stays
    // open in the background and is not blocked. The status is not set here:
    // CheckDemoWindow reports the real outcome after DemoWindowWaitMs.
    private void LaunchDemo()
    {
        try
        {
            if (PathResolver.ConsoleExePath is null)
            {
                SetStatus(
                    $"{Strings.T(Strings.Key.DemoFailed)} {Strings.T(Strings.Key.DemoNoExe)}",
                    Color.Firebrick);
                return;
            }

            Process? started = Process.Start(
                PathResolver.ConsoleExePath!,
                "--demo=enabled --demo_time=100 --demo_amplitude=100");

            // Clear any previous status: a stale "Demo запущен." must not be
            // shown while the verdict is pending. Restarting resets the full
            // interval — a repeated click gets its own check.
            _demoProcess?.Dispose();
            _demoProcess = started;
            SetStatus(string.Empty);
            _demoTimer.Stop();
            _demoTimer.Start();
        }
        catch (Exception ex)
        {
            SetStatus($"{Strings.T(Strings.Key.DemoFailed)} {ex.Message}", Color.Firebrick);
        }
    }

    // Timer tick: the console is alive and has a window -> success; a dead
    // process or a missing window -> the failure text in red (spec item 4).
    private void CheckDemoWindow()
    {
        _demoTimer.Stop();

        bool ok = false;
        try
        {
            if (_demoProcess is { } proc)
            {
                proc.Refresh(); // MainWindowHandle is cached — force a re-read
                ok = !proc.HasExited && proc.MainWindowHandle != IntPtr.Zero;
            }
        }
        catch { /* the process went away between the checks */ }

        if (ok)
            SetStatus(Strings.T(Strings.Key.DemoStarted));
        else
            SetStatus(Strings.T(Strings.Key.DemoNoWindow), Color.Firebrick);
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

    private void ApplyLanguage(string code)
    {
        Strings.Set(code);
        ApplyTexts();
        try
        {
            // Persist immediately so the choice survives even without Save.
            ConfigBinding.PersistLanguage(PathResolver.ConfigPath!, code);
        }
        catch (Exception ex)
        {
            SetStatus($"{Strings.T(Strings.Key.SaveFailed)} {ex.Message}", Color.Firebrick);
        }
    }

    private void ApplyTexts()
    {
        Text = Strings.T(Strings.Key.Title);
        _parts.LanguageItem.Text = Strings.T(Strings.Key.Language);
        _parts.AboutItem.Text = Strings.T(Strings.Key.About);
        _parts.SaveButton.Text = Strings.T(Strings.Key.Save);
        _parts.StatusLabel.Text = string.Empty;

        // Field captions and tooltips follow the current language.
        foreach ((string key, Label label, Control control) in _fields)
        {
            label.Text = Strings.ConfigLabel(key);
            string tip = Strings.KeyTip(key);
            _tips.SetToolTip(label, tip);
            _tips.SetToolTip(control, tip);
        }
    }

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
