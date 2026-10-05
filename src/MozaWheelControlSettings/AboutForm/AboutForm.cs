using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace MozaWheelControlSettings;

// About dialog per the UI spec: product title, subtitle, "About the project"
// and "Licenses" sections with browser links, a row of 32x32 SVG icon buttons
// (github / beamng / bio, rasterized at runtime with Svg.NET; a missing or
// broken file falls back to the round red stub) with a smooth hover shrink,
// the git commit stamp bottom-left and OK bottom-right — all on one bottom
// row (Step 5, item 5). Window chrome is the standard DWM one; every visual
// is a stock WinForms control with standard fonts.
internal sealed class AboutForm : Form
{
    // Icon geometry and hover animation (spec): 32x32 resting, 0.9 scale while
    // hovered, ~150 ms per transition at 15 ms ticks (3.2 px / 10 ticks).
    private const int IconSize = 32;
    private const float ShrinkScale = 0.9f;
    private const int AnimIntervalMs = 15;
    private const float AnimStep = 0.32f;

    // Bottom row geometry: 15 px from the bottom/edges, 10 px gap between
    // the icons, 20 px from the last icon to the OK button.
    private const int BottomMargin = 15;
    private const int IconGap = 10;
    private const int IconToOkGap = 20;

    private readonly HoverIcon[] _icons;
    private readonly System.Windows.Forms.Timer _animTimer;
    private readonly ToolTip _tips;

    public AboutForm()
    {
        Text = Strings.T(Strings.Key.About);
        ClientSize = new Size(480, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        // Window icon: icon.ico next to the exe when present (copied by the
        // csproj); otherwise the default embedded exe icon — no crash either way.
        string iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
            Icon = new Icon(iconPath);

        var title = new Label
        {
            Text = "SpectAPilot",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
            Location = new Point(24, 20),
        };
        var subtitle = new Label
        {
            Text = Strings.T(Strings.Key.Subtitle),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(24, 56),
        };

        var projectHeader = new Label
        {
            Text = Strings.T(Strings.Key.ProjectHeader),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(24, 100),
        };
        var projectText = new Label
        {
            Text = Strings.T(Strings.Key.ProjectText),
            // Label has no WordWrap property: with AutoSize off the text wraps
            // inside the fixed bounds.
            AutoSize = false,
            Size = new Size(432, 42),
            Location = new Point(24, 126),
        };

        var licensesHeader = new Label
        {
            Text = Strings.T(Strings.Key.LicensesHeader),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(24, 180),
        };
        var sdkLink = new LinkLabel { Text = "MozaSDK", AutoSize = true, Location = new Point(24, 208) };
        var licenseLink = new LinkLabel { Text = "MIT License", AutoSize = true, Location = new Point(24, 236) };
        sdkLink.LinkClicked += (_, _) => OpenUrl("https://mozaracing.com/pages/sdk");
        licenseLink.LinkClicked += (_, _) => OpenUrl("https://github.com/sophrosha/spectapilot/blob/main/LICENSE");

        // Bottom row, laid out right to left: OK against the right margin
        // (15 px), the row of 32x32 SVG icon buttons immediately to its left
        // with a 20 px gap — the "icons + OK" group sits right of the form's
        // center, not in the middle. Anchoring to OK keeps the group from
        // drifting into the corner by construction; the row's center line is
        // shared with OK and the commit label.
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };
        Size okSize = ok.PreferredSize;

        int rowTop = ClientSize.Height - BottomMargin - IconSize;
        int rowCenterY = rowTop + IconSize / 2;
        int iconsWidth = 3 * IconSize + 2 * IconGap;
        int iconsX = ClientSize.Width - BottomMargin - okSize.Width - IconToOkGap - iconsWidth;
        _icons = new[]
        {
            new HoverIcon(new Point(iconsX, rowTop), LoadSvgIcon("github.svg")),
            new HoverIcon(new Point(iconsX + IconSize + IconGap, rowTop), LoadSvgIcon("beamng.svg")),
            new HoverIcon(new Point(iconsX + 2 * (IconSize + IconGap), rowTop), LoadSvgIcon("bio.svg")),
        };

        ok.Location = new Point(
            ClientSize.Width - BottomMargin - okSize.Width,
            rowCenterY - okSize.Height / 2);
        AcceptButton = ok;
        // Tooltips are RU-only for now (spec: like the settings tooltips —
        // temporarily Russian), standard WinForms delay of 500 ms.
        _tips = new ToolTip { InitialDelay = 500 };
        _tips.SetToolTip(_icons[0], "Основной репозиторий проекта");
        _tips.SetToolTip(_icons[1], "Страница мода (скоро будет)");
        _tips.SetToolTip(_icons[2], "Био страница автора");
        _icons[0].Click += (_, _) => OpenUrl("https://github.com/sophrosha/spectapilot");
        // The BeamNG mod icon is a stub on purpose: the click does nothing yet.
        _icons[2].Click += (_, _) => OpenUrl("https://sophron.ru");

        // One shared timer advances every icon: linear interpolation of the
        // size while hovered (shrink) and after leaving (restore). It also
        // watches the fixed icon cell — the control gets smaller under the
        // cursor, so its own MouseLeave alone can fire too early or get stuck.
        _animTimer = new System.Windows.Forms.Timer { Interval = AnimIntervalMs };
        _animTimer.Tick += (_, _) =>
        {
            bool busy = false;
            foreach (HoverIcon icon in _icons)
            {
                if (icon.Target < IconSize && !icon.CursorOverCell())
                    icon.Target = IconSize; // the cursor really left the cell
                busy |= icon.Advance();
                busy |= icon.Target < IconSize; // hovered: keep watching the cell
            }
            if (!busy) _animTimer.Stop();
        };
        foreach (HoverIcon icon in _icons)
        {
            icon.MouseEnter += (_, _) =>
            {
                icon.Target = IconSize * ShrinkScale;
                _animTimer.Start();
            };
            icon.MouseLeave += (_, _) =>
            {
                // Restore only when the cursor is outside the resting cell:
                // shrinking can move the bounds out from under the cursor and
                // a plain restore here would cause an enter/leave flicker loop.
                if (!icon.CursorOverCell()) icon.Target = IconSize;
                _animTimer.Start();
            };
        }

        // Commit bottom-left (15 px), vertically centered on the same row.
        var commit = new Label
        {
            Text = ReadCommitStamp(),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font(Font.FontFamily, 7.5f),
        };
        commit.Location = new Point(
            BottomMargin,
            rowCenterY - commit.PreferredSize.Height / 2);

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(projectHeader);
        Controls.Add(projectText);
        Controls.Add(licensesHeader);
        Controls.Add(sdkLink);
        Controls.Add(licenseLink);
        Controls.Add(_icons[0]);
        Controls.Add(_icons[1]);
        Controls.Add(_icons[2]);
        Controls.Add(commit);
        Controls.Add(ok);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _animTimer.Stop();
        _animTimer.Dispose();
        _tips.Dispose();
        foreach (HoverIcon icon in _icons) icon.Image?.Dispose();
        base.OnFormClosed(e);
    }

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

    // An SVG icon-button that shrinks smoothly to ShrinkScale while the cursor
    // is over its fixed cell and grows back otherwise. Size only — no color
    // effects (spec); the form's shared timer drives the interpolation. With
    // no SVG loaded (missing/broken asset) the control is the round red stub
    // of the previous iteration: red back color carved into a circular region.
    private sealed class HoverIcon : PictureBox
    {
        private readonly Point _cell; // top-left of the resting IconSize box, in parent coords

        public float Current { get; private set; } = IconSize;
        public float Target { get; set; } = IconSize;

        public HoverIcon(Point cell, Image? image)
        {
            _cell = cell;
            SizeMode = PictureBoxSizeMode.Zoom; // the artwork scales with the shrink
            TabStop = false;
            if (image is not null)
            {
                Image = image;
                BackColor = Color.Transparent; // form shows through transparent SVG areas
            }
            else
            {
                BackColor = Color.Red; // fallback stub
            }
            ApplyBounds();
        }

        // One linear step toward Target; false when the size has settled.
        public bool Advance()
        {
            float delta = Target - Current;
            if (Math.Abs(delta) <= AnimStep)
            {
                if (delta == 0f) return false;
                Current = Target;
            }
            else
            {
                Current += Math.Sign(delta) * AnimStep;
            }
            ApplyBounds();
            return Current != Target;
        }

        // True while the cursor is inside the resting cell (screen coords) —
        // authoritative for hover while the bounds themselves change.
        public bool CursorOverCell()
        {
            if (Parent is null) return false;
            Point topLeft = Parent.PointToScreen(_cell);
            return new Rectangle(topLeft, new Size(IconSize, IconSize)).Contains(Cursor.Position);
        }

        // Keep the icon centered in its cell while the size changes so it
        // never slides into a corner.
        private void ApplyBounds()
        {
            int size = (int)Math.Round(Current);
            SetBounds(
                _cell.X + (IconSize - size) / 2,
                _cell.Y + (IconSize - size) / 2,
                size,
                size);

            // The SVG artwork keeps its own shape; only the red fallback stub
            // is carved into a round region (as in the previous iteration).
            if (Image is not null) return;
            Region? old = Region;
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, size - 1, size - 1);
            Region = new Region(path);
            // Region.Dispose is idempotent whether or not the control already
            // released the previous region in its setter.
            old?.Dispose();
        }
    }
}
