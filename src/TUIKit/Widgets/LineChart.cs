namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A line chart that plots a numeric series onto a <see cref="BrailleCanvas"/>, auto-scaling the
    /// data to fill the region and connecting consecutive samples. Because it draws with Braille dots,
    /// a small region still shows a smooth curve. Rebind the data and re-render to animate. Further
    /// series (<see cref="AddSeries"/>) are drawn on the same scale in their own colors, with an
    /// optional legend row.
    /// </summary>
    public sealed class LineChart : IWidget
    {
        private readonly List<double> _Values = new List<double>();
        private readonly List<ChartSeries> _Series = new List<ChartSeries>();

        /// <summary>
        /// Gets the additional series drawn after the primary values, sharing one scale. Never null.
        /// </summary>
        public IReadOnlyList<ChartSeries> Series
        {
            get { return _Series; }
        }

        /// <summary>
        /// Gets or sets a fixed scale minimum, or null to use the smallest value. Defaults to null.
        /// </summary>
        public double? Minimum { get; set; }

        /// <summary>
        /// Gets or sets a fixed scale maximum, or null to use the largest value. Defaults to null.
        /// </summary>
        public double? Maximum { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a legend row is drawn at the bottom listing each named
        /// series with its color. Defaults to false.
        /// </summary>
        public bool ShowLegend { get; set; }

        /// <summary>
        /// Gets or sets the name of the primary series, shown in the legend. Defaults to empty (omitted).
        /// </summary>
        public string PrimaryName { get; set; } = string.Empty;

        /// <summary>
        /// Adds a series drawn in its own color on the same scale as the primary values. Where lines cross
        /// in one cell the dots combine and the later series' color wins.
        /// </summary>
        /// <param name="name">The series name. Must not be null.</param>
        /// <param name="values">The values. Must not be null.</param>
        /// <param name="color">The line color.</param>
        /// <returns>The series, for later updates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ChartSeries AddSeries(string name, IEnumerable<double> values, Color color)
        {
            ChartSeries series = new ChartSeries(name, values, color);
            _Series.Add(series);
            return series;
        }

        /// <summary>
        /// Removes every additional series. The primary values are kept.
        /// </summary>
        public void ClearSeries()
        {
            _Series.Clear();
        }

        /// <summary>
        /// Gets or sets the line color. Defaults to cyan.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Initializes a new instance of the <see cref="LineChart"/> class.
        /// </summary>
        /// <param name="values">The initial samples. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        public LineChart(IEnumerable<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            SetValues(values);
        }

        /// <summary>
        /// Gets the number of samples.
        /// </summary>
        public int Count
        {
            get { return _Values.Count; }
        }

        /// <summary>
        /// Replaces the plotted samples.
        /// </summary>
        /// <param name="values">The new samples. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        public void SetValues(IEnumerable<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            _Values.Clear();
            foreach (double value in values)
                _Values.Add(value);
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
            if (width <= 0 || height <= 0)
                return;

            List<IReadOnlyList<double>> lines = new List<IReadOnlyList<double>>();
            List<Color> colors = new List<Color>();
            if (_Values.Count > 0)
            {
                lines.Add(_Values);
                colors.Add(Color);
            }

            for (int i = 0; i < _Series.Count; i++)
            {
                if (_Series[i].Values.Count > 0)
                {
                    lines.Add(_Series[i].Values);
                    colors.Add(_Series[i].Color);
                }
            }

            if (lines.Count == 0)
                return;

            int plotHeight = height;
            if (ShowLegend && height > 1)
            {
                plotHeight = height - 1;
                RenderLegend(surface, height - 1, width);
            }

            double min = double.MaxValue;
            double max = double.MinValue;
            for (int l = 0; l < lines.Count; l++)
            {
                for (int i = 0; i < lines[l].Count; i++)
                {
                    min = Math.Min(min, lines[l][i]);
                    max = Math.Max(max, lines[l][i]);
                }
            }

            if (Minimum.HasValue)
                min = Minimum.Value;
            if (Maximum.HasValue)
                max = Maximum.Value;

            if (lines.Count == 1)
            {
                BrailleCanvas single = Plot(lines[0], colors[0], width, plotHeight, min, max);
                single.Render(new SurfaceView(surface, new Rect(0, 0, width, plotHeight)));
                return;
            }

            // Plot each series on its own canvas and merge cell by cell: braille dots OR together and the
            // later series' color wins, so every line stays visible where they overlap.
            CellBuffer merged = new CellBuffer(width, plotHeight);
            for (int l = 0; l < lines.Count; l++)
            {
                CellBuffer layer = new CellBuffer(width, plotHeight);
                Plot(lines[l], colors[l], width, plotHeight, min, max).Render(new BufferSurface(layer));
                for (int y = 0; y < plotHeight; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Cell top = layer.Get(x, y);
                        int topBits = BrailleBits(top.Grapheme);
                        if (topBits <= 0)
                            continue;

                        int bottomBits = Math.Max(0, BrailleBits(merged.Get(x, y).Grapheme));
                        string glyph = char.ConvertFromUtf32(0x2800 + (topBits | bottomBits));
                        merged.Set(x, y, Cell.Glyph(glyph, top.Style, 1));
                    }
                }
            }

            for (int y = 0; y < plotHeight; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Cell cell = merged.Get(x, y);
                    if (BrailleBits(cell.Grapheme) > 0)
                        surface.Set(x, y, cell);
                }
            }
        }

        private void RenderLegend(ISurface surface, int row, int width)
        {
            int x = 0;
            if (PrimaryName.Length > 0 && _Values.Count > 0)
                x = DrawLegendEntry(surface, x, row, width, PrimaryName, Color);

            for (int i = 0; i < _Series.Count; i++)
                x = DrawLegendEntry(surface, x, row, width, _Series[i].Name, _Series[i].Color);
        }

        private static int DrawLegendEntry(ISurface surface, int x, int row, int width, string name, Color color)
        {
            if (x >= width)
                return x;

            x += surface.DrawText(x, row, "\u2500\u2500 ", CellStyle.Default.WithForeground(color));
            x += surface.DrawText(x, row, TUIKit.Unicode.TextFit.Truncate(name, Math.Max(0, width - x)) + "  ", CellStyle.Default);
            return x;
        }

        private static BrailleCanvas Plot(IReadOnlyList<double> values, Color color, int width, int height, double min, double max)
        {
            BrailleCanvas canvas = new BrailleCanvas(width, height);
            canvas.Color = color;
            double range = max - min;
            int pixelWidth = canvas.PixelWidth;
            int pixelHeight = canvas.PixelHeight;
            int previousX = 0;
            int previousY = 0;

            for (int i = 0; i < values.Count; i++)
            {
                int px = values.Count == 1 ? 0 : (int)Math.Round(i * (double)(pixelWidth - 1) / (values.Count - 1));
                double normalized = range <= 0 ? 0.5 : (values[i] - min) / range;
                normalized = Math.Max(0, Math.Min(1, normalized));
                int py = pixelHeight - 1 - (int)Math.Round(normalized * (pixelHeight - 1));

                if (i == 0)
                    canvas.Set(px, py);
                else
                    canvas.Line(previousX, previousY, px, py);

                previousX = px;
                previousY = py;
            }

            return canvas;
        }

        private static int BrailleBits(string grapheme)
        {
            if (string.IsNullOrEmpty(grapheme) || grapheme.Length != 1)
                return -1;

            int code = grapheme[0];
            return code >= 0x2800 && code <= 0x28FF ? code - 0x2800 : -1;
        }
    }
}
