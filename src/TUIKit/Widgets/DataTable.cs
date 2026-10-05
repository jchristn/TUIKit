namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A columnar table over a typed data source with row selection, column sorting, and virtualized
    /// rendering (only the visible rows are drawn, so large sources are cheap). Columns are defined
    /// with value selectors; bind any list of rows. Up/Down move the selection.
    /// </summary>
    /// <remarks>
    /// Columns can have fixed or weighted widths with minimums and alignment (<see cref="DataColumn{T}"/>);
    /// widths are measured in terminal columns so CJK and emoji text stay aligned. Sorting is typed
    /// (<see cref="DataColumn{T}.Comparer"/>, <see cref="DataColumn{T}.SortKey"/>), stable, shown with an
    /// indicator in the header, and toggled by clicking a sortable header; with <see cref="SortLocally"/>
    /// off the table only records the sort and raises <see cref="SortChanged"/> so a server can sort.
    /// <see cref="MultiSelect"/> adds a mark column: Space toggles the current row, Shift+Up/Down extend,
    /// Ctrl+A marks all, Escape clears. <see cref="KeySelector"/> keeps the selection and marks on the
    /// same rows across <see cref="Bind"/>. <see cref="EndReached"/> is a paging hook. Not thread-safe:
    /// use it from the UI loop.
    /// </remarks>
    /// <typeparam name="T">The row type.</typeparam>
    public sealed class DataTable<T> : IWidget, IFocusable, IMouseAware, IFocusAware, IEnableable, IChangeNotifier, IThemeable, IKeyHintSource
    {
        private readonly List<DataColumn<T>> _Columns = new List<DataColumn<T>>();
        private readonly List<T> _Rows = new List<T>();
        private readonly List<bool> _Marks = new List<bool>();
        private readonly List<int> _ColumnX = new List<int>();
        private readonly List<int> _ColumnWidths = new List<int>();
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private int _SortColumn = -1;
        private bool _SortAscending = true;
        private bool _Enabled = true;
        private bool _Focused = true;
        private int _MarkWidth;

        /// <summary>
        /// Raised after the selected index changes, from input, <see cref="Select"/>, or a rebind.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? SelectionChanged;

        /// <summary>
        /// Raised after the selection or the marks change; the untyped companion of the typed events.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Raised after the set of marked rows changes.
        /// </summary>
        public event EventHandler? MarkedChanged;

        /// <summary>
        /// Raised after the sort column or direction changes.
        /// </summary>
        public event EventHandler<DataTableSortChangedEventArgs>? SortChanged;

        /// <summary>
        /// Raised with the selected index when a row is activated by Enter or a double click. While no
        /// handler is attached Enter is not consumed.
        /// </summary>
        public event Action<int>? RowActivated;

        /// <summary>
        /// Raised when the selection moves to within <see cref="EndReachedThreshold"/> rows of the last
        /// row by keyboard or wheel navigation, so the application can load the next page and rebind.
        /// </summary>
        public event Action? EndReached;

        /// <summary>
        /// Gets or sets the highlight style for the selected row. Defaults to reversed cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the highlight style for the selected row while the table is not focused. Defaults
        /// to the same reversed cyan as <see cref="HighlightStyle"/>, preserving the original look.
        /// </summary>
        public CellStyle InactiveHighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of the header row. Defaults to bold with a cyan (palette 6)
        /// foreground.
        /// </summary>
        public CellStyle HeaderStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the base style applied to unselected data rows and the surface fill. Defaults
        /// to <see cref="CellStyle.Default"/>; assign a style with a background to give the table a
        /// solid background.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style composed over row styles while the table is disabled. Defaults to dim.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets a per-row style selector, or null. A returned style is composed over
        /// <see cref="NormalStyle"/> for unselected rows.
        /// </summary>
        public Func<T, CellStyle?>? RowStyle { get; set; }

        /// <summary>
        /// Gets or sets a stable key per row. When set, <see cref="Bind"/> keeps the selection and the
        /// marks on the rows with the same keys. Null (the default) keeps the selection index.
        /// </summary>
        public Func<T, string>? KeySelector { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a sort reorders the bound rows. Defaults to true. When
        /// false (server-side sorting), the table only tracks the sort column and direction for the header
        /// indicator and raises <see cref="SortChanged"/>.
        /// </summary>
        public bool SortLocally { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the header shows a sort direction indicator. Defaults to true.
        /// </summary>
        public bool ShowSortIndicator { get; set; } = true;

        /// <summary>
        /// Gets or sets the ascending indicator. Defaults to an up triangle (U+25B2).
        /// </summary>
        public string SortAscendingIndicator { get; set; } = "\u25B2";

        /// <summary>
        /// Gets or sets the descending indicator. Defaults to a down triangle (U+25BC).
        /// </summary>
        public string SortDescendingIndicator { get; set; } = "\u25BC";

        /// <summary>
        /// Gets or sets a value indicating whether a left click on a sortable header sorts by it (a second
        /// click reverses). Defaults to true.
        /// </summary>
        public bool SortOnHeaderClick { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the selection stays on the same row when a local sort
        /// reorders the rows. Defaults to false, the original behavior: the selection keeps its index.
        /// </summary>
        public bool SelectionFollowsSort { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether rows can be marked (multi-select). Defaults to false.
        /// </summary>
        public bool MultiSelect { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether columns without a fixed width absorb the columns left
        /// over by integer division (the last weighted column grows). Defaults to false, the original layout.
        /// </summary>
        public bool StretchLastColumn { get; set; }

        /// <summary>
        /// Gets or sets how many rows from the end <see cref="EndReached"/> fires. Defaults to 0 (on the
        /// last row). Must be zero or greater.
        /// </summary>
        public int EndReachedThreshold { get; set; }

        /// <summary>
        /// Gets or sets text shown under the header when there are no rows. Defaults to empty (nothing).
        /// </summary>
        public string EmptyText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the table accepts input. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the table is focused (chooses between
        /// <see cref="HighlightStyle"/> and <see cref="InactiveHighlightStyle"/>). Defaults to true.
        /// </summary>
        public bool IsFocused
        {
            get { return _Focused; }
            set { _Focused = value; }
        }

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int RowCount
        {
            get { return _Rows.Count; }
        }

        /// <summary>
        /// Gets the bound rows in display order. Never null.
        /// </summary>
        public IReadOnlyList<T> Rows
        {
            get { return _Rows; }
        }

        /// <summary>
        /// Gets the columns. Never null.
        /// </summary>
        public IReadOnlyList<DataColumn<T>> Columns
        {
            get { return _Columns; }
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
        /// Gets the sorted column index, or -1 when unsorted.
        /// </summary>
        public int SortColumn
        {
            get { return _SortColumn; }
        }

        /// <summary>
        /// Gets a value indicating whether the current sort is ascending.
        /// </summary>
        public bool SortAscending
        {
            get { return _SortAscending; }
        }

        /// <summary>
        /// Gets the index of the first visible row as of the last render.
        /// </summary>
        public int TopIndex
        {
            get { return _Top; }
        }

        /// <summary>
        /// Gets the indexes of the marked rows in display order. Never null.
        /// </summary>
        public IReadOnlyList<int> MarkedIndices
        {
            get
            {
                List<int> result = new List<int>();
                for (int i = 0; i < _Marks.Count; i++)
                {
                    if (_Marks[i])
                        result.Add(i);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the marked rows in display order. Never null.
        /// </summary>
        public IReadOnlyList<T> MarkedRows
        {
            get
            {
                List<T> result = new List<T>();
                for (int i = 0; i < _Marks.Count; i++)
                {
                    if (_Marks[i])
                        result.Add(_Rows[i]);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the number of marked rows.
        /// </summary>
        public int MarkedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _Marks.Count; i++)
                {
                    if (_Marks[i])
                        count++;
                }

                return count;
            }
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
        /// Adds a fully configured column.
        /// </summary>
        /// <param name="column">The column. Must not be null.</param>
        /// <returns>This table, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="column"/> is null.</exception>
        public DataTable<T> Column(DataColumn<T> column)
        {
            _Columns.Add(column ?? throw new ArgumentNullException(nameof(column)));
            return this;
        }

        /// <summary>
        /// Replaces the table rows. An active local sort is re-applied. With <see cref="KeySelector"/>
        /// set, the selection and marks follow their rows; otherwise the selection index is kept (clamped)
        /// and marks are cleared.
        /// </summary>
        /// <param name="rows">The rows. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
        public void Bind(IReadOnlyList<T> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            int before = SelectedIndex;
            Func<T, string>? key = KeySelector;
            string? selectedKey = key != null && before >= 0 ? key(_Rows[before]) : null;
            HashSet<string>? markedKeys = null;
            bool hadMarks = MarkedCount > 0;
            if (key != null && hadMarks)
            {
                markedKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < _Rows.Count; i++)
                {
                    if (_Marks[i])
                        markedKeys.Add(key(_Rows[i]));
                }
            }

            _Rows.Clear();
            _Marks.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                _Rows.Add(rows[i]);
                _Marks.Add(markedKeys != null && markedKeys.Contains(key!(rows[i])));
            }

            if (_SortColumn >= 0 && SortLocally && _SortColumn < _Columns.Count)
                ApplySort();

            if (selectedKey != null)
            {
                int found = _Rows.FindIndex(r => string.Equals(key!(r), selectedKey, StringComparison.Ordinal));
                if (found >= 0)
                    _Selected = found;
            }

            if (_Selected >= _Rows.Count)
                _Selected = Math.Max(0, _Rows.Count - 1);

            RaiseSelection(before);
            if (hadMarks || MarkedCount > 0)
                RaiseMarks();
        }

        /// <summary>
        /// Selects a row, clamped to the valid range. A no-op when the table is empty.
        /// </summary>
        /// <param name="index">The zero-based row index.</param>
        public void Select(int index)
        {
            if (_Rows.Count == 0)
                return;

            SetSelected(Math.Max(0, Math.Min(_Rows.Count - 1, index)));
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

            SortByColumn(columnIndex, _SortColumn == columnIndex ? !_SortAscending : true);
        }

        /// <summary>
        /// Sorts the rows by a sortable column in an explicit direction. The sort is stable; the selection
        /// follows its row when <see cref="SelectionFollowsSort"/> is set.
        /// </summary>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <param name="ascending"><c>true</c> for ascending; <c>false</c> for descending.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the column index is out of range.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the column is not sortable.</exception>
        public void SortByColumn(int columnIndex, bool ascending)
        {
            if (columnIndex < 0 || columnIndex >= _Columns.Count)
                throw new ArgumentOutOfRangeException(nameof(columnIndex));
            if (!_Columns[columnIndex].Sortable)
                throw new InvalidOperationException("Column '" + _Columns[columnIndex].Name + "' is not sortable.");

            _SortColumn = columnIndex;
            _SortAscending = ascending;
            if (SortLocally)
            {
                int before = SelectedIndex;
                ApplySort();
                RaiseSelection(before);
            }

            SortChanged?.Invoke(this, new DataTableSortChangedEventArgs(columnIndex, _Columns[columnIndex].Name, ascending));
        }

        /// <summary>
        /// Clears the sort indicator. Rows keep their current order.
        /// </summary>
        public void ClearSort()
        {
            if (_SortColumn < 0)
                return;

            _SortColumn = -1;
            _SortAscending = true;
            SortChanged?.Invoke(this, new DataTableSortChangedEventArgs(-1, null, true));
        }

        /// <summary>
        /// Gets a value indicating whether a row is marked.
        /// </summary>
        /// <param name="index">The zero-based row index.</param>
        /// <returns><c>true</c> when marked; <c>false</c> otherwise or when out of range.</returns>
        public bool IsMarked(int index)
        {
            return index >= 0 && index < _Marks.Count && _Marks[index];
        }

        /// <summary>
        /// Marks or unmarks a row.
        /// </summary>
        /// <param name="index">The zero-based row index.</param>
        /// <param name="marked">The new state.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public void SetMarked(int index, bool marked)
        {
            if (index < 0 || index >= _Marks.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (_Marks[index] == marked)
                return;

            _Marks[index] = marked;
            RaiseMarks();
        }

        /// <summary>
        /// Marks every row.
        /// </summary>
        public void MarkAll()
        {
            bool changed = false;
            for (int i = 0; i < _Marks.Count; i++)
            {
                changed |= !_Marks[i];
                _Marks[i] = true;
            }

            if (changed)
                RaiseMarks();
        }

        /// <summary>
        /// Unmarks every row.
        /// </summary>
        public void ClearMarks()
        {
            bool changed = false;
            for (int i = 0; i < _Marks.Count; i++)
            {
                changed |= _Marks[i];
                _Marks[i] = false;
            }

            if (changed)
                RaiseMarks();
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
        }

        /// <summary>
        /// Applies a theme: rows from <see cref="Theme.Text"/>, the header from <see cref="Theme.Accent"/>
        /// (bold), the highlight from <see cref="Theme.Selection"/>, the inactive highlight from
        /// <see cref="Theme.Muted"/> (reversed), and disabled from <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            HeaderStyle = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            HighlightStyle = theme.Selection;
            InactiveHighlightStyle = new CellStyle(theme.Text.Background, theme.Muted.Foreground);
            DisabledStyle = theme.Disabled;
        }

        /// <summary>
        /// Gets the table's keys for the status bar: moving the selection, <c>Enter</c> when
        /// <see cref="RowActivated"/> has a handler, and with <see cref="MultiSelect"/> the marking keys.
        /// Empty while disabled or empty. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints. Never null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            if (!_Enabled || _Rows.Count == 0)
                return hints;

            if (RowActivated != null)
                hints.Add(new KeyHint(new KeyChord(KeyCode.Enter, 0, KeyModifiers.None), "Open", 10));
            hints.Add(new KeyHint("Up/Down", "Move"));
            if (MultiSelect)
            {
                hints.Add(new KeyHint(new KeyChord(KeyCode.Character, ' ', KeyModifiers.None), "Mark"));
                hints.Add(KeyHint.For("ctrl+a", "Mark all"));
                if (MarkedCount > 0)
                    hints.Add(new KeyHint(new KeyChord(KeyCode.Escape, 0, KeyModifiers.None), "Clear marks"));
            }

            return hints;
        }

        /// <summary>
        /// Moves the selection with Up/Down (single row), PageUp/PageDown (by the visible row count),
        /// and Home/End (first and last row). With <see cref="MultiSelect"/>, Space toggles the mark,
        /// Shift+Up/Down extend the marks, Ctrl+A marks all, and Escape clears marks. Enter raises
        /// <see cref="RowActivated"/> when a handler is attached.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            int last = Math.Max(0, _Rows.Count - 1);
            bool shift = key.Modifiers == KeyModifiers.Shift;
            if (MultiSelect && _Rows.Count > 0)
            {
                if (key.Code == KeyCode.Character && key.Rune == ' ' && key.Modifiers == KeyModifiers.None)
                {
                    SetMarked(_Selected, !_Marks[_Selected]);
                    return true;
                }

                if (key.Code == KeyCode.Character && (key.Rune == 'a' || key.Rune == 'A') && key.Modifiers == KeyModifiers.Ctrl)
                {
                    MarkAll();
                    return true;
                }

                if (key.Code == KeyCode.Escape && MarkedCount > 0)
                {
                    ClearMarks();
                    return true;
                }

                if (shift && (key.Code == KeyCode.Up || key.Code == KeyCode.Down))
                {
                    bool changed = !_Marks[_Selected];
                    _Marks[_Selected] = true;
                    int next = key.Code == KeyCode.Up ? Math.Max(0, _Selected - 1) : Math.Min(last, _Selected + 1);
                    changed |= !_Marks[next];
                    _Marks[next] = true;
                    Navigate(next);
                    if (changed)
                        RaiseMarks();
                    return true;
                }
            }

            switch (key.Code)
            {
                case KeyCode.Enter:
                    if (RowActivated == null || _Rows.Count == 0 || key.Modifiers != KeyModifiers.None)
                        return false;

                    RowActivated(_Selected);
                    return true;
                case KeyCode.Up:
                    Navigate(Math.Max(0, _Selected - 1));
                    return true;
                case KeyCode.Down:
                    Navigate(Math.Min(last, _Selected + 1));
                    return true;
                case KeyCode.PageUp:
                    Navigate(Math.Max(0, _Selected - Math.Max(1, _LastViewportHeight)));
                    return true;
                case KeyCode.PageDown:
                    Navigate(Math.Min(last, _Selected + Math.Max(1, _LastViewportHeight)));
                    return true;
                case KeyCode.Home:
                    Navigate(0);
                    return true;
                case KeyCode.End:
                    Navigate(last);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Selects the row under the pointer on a left press, sorts on a header click, toggles the mark on a
        /// click in the mark column or a Ctrl+click, activates on a double click, and moves the selection one
        /// row per wheel notch. Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event was consumed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!_Enabled)
                return false;

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && mouse.Y == 0 && SortOnHeaderClick)
            {
                int column = ColumnAt(mouse.X);
                if (column >= 0 && _Columns[column].Sortable)
                {
                    SortByColumn(column);
                    return true;
                }
            }

            int count = _Rows.Count;
            if (count == 0)
                return false;

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                if (mouse.Button == MouseButton.WheelUp)
                {
                    Navigate(Math.Max(0, _Selected - 1));
                    return true;
                }

                if (mouse.Button == MouseButton.WheelDown)
                {
                    Navigate(Math.Min(count - 1, _Selected + 1));
                    return true;
                }

                return false;
            }

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                int index = _Top + (mouse.Y - 1);
                if (index >= 0 && index < count)
                {
                    SetSelected(index);
                    if (MultiSelect && (mouse.X < _MarkWidth || (mouse.Modifiers & KeyModifiers.Ctrl) != 0))
                        SetMarked(index, !_Marks[index]);
                    else if (mouse.ClickCount >= 2)
                        RowActivated?.Invoke(index);
                    return true;
                }
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

            if (NormalStyle.Background.Kind != ColorKind.Default)
                surface.Fill(new Rect(0, 0, width, height), Cell.Blank(NormalStyle));

            _MarkWidth = MultiSelect ? Math.Min(4, width) : 0;
            Layout(width - _MarkWidth);

            if (_MarkWidth > 0)
                surface.DrawText(0, 0, TextFit.PadRight(MarkedCount > 0 ? "[" + (MarkedCount == _Rows.Count ? "x" : "-") + "]" : "[ ]", _MarkWidth), HeaderStyle);

            for (int c = 0; c < _Columns.Count; c++)
            {
                if (_ColumnWidths[c] <= 0)
                    continue;

                string header = _Columns[c].Name;
                if (ShowSortIndicator && c == _SortColumn)
                {
                    string indicator = _SortAscending ? SortAscendingIndicator : SortDescendingIndicator;
                    int room = Math.Max(0, _ColumnWidths[c] - 1 - TextFit.Width(indicator) - 1);
                    header = TextFit.Truncate(header, room) + " " + indicator;
                }

                surface.DrawText(_MarkWidth + _ColumnX[c], 0, Fit(header, _ColumnWidths[c], _Columns[c].Alignment), HeaderStyle);
            }

            int listHeight = height - 1;
            _LastViewportHeight = Math.Max(1, listHeight);
            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + listHeight)
                _Top = _Selected - listHeight + 1;
            if (_Top > Math.Max(0, _Rows.Count - listHeight))
                _Top = Math.Max(0, Math.Min(_Selected, _Rows.Count - listHeight));

            if (_Rows.Count == 0 && listHeight > 0 && EmptyText.Length > 0)
            {
                surface.DrawText(0, 1, TextFit.Ellipsize(EmptyText, width), NormalStyle.WithAttribute(CellAttributes.Dim, true));
                return;
            }

            for (int row = 0; row < listHeight && _Top + row < _Rows.Count; row++)
            {
                int index = _Top + row;
                T item = _Rows[index];
                bool selected = index == _Selected;
                int y = row + 1;
                CellStyle rowStyle = NormalStyle;
                if (RowStyle != null)
                {
                    CellStyle? custom = RowStyle(item);
                    if (custom.HasValue)
                        rowStyle = custom.Value.Over(NormalStyle);
                }

                CellStyle style = selected ? (_Focused ? HighlightStyle : InactiveHighlightStyle) : rowStyle;
                if (!_Enabled)
                    style = DisabledStyle.Over(style);
                if (selected)
                    surface.Fill(new Rect(0, y, width, 1), Cell.Blank(style));

                if (_MarkWidth > 0)
                    surface.DrawText(0, y, TextFit.PadRight(_Marks[index] ? "[x]" : "[ ]", _MarkWidth), style);

                for (int c = 0; c < _Columns.Count; c++)
                {
                    if (_ColumnWidths[c] <= 0)
                        continue;

                    DataColumn<T> column = _Columns[c];
                    CellStyle cellStyle = style;
                    if (!selected && _Enabled && column.CellStyle != null)
                    {
                        CellStyle? custom = column.CellStyle(item);
                        if (custom.HasValue)
                            cellStyle = custom.Value.Over(style);
                    }

                    surface.DrawText(_MarkWidth + _ColumnX[c], y, Fit(column.Value(item) ?? string.Empty, _ColumnWidths[c], column.Alignment), cellStyle);
                }
            }
        }

        private void Layout(int width)
        {
            _ColumnX.Clear();
            _ColumnWidths.Clear();
            int count = _Columns.Count;
            int fixedTotal = 0;
            int weightTotal = 0;
            bool custom = false;
            for (int c = 0; c < count; c++)
            {
                DataColumn<T> column = _Columns[c];
                if (column.Width.HasValue)
                {
                    fixedTotal += column.Width.Value;
                    custom = true;
                }
                else
                {
                    weightTotal += column.Weight;
                    custom |= column.Weight != 1 || column.MinWidth != 1;
                }
            }

            int[] widths = new int[count];
            if (!custom)
            {
                // The original layout: equal widths of width / count, leftover columns unused.
                int equal = Math.Max(1, width / count);
                for (int c = 0; c < count; c++)
                    widths[c] = equal;
            }
            else
            {
                int remaining = Math.Max(0, width - fixedTotal);
                int lastWeighted = -1;
                int used = 0;
                for (int c = 0; c < count; c++)
                {
                    DataColumn<T> column = _Columns[c];
                    if (column.Width.HasValue)
                    {
                        widths[c] = column.Width.Value;
                    }
                    else
                    {
                        widths[c] = Math.Max(column.MinWidth, weightTotal > 0 ? remaining * column.Weight / weightTotal : 0);
                        lastWeighted = c;
                    }

                    used += widths[c];
                }

                if (StretchLastColumn && lastWeighted >= 0 && used < width)
                    widths[lastWeighted] += width - used;
            }

            if (StretchLastColumn && !custom && count > 0)
                widths[count - 1] += Math.Max(0, width - (widths[0] * count));

            int x = 0;
            for (int c = 0; c < count; c++)
            {
                int w = x >= width ? 0 : Math.Min(widths[c], width - x);
                _ColumnX.Add(x);
                _ColumnWidths.Add(w);
                x += widths[c];
            }
        }

        private int ColumnAt(int x)
        {
            int local = x - _MarkWidth;
            for (int c = 0; c < _ColumnX.Count; c++)
            {
                if (local >= _ColumnX[c] && local < _ColumnX[c] + _ColumnWidths[c])
                    return c;
            }

            return -1;
        }

        private void ApplySort()
        {
            DataColumn<T> column = _Columns[_SortColumn];
            int count = _Rows.Count;
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;

            List<T> rows = new List<T>(_Rows);
            bool ascending = _SortAscending;
            Array.Sort(order, (a, b) =>
            {
                int result = ascending ? column.Compare(rows[a], rows[b]) : column.Compare(rows[b], rows[a]);
                return result != 0 ? result : a.CompareTo(b);
            });

            List<bool> marks = new List<bool>(_Marks);
            int newSelected = 0;
            for (int i = 0; i < count; i++)
            {
                _Rows[i] = rows[order[i]];
                _Marks[i] = marks[order[i]];
                if (order[i] == _Selected)
                    newSelected = i;
            }

            if (count > 0 && SelectionFollowsSort)
                _Selected = newSelected;
        }

        private void Navigate(int index)
        {
            SetSelected(index);
            if (_Rows.Count > 0 && _Selected >= _Rows.Count - 1 - Math.Max(0, EndReachedThreshold))
                EndReached?.Invoke();
        }

        private void SetSelected(int index)
        {
            int before = SelectedIndex;
            _Selected = index;
            RaiseSelection(before);
        }

        private void RaiseSelection(int before)
        {
            int after = SelectedIndex;
            if (before == after)
                return;

            SelectionChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, after));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseMarks()
        {
            MarkedChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static string Fit(string text, int width, CellAlignment alignment)
        {
            // One spare column separates adjacent cells, as in the original layout.
            int room = width <= 1 ? Math.Max(0, width) : width - 1;
            string fitted = TextFit.Truncate(text, room);
            switch (alignment)
            {
                case CellAlignment.Right:
                    return TextFit.PadLeft(fitted, room);
                case CellAlignment.Center:
                    return TextFit.Center(fitted, room);
                default:
                    return fitted;
            }
        }
    }
}
