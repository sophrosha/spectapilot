using System.Drawing;
using System.Windows.Forms;

namespace WheelControlSettings;

// References to the controls Layout.Build creates; MainForm wires their events.
internal sealed class LayoutParts
{
    public ToolStripMenuItem DemoItem { get; init; } = null!;
    public ToolStripMenuItem LanguageItem { get; init; } = null!;
    public ToolStripMenuItem AboutItem { get; init; } = null!;
    public IReadOnlyList<(string Code, ToolStripMenuItem Item)> LanguageItems { get; init; } = null!;
    public Button SaveButton { get; init; } = null!;
    public ToolStripStatusLabel StatusLabel { get; init; } = null!;
    public TableLayoutPanel FieldsTable { get; init; } = null!;
}

// UI iteration 1 (Step 5): builds the whole form layout in code with stock
// controls — a flat MenuStrip on top, generated config fields in a scroll
// area, a Save row and a standard StatusStrip (status text only) at the
// bottom. Window chrome stays the default DWM one.
internal static class Layout
{
    private const float FieldRowHeight = 30f;

    public static LayoutParts Build(Form form)
    {
        // Standard MenuStrip: Demo | Language (submenu) | About. The flat
        // renderer kills the default gradient (spec item 1).
        var menu = new MenuStrip { Dock = DockStyle.Fill, Renderer = new FlatMenuRenderer() };
        var demo = new ToolStripMenuItem("Demo");
        var language = new ToolStripMenuItem(Strings.T(Strings.Key.Language));
        var about = new ToolStripMenuItem(Strings.T(Strings.Key.About));
        var languageItems = new List<(string, ToolStripMenuItem)>();
        foreach ((string code, string name) in Strings.Languages)
        {
            var item = new ToolStripMenuItem(name) { Checked = code == Strings.Language };
            language.DropDownItems.Add(item);
            languageItems.Add((code, item));
        }
        // Refresh the checkmark every time the dropdown opens; the click itself
        // is wired by MainForm (ApplyLanguage).
        language.DropDownOpening += (_, _) =>
        {
            foreach ((string code, ToolStripMenuItem item) in languageItems)
                item.Checked = code == Strings.Language;
        };
        menu.Items.Add(demo);
        menu.Items.Add(language);
        menu.Items.Add(about);

        // Middle: scrollable area that hosts the generated config fields.
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(8, 6, 8, 4),
        };
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(4),
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        scroll.Controls.Add(fields);

        // Save row above the status bar: the button pinned to the right
        // (main_win.svg mockup).
        var saveRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(8, 6, 16, 0),
        };
        var save = new Button { Text = Strings.T(Strings.Key.Save), AutoSize = true };
        saveRow.Controls.Add(save);

        // Standard StatusStrip with a single text zone on the left (the shared
        // 5 s auto-clear target). The indicator dot was removed per Step 5.
        var strip = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false };
        var status = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            BorderSides = ToolStripStatusLabelBorderSides.None,
        };
        strip.Items.Add(status);

        // Root: menu / fields / save row / status strip — only fields grow.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(scroll, 0, 1);
        root.Controls.Add(saveRow, 0, 2);
        root.Controls.Add(strip, 0, 3);
        form.Controls.Add(root);
        form.MainMenuStrip = menu;

        return new LayoutParts
        {
            DemoItem = demo,
            LanguageItem = language,
            AboutItem = about,
            LanguageItems = languageItems,
            SaveButton = save,
            StatusLabel = status,
            FieldsTable = fields,
        };
    }

    // Two-column placement: the first half of the entries on the left,
    // the rest on the right; the table grows rows as needed.
    // Labels get the localized caption for their key (Strings.ConfigLabel),
    // label+control get the localized tooltip (Strings.KeyTip). The created
    // (key, label, control) triples are collected so MainForm can refresh
    // captions AND tooltips together on a language change.
    public static void AddFields(
        TableLayoutPanel table,
        IReadOnlyList<ConfigBinding.Entry> entries,
        ToolTip tips,
        List<(string Key, Label Label, Control Control)> fields)
    {
        if (entries.Count == 0) return;

        int rows = (entries.Count + 1) / 2;
        // Grow the style list explicitly: setting RowCount alone does not add
        // RowStyle entries, so the indexer below would go out of range.
        while (table.RowStyles.Count < rows)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRowHeight));
        table.RowCount = rows;
        for (int r = 0; r < rows; r++)
            table.RowStyles[r] = new RowStyle(SizeType.Absolute, FieldRowHeight);

        for (int i = 0; i < entries.Count; i++)
        {
            ConfigBinding.Entry entry = entries[i];
            int row = i < rows ? i : i - rows;
            int col = i < rows ? 0 : 2;

            var label = new Label
            {
                Text = Strings.ConfigLabel(entry.Key),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            entry.Control.Dock = DockStyle.Fill;

            // Tooltips follow the UI language and are re-applied together
            // with the captions on a language change.
            string tip = Strings.KeyTip(entry.Key);
            tips.SetToolTip(label, tip);
            tips.SetToolTip(entry.Control, tip);

            fields.Add((entry.Key, label, entry.Control));
            table.Controls.Add(label, col, row);
            table.Controls.Add(entry.Control, col + 1, row);
        }
    }
}

// Flat renderer for the MenuStrip (Step 5, item 1): one flat background
// everywhere, no gradients anywhere — only the plain hover fill remains.
internal sealed class FlatMenuRenderer : ToolStripProfessionalRenderer
{
    public FlatMenuRenderer() : base(new FlatColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        // Unselected items stay unpainted so the strip background shows
        // through: bar (#E8E8E8) for the top level, dropdown (#F0F0F0) below.
        if (!e.Item.Selected) return;
        Rectangle rect = new(Point.Empty, e.Item.Size);
        using var brush = new SolidBrush(FlatColorTable.Hover);
        e.Graphics.FillRectangle(brush, rect);
    }
}

// Every menu gradient replaced by flat colors: the bar is a couple of
// percent darker than the form (#E8E8E8, the Windows Explorer toolbar
// shade — it reads as a toolbar but never looks dirty), dropdowns and the
// status strip keep the form color (#F0F0F0); hover stays a subtle flat
// gray, visible on both backgrounds. ProfessionalColorTable exposes
// get-only virtual properties — they are overridden, not assigned.
internal sealed class FlatColorTable : ProfessionalColorTable
{
    // Menu bar background: SystemColors.Control (#F0F0F0) darkened ~2-3%.
    internal static readonly Color MenuBar = Color.FromArgb(0xE8, 0xE8, 0xE8);

    // Hover fill for menu items — one step below the bar, clearly visible
    // on both #E8E8E8 (bar) and #F0F0F0 (dropdown).
    internal static readonly Color Hover = Color.FromArgb(0xDB, 0xDB, 0xDB);

    public FlatColorTable()
    {
        UseSystemColors = false;
    }

    // MenuStrip bar background — flat, darker than the form.
    public override Color MenuStripGradientBegin => MenuBar;
    public override Color MenuStripGradientEnd => MenuBar;

    // Dropdown background and image margin — flat, same as the form.
    public override Color ToolStripDropDownBackground => SystemColors.Control;
    public override Color ImageMarginGradientBegin => SystemColors.Control;
    public override Color ImageMarginGradientEnd => SystemColors.Control;

    // Pressed / open top-level item — matches the bar, no gradient.
    public override Color MenuItemPressedGradientBegin => MenuBar;
    public override Color MenuItemPressedGradientEnd => MenuBar;

    // Hover highlight (the only allowed highlight, kept flat).
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemBorder => SystemColors.ControlDark;

    // Borders and separators — plain gray.
    public override Color ToolStripBorder => SystemColors.ControlDark;
    public override Color SeparatorDark => SystemColors.ControlDark;
    public override Color SeparatorLight => SystemColors.ControlLight;
    public override Color RaftingContainerGradientBegin => SystemColors.Control;
    public override Color RaftingContainerGradientEnd => SystemColors.Control;
    public override Color StatusStripGradientBegin => SystemColors.Control;
    public override Color StatusStripGradientEnd => SystemColors.Control;
}
