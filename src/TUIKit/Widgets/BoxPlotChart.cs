namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;

    /// <summary>
    /// A horizontal box-and-whisker (distribution) chart. Each row renders one <see cref="BoxSummary"/>
    /// as a left-padded label followed by a plot region: whiskers drawn with <c>─</c> from the minimum to
    /// the maximum, a shaded box spanning the low-to-high edges, and a distinct mid marker (<c>┃</c>).
    /// All rows share one value range so the boxes are directly comparable. The five numbers are
    /// caller-supplied, so the host decides whether "box" means quartiles or, for example, a
    /// min / average / p95 / p99 / max latency quintuple.
    /// </summary>
    /// <remarks>
    /// Rendering is pure block/line glyphs on <see cref="ISurface"/>; there are no dependencies and no
    /// interactivity. An empty chart renders nothing and measures to a zero height. Non-finite
    /// (<see cref="double.NaN"/>, infinity) inputs are clamped on the draw path rather than rejected, so a
    /// live feed with a bad sample degrades instead of crashing. This type is not thread-safe; render on a
    /// single thread as the engine does.
    /// </remarks>
    public sealed class BoxPlotChart : IWidget
    {
        private readonly List<BoxSummary> _Summaries = new List<BoxSummary>();
        private bool _HasRange;
        private double _RangeMin;
        private double _RangeMax;

        /// <summary>
        /// Gets or sets the color of the whiskers and axis ticks. Defaults to palette gray.
        /// </summary>
        public Color WhiskerColor { get; set; } = Color.FromPalette(8);

        /// <summary>
        /// Gets or sets the color of the box. Defaults to palette blue.
        /// </summary>
        public Color BoxColor { get; set; } = Color.FromPalette(4);

        /// <summary>
        /// Gets or sets the color of the mid marker. Defaults to palette cyan.
        /// </summary>
        public Color MidColor { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Gets or sets whether a bottom axis row of shared min/max scale ticks is drawn. Defaults to true.
        /// When true, the axis consumes one row of height below the data rows.
        /// </summary>
        public bool ShowAxis { get; set; } = true;

        /// <summary>
        /// Gets or sets whether each row prints its mid value at the right edge in the row's units.
        /// Defaults to false, to keep narrow terminals clean.
        /// </summary>
        public bool ShowValues { get; set; }

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int Count
        {
            get { return _Summaries.Count; }
        }

        /// <summary>
        /// Adds a distribution row.
        /// </summary>
        /// <param name="label">The row label. Must not be null.</param>
        /// <param name="min">The minimum value (left whisker end).</param>
        /// <param name="low">The lower box edge.</param>
        /// <param name="mid">The mid marker value.</param>
        /// <param name="high">The upper box edge.</param>
        /// <param name="max">The maximum value (right whisker end).</param>
        /// <returns>This chart, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public BoxPlotChart Add(string label, double min, double low, double mid, double high, double max)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            _Summaries.Add(new BoxSummary(label, min, low, mid, high, max));
            return this;
        }

        /// <summary>
        /// Adds a pre-built distribution row.
        /// </summary>
        /// <param name="summary">The summary to add. Must not be null.</param>
        /// <returns>This chart, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="summary"/> is null.</exception>
        public BoxPlotChart Add(BoxSummary summary)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            _Summaries.Add(summary);
            return this;
        }

        /// <summary>
        /// Removes every row.
        /// </summary>
        public void Clear()
        {
            _Summaries.Clear();
        }

        /// <summary>
        /// Fixes the shared value axis to the supplied range instead of deriving it from the data, so two
        /// datasets with different natural ranges render on the same scale.
        /// </summary>
        /// <param name="min">The value mapped to the left of the plot region.</param>
        /// <param name="max">The value mapped to the right of the plot region.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="min"/> or <paramref name="max"/> is not finite, or when
        /// <paramref name="max"/> is not greater than <paramref name="min"/>.
        /// </exception>
        public void SetRange(double min, double max)
        {
            if (double.IsNaN(min) || double.IsInfinity(min))
                throw new ArgumentException("Range minimum must be finite.", nameof(min));
            if (double.IsNaN(max) || double.IsInfinity(max))
                throw new ArgumentException("Range maximum must be finite.", nameof(max));
            if (max <= min)
                throw new ArgumentException("Range maximum must be greater than the minimum.", nameof(max));

            _RangeMin = min;
            _RangeMax = max;
            _HasRange = true;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (_Summaries.Count == 0)
                return new Size(available.Width, 0);

            int desired = _Summaries.Count + (ShowAxis ? 1 : 0);
            return new Size(available.Width, Math.Min(available.Height, desired));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0 || _Summaries.Count == 0)
                return;

            int labelWidth = 0;
            for (int i = 0; i < _Summaries.Count; i++)
            {
                int length = _Summaries[i].Label.Length;
                if (length > labelWidth)
                    labelWidth = length;
            }

            labelWidth = Math.Min(labelWidth, Math.Max(1, width / 3));

            double rangeMin;
            double rangeMax;
            ResolveRange(out rangeMin, out rangeMax);

            string[] valueTexts = new string[_Summaries.Count];
            int valueColumn = 0;
            if (ShowValues)
            {
                for (int i = 0; i < _Summaries.Count; i++)
                {
                    valueTexts[i] = " " + Sanitize(_Summaries[i].Mid, rangeMin, rangeMax).ToString("0.##", CultureInfo.InvariantCulture);
                    if (valueTexts[i].Length > valueColumn)
                        valueColumn = valueTexts[i].Length;
                }
            }

            int plotStart = labelWidth + 1;
            int plotWidth = width - plotStart - valueColumn;

            CellStyle labelStyle = CellStyle.Default;
            CellStyle whiskerStyle = CellStyle.Default.WithForeground(WhiskerColor);
            CellStyle boxStyle = CellStyle.Default.WithForeground(BoxColor);
            CellStyle midStyle = CellStyle.Default.WithForeground(MidColor);

            int dataRows = Math.Min(_Summaries.Count, height);
            for (int row = 0; row < dataRows; row++)
            {
                BoxSummary summary = _Summaries[row];
                string label = Fit(summary.Label, labelWidth).PadRight(labelWidth);
                surface.DrawText(0, row, label, labelStyle);

                if (plotWidth >= 1)
                    DrawRow(surface, row, plotStart, plotWidth, rangeMin, rangeMax, summary, whiskerStyle, boxStyle, midStyle);

                if (ShowValues && valueColumn > 0)
                    surface.DrawText(width - valueTexts[row].Length, row, valueTexts[row], labelStyle);
            }

            if (ShowAxis && dataRows < height && plotWidth >= 1)
                DrawAxis(surface, dataRows, plotStart, plotWidth, rangeMin, rangeMax, whiskerStyle);
        }

        private void ResolveRange(out double rangeMin, out double rangeMax)
        {
            if (_HasRange)
            {
                rangeMin = _RangeMin;
                rangeMax = _RangeMax;
                return;
            }

            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < _Summaries.Count; i++)
            {
                double low = _Summaries[i].Min;
                double high = _Summaries[i].Max;
                if (!double.IsNaN(low) && !double.IsInfinity(low) && low < min)
                    min = low;
                if (!double.IsNaN(high) && !double.IsInfinity(high) && high > max)
                    max = high;
            }

            if (min == double.MaxValue || max == double.MinValue || max <= min)
            {
                // No finite spread (all equal, or all non-finite): fall back to a unit range so the
                // degenerate case draws a single centered marker rather than dividing by zero.
                double center = (min == double.MaxValue) ? 0.0 : min;
                rangeMin = center - 0.5;
                rangeMax = center + 0.5;
                return;
            }

            rangeMin = min;
            rangeMax = max;
        }

        private static void DrawRow(
            ISurface surface,
            int row,
            int plotStart,
            int plotWidth,
            double rangeMin,
            double rangeMax,
            BoxSummary summary,
            CellStyle whiskerStyle,
            CellStyle boxStyle,
            CellStyle midStyle)
        {
            int minCol = Column(summary.Min, rangeMin, rangeMax, plotWidth);
            int lowCol = Column(summary.Low, rangeMin, rangeMax, plotWidth);
            int midCol = Column(summary.Mid, rangeMin, rangeMax, plotWidth);
            int highCol = Column(summary.High, rangeMin, rangeMax, plotWidth);
            int maxCol = Column(summary.Max, rangeMin, rangeMax, plotWidth);

            // Degenerate distribution: every value maps to the same column. Draw a single marker only.
            if (minCol == maxCol)
            {
                surface.Set(plotStart + midCol, row, Cell.Glyph("┃", midStyle, 1));
                return;
            }

            for (int c = minCol; c <= maxCol; c++)
                surface.Set(plotStart + c, row, Cell.Glyph("─", whiskerStyle, 1));

            for (int c = lowCol; c <= highCol; c++)
                surface.Set(plotStart + c, row, Cell.Glyph("█", boxStyle, 1));

            surface.Set(plotStart + midCol, row, Cell.Glyph("┃", midStyle, 1));
        }

        private static void DrawAxis(
            ISurface surface,
            int row,
            int plotStart,
            int plotWidth,
            double rangeMin,
            double rangeMax,
            CellStyle style)
        {
            for (int c = 0; c < plotWidth; c++)
                surface.Set(plotStart + c, row, Cell.Glyph("─", style, 1));

            string minText = rangeMin.ToString("0.##", CultureInfo.InvariantCulture);
            string maxText = rangeMax.ToString("0.##", CultureInfo.InvariantCulture);
            surface.DrawText(plotStart, row, minText, style);
            if (maxText.Length < plotWidth)
                surface.DrawText(plotStart + plotWidth - maxText.Length, row, maxText, style);
        }

        private static int Column(double value, double rangeMin, double rangeMax, int plotWidth)
        {
            double span = rangeMax - rangeMin;
            if (span <= 0)
                return 0;

            double clamped = Sanitize(value, rangeMin, rangeMax);
            double normalized = (clamped - rangeMin) / span;
            int col = (int)Math.Round(normalized * (plotWidth - 1), MidpointRounding.AwayFromZero);
            if (col < 0)
                col = 0;
            if (col > plotWidth - 1)
                col = plotWidth - 1;

            return col;
        }

        private static double Sanitize(double value, double lo, double hi)
        {
            if (double.IsNaN(value))
                return lo;
            if (double.IsPositiveInfinity(value))
                return hi;
            if (double.IsNegativeInfinity(value))
                return lo;
            if (value < lo)
                return lo;
            if (value > hi)
                return hi;

            return value;
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
