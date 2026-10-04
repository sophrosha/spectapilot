using System.Drawing;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// Step 3: minimal settings form — toolbar on top (Demo / Language / About),
// config.json fields in the middle, Save button + status line at the bottom.
// All UI is built in code (Layout.cs); no designer files.
internal sealed class MainForm : Form
{
    private readonly LayoutParts _parts;
    private readonly List<ConfigBinding.Entry> _entries;

    public MainForm()
    {
        Text = Strings.T(Strings.Key.Title);
        ClientSize = new Size(720, 520);
        MinimumSize = new Size(720, 520);

        // PathResolver.ConfigPath is non-null here: Main runs AppInit.Init()
        // and aborts before this form is constructed when config.json is missing.
        // Layout is qualified: Control.Layout (the inherited event) shadows the class name.
        _parts = MozaWheelControlSettings.Layout.Build(this);
        _entries = ConfigBinding.Build(PathResolver.ConfigPath!);
        MozaWheelControlSettings.Layout.AddFields(_parts.FieldsTable, _entries);

        _parts.SaveButton.Click += (_, _) => SaveConfig();
        _parts.AboutButton.Click += (_, _) =>
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        };
        _parts.LanguageButton.Click += (_, _) => ShowLanguageMenu();
        // Step 4: DemoButton starts MozaWheelControl.exe with the demo args.
    }

    private void SaveConfig()
    {
        try
        {
            ConfigBinding.Save(_entries, PathResolver.ConfigPath!);
            SetStatus(
                ProcessWatch.IsConsoleRunning()
                    ? Strings.T(Strings.Key.SavedRunning)
                    : Strings.T(Strings.Key.Saved),
                isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"{Strings.T(Strings.Key.SaveFailed)} {ex.Message}", isError: true);
        }
    }

    private void ShowLanguageMenu()
    {
        var menu = new ContextMenuStrip();
        foreach ((string code, string name) in Strings.Languages)
        {
            var item = new ToolStripMenuItem(name) { Checked = code == Strings.Language };
            string selected = code;
            item.Click += (_, _) => ApplyLanguage(selected);
            menu.Items.Add(item);
        }
        // Dispose strictly after the click pipeline unwinds: Closed fires inside
        // OnItemClicked, and disposing there makes the dropdown's own follow-up
        // SetVisibleCore hit a destroyed handle (ObjectDisposedException) and
        // abort the pipeline before the item's Click event is raised.
        menu.Closed += (_, _) =>
        {
            try
            {
                BeginInvoke((Action)(() => menu.Dispose()));
            }
            catch (InvalidOperationException)
            {
                // The form is going away too; the menu will be collected anyway.
            }
        };
        menu.Show(_parts.LanguageButton, new Point(0, _parts.LanguageButton.Height));
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
            SetStatus($"{Strings.T(Strings.Key.SaveFailed)} {ex.Message}", isError: true);
        }
    }

    private void ApplyTexts()
    {
        Text = Strings.T(Strings.Key.Title);
        _parts.LanguageButton.Text = Strings.T(Strings.Key.Language);
        _parts.AboutButton.Text = Strings.T(Strings.Key.About);
        _parts.SaveButton.Text = Strings.T(Strings.Key.Save);
        _parts.StatusLabel.Text = string.Empty;
    }

    private void SetStatus(string text, bool isError)
    {
        _parts.StatusLabel.Text = text;
        _parts.StatusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
    }
}
