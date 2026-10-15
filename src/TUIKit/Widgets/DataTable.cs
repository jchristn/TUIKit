namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
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
    /// same rows across <see cref="Bind"/>. <see cref="EndReached"/> is an infinite-scroll hook.
    /// <see cref="PagingMode"/> pages the rows locally or one server page at a time, with a paging bar,
    /// load states (<see cref="SetLoading"/>, <see cref="SetError"/>), and <see cref="DataTableLoader{T}"/> for
    /// the async plumbing. Columns have stable keys, can be hidden (<see cref="SetColumnVisible"/>,
    /// <see cref="CreateColumnChooser"/>), and can be sorted from the keyboard with
    /// <see cref="ShowColumnCursor"/>. Not thread-safe: use it from the UI loop.
    /// </remarks>
    /// <typeparam name="T">The row type.</typeparam>
    public sealed class DataTable<T> : IWidget, IFocusable, IMouseAware, IFocusAware, IEnableable, IChangeNotifier, IThemeable, IKeyHintSource
    {
        #region Private-Members

        private static readonly IReadOnlyList<int> _DefaultPageSizes = new[] { 10, 25, 50, 100 };

        private readonly List<DataColumn<T>> _Columns = new List<DataColumn<T>>();
        private readonly List<T> _Rows = new List<T>();
        private readonly List<bool> _Marks = new List<bool>();
        private readonly List<int> _ColumnX = new List<int>();
        private readonly List<int> _ColumnWidths = new List<int>();
        private readonly HashSet<string> _RemoteMarkedKeys = new HashSet<string>(StringComparer.Ordinal);
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private int _SortColumn = -1;
        private bool _SortAscending = true;
        private bool _Enabled = true;
        private bool _Focused = true;
        private int _MarkWidth;
        private DataTablePagingMode _PagingMode = DataTablePagingMode.None;
        private int _PageSize = 25;
        private IReadOnlyList<int> _PageSizes = _DefaultPageSizes;
        private int _PageNumber = 1;
        private int _HighestPage = 1;
        private long _TotalRows = -1;
        private bool _PendingSelectLast;
        private DataTableState _State = DataTableState.Ready;
        private string? _ErrorMessage;
        private string _LoadingText = "Loading...";
        private string _RefreshingText = "Refreshing...";
        private string _ErrorFormat = "Failed to load: {0}";
        private string _PagingFormat = "Showing {0}-{1} of {2}. Page {3} of {4}.";
        private string _UnknownTotalFormat = "Showing {0}-{1}. Page {3}.";
        private int _BarY = -1;
        private int _BarWidth;
        private bool _ShowColumnCursor;
        private int _FocusedColumn = -1;
        private CellStyle? _FocusedHeaderStyle;

        #endregion

        #region Public-Members

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
        /// row by keyboard or wheel navigation, so the application can load more rows and rebind. Never raised
        /// while <see cref="PagingMode"/> is not <see cref="DataTablePagingMode.None"/>: paging and infinite
        /// scroll are alternatives.
        /// </summary>
        public event Action? EndReached;

        /// <summary>
        /// Raised in <see cref="DataTablePagingMode.Remote"/> mode when the table needs a page: a page move, a
        /// page-size change, or a sort change (which requests page 1, after <see cref="SortChanged"/>). The table
        /// keeps showing the current rows until <see cref="BindPage"/>.
        /// </summary>
        public event EventHandler<DataTablePageRequest>? PageRequested;

        /// <summary>
        /// Raised when <see cref="RetryKey"/> is pressed while <see cref="State"/> is <see cref="DataTableState.Error"/>.
        /// </summary>
        public event EventHandler? RetryRequested;

        /// <summary>
        /// Raised after column visibility changes, from <see cref="SetColumnVisible"/>,
        /// <see cref="SetVisibleColumns"/>, or <see cref="ApplyColumnChooser"/>.
        /// </summary>
        public event EventHandler? ColumnsChanged;

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
        /// Gets or sets the header style of the focused column while <see cref="ShowColumnCursor"/> is on.
        /// Defaults to <see cref="HeaderStyle"/> with underline, following later changes to
        /// <see cref="HeaderStyle"/> until assigned.
        /// </summary>
        public CellStyle FocusedHeaderStyle
        {
            get { return _FocusedHeaderStyle ?? HeaderStyle.WithAttribute(CellAttributes.Underline, true); }
            set { _FocusedHeaderStyle = value; }
        }

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
        /// Gets or sets the style of the error text while <see cref="State"/> is <see cref="DataTableState.Error"/>.
        /// Defaults to palette color 1 (red).
        /// </summary>
        public CellStyle ErrorStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(1));

        /// <summary>
        /// Gets or sets the style of the paging bar. Defaults to palette color 8 (grey).
        /// </summary>
        public CellStyle PagingBarStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets a per-row style selector, or null. A returned style is composed over
        /// <see cref="NormalStyle"/> for unselected rows.
        /// </summary>
        public Func<T, CellStyle?>? RowStyle { get; set; }

        /// <summary>
        /// Gets or sets a stable key per row. When set, <see cref="Bind"/> keeps the selection and the
        /// marks on the rows with the same keys, and in <see cref="DataTablePagingMode.Remote"/> mode marks are
        /// kept by key across pages (<see cref="MarkedKeys"/>). Null (the default) keeps the selection index.
        /// </summary>
        public Func<T, string>? KeySelector { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a sort reorders the bound rows. Defaults to true. When
        /// false (server-side sorting), the table only tracks the sort column and direction for the header
        /// indicator and raises <see cref="SortChanged"/>. Ignored, and treated as false, in
        /// <see cref="DataTablePagingMode.Remote"/> mode: a server-backed table can only sort on the server, so a
        /// sort change requests page 1 and never reorders the bound page.
        /// </summary>
        public bool SortLocally { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the header shows a sort direction indicator. Defaults to true.
        /// </summary>
        public bool ShowSortIndicator { get; set; } = true;

        /// <summary>
        /// Gets or sets the ascending indicator. Defaults to an up triangle (U+25B2).
        /// </summary>
        public string SortAscendingIndicator { get; set; } = "▲";

        /// <summary>
        /// Gets or sets the descending indicator. Defaults to a down triangle (U+25BC).
        /// </summary>
        public string SortDescendingIndicator { get; set; } = "▼";

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
        /// Gets or sets how the table pages its rows. Defaults to <see cref="DataTablePagingMode.None"/>, the 1.7
        /// behavior: no bar, no page keys, and <see cref="EndReached"/> unchanged. <see cref="DataTablePagingMode.Local"/>
        /// pages the rows given to <see cref="Bind"/>, keeping every index (<see cref="SelectedIndex"/>,
        /// <see cref="RowActivated"/>, marks) absolute into the bound rows; <see cref="DataTablePagingMode.Remote"/>
        /// shows one page from <see cref="BindPage"/>, with indexes into that page and <see cref="SortLocally"/> ignored.
        /// Changing the mode returns to page 1.
        /// </summary>
        public DataTablePagingMode PagingMode
        {
            get { return _PagingMode; }
            set
            {
                if (_PagingMode == value)
                    return;

                _PagingMode = value;
                _PageNumber = 1;
                _HighestPage = 1;
                _TotalRows = -1;
                _RemoteMarkedKeys.Clear();
                if (_PagingMode == DataTablePagingMode.Local)
                    FollowSelectionToPage();
            }
        }

        /// <summary>
        /// Gets or sets the page size in rows. Defaults to 25; at least 1. Setting it does not move to another
        /// page or request one; <see cref="SetPageSize"/> does, keeping the first visible row on screen.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int PageSize
        {
            get { return _PageSize; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "The page size must be at least 1.");
                _PageSize = value;
                if (_PagingMode == DataTablePagingMode.Local)
                    ClampLocalPage();
            }
        }

        /// <summary>
        /// Gets or sets the page sizes <see cref="CyclePageSize"/> steps through. Defaults to 10, 25, 50, 100.
        /// Must not be null or empty, and each entry must be at least 1.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when empty or when an entry is below 1.</exception>
        public IReadOnlyList<int> PageSizes
        {
            get { return _PageSizes; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.Count == 0)
                    throw new ArgumentException("At least one page size is required.", nameof(value));
                if (value.Any(v => v < 1))
                    throw new ArgumentException("Every page size must be at least 1.", nameof(value));
                _PageSizes = value.ToList();
            }
        }

        /// <summary>
        /// Gets the current page number, 1-based. 1 while <see cref="PagingMode"/> is <see cref="DataTablePagingMode.None"/>.
        /// </summary>
        public int PageNumber
        {
            get { return _PageNumber; }
        }

        /// <summary>
        /// Gets the page count: at least 1 (1 when there are no rows). With an unknown total in
        /// <see cref="DataTablePagingMode.Remote"/> mode, the highest page known so far, plus one while the
        /// current page is full (so <see cref="NextPage"/> stays possible).
        /// </summary>
        public int PageCount
        {
            get
            {
                if (_PagingMode == DataTablePagingMode.None)
                    return 1;

                long total = TotalRows;
                if (total < 0)
                    return Math.Max(_HighestPage, _Rows.Count >= _PageSize ? _PageNumber + 1 : _PageNumber);

                return (int)Math.Max(1, (total + _PageSize - 1) / _PageSize);
            }
        }

        /// <summary>
        /// Gets the total row count: the bound row count outside <see cref="DataTablePagingMode.Remote"/> mode, and
        /// in <see cref="DataTablePagingMode.Remote"/> mode the total given to <see cref="BindPage"/>, where -1 means
        /// unknown.
        /// </summary>
        public long TotalRows
        {
            get { return _PagingMode == DataTablePagingMode.Remote ? _TotalRows : _Rows.Count; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the paging bar is drawn as the table's last row. Defaults to
        /// true; has effect only while <see cref="PagingMode"/> is not <see cref="DataTablePagingMode.None"/>, so a 1.7
        /// table never grows a bar.
        /// </summary>
        public bool ShowPagingBar { get; set; } = true;

        /// <summary>
        /// Gets or sets the paging bar text: a composite format with {0} the first row shown (1-based), {1} the
        /// last, {2} the total, {3} the page, and {4} the page count. Defaults to
        /// <c>"Showing {0}-{1} of {2}. Page {3} of {4}."</c>; this is the localization hook.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the format is not a valid composite format for five arguments.</exception>
        public string PagingFormat
        {
            get { return _PagingFormat; }
            set { _PagingFormat = ValidateFormat(value, 5, false, nameof(PagingFormat)); }
        }

        /// <summary>
        /// Gets or sets the paging bar text used while the total is unknown (-1); same arguments as
        /// <see cref="PagingFormat"/> ({2} and {4} are blank). Defaults to <c>"Showing {0}-{1}. Page {3}."</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the format is not a valid composite format for five arguments.</exception>
        public string UnknownTotalFormat
        {
            get { return _UnknownTotalFormat; }
            set { _UnknownTotalFormat = ValidateFormat(value, 5, false, nameof(UnknownTotalFormat)); }
        }

        /// <summary>
        /// Gets the load state. <see cref="DataTableState.Ready"/> by default and after every <see cref="Bind"/> or
        /// <see cref="BindPage"/>; the table never enters another state on its own.
        /// </summary>
        public DataTableState State
        {
            get { return _State; }
        }

        /// <summary>
        /// Gets the message given to <see cref="SetError"/> while <see cref="State"/> is <see cref="DataTableState.Error"/>,
        /// otherwise null.
        /// </summary>
        public string? ErrorMessage
        {
            get { return _State == DataTableState.Error ? _ErrorMessage : null; }
        }

        /// <summary>
        /// Gets or sets the text shown in the body while loading with no rows. Defaults to <c>"Loading..."</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string LoadingText
        {
            get { return _LoadingText; }
            set { _LoadingText = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the text appended to the paging bar (or drawn at the end of the last row when there is no
        /// bar) while loading with rows on screen. Defaults to <c>"Refreshing..."</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string RefreshingText
        {
            get { return _RefreshingText; }
            set { _RefreshingText = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the error text: a composite format whose {0} is the message. Defaults to
        /// <c>"Failed to load: {0}"</c>. Must contain {0}.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the format lacks {0} or is not a valid composite format.</exception>
        public string ErrorFormat
        {
            get { return _ErrorFormat; }
            set { _ErrorFormat = ValidateFormat(value, 1, true, nameof(ErrorFormat)); }
        }

        /// <summary>
        /// Gets or sets the chord that raises <see cref="RetryRequested"/> while <see cref="State"/> is
        /// <see cref="DataTableState.Error"/>, or null for none (the default). F5 is the common choice. Not consumed
        /// in any other state.
        /// </summary>
        public KeyChord? RetryKey { get; set; }

        /// <summary>
        /// Gets or sets the chord that moves to the next page in a paging mode, or null for none (the default).
        /// Letters (<c>&gt;</c>) and Ctrl+PageDown (used by tab strips) are left to the application to choose.
        /// </summary>
        public KeyChord? NextPageKey { get; set; }

        /// <summary>
        /// Gets or sets the chord that moves to the previous page in a paging mode, or null for none (the default).
        /// </summary>
        public KeyChord? PreviousPageKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Down on the last row of a page moves to the first row of the
        /// next page, and Up on the first row to the last row of the previous page. Defaults to false.
        /// </summary>
        public bool PageAtEdges { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Left and Right move a focused column across the visible
        /// columns. Defaults to false: Left and Right stay unbound, as in 1.7. While true the focused column's
        /// header is drawn in <see cref="FocusedHeaderStyle"/> and <see cref="SortKey"/> and
        /// <see cref="ReverseSortKey"/> sort by it.
        /// </summary>
        public bool ShowColumnCursor
        {
            get { return _ShowColumnCursor; }
            set
            {
                _ShowColumnCursor = value;
                if (value && (_FocusedColumn < 0 || !IsColumnShown(_FocusedColumn)))
                    _FocusedColumn = NextVisibleColumn(-1, 1);
            }
        }

        /// <summary>
        /// Gets the focused column as an index into <see cref="VisibleColumns"/>, or -1 while
        /// <see cref="ShowColumnCursor"/> is off or no column is visible.
        /// </summary>
        public int FocusedColumn
        {
            get
            {
                if (!_ShowColumnCursor || _FocusedColumn < 0 || !IsColumnShown(_FocusedColumn))
                    return -1;

                int visible = 0;
                for (int c = 0; c < _FocusedColumn; c++)
                {
                    if (_Columns[c].Visible)
                        visible++;
                }

                return visible;
            }
        }

        /// <summary>
        /// Gets or sets the chord that sorts by the focused column (toggling direction when already sorted by it)
        /// while <see cref="ShowColumnCursor"/> is on, or null for none (the default). Not consumed on a
        /// non-sortable column.
        /// </summary>
        public KeyChord? SortKey { get; set; }

        /// <summary>
        /// Gets or sets the chord that sorts by the focused column in descending order while
        /// <see cref="ShowColumnCursor"/> is on, or null for none (the default). Not consumed on a non-sortable column.
        /// Chords store letters lower-cased, so bind upper-case S with <c>KeyChord.Parse("shift+s")</c>: an upper-case
        /// letter matches its shift+letter chord, and this key is checked before <see cref="SortKey"/>.
        /// </summary>
        public KeyChord? ReverseSortKey { get; set; }

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
        /// Gets the number of bound rows (in <see cref="DataTablePagingMode.Remote"/> mode, the rows on the current page).
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
        /// Gets the columns, visible or not. Never null.
        /// </summary>
        public IReadOnlyList<DataColumn<T>> Columns
        {
            get { return _Columns; }
        }

        /// <summary>
        /// Gets the visible columns in order. Never null.
        /// </summary>
        public IReadOnlyList<DataColumn<T>> VisibleColumns
        {
            get { return _Columns.Where(c => c.Visible).ToList(); }
        }

        /// <summary>
        /// Gets the zero-based selected row index, or -1 when the table is empty. In
        /// <see cref="DataTablePagingMode.Local"/> mode the index is absolute into the bound rows.
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
        /// Gets the marked rows in display order (in <see cref="DataTablePagingMode.Remote"/> mode, those on the
        /// current page). Never null.
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
        /// Gets the keys of the marked rows when <see cref="KeySelector"/> is set: in
        /// <see cref="DataTablePagingMode.Remote"/> mode every marked key, including rows on other pages; otherwise
        /// the keys of the marked bound rows. Empty without a <see cref="KeySelector"/>. Never null.
        /// </summary>
        public IReadOnlyCollection<string> MarkedKeys
        {
            get
            {
                Func<T, string>? key = KeySelector;
                if (key == null)
                    return new List<string>();

                if (_PagingMode == DataTablePagingMode.Remote)
                    return _RemoteMarkedKeys.ToList();

                List<string> result = new List<string>();
                for (int i = 0; i < _Marks.Count; i++)
                {
                    if (_Marks[i])
                        result.Add(key(_Rows[i]));
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the number of marked rows among the bound rows.
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

        #endregion

        #region Public-Methods

        /// <summary>
        /// Adds a column whose key is its header.
        /// </summary>
        /// <param name="name">The header. Must not be null.</param>
        /// <param name="value">A selector returning the cell text for a row. Must not be null.</param>
        /// <param name="sortable">Whether the column can be sorted.</param>
        /// <returns>This table, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name equals another column's explicit key.</exception>
        public DataTable<T> Column(string name, Func<T, string> value, bool sortable = false)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return Column(new DataColumn<T>(name, value, sortable));
        }

        /// <summary>
        /// Adds a column with an explicit key, separate from its header.
        /// </summary>
        /// <param name="key">The stable key. Must not be null or empty.</param>
        /// <param name="name">The header. Must not be null.</param>
        /// <param name="value">A selector returning the cell text for a row. Must not be null.</param>
        /// <param name="sortable">Whether the column can be sorted.</param>
        /// <returns>This table, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the key is empty or equals another column's key.</exception>
        public DataTable<T> Column(string key, string name, Func<T, string> value, bool sortable = false)
        {
            return Column(new DataColumn<T>(key, name, value, sortable));
        }

        /// <summary>
        /// Adds a fully configured column. Keys must be unique where an explicit key is involved: the column is
        /// rejected when it has an explicit key equal to any existing column's key, or a defaulted key (its header)
        /// equal to an existing column's explicit key. Two columns that both default their keys may share one, as
        /// two columns could share a header in 1.7; key-based lookups then act on the first.
        /// </summary>
        /// <param name="column">The column. Must not be null.</param>
        /// <returns>This table, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="column"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the key collides as described.</exception>
        public DataTable<T> Column(DataColumn<T> column)
        {
            if (column == null)
                throw new ArgumentNullException(nameof(column));

            foreach (DataColumn<T> existing in _Columns)
            {
                if (!string.Equals(existing.Key, column.Key, StringComparison.Ordinal))
                    continue;
                if (column.HasExplicitKey || existing.HasExplicitKey)
                    throw new ArgumentException("A column with key '" + column.Key + "' already exists.", nameof(column));
            }

            _Columns.Add(column);
            return this;
        }

        /// <summary>
        /// Replaces the table rows. An active local sort is re-applied. With <see cref="KeySelector"/>
        /// set, the selection and marks follow their rows; otherwise the selection index is kept (clamped)
        /// and marks are cleared. Returns <see cref="State"/> to <see cref="DataTableState.Ready"/>. In
        /// <see cref="DataTablePagingMode.Local"/> mode the page moves to the one holding the selection; in
        /// <see cref="DataTablePagingMode.Remote"/> mode the rows replace the current page, keeping the page number
        /// and total.
        /// </summary>
        /// <param name="rows">The rows. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
        public void Bind(IReadOnlyList<T> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            BindCore(rows, false);
            _State = DataTableState.Ready;
            if (_PagingMode == DataTablePagingMode.Local)
                FollowSelectionToPage();
        }

        /// <summary>
        /// Binds one page in <see cref="DataTablePagingMode.Remote"/> mode: replaces the rows, sets the page and the
        /// total, and returns <see cref="State"/> to <see cref="DataTableState.Ready"/>. A new page number selects
        /// the page's first row (or its last after <see cref="PageAtEdges"/> moved up); the same page keeps the
        /// selection. With <see cref="KeySelector"/> set, marks are restored from <see cref="MarkedKeys"/>; without
        /// one, marks are dropped, as <see cref="Bind"/> drops them.
        /// </summary>
        /// <param name="rows">The page rows. Must not be null.</param>
        /// <param name="pageNumber">The page number, 1-based. At least 1.</param>
        /// <param name="totalRows">The total row count across pages, or -1 for unknown. At least -1.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is below its minimum.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="PagingMode"/> is not <see cref="DataTablePagingMode.Remote"/>.</exception>
        public void BindPage(IReadOnlyList<T> rows, int pageNumber, long totalRows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page numbers start at 1.");
            if (totalRows < -1)
                throw new ArgumentOutOfRangeException(nameof(totalRows), "The total must be -1 (unknown) or greater.");
            if (_PagingMode != DataTablePagingMode.Remote)
                throw new InvalidOperationException("BindPage requires PagingMode Remote; the table is in " + _PagingMode + " mode. Use Bind instead.");

            bool newPage = pageNumber != _PageNumber;
            _PageNumber = pageNumber;
            _HighestPage = Math.Max(_HighestPage, pageNumber);
            _TotalRows = totalRows;
            BindCore(rows, newPage);
            if (newPage)
            {
                int before = SelectedIndex;
                _Selected = _PendingSelectLast ? Math.Max(0, _Rows.Count - 1) : 0;
                _Top = 0;
                RaiseSelection(before);
            }

            _PendingSelectLast = false;
            _State = DataTableState.Ready;
        }

        /// <summary>
        /// Returns the request for the current page, sort, and page size, so an app can load the first page
        /// without waiting for <see cref="PageRequested"/>.
        /// </summary>
        /// <returns>The request. Never null.</returns>
        public DataTablePageRequest CurrentRequest()
        {
            return BuildRequest(_PageNumber);
        }

        /// <summary>
        /// Moves to a page, clamped to 1 through <see cref="PageCount"/>. In <see cref="DataTablePagingMode.Local"/>
        /// mode the table re-slices and selects the page's first row; in <see cref="DataTablePagingMode.Remote"/> mode
        /// it raises <see cref="PageRequested"/> and keeps the current rows until <see cref="BindPage"/>. Does nothing
        /// in <see cref="DataTablePagingMode.None"/> mode.
        /// </summary>
        /// <param name="pageNumber">The page number, 1-based.</param>
        /// <returns><c>true</c> when the page changed or was requested; <c>false</c> when nothing changed.</returns>
        public bool GoToPage(int pageNumber)
        {
            return GoToPageCore(pageNumber, false);
        }

        /// <summary>
        /// Moves to the next page. See <see cref="GoToPage"/>.
        /// </summary>
        /// <returns><c>true</c> when the page changed or was requested; <c>false</c> at the last page.</returns>
        public bool NextPage()
        {
            return GoToPageCore(_PageNumber + 1, false);
        }

        /// <summary>
        /// Moves to the previous page. See <see cref="GoToPage"/>.
        /// </summary>
        /// <returns><c>true</c> when the page changed or was requested; <c>false</c> at page 1.</returns>
        public bool PreviousPage()
        {
            return GoToPageCore(_PageNumber - 1, false);
        }

        /// <summary>
        /// Changes the page size and moves to the page that contains the first visible row, so it stays on
        /// screen. In <see cref="DataTablePagingMode.Remote"/> mode this raises <see cref="PageRequested"/> for that
        /// page.
        /// </summary>
        /// <param name="pageSize">The new page size. At least 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize"/> is below 1.</exception>
        public void SetPageSize(int pageSize)
        {
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "The page size must be at least 1.");

            long firstVisible = _PagingMode == DataTablePagingMode.Remote
                ? (long)(_PageNumber - 1) * _PageSize + Math.Max(0, _Top)
                : Math.Max(0, _Top);
            _PageSize = pageSize;
            int page = (int)(firstVisible / pageSize) + 1;
            if (_PagingMode == DataTablePagingMode.Local)
            {
                _PageNumber = Math.Max(1, Math.Min(page, PageCount));
                int before = SelectedIndex;
                if (_Selected < RangeStart() || _Selected >= RangeEnd())
                    _Selected = Math.Max(0, Math.Min((int)firstVisible, RangeEnd() - 1));
                _Top = RangeStart();
                RaiseSelection(before);
            }
            else if (_PagingMode == DataTablePagingMode.Remote)
            {
                PageRequested?.Invoke(this, BuildRequest(Math.Max(1, page)));
            }
        }

        /// <summary>
        /// Moves to the next entry of <see cref="PageSizes"/> after the current page size, wrapping to the first,
        /// through <see cref="SetPageSize"/>.
        /// </summary>
        public void CyclePageSize()
        {
            int next = _PageSizes.FirstOrDefault(s => s > _PageSize);
            SetPageSize(next > 0 ? next : _PageSizes[0]);
        }

        /// <summary>
        /// Enters <see cref="DataTableState.Loading"/>: with no rows the body shows <see cref="LoadingText"/>; with rows
        /// they stay and <see cref="RefreshingText"/> is shown. Works in every paging mode.
        /// </summary>
        public void SetLoading()
        {
            _State = DataTableState.Loading;
            _ErrorMessage = null;
        }

        /// <summary>
        /// Enters <see cref="DataTableState.Error"/>: the body shows <see cref="ErrorFormat"/> with the message in
        /// <see cref="ErrorStyle"/>, plus <see cref="RetryKey"/> when set. Works in every paging mode.
        /// </summary>
        /// <param name="message">The error message. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
        public void SetError(string message)
        {
            _ErrorMessage = message ?? throw new ArgumentNullException(nameof(message));
            _State = DataTableState.Error;
        }

        /// <summary>
        /// Selects a row, clamped to the valid range. A no-op when the table is empty. In
        /// <see cref="DataTablePagingMode.Local"/> mode the page moves to the one holding the row.
        /// </summary>
        /// <param name="index">The zero-based row index.</param>
        public void Select(int index)
        {
            if (_Rows.Count == 0)
                return;

            SetSelected(Math.Max(0, Math.Min(_Rows.Count - 1, index)));
            if (_PagingMode == DataTablePagingMode.Local)
                FollowSelectionToPage();
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
        /// follows its row when <see cref="SelectionFollowsSort"/> is set. In <see cref="DataTablePagingMode.Local"/>
        /// mode all rows are sorted before slicing; in <see cref="DataTablePagingMode.Remote"/> mode the rows are not
        /// reordered and <see cref="PageRequested"/> asks for page 1 after <see cref="SortChanged"/>.
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
            if (SortsLocally())
            {
                int before = SelectedIndex;
                ApplySort();
                RaiseSelection(before);
                if (_PagingMode == DataTablePagingMode.Local)
                    FollowSelectionToPage();
            }

            DataColumn<T> column = _Columns[columnIndex];
            SortChanged?.Invoke(this, new DataTableSortChangedEventArgs(columnIndex, column.Name, column.Key, ascending));
            if (_PagingMode == DataTablePagingMode.Remote)
                PageRequested?.Invoke(this, BuildRequest(1));
        }

        /// <summary>
        /// Sorts by the column with a key, in an explicit direction. Acts on the first column with that key.
        /// </summary>
        /// <param name="key">The column key. Must not be null.</param>
        /// <param name="ascending"><c>true</c> for ascending; <c>false</c> for descending.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when no column has the key.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the column is not sortable.</exception>
        public void SortByColumn(string key, bool ascending)
        {
            SortByColumn(RequireColumn(key), ascending);
        }

        /// <summary>
        /// Clears the sort indicator. Rows keep their current order. In <see cref="DataTablePagingMode.Remote"/>
        /// mode this requests page 1.
        /// </summary>
        public void ClearSort()
        {
            if (_SortColumn < 0)
                return;

            _SortColumn = -1;
            _SortAscending = true;
            SortChanged?.Invoke(this, new DataTableSortChangedEventArgs(-1, null, null, true));
            if (_PagingMode == DataTablePagingMode.Remote)
                PageRequested?.Invoke(this, BuildRequest(1));
        }

        /// <summary>
        /// Shows or hides the column with a key (the first column with it).
        /// </summary>
        /// <param name="key">The column key. Must not be null.</param>
        /// <param name="visible">True to show; false to hide.</param>
        /// <returns><c>true</c> when visibility changed; <c>false</c>, changing nothing, when hiding a column with
        /// <see cref="DataColumn{T}.CanHide"/> false or the last visible column, or when it is already in that state.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when no column has the key.</exception>
        public bool SetColumnVisible(string key, bool visible)
        {
            DataColumn<T> column = _Columns[RequireColumn(key)];
            if (column.Visible == visible)
                return false;
            if (!visible && (!column.CanHide || _Columns.Count(c => c.Visible) <= 1))
                return false;

            column.Visible = visible;
            OnColumnsChanged();
            return true;
        }

        /// <summary>
        /// Makes exactly the columns with the given keys visible, plus every column with
        /// <see cref="DataColumn{T}.CanHide"/> false. Unknown keys are ignored, so a layout saved by an older
        /// version still loads. When the result would hide every column, the first column stays visible.
        /// </summary>
        /// <param name="keys">The keys to show. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="keys"/> is null.</exception>
        public void SetVisibleColumns(IEnumerable<string> keys)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            HashSet<string> wanted = new HashSet<string>(keys.Where(k => k != null), StringComparer.Ordinal);
            bool changed = false;
            foreach (DataColumn<T> column in _Columns)
            {
                bool visible = !column.CanHide || wanted.Contains(column.Key);
                changed |= column.Visible != visible;
                column.Visible = visible;
            }

            if (_Columns.Count > 0 && !_Columns.Any(c => c.Visible))
                _Columns[0].Visible = true;

            if (changed)
                OnColumnsChanged();
        }

        /// <summary>
        /// Creates a column chooser: a <see cref="MultiSelectModal{T}"/> listing the columns with
        /// <see cref="DataColumn{T}.CanHide"/> true by header, pre-checked when visible. The table never pushes a
        /// modal; the app shows it and, when <see cref="Modal.Completion"/> completes, calls
        /// <see cref="ApplyColumnChooser"/>.
        /// </summary>
        /// <param name="title">The dialog title. Must not be null.</param>
        /// <returns>The chooser. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no column can be hidden.</exception>
        public MultiSelectModal<DataColumn<T>> CreateColumnChooser(string title)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            List<DataColumn<T>> hideable = _Columns.Where(c => c.CanHide).ToList();
            if (hideable.Count == 0)
                throw new InvalidOperationException("No column can be hidden, so there is nothing to choose.");

            MultiSelectModal<DataColumn<T>> chooser = new MultiSelectModal<DataColumn<T>>(title, hideable, c => c.Name);
            for (int i = 0; i < hideable.Count; i++)
                chooser.List.SetChecked(i, hideable[i].Visible);
            return chooser;
        }

        /// <summary>
        /// Applies a completed column chooser: checked columns become visible and unchecked ones hidden. Does
        /// nothing when the chooser has not completed, was cancelled, or left no column visible.
        /// </summary>
        /// <param name="chooser">A chooser from <see cref="CreateColumnChooser"/>. Must not be null.</param>
        /// <returns><c>true</c> when the choice was applied; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="chooser"/> is null.</exception>
        public bool ApplyColumnChooser(MultiSelectModal<DataColumn<T>> chooser)
        {
            if (chooser == null)
                throw new ArgumentNullException(nameof(chooser));

            if (!chooser.Completion.IsCompleted || chooser.Completion.Result == null)
                return false;

            IReadOnlyList<DataColumn<T>> checkedColumns = chooser.List.CheckedItems;
            if (checkedColumns.Count == 0 && _Columns.All(c => c.CanHide))
                return false;

            bool changed = false;
            foreach (DataColumn<T> column in chooser.List.Items)
            {
                bool visible = checkedColumns.Contains(column);
                changed |= column.Visible != visible;
                column.Visible = visible;
            }

            if (changed)
                OnColumnsChanged();
            return true;
        }

        /// <summary>
        /// Sorts by the focused column, toggling the direction when already sorted by it. Does nothing while
        /// <see cref="ShowColumnCursor"/> is off or the focused column is not sortable.
        /// </summary>
        public void SortFocusedColumn()
        {
            if (!CanSortFocused())
                return;

            SortByColumn(_FocusedColumn);
        }

        /// <summary>
        /// Sorts by the focused column in an explicit direction. Does nothing while <see cref="ShowColumnCursor"/>
        /// is off or the focused column is not sortable.
        /// </summary>
        /// <param name="ascending"><c>true</c> for ascending; <c>false</c> for descending.</param>
        public void SortFocusedColumn(bool ascending)
        {
            if (!CanSortFocused())
                return;

            SortByColumn(_FocusedColumn, ascending);
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
        /// Marks every bound row: in <see cref="DataTablePagingMode.Local"/> mode every row on every page, and in
        /// <see cref="DataTablePagingMode.Remote"/> mode only the current page.
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
        /// Unmarks every row, including (in <see cref="DataTablePagingMode.Remote"/> mode) rows on other pages held in
        /// <see cref="MarkedKeys"/>.
        /// </summary>
        public void ClearMarks()
        {
            bool changed = _RemoteMarkedKeys.Count > 0;
            _RemoteMarkedKeys.Clear();
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
        /// <see cref="Theme.Muted"/> (reversed), disabled from <see cref="Theme.Disabled"/>, the error text from
        /// <see cref="Theme.Error"/>, and the paging bar from <see cref="Theme.Muted"/>.
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
            ErrorStyle = theme.Error;
            PagingBarStyle = theme.Muted;
        }

        /// <summary>
        /// Gets the table's keys for the status bar: moving the selection, <c>Enter</c> when
        /// <see cref="RowActivated"/> has a handler, with <see cref="MultiSelect"/> the marking keys, with
        /// <see cref="ShowColumnCursor"/> the column keys, in a paging mode the page keys that are set, and
        /// <see cref="RetryKey"/> while in error. Empty while disabled or empty (except the retry key). Part of
        /// <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints. Never null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            if (!_Enabled)
                return hints;

            if (_State == DataTableState.Error && RetryKey.HasValue)
                hints.Add(new KeyHint(RetryKey.Value, "Retry", 10));

            if (_Rows.Count == 0)
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

            if (_ShowColumnCursor)
            {
                hints.Add(new KeyHint("Left/Right", "Column"));
                if (SortKey.HasValue)
                    hints.Add(new KeyHint(SortKey.Value, "Sort"));
                if (ReverseSortKey.HasValue)
                    hints.Add(new KeyHint(ReverseSortKey.Value, "Sort descending"));
            }

            if (_PagingMode != DataTablePagingMode.None)
            {
                if (PreviousPageKey.HasValue)
                    hints.Add(new KeyHint(PreviousPageKey.Value, "Previous page"));
                if (NextPageKey.HasValue)
                    hints.Add(new KeyHint(NextPageKey.Value, "Next page"));
            }

            return hints;
        }

        /// <summary>
        /// Moves the selection with Up/Down (single row), PageUp/PageDown (by the visible row count),
        /// and Home/End (first and last row of the page). With <see cref="MultiSelect"/>, Space toggles the mark,
        /// Shift+Up/Down extend the marks, Ctrl+A marks all, and Escape clears marks. Enter raises
        /// <see cref="RowActivated"/> when a handler is attached. The opt-in keys are <see cref="RetryKey"/>,
        /// <see cref="NextPageKey"/>, <see cref="PreviousPageKey"/>, Left/Right with <see cref="ShowColumnCursor"/>,
        /// <see cref="SortKey"/>, and <see cref="ReverseSortKey"/>.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            KeyChord chord = KeyChord.FromKeyEvent(key);
            if (_State == DataTableState.Error && RetryKey.HasValue && chord.Equals(RetryKey.Value))
            {
                RetryRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            if (_PagingMode != DataTablePagingMode.None)
            {
                if (NextPageKey.HasValue && chord.Equals(NextPageKey.Value))
                {
                    NextPage();
                    return true;
                }

                if (PreviousPageKey.HasValue && chord.Equals(PreviousPageKey.Value))
                {
                    PreviousPage();
                    return true;
                }
            }

            if (_ShowColumnCursor && TryColumnKey(key, chord, out bool consumed))
                return consumed;

            int first = RangeStart();
            int last = Math.Max(first, RangeEnd() - 1);
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

                if (key.Code == KeyCode.Escape && (MarkedCount > 0 || _RemoteMarkedKeys.Count > 0))
                {
                    ClearMarks();
                    return true;
                }

                if (shift && (key.Code == KeyCode.Up || key.Code == KeyCode.Down))
                {
                    bool changed = !_Marks[_Selected];
                    _Marks[_Selected] = true;
                    int next = key.Code == KeyCode.Up ? Math.Max(first, _Selected - 1) : Math.Min(last, _Selected + 1);
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
                    if (PageAtEdges && _PagingMode != DataTablePagingMode.None && _Selected <= first && _PageNumber > 1)
                    {
                        GoToPageCore(_PageNumber - 1, true);
                        return true;
                    }

                    Navigate(Math.Max(first, _Selected - 1));
                    return true;
                case KeyCode.Down:
                    if (PageAtEdges && _PagingMode != DataTablePagingMode.None && _Selected >= last && _PageNumber < PageCount)
                    {
                        GoToPageCore(_PageNumber + 1, false);
                        return true;
                    }

                    Navigate(Math.Min(last, _Selected + 1));
                    return true;
                case KeyCode.PageUp:
                    Navigate(Math.Max(first, _Selected - Math.Max(1, _LastViewportHeight)));
                    return true;
                case KeyCode.PageDown:
                    Navigate(Math.Min(last, _Selected + Math.Max(1, _LastViewportHeight)));
                    return true;
                case KeyCode.Home:
                    Navigate(first);
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
        /// click in the mark column or a Ctrl+click, activates on a double click, moves the selection one
        /// row per wheel notch, and pages on a click on the paging bar's <c>&lt;</c> and <c>&gt;</c>.
        /// Coordinates are widget-local.
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

            bool leftPress = mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left;
            if (leftPress && mouse.Y == 0 && SortOnHeaderClick)
            {
                int column = ColumnAt(mouse.X);
                if (column >= 0 && _Columns[column].Sortable)
                {
                    SortByColumn(column);
                    return true;
                }
            }

            if (leftPress && _BarY > 0 && mouse.Y == _BarY)
            {
                if (mouse.X == _BarWidth - 3)
                {
                    PreviousPage();
                    return true;
                }

                if (mouse.X == _BarWidth - 1)
                {
                    NextPage();
                    return true;
                }

                return false;
            }

            int count = _Rows.Count;
            if (count == 0)
                return false;

            int first = RangeStart();
            int last = Math.Max(first, RangeEnd() - 1);
            if (mouse.Kind == MouseEventKind.Wheel)
            {
                if (mouse.Button == MouseButton.WheelUp)
                {
                    Navigate(Math.Max(first, _Selected - 1));
                    return true;
                }

                if (mouse.Button == MouseButton.WheelDown)
                {
                    Navigate(Math.Min(last, _Selected + 1));
                    return true;
                }

                return false;
            }

            if (leftPress && _State != DataTableState.Error)
            {
                int index = _Top + (mouse.Y - 1);
                if (index >= first && index <= last && index < count && mouse.Y >= 1 && (_BarY < 0 || mouse.Y < _BarY))
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
            if (_PagingMode == DataTablePagingMode.None)
                return new Size(available.Width, Math.Min(available.Height, _Rows.Count + 1));

            int rows = Math.Max(1, RangeEnd() - RangeStart());
            return new Size(available.Width, Math.Min(available.Height, rows + 1 + (ShowsBar() ? 1 : 0)));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            _BarY = -1;
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

                CellStyle headerStyle = _ShowColumnCursor && c == _FocusedColumn ? FocusedHeaderStyle : HeaderStyle;
                surface.DrawText(_MarkWidth + _ColumnX[c], 0, Fit(header, _ColumnWidths[c], _Columns[c].Alignment), headerStyle);
            }

            bool bar = ShowsBar() && height >= 3;
            int listHeight = height - 1 - (bar ? 1 : 0);
            if (bar)
            {
                _BarY = height - 1;
                _BarWidth = width;
                DrawBar(surface, width, _BarY);
            }

            int first = RangeStart();
            int end = RangeEnd();
            _LastViewportHeight = Math.Max(1, listHeight);
            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + listHeight)
                _Top = _Selected - listHeight + 1;
            if (_Top > Math.Max(first, end - listHeight))
                _Top = Math.Max(first, Math.Min(_Selected, end - listHeight));
            if (_Top < first)
                _Top = first;

            if (listHeight <= 0)
                return;

            if (_State == DataTableState.Error)
            {
                string text = string.Format(CultureInfo.CurrentCulture, _ErrorFormat, _ErrorMessage ?? string.Empty);
                if (RetryKey.HasValue)
                    text += "  " + RetryKey.Value.ToLabel(KeyLabelStyle.Ascii) + " Retry";
                surface.DrawText(0, 1, TextFit.Ellipsize(text, width), ErrorStyle.Over(NormalStyle));
                return;
            }

            if (end <= first)
            {
                string empty = _State == DataTableState.Loading ? _LoadingText : EmptyText;
                if (empty.Length > 0)
                    surface.DrawText(0, 1, TextFit.Ellipsize(empty, width), NormalStyle.WithAttribute(CellAttributes.Dim, true));
                return;
            }

            for (int row = 0; row < listHeight && _Top + row < end; row++)
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

            if (_State == DataTableState.Loading && !bar && _RefreshingText.Length > 0)
            {
                int y = Math.Min(listHeight, end - _Top);
                string text = TextFit.Truncate(_RefreshingText, width);
                surface.DrawText(Math.Max(0, width - TextFit.Width(text)), y, text, PagingBarStyle.Over(NormalStyle));
            }
        }

        #endregion

        #region Internal-Methods

        internal void ClearLoadState()
        {
            if (_State == DataTableState.Loading)
                _State = DataTableState.Ready;
        }

        #endregion

        #region Private-Methods

        private void BindCore(IReadOnlyList<T> rows, bool newPage)
        {
            int before = SelectedIndex;
            Func<T, string>? key = KeySelector;
            bool remote = _PagingMode == DataTablePagingMode.Remote;
            string? selectedKey = key != null && before >= 0 && !newPage ? key(_Rows[before]) : null;
            HashSet<string>? markedKeys = null;
            bool hadMarks = MarkedCount > 0;
            if (key != null && remote)
            {
                markedKeys = _RemoteMarkedKeys;
            }
            else if (key != null && hadMarks)
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

            if (_SortColumn >= 0 && SortsLocally() && _SortColumn < _Columns.Count)
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

        private bool GoToPageCore(int pageNumber, bool selectLast)
        {
            if (_PagingMode == DataTablePagingMode.None)
                return false;

            int target = Math.Max(1, Math.Min(pageNumber, PageCount));
            if (target == _PageNumber)
                return false;

            if (_PagingMode == DataTablePagingMode.Remote)
            {
                _PendingSelectLast = selectLast;
                PageRequested?.Invoke(this, BuildRequest(target));
                return true;
            }

            int before = SelectedIndex;
            _PageNumber = target;
            _Selected = selectLast ? Math.Max(RangeStart(), RangeEnd() - 1) : RangeStart();
            _Top = RangeStart();
            RaiseSelection(before);
            return true;
        }

        private DataTablePageRequest BuildRequest(int pageNumber)
        {
            string? key = _SortColumn >= 0 && _SortColumn < _Columns.Count ? _Columns[_SortColumn].Key : null;
            return new DataTablePageRequest(Math.Max(1, pageNumber), _PageSize, _SortColumn, key, _SortAscending);
        }

        private void FollowSelectionToPage()
        {
            if (_Rows.Count == 0)
            {
                _PageNumber = 1;
                return;
            }

            _PageNumber = Math.Max(1, _Selected / _PageSize + 1);
        }

        private void ClampLocalPage()
        {
            _PageNumber = Math.Max(1, Math.Min(_PageNumber, PageCount));
            if (_Rows.Count > 0 && (_Selected < RangeStart() || _Selected >= RangeEnd()))
                SetSelected(RangeStart());
        }

        private int RangeStart()
        {
            if (_PagingMode != DataTablePagingMode.Local)
                return 0;

            return Math.Min(_Rows.Count, (_PageNumber - 1) * _PageSize);
        }

        private int RangeEnd()
        {
            if (_PagingMode != DataTablePagingMode.Local)
                return _Rows.Count;

            return Math.Min(_Rows.Count, RangeStart() + _PageSize);
        }

        private bool SortsLocally()
        {
            return SortLocally && _PagingMode != DataTablePagingMode.Remote;
        }

        private bool ShowsBar()
        {
            return ShowPagingBar && _PagingMode != DataTablePagingMode.None;
        }

        private void DrawBar(ISurface surface, int width, int y)
        {
            long total = TotalRows;
            int shown = RangeEnd() - RangeStart();
            long firstRow = _PagingMode == DataTablePagingMode.Remote ? (long)(_PageNumber - 1) * _PageSize : RangeStart();
            long from = shown > 0 ? firstRow + 1 : 0;
            long to = shown > 0 ? firstRow + shown : 0;
            string text = total < 0
                ? string.Format(CultureInfo.CurrentCulture, _UnknownTotalFormat, from, to, string.Empty, _PageNumber, string.Empty)
                : string.Format(CultureInfo.CurrentCulture, _PagingFormat, from, to, total, _PageNumber, PageCount);
            if (_State == DataTableState.Loading && _RefreshingText.Length > 0)
                text += " " + _RefreshingText;

            CellStyle style = PagingBarStyle.Over(NormalStyle);
            surface.Fill(new Rect(0, y, width, 1), Cell.Blank(NormalStyle));
            int room = Math.Max(0, width - 4);
            surface.DrawText(0, y, TextFit.Ellipsize(text, room), style);
            if (width >= 3)
            {
                surface.DrawText(width - 3, y, "<", _PageNumber > 1 ? style.WithAttribute(CellAttributes.Bold, true) : style.WithAttribute(CellAttributes.Dim, true));
                surface.DrawText(width - 1, y, ">", _PageNumber < PageCount ? style.WithAttribute(CellAttributes.Bold, true) : style.WithAttribute(CellAttributes.Dim, true));
            }
        }

        private bool TryColumnKey(KeyEvent key, KeyChord chord, out bool consumed)
        {
            consumed = false;
            if ((key.Code == KeyCode.Left || key.Code == KeyCode.Right) && key.Modifiers == KeyModifiers.None)
            {
                int next = NextVisibleColumn(_FocusedColumn, key.Code == KeyCode.Right ? 1 : -1);
                if (next >= 0)
                    _FocusedColumn = next;
                consumed = true;
                return true;
            }

            bool reverse = ReverseSortKey.HasValue && Matches(key, chord, ReverseSortKey.Value);
            bool sort = !reverse && SortKey.HasValue && Matches(key, chord, SortKey.Value);
            if (!sort && !reverse)
                return false;

            consumed = CanSortFocused();
            if (consumed && sort)
                SortFocusedColumn();
            else if (consumed)
                SortFocusedColumn(false);
            return true;
        }

        private static bool Matches(KeyEvent key, KeyChord chord, KeyChord target)
        {
            // Chords store letters lower-cased, so an upper-case letter also matches its shift+letter chord.
            return chord.Equals(target)
                || (key.Code == KeyCode.Character && key.Rune >= 'A' && key.Rune <= 'Z'
                    && target.Equals(new KeyChord(KeyCode.Character, key.Rune, key.Modifiers | KeyModifiers.Shift)));
        }

        private bool CanSortFocused()
        {
            return _ShowColumnCursor && _FocusedColumn >= 0 && _FocusedColumn < _Columns.Count
                && IsColumnShown(_FocusedColumn) && _Columns[_FocusedColumn].Sortable;
        }

        private bool IsColumnShown(int index)
        {
            return index >= 0 && index < _Columns.Count && _Columns[index].Visible;
        }

        private int NextVisibleColumn(int from, int direction)
        {
            for (int c = from + direction; c >= 0 && c < _Columns.Count; c += direction)
            {
                if (_Columns[c].Visible)
                    return c;
            }

            return from >= 0 && IsColumnShown(from) ? from : -1;
        }

        private int RequireColumn(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            int index = _Columns.FindIndex(c => string.Equals(c.Key, key, StringComparison.Ordinal));
            if (index < 0)
                throw new ArgumentException("No column has key '" + key + "'.", nameof(key));
            return index;
        }

        private void OnColumnsChanged()
        {
            if (_ShowColumnCursor && !IsColumnShown(_FocusedColumn))
            {
                int next = NextVisibleColumn(_FocusedColumn, 1);
                _FocusedColumn = next >= 0 ? next : NextVisibleColumn(_Columns.Count, -1);
            }

            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Layout(int width)
        {
            _ColumnX.Clear();
            _ColumnWidths.Clear();
            int count = _Columns.Count;
            int visibleCount = 0;
            int fixedTotal = 0;
            int weightTotal = 0;
            bool custom = false;
            for (int c = 0; c < count; c++)
            {
                DataColumn<T> column = _Columns[c];
                if (!column.Visible)
                    continue;

                visibleCount++;
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
            int lastVisible = -1;
            if (visibleCount > 0 && !custom)
            {
                // The original layout: equal widths of width / count, leftover columns unused.
                int equal = Math.Max(1, width / visibleCount);
                for (int c = 0; c < count; c++)
                {
                    if (!_Columns[c].Visible)
                        continue;
                    widths[c] = equal;
                    lastVisible = c;
                }
            }
            else if (visibleCount > 0)
            {
                int remaining = Math.Max(0, width - fixedTotal);
                int lastWeighted = -1;
                int used = 0;
                for (int c = 0; c < count; c++)
                {
                    DataColumn<T> column = _Columns[c];
                    if (!column.Visible)
                        continue;

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

            if (StretchLastColumn && !custom && lastVisible >= 0)
                widths[lastVisible] += Math.Max(0, width - (widths[lastVisible] * visibleCount));

            int x = 0;
            for (int c = 0; c < count; c++)
            {
                int w = x >= width || !_Columns[c].Visible ? 0 : Math.Min(widths[c], width - x);
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
            if (_PagingMode == DataTablePagingMode.None && _Rows.Count > 0 && _Selected >= _Rows.Count - 1 - Math.Max(0, EndReachedThreshold))
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
            Func<T, string>? key = KeySelector;
            if (key != null && _PagingMode == DataTablePagingMode.Remote)
            {
                for (int i = 0; i < _Rows.Count; i++)
                {
                    string k = key(_Rows[i]);
                    if (_Marks[i])
                        _RemoteMarkedKeys.Add(k);
                    else
                        _RemoteMarkedKeys.Remove(k);
                }
            }

            MarkedChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static string ValidateFormat(string value, int argumentCount, bool requireZero, string name)
        {
            if (value == null)
                throw new ArgumentNullException(name);
            if (requireZero && value.IndexOf("{0", StringComparison.Ordinal) < 0)
                throw new ArgumentException(name + " must contain {0}.", name);

            object[] probe = new object[argumentCount];
            for (int i = 0; i < argumentCount; i++)
                probe[i] = i;

            try
            {
                string.Format(CultureInfo.InvariantCulture, value, probe);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException(name + " is not a valid composite format: " + ex.Message, name, ex);
            }

            return value;
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

        #endregion
    }
}
