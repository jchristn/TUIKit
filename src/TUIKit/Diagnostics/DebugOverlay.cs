namespace TUIKit.Diagnostics
{
    using System;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Layout;

    /// <summary>
    /// Draws a diagnostic overlay: an outline around every layout region and a one-line readout of
    /// frame timing. Toggle it on to inspect layout and performance while the UI runs.
    /// </summary>
    public static class DebugOverlay
    {
        /// <summary>
        /// Renders the overlay onto the surface.
        /// </summary>
        /// <param name="surface">The screen surface. Must not be null.</param>
        /// <param name="layout">The layout whose regions are outlined, or null to skip outlines.</param>
        /// <param name="stats">The frame statistics to read, or null to skip the readout.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public static void Render(ISurface surface, Layout? layout, FrameStats? stats)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            CellStyle outline = CellStyle.Default.WithForeground(Color.FromPalette(5));

            if (layout != null)
            {
                for (int i = 0; i < layout.Regions.Count; i++)
                {
                    Rect rect = layout.Resolve(layout.Regions[i], surface.Size);
                    if (!rect.IsEmpty)
                        surface.DrawBox(rect, outline, layout.Regions[i].Id);
                }
            }

            if (stats != null)
            {
                string readout = "frame " + stats.LastMilliseconds.ToString("F1", CultureInfo.InvariantCulture)
                    + "ms  avg " + stats.AverageMilliseconds().ToString("F1", CultureInfo.InvariantCulture)
                    + "ms  " + stats.FramesPerSecond().ToString("F0", CultureInfo.InvariantCulture) + "fps";
                CellStyle style = CellStyle.Default
                    .WithForeground(Color.FromRgb(0, 0, 0))
                    .WithBackground(Color.FromPalette(5));
                surface.DrawText(0, 0, readout, style);
            }
        }
    }
}
