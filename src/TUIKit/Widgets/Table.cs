namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A simple columnar table with a bold header row. Column widths are distributed evenly across the
    /// available width.
    /// </summary>
    public sealed class Table : IWidget
    {
        private readonly string[] _Headers;
        private readonly List<string[]> _Rows = new List<string[]>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class.
        /// </summary>
        /// <param name="headers">The column headers. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="headers"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="headers"/> is empty.</exception>
        public Table(string[] headers)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (headers.Length == 0)
                throw new ArgumentException("At least one column is required.", nameof(headers));

            _Headers = headers;
        }

        /// <summary>
        /// Gets the number of data rows.
        /// </summary>
        public int RowCount
        {
            get { return _Rows.Count; }
        }

        /// <summary>
        /// Adds a data row.
        /// </summary>
        /// <param name="cells">The cell values, one per column. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        public void AddRow(string[] cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            _Rows.Add(cells);
        }

        /// <summary>
        /// Removes all data rows.
        /// </summary>
        public void ClearRows()
        {
            _Rows.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Rows.Count + 1));
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

            int columnWidth = Math.Max(1, width / _Headers.Length);
            CellStyle header = CellStyle.Default.WithAttribute(CellAttributes.Bold, true).WithForeground(Color.FromPalette(6));

            for (int c = 0; c < _Headers.Length; c++)
                surface.DrawText(c * columnWidth, 0, Fit(_Headers[c], columnWidth), header);

            for (int r = 0; r < _Rows.Count && r + 1 < height; r++)
            {
                string[] row = _Rows[r];
                for (int c = 0; c < _Headers.Length; c++)
                {
                    string value = c < row.Length ? row[c] : string.Empty;
                    surface.DrawText(c * columnWidth, r + 1, Fit(value, columnWidth), CellStyle.Default);
                }
            }
        }

        private static string Fit(string text, int width)
        {
            if (text == null)
                return string.Empty;
            if (text.Length <= width - 1)
                return text;
            if (width <= 1)
                return text.Substring(0, Math.Max(0, width));

            return text.Substring(0, width - 1);
        }
    }
}
