namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;

    /// <summary>
    /// A horizontal bar chart. Each row shows a label, a proportional bar drawn with block glyphs
    /// (including one-eighth partial cells for sub-cell precision), and the value. Bars scale to the
    /// largest value so the widest bar fills the available width. Labels are padded to a common width
    /// so the bars align.
    /// </summary>
    public sealed class BarChart : IWidget
    {
        private static readonly string[] _Partials = { "", "▏", "▎", "▍", "▌", "▋", "▊", "▉" };

        private readonly List<BarEntry> _Entries = new List<BarEntry>();

        /// <summary>
        /// Gets or sets the bar color. Defaults to green.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(2);

        /// <summary>
        /// Gets the number of bars.
        /// </summary>
        public int Count
        {
            get { return _Entries.Count; }
        }

        /// <summary>
        /// Adds a labeled bar.
        /// </summary>
        /// <param name="label">The bar label. Must not be null.</param>
        /// <param name="value">The bar value. Negative values are clamped to zero when drawn.</param>
        /// <returns>This chart, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public BarChart Add(string label, double value)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            _Entries.Add(new BarEntry(label, value));
            return this;
        }

        /// <summary>
        /// Removes every bar.
        /// </summary>
        public void Clear()
        {
            _Entries.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Entries.Count));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0 || _Entries.Count == 0)
                return;

            int labelWidth = 0;
            double max = 0;
            for (int i = 0; i < _Entries.Count; i++)
            {
                if (_Entries[i].Label.Length > labelWidth)
                    labelWidth = _Entries[i].Label.Length;
                if (_Entries[i].Value > max)
                    max = _Entries[i].Value;
            }

            labelWidth = Math.Min(labelWidth, Math.Max(1, width / 3));
            CellStyle labelStyle = CellStyle.Default;
            CellStyle barStyle = CellStyle.Default.WithForeground(Color);

            for (int row = 0; row < height && row < _Entries.Count; row++)
            {
                BarEntry entry = _Entries[row];
                string label = Fit(entry.Label, labelWidth).PadRight(labelWidth);
                surface.DrawText(0, row, label, labelStyle);

                string valueText = " " + entry.Value.ToString("0.##", CultureInfo.InvariantCulture);
                int barArea = width - labelWidth - 1 - valueText.Length;
                if (barArea < 1)
                {
                    surface.DrawText(labelWidth, row, valueText.TrimStart(), labelStyle);
                    continue;
                }

                double fraction = max <= 0 ? 0 : Math.Max(0, entry.Value) / max;
                double cells = fraction * barArea;
                int full = (int)Math.Floor(cells);
                int eighths = (int)Math.Round((cells - full) * 8);
                if (eighths == 8)
                {
                    full++;
                    eighths = 0;
                }

                int x = labelWidth + 1;
                for (int b = 0; b < full && x < width; b++, x++)
                    surface.DrawText(x, row, "█", barStyle);

                if (eighths > 0 && x < width)
                {
                    surface.DrawText(x, row, _Partials[eighths], barStyle);
                    x++;
                }

                surface.DrawText(width - valueText.Length, row, valueText, labelStyle);
            }
        }

        private static string Fit(string text, int width)
        {
            if (text.Length <= width)
                return text;
            if (width <= 1)
                return text.Substring(0, Math.Max(0, width));

            return text.Substring(0, width - 1) + "…";
        }
    }
}
