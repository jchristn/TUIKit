namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Unicode;

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
        /// Adds a horizontal stacked bar whose segments are drawn in <see cref="SegmentColors"/> and
        /// <see cref="SegmentGlyphs"/>; the bar's total is the sum of the non-negative segments.
        /// </summary>
        /// <param name="label">The bar label. Must not be null.</param>
        /// <param name="segments">The segment values. Must not be null.</param>
        /// <returns>This chart, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public BarChart AddStacked(string label, params double[] segments)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            _Entries.Add(new BarEntry(label, (double[])segments.Clone()));
            return this;
        }

        /// <summary>
        /// Gets the segment colors for stacked bars, cycled by segment index. Defaults to green, cyan,
        /// yellow, magenta, blue, red (palette 2, 6, 3, 5, 4, 1).
        /// </summary>
        public List<Color> SegmentColors { get; } = new List<Color>
        {
            Color.FromPalette(2), Color.FromPalette(6), Color.FromPalette(3), Color.FromPalette(5), Color.FromPalette(4), Color.FromPalette(1)
        };

        /// <summary>
        /// Gets the segment glyphs for stacked bars, cycled by segment index, so segments differ without
        /// color. Defaults to full, dark, medium, and light shade blocks.
        /// </summary>
        public List<string> SegmentGlyphs { get; } = new List<string> { "\u2588", "\u2593", "\u2592", "\u2591" };

        /// <summary>
        /// Gets or sets an optional formatter for the value printed at the right of each bar, or null for
        /// the default invariant format (<c>0.##</c>).
        /// </summary>
        public Func<double, string>? ValueFormatter { get; set; }

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
                if (TextFit.Width(_Entries[i].Label) > labelWidth)
                    labelWidth = TextFit.Width(_Entries[i].Label);
                if (_Entries[i].Value > max)
                    max = _Entries[i].Value;
            }

            labelWidth = Math.Min(labelWidth, Math.Max(1, width / 3));
            CellStyle labelStyle = CellStyle.Default;
            CellStyle barStyle = CellStyle.Default.WithForeground(Color);

            for (int row = 0; row < height && row < _Entries.Count; row++)
            {
                BarEntry entry = _Entries[row];
                string label = TextFit.PadRight(Fit(entry.Label, labelWidth), labelWidth);
                surface.DrawText(0, row, label, labelStyle);

                string valueText = " " + (ValueFormatter != null ? ValueFormatter(entry.Value) : entry.Value.ToString("0.##", CultureInfo.InvariantCulture));
                int barArea = width - labelWidth - 1 - TextFit.Width(valueText);
                if (barArea < 1)
                {
                    surface.DrawText(labelWidth, row, valueText.TrimStart(), labelStyle);
                    continue;
                }

                if (entry.Segments != null)
                {
                    DrawStacked(surface, entry, labelWidth + 1, row, barArea, max);
                    surface.DrawText(width - TextFit.Width(valueText), row, valueText, labelStyle);
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

                surface.DrawText(width - TextFit.Width(valueText), row, valueText, labelStyle);
            }
        }

        private void DrawStacked(ISurface surface, BarEntry entry, int x, int row, int barArea, double max)
        {
            double[] segments = entry.Segments!;
            double running = 0;
            int drawn = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                running += Math.Max(0, segments[i]);
                int end = max <= 0 ? 0 : (int)Math.Round(running / max * barArea);
                Color color = SegmentColors.Count > 0 ? SegmentColors[i % SegmentColors.Count] : Color;
                string glyph = SegmentGlyphs.Count > 0 ? SegmentGlyphs[i % SegmentGlyphs.Count] : "\u2588";
                CellStyle style = CellStyle.Default.WithForeground(color);
                for (; drawn < end && drawn < barArea; drawn++)
                    surface.DrawText(x + drawn, row, glyph, style);
            }
        }

        private static string Fit(string text, int width)
        {
            return TextFit.Ellipsize(text, width);
        }
    }
}
