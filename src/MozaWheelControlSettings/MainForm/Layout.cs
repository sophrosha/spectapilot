using System.Drawing;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// References to the controls Layout.Build creates; MainForm wires their events.
internal sealed class LayoutParts
{
    public Button DemoButton { get; init; } = null!;
    public Button LanguageButton { get; init; } = null!;
    public Button AboutButton { get; init; } = null!;
    public Button SaveButton { get; init; } = null!;
    public Label StatusLabel { get; init; } = null!;
    public TableLayoutPanel FieldsTable { get; init; } = null!;
}

// Step 3: builds the whole form layout in code (toolbar, field area, status line).
internal static class Layout
{
    private const float FieldRowHeight = 30f;

    public static LayoutParts Build(Form form)
    {
        // Toolbar on top: Demo | Language | About.
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(4, 4, 0, 0),
        };
        // Step 4 wires the Demo launch (MozaWheelControl.exe --demo=...).
        var demo = new Button { Text = "Demo", AutoSize = true, Enabled = false };
        var language = new Button { Text = Strings.T(Strings.Key.Language), AutoSize = true };
        var about = new Button { Text = Strings.T(Strings.Key.About), AutoSize = true };
        toolbar.Controls.Add(demo);
        toolbar.Controls.Add(language);
        toolbar.Controls.Add(about);

        // Middle: scrollable area that hosts the generated config fields.
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
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

        // Bottom: status line (fills) with the Save button pinned right.
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4, 6, 4, 0),
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        var save = new Button { Text = Strings.T(Strings.Key.Save), AutoSize = true };
        bottom.Controls.Add(status, 0, 0);
        bottom.Controls.Add(save, 1, 0);

        // Root: three rows — the fields row takes all remaining space.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(scroll, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        form.Controls.Add(root);

        return new LayoutParts
        {
            DemoButton = demo,
            LanguageButton = language,
            AboutButton = about,
            SaveButton = save,
            StatusLabel = status,
            FieldsTable = fields,
        };
    }

    // Two-column placement: the first half of the entries on the left,
    // the rest on the right; the table grows rows as needed.
    public static void AddFields(TableLayoutPanel table, IReadOnlyList<ConfigBinding.Entry> entries)
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
                Text = entry.Key,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            entry.Control.Dock = DockStyle.Fill;
            table.Controls.Add(label, col, row);
            table.Controls.Add(entry.Control, col + 1, row);
        }
    }
}
