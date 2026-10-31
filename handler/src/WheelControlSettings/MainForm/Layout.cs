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
    public MenuStrip Menu { get; init; } = null!;
    public Button SaveButton { get; init; } = null!;
    public FlowLayoutPanel SaveRow { get; init; } = null!;

    // Status messages (Step 5, 5 s auto-clear) as a plain label on the Save
    // button's line: transparent and empty while idle — paints nothing.
    public Label StatusLabel { get; init; } = null!;

    // The root table: FitToContent rewrites its two row heights to the
    // exact measured values (no percent row — no remainder).
    public TableLayoutPanel Root { get; init; } = null!;
    public TableLayoutPanel SectionsTable { get; init; } = null!;

    // Form client width: AddFields sizes the GroupBox frames and their inner
    // field tables arithmetically, before any layout pass runs.
    public int ClientWidth { get; init; }

    // The section GroupBoxes are created by AddFields (which runs after
    // Build), so this list starts empty here and is filled there; ApplyTexts
    // refreshes the frame titles through it on every language change.
    public List<GroupBox> SectionBoxes { get; } = new();
}

// UI iteration 1 (Step 5), sections-in-frame rework: builds the whole form
// layout in code with stock controls — a flat MenuStrip on top, five category
// GroupBox frames stacked vertically on one screen (bold caption in the
// standard control color, two field columns inside, 12 px margins, no
// scrolling), a Save button with its 5 s status text line at the bottom —
// no strip/bar widgets anywhere (spec: nothing but the frames, the button
// and the plain background at the window's bottom).
internal static class Layout
{
    private const float FieldRowHeight = 32f;

    // Extra window height beyond the strict content sum (spec: the window
    // itself is increased vertically a bit), spread over the five frames as
    // extra bottom padding — every spec'd 12 px gap stays exact.
    private const int WindowExtraHeight = 40;

    // Sections-in-frame chrome (spec): 12 px from the window edge to the
    // first frame and between frames; fields start 12 px from a frame's side
    // borders, GroupBoxPadTop px below the bold caption band and
    // GroupBoxPadBottom px above the bottom border (all px).
    private const int SectionMargin = 12;
    private const float SectionGapHeight = 12f;
    private const int GroupBoxPadH = 12;
    private const int GroupBoxPadTop = 6;
    private const int GroupBoxPadBottom = 8;

    // Gap between the last frame and the Save button AND between the button
    // and the client bottom (spec: both gaps equal, 6 px): the window ends
    // exactly under the button — no strip, no band, no extra empty space.
    private const int SaveGap = 6;

    // Step 9c field grid spacing: the label text ends LabelFieldGap px before
    // its input, adjacent rows are separated by 2 * RowMargin px, and the two
    // label/field halves are split by a ColumnGutter px gutter.
    private const int LabelFieldGap = 8;
    private const int RowMargin = 6;
    private const int ColumnGutter = 20;

    // Section layout (single screen): fields per section in row-major pairs
    // (left | right on every row); the key order matches the spec grid
    // exactly; keys are config.json keys.
    internal static readonly string[][] SectionGroups =
    {
        new[] { "debug", "smooth", "simulate", "smoothTimeMs" },
        new[] { "maxTorque", "userFullLockDeg", "forceInvert" },
        new[] { "holdForce", "holdPosition", "holdRampMs", "progressiveHold", "lockNudge" },
        new[] { "maxDegPerSec", "moveForceLimit", "maxMoveSpeedRpm" },
        new[] { "socketHost", "socketPort", "socketAutoStart", "invertSocket", "socketHandsMode", "socketSmoothTimeMs", "dataIntervalMs" },
    };

    // Localized title per section, parallel to SectionGroups (refreshed by
    // ApplyTexts through LayoutParts.SectionBoxes).
    internal static readonly Strings.Key[] SectionTitleKeys =
    {
        Strings.Key.TabGeneral,
        Strings.Key.TabWheel,
        Strings.Key.TabHold,
        Strings.Key.TabMove,
        Strings.Key.TabSocket,
    };

    public static LayoutParts Build(Form form)
    {
        // Standard MenuStrip: Demo | Language (submenu) | About. The flat
        // renderer kills the default gradient (spec item 1).
        var menu = new MenuStrip { Dock = DockStyle.Fill, Renderer = new FlatMenuRenderer() };
        // Text: ApplyTexts (the single source), like language/about — starts empty.
        var demo = new ToolStripMenuItem();
        // Localized texts are applied by MainForm.ApplyTexts (the single
        // source, also re-run on every language change); items start empty.
        var language = new ToolStripMenuItem();
        var about = new ToolStripMenuItem();
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

        // Middle: five GroupBox frames stacked vertically (sections-in-frame
        // style), 12 px from the window edges — the boxes themselves are
        // created by AddFields, this table only holds their rows and the gaps
        // between them. Nothing scrolls; everything fits the height.
        var sections = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(SectionMargin, SectionMargin, SectionMargin, 0),
        };
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // Save button: floats in FORM coordinates (PlaceSaveRow) — top =
        // the last frame's bottom + 6 px (the client height is built the
        // same way, so the bottom gap matches), pinned 15 px from the right
        // edge with explicit bounds; the button itself stays AutoSize and
        // PlaceSaveRow re-runs on every language change (and re-checks the
        // fit, growing the client for a wider localized caption).
        var saveRow = new FlowLayoutPanel
        {
            Dock = DockStyle.None,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Padding = Padding.Empty,
        };
        // Margin = Empty: the default 3 px control margin inside a row sized
        // to the button shifted it out of the row (X=-3, Y=3) and the parent
        // clipped its border — now the button fills the row exactly.
        var save = new Button { AutoSize = true, Margin = Padding.Empty }; // text: ApplyTexts
        saveRow.Controls.Add(save);

        // Status text (Step 5, shared 5 s auto-clear target): a plain label
        // on the Save button's line, to the left of it — transparent and
        // empty while idle, so it paints no bar or fragment anywhere; a
        // message appears as text only while it lives. The former
        // StatusStrip widget is not created at all (spec: no strip, no
        // gray remnants in the corner).
        var status = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // Root: menu / frames — only the frames row grows; zero margins on
        // the root children so FitToContent's explicit row heights map 1:1
        // onto the rows. The window ends exactly under the Save button
        // (FitToContent: last frame bottom + gap + button + gap); the button
        // and the status label float in FORM coordinates over the root's
        // empty bottom area.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        menu.Margin = Padding.Empty;
        sections.Margin = Padding.Empty;
        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(sections, 0, 1);
        form.Controls.Add(root);
        form.Controls.Add(status);
        form.Controls.Add(saveRow); // floating over the root: never covered
        form.MainMenuStrip = menu;

        return new LayoutParts
        {
            DemoItem = demo,
            LanguageItem = language,
            AboutItem = about,
            LanguageItems = languageItems,
            Menu = menu,
            SaveButton = save,
            SaveRow = saveRow,
            StatusLabel = status,
            Root = root,
            SectionsTable = sections,
            ClientWidth = form.ClientSize.Width,
        };
    }

    // Builds the sections-in-frame layout on the one screen: one GroupBox per
    // section (SectionGroups order; bold 9pt caption, standard control color),
    // the section's fields in row-major pairs (left | right) inside a
    // two-column table set 12 px from the frame borders. A config key not
    // listed in SectionGroups falls back to the first section, so an
    // unknown/new key stays editable. Labels get the localized caption for
    // their key (Strings.ConfigLabel), label+control get the localized tooltip
    // (Strings.KeyTip). The created (key, label, control) triples are
    // collected so MainForm can refresh captions AND tooltips together on a
    // language change; the frames go to parts.SectionBoxes the same way —
    // ApplyTexts refreshes their titles too. Field fonts are pinned to
    // Control.DefaultFont: inside a GroupBox the bold caption font would
    // otherwise be inherited by every child.
    public static void AddFields(
        LayoutParts parts,
        IReadOnlyList<ConfigBinding.Entry> entries,
        ToolTip tips,
        List<(string Key, Label Label, Control Control)> fields)
    {
        var byKey = new Dictionary<string, ConfigBinding.Entry>(StringComparer.Ordinal);
        foreach (ConfigBinding.Entry entry in entries)
            byKey[entry.Key] = entry;

        var grouped = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] group in SectionGroups)
            grouped.UnionWith(group);

        // Unknown keys (new config additions) keep their place in the first section.
        var extras = new List<ConfigBinding.Entry>();
        foreach (ConfigBinding.Entry entry in entries)
            if (!grouped.Contains(entry.Key)) extras.Add(entry);

        TableLayoutPanel sections = parts.SectionsTable;
        // One bold 9 pt frame-title font shared by all five boxes (process lifetime).
        var headerFont = new Font(Control.DefaultFont.FontFamily, 9f, FontStyle.Bold);
        // Frame width = the sections table client width; the inner table sits
        // GroupBoxPadH px from both frame borders, so this defines its width.
        int boxWidth = parts.ClientWidth - 2 * SectionMargin;
        int innerWidth = boxWidth - 2 * GroupBoxPadH;

        int row = 0;

        for (int s = 0; s < SectionGroups.Length; s++)
        {
            // The gap row before every frame but the first (spec: 12 px).
            if (s > 0)
                SetRowHeight(sections, row++, SectionGapHeight);

            var box = new GroupBox
            {
                Text = Strings.T(SectionTitleKeys[s]),
                Font = headerFont,
                ForeColor = SystemColors.ControlText,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
            };
            var table = new TableLayoutPanel
            {
                AutoSize = false,
                ColumnCount = 4,
                Padding = Padding.Empty,
            };
            // Placeholder styles: the loop below measures the labels and the
            // final block replaces all four with absolute/percent columns.
            for (int c = 0; c < 4; c++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            box.Controls.Add(table);

            var wanted = new List<ConfigBinding.Entry>();
            foreach (string key in SectionGroups[s])
                if (byKey.TryGetValue(key, out var entry))
                    wanted.Add(entry);
            if (s == 0) wanted.AddRange(extras);

            // Row-major pairs: entry i sits on row i / 2, left or right half.
            int fieldRows = (wanted.Count + 1) / 2;
            for (int r = 0; r < fieldRows; r++)
                SetRowHeight(table, r, FieldRowHeight);

            int maxLeft = 0;
            int maxRight = 0;

            for (int i = 0; i < wanted.Count; i++)
            {
                ConfigBinding.Entry entry = wanted[i];
                int cellRow = i / 2;
                int col = (i % 2) * 2;

                var label = new Label
                {
                    // Pinned: inside the frame the bold caption font would
                    // otherwise be inherited by the label and the input.
                    Font = Control.DefaultFont,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    // The gutter (left half of the right column) and the gap to
                    // the input live in the margin: the cell fits the widest
                    // caption exactly, so the visible gap stays constant.
                    Margin = new Padding(col == 0 ? 0 : ColumnGutter, RowMargin, LabelFieldGap, RowMargin),
                };
                entry.Control.Font = Control.DefaultFont;
                entry.Control.Dock = DockStyle.Fill;
                entry.Control.Margin = new Padding(0, RowMargin, 0, RowMargin);

                ApplyFieldText(entry.Key, label, entry.Control, tips);

                if (col == 0)
                    maxLeft = Math.Max(maxLeft, label.PreferredSize.Width);
                else
                    maxRight = Math.Max(maxRight, label.PreferredSize.Width);

                fields.Add((entry.Key, label, entry.Control));
                table.Controls.Add(label, col, cellRow);
                table.Controls.Add(entry.Control, col + 1, cellRow);
            }

            // Absolute columns for everything (spec, item 3): the label cells
            // hug their widest caption of this frame, the two input cells
            // split what is left — the four widths sum to innerWidth by
            // construction, so the right column with its numeric spinner
            // always ends inside the frame (60 px input floor keeps fields
            // editable even if a future label grows absurdly long).
            int leftLabel = maxLeft + LabelFieldGap;
            int rightLabel = ColumnGutter + maxRight + LabelFieldGap;
            int inputCol = Math.Max(60, (innerWidth - leftLabel - rightLabel) / 2);
            table.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, leftLabel);
            table.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, inputCol);
            table.ColumnStyles[2] = new ColumnStyle(SizeType.Absolute, rightLabel);
            table.ColumnStyles[3] = new ColumnStyle(SizeType.Absolute, inputCol);

            // Manual inner geometry: below the bold caption band,
            // GroupBoxPadH px from the side borders. The frame height wraps
            // the table exactly plus this frame's share of the window's extra
            // height (WindowExtraHeight); the row style gets the same number,
            // and with an empty Margin the docked frame fills that row
            // unchanged — so the whole window grows by the constant above.
            int innerHeight = (int)(fieldRows * FieldRowHeight);
            table.Location = new Point(GroupBoxPadH, box.Font.Height + GroupBoxPadTop);
            table.Size = new Size(innerWidth, innerHeight);

            int boxHeight = table.Location.Y + innerHeight + GroupBoxPadBottom
                + WindowExtraHeight / SectionGroups.Length;
            SetRowHeight(sections, row, boxHeight);
            sections.Controls.Add(box, 0, row);
            parts.SectionBoxes.Add(box);
            row++;
        }
    }

    // One absolute row style per row, grown in order (RowCount follows).
    private static void SetRowHeight(TableLayoutPanel table, int row, float height)
    {
        while (table.RowStyles.Count <= row)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRowHeight));
        table.RowStyles[row] = new RowStyle(SizeType.Absolute, height);
        table.RowCount = table.RowStyles.Count;
    }

    // Fits the form exactly to its parts (spec, item 1): two explicit root
    // rows — menu and the frames table — and the client height is
    // frames bottom + SaveGap + button height + SaveGap, so the window ends
    // exactly under the Save button with the same 6 px gap above and below
    // it. No percent row: no remainder, so no "chin" and no clipping; no
    // band, no strip — nothing but background below the button. Call it
    // AFTER ApplyTexts: the menu and the Save button are then measured with
    // their final texts.
    public static void FitToContent(Form form, LayoutParts parts)
    {
        int menuH = Math.Max(24, parts.Menu.PreferredSize.Height);
        int sectionsH = (int)Math.Ceiling(VerticalSum(parts.SectionsTable));
        int buttonH = parts.SaveButton.PreferredSize.Height;

        TableLayoutPanel root = parts.Root;
        root.RowStyles[0] = new RowStyle(SizeType.Absolute, menuH);
        root.RowStyles[1] = new RowStyle(SizeType.Absolute, sectionsH);
        root.RowCount = 2;

        // ClientHeight = lastGroupBox.Bottom (menuH + sectionsH, the frame
        // chain fills its rows exactly) + gap + button + gap — the same
        // SaveGap on both sides by construction.
        form.ClientSize = new Size(
            form.ClientSize.Width,
            menuH + sectionsH + SaveGap + buttonH + SaveGap);

        // Lay the whole chain out (form → root → sections → frames) so
        // PlaceSaveRow reads REAL coordinates, not row-sum estimates.
        form.PerformLayout();
        PlaceSaveRow(form, parts);
    }

    // Places the Save button in form coordinates (spec, step 4): top gap =
    // the last frame's real bottom + SaveGap, left = client width − width −
    // 15. Strict order: first the position, THEN the fit check — if the
    // button crosses the 15 px right / SaveGap bottom margins (a wider
    // localized caption on uk/by, a taller frame chain), the client grows by
    // exactly the missing amount, so the button is always whole and never
    // glued to an edge. The status label shares the button's line at the
    // left. Called from FitToContent and from ApplyTexts (after
    // PerformLayout): the localized caption changes the button width.
    public static void PlaceSaveRow(Form form, LayoutParts parts)
    {
        int buttonW = parts.SaveButton.PreferredSize.Width;
        int buttonH = parts.SaveButton.PreferredSize.Height;

        // Real geometry: force the table layout and walk up the parents —
        // frame (child of sections) → sections (row 1 of root) → root.
        parts.Root.PerformLayout();
        parts.SectionsTable.PerformLayout();
        Control lastBox = parts.SectionBoxes[^1];
        int lastBoxBottom = lastBox.Bottom + parts.SectionsTable.Top + parts.Root.Top;

        // 1) Position: last frame bottom + SaveGap on top, 15 px from the
        // right edge; deterministic bounds (no AutoSize pass can collapse
        // the row to 0×0) and brought to the front over the root's backcolor.
        int y = lastBoxBottom + SaveGap;
        parts.SaveRow.AutoSize = false;
        parts.SaveRow.SetBounds(
            form.ClientSize.Width - 15 - buttonW,
            y,
            buttonW,
            buttonH);
        parts.SaveRow.BringToFront();

        // Real flow position of the button inside the row (its actual
        // bounds, not the row's — the two can differ if anything shifts).
        parts.SaveRow.PerformLayout();
        int right = parts.SaveRow.Left + parts.SaveButton.Right;
        int bottom = parts.SaveRow.Top + parts.SaveButton.Bottom;

        // 2) Fit check AFTER the position (never before): grow the client by
        // exactly what is missing — the margins then stay 15 px right and
        // SaveGap bottom, and nothing is clipped by the client area.
        if (right > form.ClientSize.Width - 15)
            form.ClientSize = new Size(right + 15, form.ClientSize.Height);
        if (bottom > form.ClientSize.Height - SaveGap)
            form.ClientSize = new Size(form.ClientSize.Width, bottom + SaveGap);

        // Status text on the same line, left of the button (transparent and
        // empty while idle — nothing is painted, no fragment in the corner);
        // brought forward because the root's own background covers its
        // empty bottom area. Width read after the possible client growth.
        Size client = form.ClientSize;
        parts.StatusLabel.SetBounds(
            SectionMargin,
            y,
            Math.Max(0, client.Width - 15 - buttonW - SectionMargin - LabelFieldGap),
            buttonH);
        parts.StatusLabel.BringToFront();
    }

    // TEMP diagnostics for the Step 9 run (removed after acceptance): dumps
    // the real laid-out geometry to layout-dump.txt next to the exe, so the
    // button's placement can be verified offline instead of guessed.
    public static void DumpLayout(Form form, LayoutParts parts)
    {
        Control lastBox = parts.SectionBoxes[^1];
        string dump =
            $"client={form.ClientSize}\n" +
            $"menu={parts.Menu.Bounds}\n" +
            $"root={parts.Root.Bounds} top={parts.Root.Top}\n" +
            $"sections={parts.SectionsTable.Bounds} top={parts.SectionsTable.Top}\n" +
            $"lastBox={lastBox.Bounds} bottom={lastBox.Bottom} parent={lastBox.Parent?.Name}\n" +
            $"saveRow={parts.SaveRow.Bounds}\n" +
            $"button={parts.SaveButton.Bounds} pref={parts.SaveButton.PreferredSize}\n" +
            $"status={parts.StatusLabel.Bounds} text='{parts.StatusLabel.Text}'\n";
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "layout-dump.txt"), dump);
        }
        catch
        {
            // diagnostics only — never break the form over a dump file
        }
    }

    // Height of the frames table: its padding plus the row styles — the same
    // number the table lays itself out to (AutoSize + Dock=Top), so the
    // frames row can never end up shorter (clip) or longer (gap) than the
    // table: last frame bottom == row bottom == row1 end, by construction.
    private static float VerticalSum(TableLayoutPanel table)
    {
        float sum = table.Padding.Vertical;
        foreach (RowStyle row in table.RowStyles)
            sum += row.Height;
        return sum;
    }

    // Caption + tooltip for one config field — the single place that maps a
    // key to its texts. Called when the fields are created (AddFields) and
    // on every language change (MainForm.ApplyTexts), so the initial build
    // and the refresh can never drift apart.
    public static void ApplyFieldText(string key, Label label, Control control, ToolTip tips)
    {
        label.Text = Strings.ConfigLabel(key);
        string tip = Strings.KeyTip(key);
        tips.SetToolTip(label, tip);
        tips.SetToolTip(control, tip);
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
// shade — it reads as a toolbar but never looks dirty), dropdowns keep
// the form color (#F0F0F0); hover stays a subtle flat gray, visible on
// both backgrounds. ProfessionalColorTable exposes get-only virtual
// properties — they are overridden, not assigned.
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
}
