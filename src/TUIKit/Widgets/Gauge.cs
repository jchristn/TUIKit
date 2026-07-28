namespace TUIKit.Widgets
{
    using System;
    using TUIKit;

    /// <summary>
    /// A horizontal bar that fills in proportion to a value between 0 and 1.
    /// </summary>
    public sealed class Gauge : IWidget
    {
        private double _Value;

        /// <summary>
        /// Gets or sets the fill fraction, clamped to the range 0.0 through 1.0. Defaults to 0.
        /// </summary>
        public double Value
        {
            get { return _Value; }
            set
            {
                if (value < 0.0)
                    _Value = 0.0;
                else if (value > 1.0)
                    _Value = 1.0;
                else
                    _Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the color of the filled portion. Defaults to palette green.
        /// </summary>
        public Color FillColor { get; set; } = Color.FromPalette(2);

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            if (width <= 0)
                return;

            int filled = (int)Math.Round(_Value * width, MidpointRounding.AwayFromZero);
            CellStyle fill = CellStyle.Default.WithForeground(FillColor);
            CellStyle track = CellStyle.Default.WithForeground(Color.FromPalette(8));

            for (int x = 0; x < width; x++)
            {
                string glyph = x < filled ? "█" : "░";
                surface.Set(x, 0, Cell.Glyph(glyph, x < filled ? fill : track, 1));
            }
        }
    }
}
