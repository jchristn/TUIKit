namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;

    /// <summary>
    /// A box-and-whisker (distribution) chart. Each <see cref="BoxSummary"/> is drawn as whiskers spanning
    /// the minimum to the maximum, a shaded box spanning the low-to-high edges, and a distinct mid marker.
    /// All summaries share one value range so the boxes are directly comparable. The five numbers are
    /// caller-supplied, so the host decides whether "box" means quartiles or, for example, a
    /// min / average / p95 / p99 / max latency quintuple.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The chart renders in either <see cref="BoxPlotOrientation.Horizontal"/> (the default) or
    /// <see cref="BoxPlotOrientation.Vertical"/>. Horizontal draws one row per summary — a left-padded
    /// label, then a left-to-right plot region with whiskers (<c>─</c>), box (<c>█</c>), and mid marker
    /// (<c>┃</c>) — with the optional scale axis as a bottom row. Vertical draws one column per summary —
    /// a bottom-to-top plot region with whiskers (<c>│</c>), box (<c>█</c>), and mid marker (<c>━</c>),
    /// a category label under each column — with the optional scale axis as a left-hand gutter.
    /// </para>
    /// <para>
    /// Rendering is pure block/line glyphs on <see cref="ISurface"/>; there are no dependencies and no
    /// interactivity. An empty chart renders nothing and measures to a zero height. Non-finite
    /// (<see cref="double.NaN"/>, infinity) inputs are clamped on the draw path rather than rejected, so a
    /// live feed with a bad sample degrades instead of crashing. This type is not thread-safe; render on a
    /// single thread as the engine does.
    /// </para>
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
        /// Gets or sets whether each summary prints its mid value in the summary's units — at the right
        /// edge of the row when <see cref="BoxPlotOrientation.Horizontal"/>, or above the column when
        /// <see cref="BoxPlotOrientation.Vertical"/>. Defaults to false, to keep narrow terminals clean.
        /// </summary>
        public bool ShowValues { get; set; }

        /// <summary>
        /// Gets or sets the axis the distributions are laid out along. Defaults to
        /// <see cref="BoxPlotOrientation.Horizontal"/> (one row per summary). Set to
        /// <see cref="BoxPlotOrientation.Vertical"/> to draw one column per summary.
        /// </summary>
        public BoxPlotOrientation Orientation { get; set; } = BoxPlotOrientation.Horizontal;

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

            if (Orientation == BoxPlotOrientation.Vertical)
            {
                // A vertical chart is a filling 2-D plot: it uses the full height it is given.
                return new Size(available.Width, available.Height);
            }

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

            double rangeMin;
            double rangeMax;
            ResolveRange(out rangeMin, out rangeMax);

            CellStyle labelStyle = CellStyle.Default;
            CellStyle whiskerStyle = CellStyle.Default.WithForeground(WhiskerColor);
            CellStyle boxStyle = CellStyle.Default.WithForeground(BoxColor);
            CellStyle midStyle = CellStyle.Default.WithForeground(MidColor);

            if (Orientation == BoxPlotOrientation.Vertical)
            {
                RenderVertical(surface, width, height, rangeMin, rangeMax, labelStyle, whiskerStyle, boxStyle, midStyle);
                return;
            }

            RenderHorizontal(surface, width, height, rangeMin, rangeMax, labelStyle, whiskerStyle, boxStyle, midStyle);
        }

        private void RenderHorizontal(
            ISurface surface,
            int width,
            int height,
            double rangeMin,
            double rangeMax,
            CellStyle labelStyle,
            CellStyle whiskerStyle,
            CellStyle boxStyle,
            CellStyle midStyle)
        {
            int labelWidth = 0;
            for (int i = 0; i < _Summaries.Count; i++)
            {
                int length = _Summaries[i].Label.Length;
                if (length > labelWidth)
                    labelWidth = length;
            }

            labelWidth = Math.Min(labelWidth, Math.Max(1, width / 3));

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

        private void RenderVertical(
            ISurface surface,
            int width,
            int height,
            double rangeMin,
            double rangeMax,
            CellStyle labelStyle,
            CellStyle whiskerStyle,
            CellStyle boxStyle,
            CellStyle midStyle)
        {
            // Bottom row holds category labels; an optional top row holds the mid-value readout.
            int labelRow = height - 1;
            int valueRows = ShowValues ? 1 : 0;
            int plotTop = valueRows;
            int plotBottom = labelRow - 1;
            if (plotBottom < plotTop)
                return; // Not enough vertical room for a plot plus its label row.

            string minText = rangeMin.ToString("0.##", CultureInfo.InvariantCulture);
            string maxText = rangeMax.ToString("0.##", CultureInfo.InvariantCulture);

            // Left gutter: the scale axis line plus right-aligned min/max tick labels.
            int gutter = 0;
            int axisCol = -1;
            if (ShowAxis)
            {
                gutter = Math.Min(Math.Max(minText.Length, maxText.Length) + 1, Math.Max(1, width / 3));
                axisCol = gutter - 1;
            }

            int plotStart = gutter;
            int plotWidth = width - plotStart;
            if (plotWidth < 1)
                return;

            if (ShowAxis)
            {
                for (int r = plotTop; r <= plotBottom; r++)
                    surface.Set(axisCol, r, Cell.Glyph("│", whiskerStyle, 1));

                // Value increases upward, so the maximum tick sits at the top of the plot.
                surface.DrawText(Math.Max(0, axisCol - maxText.Length), plotTop, Fit(maxText, gutter), whiskerStyle);
                surface.DrawText(Math.Max(0, axisCol - minText.Length), plotBottom, Fit(minText, gutter), whiskerStyle);
            }

            int count = _Summaries.Count;
            int stride = plotWidth / count;
            int drawn = count;
            if (stride < 1)
            {
                // More categories than columns: draw one column each until the plot region is full.
                stride = 1;
                drawn = plotWidth;
            }

            for (int i = 0; i < drawn; i++)
            {
                BoxSummary summary = _Summaries[i];
                int columnLeft = plotStart + (i * stride);
                int center = columnLeft + (stride / 2);
                if (center >= width)
                    center = width - 1;

                DrawColumn(surface, center, plotTop, plotBottom, rangeMin, rangeMax, summary, whiskerStyle, boxStyle, midStyle);

                string label = Fit(summary.Label, stride);
                int labelX = center - (label.Length / 2);
                if (labelX < plotStart)
                    labelX = plotStart;
                if (labelX + label.Length > width)
                    labelX = Math.Max(plotStart, width - label.Length);
                surface.DrawText(labelX, labelRow, label, labelStyle);

                if (ShowValues)
                {
                    string valueText = Sanitize(summary.Mid, rangeMin, rangeMax).ToString("0.##", CultureInfo.InvariantCulture);
                    valueText = Fit(valueText, stride);
                    int valueX = center - (valueText.Length / 2);
                    if (valueX < plotStart)
                        valueX = plotStart;
                    if (valueX + valueText.Length > width)
                        valueX = Math.Max(plotStart, width - valueText.Length);
                    surface.DrawText(valueX, 0, valueText, labelStyle);
                }
            }
        }

        private static void DrawColumn(
            ISurface surface,
            int center,
            int plotTop,
            int plotBottom,
            double rangeMin,
            double rangeMax,
            BoxSummary summary,
            CellStyle whiskerStyle,
            CellStyle boxStyle,
            CellStyle midStyle)
        {
            int minRow = Row(summary.Min, rangeMin, rangeMax, plotTop, plotBottom);
            int lowRow = Row(summary.Low, rangeMin, rangeMax, plotTop, plotBottom);
            int midRow = Row(summary.Mid, rangeMin, rangeMax, plotTop, plotBottom);
            int highRow = Row(summary.High, rangeMin, rangeMax, plotTop, plotBottom);
            int maxRow = Row(summary.Max, rangeMin, rangeMax, plotTop, plotBottom);

            // Degenerate distribution: every value maps to the same row. Draw a single marker only.
            if (minRow == maxRow)
            {
                surface.Set(center, midRow, Cell.Glyph("━", midStyle, 1));
                return;
            }

            // Rows increase downward, so the maximum is the topmost (smallest) row index.
            for (int r = maxRow; r <= minRow; r++)
                surface.Set(center, r, Cell.Glyph("│", whiskerStyle, 1));

            for (int r = highRow; r <= lowRow; r++)
                surface.Set(center, r, Cell.Glyph("█", boxStyle, 1));

            surface.Set(center, midRow, Cell.Glyph("━", midStyle, 1));
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

        private static int Row(double value, double rangeMin, double rangeMax, int plotTop, int plotBottom)
        {
            int plotHeight = plotBottom - plotTop + 1;
            double span = rangeMax - rangeMin;
            if (span <= 0 || plotHeight <= 1)
                return plotBottom;

            double clamped = Sanitize(value, rangeMin, rangeMax);
            double normalized = (clamped - rangeMin) / span;
            int offset = (int)Math.Round(normalized * (plotHeight - 1), MidpointRounding.AwayFromZero);
            if (offset < 0)
                offset = 0;
            if (offset > plotHeight - 1)
                offset = plotHeight - 1;

            // Value increases upward: a larger value maps to a smaller (higher) row index.
            return plotBottom - offset;
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
