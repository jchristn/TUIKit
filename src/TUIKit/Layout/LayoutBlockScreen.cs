namespace TUIKit.Layout
{
    using System;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// Renders the "terminal too small" screen shown when the surface is smaller than a layout's
    /// <see cref="Layout.MinimumSize"/>. Normal rendering resumes automatically once the surface
    /// grows back. All members are thread-safe.
    /// </summary>
    public static class LayoutBlockScreen
    {
        /// <summary>
        /// Draws the block screen centered on the surface.
        /// </summary>
        /// <param name="surface">The surface to draw on. Must not be null.</param>
        /// <param name="required">The minimum size the layout needs.</param>
        /// <param name="actual">The current surface size.</param>
        /// <param name="style">The text style to use.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public static void Render(ISurface surface, Size required, Size actual, CellStyle style)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            surface.Fill(new Rect(0, 0, surface.Size.Width, surface.Size.Height), Cell.Blank(style));

            string line1 = "Terminal too small";
            string line2 = "Need " + required.Width + "x" + required.Height
                + ", have " + actual.Width + "x" + actual.Height;

            int centerRow = surface.Size.Height / 2;
            DrawCentered(surface, centerRow - 1, line1, style);
            DrawCentered(surface, centerRow + 1, line2, style);
        }

        private static void DrawCentered(ISurface surface, int row, string text, CellStyle style)
        {
            if (row < 0 || row >= surface.Size.Height)
                return;

            int width = Graphemes.MeasureWidth(text);
            int start = (surface.Size.Width - width) / 2;
            if (start < 0)
                start = 0;

            surface.DrawText(start, row, text, style);
        }
    }
}
