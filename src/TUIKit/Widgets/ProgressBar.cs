namespace TUIKit.Widgets
{
    using System;
    using System.Globalization;
    using TUIKit;

    /// <summary>
    /// A horizontal progress bar with a centered percentage label.
    /// </summary>
    public sealed class ProgressBar : IWidget
    {
        private double _Value;

        /// <summary>
        /// Gets or sets the completion fraction, clamped to 0.0 through 1.0. Defaults to 0.
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
        /// Gets or sets the color of the filled portion. Defaults to palette blue.
        /// </summary>
        public Color FillColor { get; set; } = Color.FromPalette(4);

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
            string percent = ((int)Math.Round(_Value * 100.0)).ToString(CultureInfo.InvariantCulture) + "%";
            int labelStart = Math.Max(0, (width - percent.Length) / 2);

            for (int x = 0; x < width; x++)
            {
                bool inLabel = x >= labelStart && x < labelStart + percent.Length;
                string glyph = inLabel ? percent[x - labelStart].ToString() : (x < filled ? "█" : "░");
                CellStyle style = x < filled
                    ? CellStyle.Default.WithForeground(inLabel ? Color.FromRgb(0, 0, 0) : FillColor).WithBackground(inLabel ? FillColor : Color.Default)
                    : CellStyle.Default.WithForeground(inLabel ? FillColor : Color.FromPalette(8));
                surface.Set(x, 0, Cell.Glyph(glyph, style, 1));
            }
        }
    }
}
