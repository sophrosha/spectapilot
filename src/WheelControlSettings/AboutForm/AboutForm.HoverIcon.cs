using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
