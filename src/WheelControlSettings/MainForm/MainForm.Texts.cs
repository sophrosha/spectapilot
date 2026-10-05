using System.Drawing;
using System.Windows.Forms;

namespace WheelControlSettings;

// UI iteration 1 (Step 5): standard MenuStrip on top (Demo / Language /
// About, flat renderer), config.json fields in the middle, Save row +
// StatusStrip (status text only) at the bottom. All UI is built in code
// (Layout.cs); no designer files.
internal sealed partial class MainForm : Form
{
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

        // Field captions and tooltips follow the current language — the same
        // helper the field creation uses, so both can never drift apart.
        foreach ((string key, Label label, Control control) in _fields)
            WheelControlSettings.Layout.ApplyFieldText(key, label, control, _tips);
    }
}
