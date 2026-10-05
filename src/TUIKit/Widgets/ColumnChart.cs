namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// A vertical column chart over buckets (for example days) with one or more series, drawn either
    /// stacked (one column per bucket, segments per series) or grouped (side-by-side columns per bucket).
    /// Each series has its own color and glyph so the chart does not rely on color alone. Optional x
    /// labels, a scale label, and a legend. Use <see cref="BarChart"/> for horizontal bars.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class ColumnChart : IWidget
    {
        private readonly List<ChartSeries> _Series = new List<ChartSeries>();
        private readonly List<string> _Labels = new List<string>();

        /// <summary>
        /// Gets the series. Never null.
        /// </summary>
        public IReadOnlyList<ChartSeries> Series
        {
            get { return _Series; }
        }

        /// <summary>
        /// Gets the bucket labels drawn under the columns (sparsely when they do not all fit). Never null.
        /// </summary>
        public List<string> Labels
        {
            get { return _Labels; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether series stack in one column per bucket (true, the
        /// default) or stand side by side (false).
        /// </summary>
        public bool Stacked { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether a legend row is drawn at the bottom. Defaults to true.
        /// </summary>
        public bool ShowLegend { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the scale maximum is printed at the top-left. Defaults to true.
        /// </summary>
        public bool ShowScale { get; set; } = true;

        /// <summary>
        /// Gets or sets the formatter for the scale label, or null for the invariant <c>0.##</c> format.
        /// </summary>
        public Func<double, string>? ValueFormatter { get; set; }

        /// <summary>
        /// Gets or sets the style of labels, scale, and legend text. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle LabelStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Adds a series.
        /// </summary>
        /// <param name="name">The series name. Must not be null.</param>
        /// <param name="values">The values, one per bucket. Must not be null.</param>
        /// <param name="color">The series color.</param>
        /// <param name="glyph">The cell glyph, or null for a full block.</param>
        /// <returns>The series.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="values"/> is null.</exception>
        public ChartSeries AddSeries(string name, IEnumerable<double> values, Color color, string? glyph = null)
        {
            ChartSeries series = new ChartSeries(name, values, color);
            if (!string.IsNullOrEmpty(glyph))
                series.Glyph = glyph!;
            _Series.Add(series);
            return series;
        }

        /// <summary>
        /// Removes every series and label.
        /// </summary>
        public void Clear()
        {
            _Series.Clear();
            _Labels.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            int buckets = 0;
            for (int s = 0; s < _Series.Count; s++)
                buckets = Math.Max(buckets, _Series[s].Values.Count);
            if (width <= 0 || height <= 0 || buckets == 0)
                return;

            int legendRows = ShowLegend && height > 2 ? 1 : 0;
            int labelRows = _Labels.Count > 0 && height - legendRows > 2 ? 1 : 0;
            int plotHeight = height - legendRows - labelRows;
            if (plotHeight < 1)
                return;

            double max = 0;
            for (int b = 0; b < buckets; b++)
            {
                double total = 0;
                for (int s = 0; s < _Series.Count; s++)
                {
                    double value = b < _Series[s].Values.Count ? Math.Max(0, _Series[s].Values[b]) : 0;
                    total = Stacked ? total + value : Math.Max(total, value);
                }

                max = Math.Max(max, total);
            }

            string scale = ShowScale ? Format(max) : string.Empty;
            int gutter = scale.Length > 0 ? TextFit.Width(scale) + 1 : 0;
            if (gutter >= width)
                gutter = 0;
            if (gutter > 0)
                surface.DrawText(0, 0, scale, LabelStyle);

            int plotWidth = width - gutter;
            int slot = Math.Max(1, plotWidth / buckets);
            int columnWidth = Math.Max(1, slot - (slot > 2 ? 1 : 0));
            for (int b = 0; b < buckets; b++)
            {
                int x0 = gutter + b * slot;
                if (x0 >= width)
                    break;

                if (Stacked)
                {
                    double running = 0;
                    int drawn = 0;
                    for (int s = 0; s < _Series.Count; s++)
                    {
                        double value = b < _Series[s].Values.Count ? Math.Max(0, _Series[s].Values[b]) : 0;
                        running += value;
                        int top = max <= 0 ? 0 : (int)Math.Round(running / max * plotHeight);
                        CellStyle style = CellStyle.Default.WithForeground(_Series[s].Color);
                        for (; drawn < top && drawn < plotHeight; drawn++)
                            FillRow(surface, x0, plotHeight - 1 - drawn, Math.Min(columnWidth, width - x0), _Series[s].Glyph, style);
                    }
                }
                else
                {
                    int sub = Math.Max(1, columnWidth / Math.Max(1, _Series.Count));
                    for (int s = 0; s < _Series.Count; s++)
                    {
                        int x = x0 + s * sub;
                        if (x >= width)
                            break;

                        double value = b < _Series[s].Values.Count ? Math.Max(0, _Series[s].Values[b]) : 0;
                        int cells = max <= 0 ? 0 : (int)Math.Round(value / max * plotHeight);
                        CellStyle style = CellStyle.Default.WithForeground(_Series[s].Color);
                        for (int r = 0; r < cells && r < plotHeight; r++)
                            FillRow(surface, x, plotHeight - 1 - r, Math.Min(sub, width - x), _Series[s].Glyph, style);
                    }
                }
            }

            if (labelRows > 0)
                DrawLabels(surface, gutter, plotHeight, slot, buckets, width);

            if (legendRows > 0)
            {
                int x = 0;
                int row = height - 1;
                for (int s = 0; s < _Series.Count && x < width; s++)
                {
                    x += surface.DrawText(x, row, _Series[s].Glyph, CellStyle.Default.WithForeground(_Series[s].Color));
                    x += surface.DrawText(x, row, " " + TextFit.Truncate(_Series[s].Name, Math.Max(0, width - x - 1)) + "  ", LabelStyle);
                }
            }
        }

        private void DrawLabels(ISurface surface, int gutter, int row, int slot, int buckets, int width)
        {
            int nextFree = 0;
            for (int b = 0; b < buckets && b < _Labels.Count; b++)
            {
                int x = gutter + b * slot;
                if (x < nextFree || x >= width)
                    continue;

                string label = TextFit.Truncate(_Labels[b], width - x);
                surface.DrawText(x, row, label, LabelStyle);
                nextFree = x + TextFit.Width(label) + 1;
            }
        }

        private static void FillRow(ISurface surface, int x, int y, int count, string glyph, CellStyle style)
        {
            for (int i = 0; i < count; i++)
                surface.DrawText(x + i, y, glyph, style);
        }

        private string Format(double value)
        {
            return ValueFormatter != null ? ValueFormatter(value) : value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
