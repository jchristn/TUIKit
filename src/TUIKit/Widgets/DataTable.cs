namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A columnar table over a typed data source with row selection, column sorting, and virtualized
    /// rendering (only the visible rows are drawn, so large sources are cheap). Columns are defined
    /// with value selectors; bind any list of rows. Up/Down move the selection.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    public sealed class DataTable<T> : IWidget, IFocusable
    {
        private readonly List<DataColumn<T>> _Columns = new List<DataColumn<T>>();
        private readonly List<T> _Rows = new List<T>();
        private int _Selected;
        private int _Top;
        private int _SortColumn = -1;
        private bool _SortAscending = true;

        /// <summary>
        /// Gets or sets the highlight style for the selected row. Defaults to reversed cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int RowCount
        {
            get { return _Rows.Count; }
        }

        /// <summary>
        /// Gets the zero-based selected row index, or -1 when the table is empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Rows.Count == 0 ? -1 : _Selected; }
        }

        /// <summary>
        /// Gets the selected row, or the default of <typeparamref name="T"/> when the table is empty.
        /// </summary>
        public T? SelectedRow
        {
            get { return _Rows.Count == 0 ? default : _Rows[_Selected]; }
        }

        /// <summary>
        /// Adds a column.
        /// </summary>
        /// <param name="name">The header. Must not be null.</param>
        /// <param name="value">A selector returning the cell text for a row. Must not be null.</param>
        /// <param name="sortable">Whether the column can be sorted.</param>
        /// <returns>This table, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public DataTable<T> Column(string name, Func<T, string> value, bool sortable = false)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _Columns.Add(new DataColumn<T>(name, value, sortable));
            return this;
        }

        /// <summary>
        /// Replaces the table rows.
        /// </summary>
        /// <param name="rows">The rows. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
        public void Bind(IReadOnlyList<T> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            _Rows.Clear();
            for (int i = 0; i < rows.Count; i++)
                _Rows.Add(rows[i]);

            if (_Selected >= _Rows.Count)
                _Selected = Math.Max(0, _Rows.Count - 1);
        }

        /// <summary>
        /// Sorts the rows by a sortable column, toggling ascending/descending on repeated calls.
        /// </summary>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the column index is out of range.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the column is not sortable.</exception>
        public void SortByColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _Columns.Count)
                throw new ArgumentOutOfRangeException(nameof(columnIndex));
            if (!_Columns[columnIndex].Sortable)
                throw new InvalidOperationException("Column '" + _Columns[columnIndex].Name + "' is not sortable.");

            _SortAscending = _SortColumn == columnIndex ? !_SortAscending : true;
            _SortColumn = columnIndex;
            Func<T, string> selector = _Columns[columnIndex].Value;
            _Rows.Sort((x, y) => _SortAscending
                ? string.Compare(selector(x), selector(y), StringComparison.Ordinal)
                : string.Compare(selector(y), selector(x), StringComparison.Ordinal));
        }

        /// <summary>
        /// Moves the selection with Up/Down.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Up)
            {
                _Selected = Math.Max(0, _Selected - 1);
                return true;
            }

            if (key.Code == KeyCode.Down)
            {
                _Selected = Math.Min(Math.Max(0, _Rows.Count - 1), _Selected + 1);
                return true;
            }

            return false;
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
            if (width <= 0 || height <= 0 || _Columns.Count == 0)
                return;

            int columnWidth = Math.Max(1, width / _Columns.Count);
            CellStyle header = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);
            for (int c = 0; c < _Columns.Count; c++)
                surface.DrawText(c * columnWidth, 0, Fit(_Columns[c].Name, columnWidth), header);

            int listHeight = height - 1;
            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + listHeight)
                _Top = _Selected - listHeight + 1;

            for (int row = 0; row < listHeight && _Top + row < _Rows.Count; row++)
            {
                int index = _Top + row;
                bool selected = index == _Selected;
                int y = row + 1;
                CellStyle style = selected ? HighlightStyle : CellStyle.Default;
                if (selected)
                    surface.Fill(new Rect(0, y, width, 1), Cell.Blank(style));

                for (int c = 0; c < _Columns.Count; c++)
                    surface.DrawText(c * columnWidth, y, Fit(_Columns[c].Value(_Rows[index]), columnWidth), style);
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
