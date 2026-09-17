namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A grid of intensity cells — rows by columns of values shaded by magnitude — for two-dimensional
    /// density such as activity by hour-of-day across a week. Each cell is drawn with a shaded glyph
    /// (<c>░▒▓█</c>) chosen from the cell's normalized magnitude, tinted with <see cref="CellColor"/>.
    /// Optional row and column labels offset the grid.
    /// </summary>
    /// <remarks>
    /// Glyph shading (rather than a two-color gradient) was chosen because it reads clearly on every
    /// terminal color depth and is deterministic to test against the headless buffer. An empty or
    /// zero-size grid renders nothing. This type is not thread-safe.
    /// </remarks>
    public sealed class HeatMap : IWidget
    {
        private static readonly string[] _Shades = { "░", "▒", "▓", "█" };

        private double[,] _Cells = new double[0, 0];
        private readonly List<string> _RowLabels = new List<string>();
        private readonly List<string> _ColumnLabels = new List<string>();
        private bool _HasRange;
        private double _RangeMin;
        private double _RangeMax;

        /// <summary>
        /// Gets or sets the color the shade glyphs are drawn in. Defaults to palette green.
        /// </summary>
        public Color CellColor { get; set; } = Color.FromPalette(2);

        /// <summary>
        /// Gets or sets the color of row and column labels. Defaults to palette gray.
        /// </summary>
        public Color LabelColor { get; set; } = Color.FromPalette(8);

        /// <summary>
        /// Gets the number of rows in the current grid.
        /// </summary>
        public int Rows
        {
            get { return _Cells.GetLength(0); }
        }

        /// <summary>
        /// Gets the number of columns in the current grid.
        /// </summary>
        public int Columns
        {
            get { return _Cells.GetLength(1); }
        }

        /// <summary>
        /// Replaces the grid values. A copy is taken, so later mutations of the caller's array do not
        /// affect the widget.
        /// </summary>
        /// <param name="values">The row-major grid of magnitudes. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        public void SetCells(double[,] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            int rows = values.GetLength(0);
            int columns = values.GetLength(1);
            double[,] copy = new double[rows, columns];
            Array.Copy(values, copy, values.Length);
            _Cells = copy;
        }

        /// <summary>
        /// Sets the row labels, drawn in a left gutter. Pass an empty sequence to clear them.
        /// </summary>
        /// <param name="labels">The row labels. Must not be null; individual entries may be null and render blank.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is null.</exception>
        public void SetRowLabels(IEnumerable<string> labels)
        {
            if (labels == null)
                throw new ArgumentNullException(nameof(labels));

            _RowLabels.Clear();
            foreach (string label in labels)
                _RowLabels.Add(label ?? string.Empty);
        }

        /// <summary>
        /// Sets the column labels, drawn in a header row (first character per column). Pass an empty
        /// sequence to clear them.
        /// </summary>
        /// <param name="labels">The column labels. Must not be null; individual entries may be null and render blank.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is null.</exception>
        public void SetColumnLabels(IEnumerable<string> labels)
        {
            if (labels == null)
                throw new ArgumentNullException(nameof(labels));

            _ColumnLabels.Clear();
            foreach (string label in labels)
                _ColumnLabels.Add(label ?? string.Empty);
        }

        /// <summary>
        /// Fixes the shading scale to the supplied range instead of deriving it from the data, so two
        /// grids with different natural ranges shade on the same scale.
        /// </summary>
        /// <param name="min">The value mapped to the lightest shade.</param>
        /// <param name="max">The value mapped to the densest shade.</param>
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
            int rows = _Cells.GetLength(0);
            int columns = _Cells.GetLength(1);
            if (rows == 0 || columns == 0)
                return new Size(0, 0);

            int gutter = RowLabelGutter();
            int header = _ColumnLabels.Count > 0 ? 1 : 0;
            int desiredWidth = Math.Min(available.Width, gutter + columns);
            int desiredHeight = Math.Min(available.Height, header + rows);
            return new Size(desiredWidth, desiredHeight);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            int rows = _Cells.GetLength(0);
            int columns = _Cells.GetLength(1);
            if (width <= 0 || height <= 0 || rows == 0 || columns == 0)
                return;

            int gutter = RowLabelGutter();
            int header = _ColumnLabels.Count > 0 ? 1 : 0;

            double min;
            double max;
            ResolveRange(out min, out max);
            double span = max - min;

            CellStyle labelStyle = CellStyle.Default.WithForeground(LabelColor);
            CellStyle cellStyle = CellStyle.Default.WithForeground(CellColor);

            if (header == 1)
            {
                for (int c = 0; c < columns; c++)
                {
                    int x = gutter + c;
                    if (x >= width)
                        break;
                    if (c < _ColumnLabels.Count && _ColumnLabels[c].Length > 0)
                        surface.DrawText(x, 0, _ColumnLabels[c].Substring(0, 1), labelStyle);
                }
            }

            for (int r = 0; r < rows; r++)
            {
                int y = header + r;
                if (y >= height)
                    break;

                if (gutter > 0 && r < _RowLabels.Count)
                {
                    string label = Fit(_RowLabels[r], gutter);
                    surface.DrawText(0, y, label, labelStyle);
                }

                for (int c = 0; c < columns; c++)
                {
                    int x = gutter + c;
                    if (x >= width)
                        break;

                    double value = Sanitize(_Cells[r, c], min, max);
                    double normalized = span <= 0 ? 0.0 : (value - min) / span;
                    int index = (int)Math.Round(normalized * (_Shades.Length - 1), MidpointRounding.AwayFromZero);
                    if (index < 0)
                        index = 0;
                    if (index > _Shades.Length - 1)
                        index = _Shades.Length - 1;

                    surface.Set(x, y, Cell.Glyph(_Shades[index], cellStyle, 1));
                }
            }
        }

        private int RowLabelGutter()
        {
            int gutter = 0;
            for (int i = 0; i < _RowLabels.Count; i++)
            {
                if (_RowLabels[i].Length > gutter)
                    gutter = _RowLabels[i].Length;
            }

            if (gutter > 0)
                gutter += 1;

            return gutter;
        }

        private void ResolveRange(out double min, out double max)
        {
            if (_HasRange)
            {
                min = _RangeMin;
                max = _RangeMax;
                return;
            }

            min = double.MaxValue;
            max = double.MinValue;
            int rows = _Cells.GetLength(0);
            int columns = _Cells.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    double value = _Cells[r, c];
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        continue;
                    if (value < min)
                        min = value;
                    if (value > max)
                        max = value;
                }
            }

            if (min == double.MaxValue || max == double.MinValue)
            {
                min = 0.0;
                max = 0.0;
            }
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
            if (width <= 0)
                return string.Empty;
            if (text.Length <= width)
                return text.PadRight(width);
            if (width <= 1)
                return text.Substring(0, width);

            return text.Substring(0, width - 1) + "…";
        }
    }
}
