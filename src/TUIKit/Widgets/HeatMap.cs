namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A grid of intensity cells — rows by columns of values shaded by magnitude — for two-dimensional
    /// density such as activity by hour-of-day across a week. Each cell is drawn with a shaded glyph
    /// (<c>░▒▓█</c>) chosen from the cell's normalized magnitude, tinted with <see cref="CellColor"/>.
    /// Optional row and column labels offset the grid.
    /// </summary>
    /// <remarks>
    /// Glyph shading (rather than a two-color gradient) was chosen because it reads clearly on every
    /// terminal color depth and is deterministic to test against the headless buffer. An empty or
    /// zero-size grid renders nothing. Opt-in features: a selection the user moves with the keyboard and mouse
    /// (<see cref="IsSelectable"/>), discrete levels with their own glyphs and styles (<see cref="LevelCount"/>), a
    /// legend (<see cref="ShowLegend"/>), wide cells (<see cref="CellWidth"/>), labels that span columns
    /// (<see cref="SpanColumnLabels"/>), cell descriptions as tooltips or a footer (<see cref="CellDescription"/>),
    /// and theme styles (<see cref="UseThemeStyles"/>). While not selectable the map takes no keys, clicks, or
    /// focus, as in 1.9. This type is not thread-safe.
    /// </remarks>
    public sealed class HeatMap : IWidget, IFocusable, IFocusStop, IFocusAware, IMouseAware, IKeyHintSource, IThemeable, ITooltipProvider, IFocusChildren
    {
        #region Private-Members

        private static readonly string[] _Shades = { "\u2591", "\u2592", "\u2593", "\u2588" };

        private double[,] _Cells = new double[0, 0];
        private readonly List<string> _RowLabels = new List<string>();
        private readonly List<string> _ColumnLabels = new List<string>();
        private bool _HasRange;
        private double _RangeMin;
        private double _RangeMax;
        private int _LevelCount;
        private IReadOnlyList<string>? _LevelGlyphs;
        private IReadOnlyList<CellStyle>? _LevelStyles;
        private int _CellWidth = 1;
        private int _SelectedRow;
        private int _SelectedColumn;
        private int _FirstVisibleColumn;
        private bool _Focused;
        private Theme? _Theme;
        private string _LegendLowText = "Less";
        private string _LegendHighText = "More";
        private int _LastGutter;
        private int _LastHeader;
        private int _LastVisibleColumns;

        #endregion

        #region Public-Members

        /// <summary>
        /// Raised when the selected cell changes, from keys, clicks, or <see cref="Select"/>.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Raised when Enter or a double click activates the selected cell. Enter is consumed only while a handler is
        /// attached.
        /// </summary>
        public event EventHandler? CellActivated;

        /// <summary>
        /// Gets or sets the color the shade glyphs are drawn in. Defaults to palette green.
        /// </summary>
        public Color CellColor { get; set; } = Color.FromPalette(2);

        /// <summary>
        /// Gets or sets the color of row and column labels. Defaults to palette gray.
        /// </summary>
        public Color LabelColor { get; set; } = Color.FromPalette(8);

        /// <summary>
        /// Gets or sets the style the selected cell is drawn in, so it shows without color. Defaults to reverse video.
        /// </summary>
        public CellStyle SelectionStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Reverse, true);

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
        /// Gets or sets a value indicating whether the user can select cells with the arrow keys, Home and End, and the
        /// mouse. Defaults to false: the map takes no keys, clicks, or focus (the 1.9 behavior).
        /// </summary>
        public bool IsSelectable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Tab stops on the map while it is selectable. Defaults to false (the
        /// rule for widgets that newly become focusable); a click still focuses a selectable map.
        /// </summary>
        public bool IsFocusStop { get; set; }

        /// <summary>
        /// Gets a value indicating whether the map has keyboard focus, as last reported through <see cref="OnFocusChanged"/>.
        /// </summary>
        public bool IsFocused
        {
            get { return _Focused; }
        }

        /// <summary>
        /// Gets the selected row (0 when the grid is empty).
        /// </summary>
        public int SelectedRow
        {
            get { return _SelectedRow; }
        }

        /// <summary>
        /// Gets the selected column (0 when the grid is empty).
        /// </summary>
        public int SelectedColumn
        {
            get { return _SelectedColumn; }
        }

        /// <summary>
        /// Gets a value indicating whether any cell can be selected (the grid is not empty and
        /// <see cref="IsCellSelectable"/> allows at least one cell).
        /// </summary>
        public bool HasSelection
        {
            get { return Rows > 0 && Columns > 0 && CanSelect(_SelectedRow, _SelectedColumn); }
        }

        /// <summary>
        /// Gets or sets a predicate (row, column) deciding which cells can be selected, or null for every cell (the
        /// default). Arrow keys skip unselectable cells in the direction of travel and stop at the edge when none
        /// remains, Home and End go to the first and last selectable cell of the row, clicks on them are ignored, and
        /// <see cref="Select"/> moves to the nearest selectable cell. Use it for empty slots in a sparse grid.
        /// </summary>
        public Func<int, int, bool>? IsCellSelectable { get; set; }

        /// <summary>
        /// Gets or sets a navigation override (row, column, key) asked first for every navigation key while selectable.
        /// A non-null result is the new cell (then clamped and checked with <see cref="IsCellSelectable"/>); null falls
        /// back to the default movement. Lets a composing widget define movement such as "the next day wraps to the next
        /// week". Defaults to null.
        /// </summary>
        public Func<int, int, KeyEvent, Point?>? NavigateOverride { get; set; }

        /// <summary>
        /// Gets or sets the number of discrete levels, or 0 for continuous shading (the default, the 1.9 behavior). With
        /// levels, a value at or below the range minimum is level 0 and the rest of the range splits into equal steps.
        /// Otherwise 2 to 16. Changing it clears <see cref="LevelGlyphs"/> and <see cref="LevelStyles"/> when their
        /// length no longer matches.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value other than 0 or 2 to 16.</exception>
        public int LevelCount
        {
            get { return _LevelCount; }
            set
            {
                if (value != 0 && (value < 2 || value > 16))
                    throw new ArgumentOutOfRangeException(nameof(value), "LevelCount must be 0 (continuous) or 2 to 16.");

                _LevelCount = value;
                if (_LevelGlyphs != null && _LevelGlyphs.Count != value)
                    _LevelGlyphs = null;
                if (_LevelStyles != null && _LevelStyles.Count != value)
                    _LevelStyles = null;
            }
        }

        /// <summary>
        /// Gets or sets one glyph per level, or null to fall back to the shade glyphs. Distinct glyphs (for example
        /// <c>" "</c>, <c>"."</c>, <c>":"</c>, <c>"+"</c>, <c>"#"</c>) keep the map readable without color.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the list does not hold exactly <see cref="LevelCount"/> entries or holds a null.</exception>
        public IReadOnlyList<string>? LevelGlyphs
        {
            get { return _LevelGlyphs; }
            set
            {
                if (value != null && (value.Count != _LevelCount || value.Any(g => g == null)))
                    throw new ArgumentException("LevelGlyphs must hold exactly LevelCount glyphs and no null.", nameof(value));
                _LevelGlyphs = value?.ToList();
            }
        }

        /// <summary>
        /// Gets or sets one style per level, or null to fall back to <see cref="CellColor"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the list does not hold exactly <see cref="LevelCount"/> entries.</exception>
        public IReadOnlyList<CellStyle>? LevelStyles
        {
            get { return _LevelStyles; }
            set
            {
                if (value != null && value.Count != _LevelCount)
                    throw new ArgumentException("LevelStyles must hold exactly LevelCount styles.", nameof(value));
                _LevelStyles = value?.ToList();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a legend row is drawn below the grid: <see cref="LegendLowText"/>, each
        /// level's glyph in its style, and <see cref="LegendHighText"/>. Defaults to false.
        /// </summary>
        public bool ShowLegend { get; set; }

        /// <summary>
        /// Gets or sets the legend's low-end text. Defaults to <c>"Less"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string LegendLowText
        {
            get { return _LegendLowText; }
            set { _LegendLowText = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the legend's high-end text. Defaults to <c>"More"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string LegendHighText
        {
            get { return _LegendHighText; }
            set { _LegendHighText = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the width of each cell in columns: the glyph is repeated to fill it, which gives column labels
        /// room. Defaults to 1; 1 to 4.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 4.</exception>
        public int CellWidth
        {
            get { return _CellWidth; }
            set
            {
                if (value < 1 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value), "CellWidth must be 1 to 4.");
                _CellWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a column label is drawn in full from its column and may run over
        /// following columns whose labels are empty (month names over weeks), truncated where the next non-empty label
        /// starts. Defaults to false: each column shows the start of its own label.
        /// </summary>
        public bool SpanColumnLabels { get; set; }

        /// <summary>
        /// Gets or sets a function (row, column) returning a description of a cell, or null for none (the default). It
        /// serves as the tooltip (<see cref="ITooltipProvider"/>) and, with <see cref="ShowSelectionDescription"/>, as a
        /// footer for the selected cell.
        /// </summary>
        public Func<int, int, string?>? CellDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the selected cell's description is drawn on a footer row while
        /// selectable. Defaults to false.
        /// </summary>
        public bool ShowSelectionDescription { get; set; }

        /// <summary>
        /// Gets the first column shown, which follows the selection when the grid is wider than the widget. 0 while not
        /// selectable.
        /// </summary>
        public int FirstVisibleColumn
        {
            get { return _FirstVisibleColumn; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the map follows the theme passed to <see cref="ApplyTheme"/>. Defaults
        /// to false: <see cref="ApplyTheme"/> only remembers the theme. While true, labels draw from
        /// <see cref="Theme.Muted"/>, the selection from <see cref="Theme.Selection"/>, and cells (without
        /// <see cref="LevelStyles"/>) from <see cref="Theme.Success"/>.
        /// </summary>
        public bool UseThemeStyles { get; set; }

        internal Func<int, int, bool>? CellBlank { get; set; }

        bool IFocusChildren.HasFocusableChild
        {
            get { return IsSelectable; }
        }

        bool IFocusChildren.HasTabStopChild
        {
            get { return IsSelectable && IsFocusStop; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Replaces the grid values. A copy is taken, so later mutations of the caller's array do not
        /// affect the widget. The selection is clamped to the new grid.
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
            _SelectedRow = Math.Max(0, Math.Min(_SelectedRow, rows - 1));
            _SelectedColumn = Math.Max(0, Math.Min(_SelectedColumn, columns - 1));
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
        /// Sets the column labels, drawn in a header row (the start of each label per column, or in full with
        /// <see cref="SpanColumnLabels"/>). Pass an empty sequence to clear them.
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

        /// <summary>
        /// Gets the level of a cell: with <see cref="LevelCount"/> set, 0 to LevelCount - 1; with continuous shading, the
        /// shade index 0 to 3.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        /// <returns>The level.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is outside the grid.</exception>
        public int LevelOf(int row, int column)
        {
            if (row < 0 || row >= Rows || column < 0 || column >= Columns)
                throw new ArgumentOutOfRangeException(nameof(row), "The cell must be inside the grid.");

            ResolveRange(out double min, out double max);
            return Level(_Cells[row, column], min, max);
        }

        /// <summary>
        /// Selects a cell, clamped to the grid, moving to the nearest selectable cell (searching forward through the
        /// grid, then backward) when <see cref="IsCellSelectable"/> refuses it; unchanged when no cell is selectable.
        /// Works whether or not <see cref="IsSelectable"/> is on.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="column">The column.</param>
        public void Select(int row, int column)
        {
            if (Rows == 0 || Columns == 0)
                return;

            int r = Math.Max(0, Math.Min(Rows - 1, row));
            int c = Math.Max(0, Math.Min(Columns - 1, column));
            if (!CanSelect(r, c) && !Nearest(ref r, ref c))
                return;

            SetSelection(r, c);
        }

        /// <summary>
        /// Moves the selection with the arrow keys and Home and End (the first and last selectable cell of the row), and
        /// raises <see cref="CellActivated"/> on Enter while a handler is attached. Nothing is consumed while
        /// <see cref="IsSelectable"/> is false.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!IsSelectable || Rows == 0 || Columns == 0 || key.Modifiers != KeyModifiers.None)
                return false;

            if (key.Code == KeyCode.Enter)
            {
                EventHandler? activated = CellActivated;
                if (activated == null)
                    return false;
                activated(this, EventArgs.Empty);
                return true;
            }

            if (key.Code != KeyCode.Up && key.Code != KeyCode.Down && key.Code != KeyCode.Left && key.Code != KeyCode.Right
                && key.Code != KeyCode.Home && key.Code != KeyCode.End)
                return false;

            Func<int, int, KeyEvent, Point?>? navigate = NavigateOverride;
            Point? target = navigate?.Invoke(_SelectedRow, _SelectedColumn, key);
            if (target.HasValue)
            {
                int r = Math.Max(0, Math.Min(Rows - 1, target.Value.Y));
                int c = Math.Max(0, Math.Min(Columns - 1, target.Value.X));
                if (CanSelect(r, c))
                    SetSelection(r, c);
                return true;
            }

            switch (key.Code)
            {
                case KeyCode.Up:
                    Step(-1, 0);
                    break;
                case KeyCode.Down:
                    Step(1, 0);
                    break;
                case KeyCode.Left:
                    Step(0, -1);
                    break;
                case KeyCode.Right:
                    Step(0, 1);
                    break;
                case KeyCode.Home:
                    Edge(_SelectedRow, true);
                    break;
                case KeyCode.End:
                    Edge(_SelectedRow, false);
                    break;
            }

            return true;
        }

        /// <summary>
        /// Selects the selectable cell under a left press and activates it on a double click. Nothing is consumed while
        /// <see cref="IsSelectable"/> is false. Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns><c>true</c> when a cell was selected; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!IsSelectable || mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left)
                return false;

            if (!TryCellAt(mouse.X, mouse.Y, out int row, out int column) || !CanSelect(row, column))
                return false;

            SetSelection(row, column);
            if (mouse.ClickCount >= 2)
                CellActivated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Gets the description of the cell under the pointer from <see cref="CellDescription"/>. Part of
        /// <see cref="ITooltipProvider"/>.
        /// </summary>
        /// <param name="x">The column, widget-local.</param>
        /// <param name="y">The row, widget-local.</param>
        /// <returns>The description, or null.</returns>
        public string? GetTooltip(int x, int y)
        {
            Func<int, int, string?>? describe = CellDescription;
            if (describe == null || !TryCellAt(x, y, out int row, out int column))
                return null;
            return describe(row, column);
        }

        /// <summary>
        /// Records the focused state. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the map gained focus.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
        }

        /// <summary>
        /// Gets the map's keys while selectable and focused: arrows to move and Enter while <see cref="CellActivated"/> has
        /// a handler. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null when not selectable or not focused.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!IsSelectable || !_Focused)
                return null;

            List<KeyHint> hints = new List<KeyHint>();
            if (CellActivated != null)
                hints.Add(new KeyHint(new KeyChord(KeyCode.Enter, 0, KeyModifiers.None), "Open", 10));
            hints.Add(new KeyHint("Arrows", "Move"));
            return hints;
        }

        /// <summary>
        /// Remembers the theme; while <see cref="UseThemeStyles"/> is true the map draws with it. Part of <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? throw new ArgumentNullException(nameof(theme));
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
            int footer = (ShowLegend ? 1 : 0) + (ShowsDescription() ? 1 : 0);
            int desiredWidth = Math.Min(available.Width, gutter + columns * _CellWidth);
            int desiredHeight = Math.Min(available.Height, header + rows + footer);
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
            int visibleColumns = Math.Max(1, (width - gutter) / _CellWidth);
            _LastGutter = gutter;
            _LastHeader = header;
            _LastVisibleColumns = visibleColumns;
            if (IsSelectable && columns > visibleColumns)
            {
                if (_SelectedColumn < _FirstVisibleColumn)
                    _FirstVisibleColumn = _SelectedColumn;
                else if (_SelectedColumn >= _FirstVisibleColumn + visibleColumns)
                    _FirstVisibleColumn = _SelectedColumn - visibleColumns + 1;
                _FirstVisibleColumn = Math.Max(0, Math.Min(_FirstVisibleColumn, columns - visibleColumns));
            }
            else
            {
                _FirstVisibleColumn = 0;
            }

            ResolveRange(out double min, out double max);
            Theme? theme = UseThemeStyles ? _Theme : null;
            CellStyle labelStyle = theme != null ? theme.Muted : CellStyle.Default.WithForeground(LabelColor);
            CellStyle cellStyle = theme != null ? theme.Success : CellStyle.Default.WithForeground(CellColor);
            CellStyle selection = theme != null ? theme.Selection : SelectionStyle;

            if (header == 1)
                DrawColumnLabels(surface, gutter, width, labelStyle);

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

                for (int c = _FirstVisibleColumn; c < columns; c++)
                {
                    int x = gutter + (c - _FirstVisibleColumn) * _CellWidth;
                    if (x >= width)
                        break;

                    Func<int, int, bool>? blank = CellBlank;
                    if (blank != null && blank(r, c))
                        continue;

                    int level = Level(_Cells[r, c], min, max);
                    string glyph = GlyphOf(level);
                    CellStyle style = StyleOf(level, cellStyle);
                    if (IsSelectable && r == _SelectedRow && c == _SelectedColumn && HasSelection)
                        style = selection.Over(style);

                    for (int k = 0; k < _CellWidth && x + k < width; k++)
                        surface.Set(x + k, y, Cell.Glyph(glyph, style, 1));
                }
            }

            int footerY = header + rows;
            if (ShowLegend && footerY < height)
            {
                DrawLegend(surface, gutter, footerY, width, labelStyle, cellStyle);
                footerY++;
            }

            if (ShowsDescription() && footerY < height && HasSelection)
            {
                string? text = CellDescription!(_SelectedRow, _SelectedColumn);
                if (!string.IsNullOrEmpty(text))
                    surface.DrawText(0, footerY, TextFit.Ellipsize(text, width), labelStyle);
            }
        }

        #endregion

        #region Private-Methods

        private bool ShowsDescription()
        {
            return ShowSelectionDescription && IsSelectable && CellDescription != null;
        }

        private void DrawColumnLabels(ISurface surface, int gutter, int width, CellStyle labelStyle)
        {
            int columns = Columns;
            for (int c = _FirstVisibleColumn; c < columns; c++)
            {
                int x = gutter + (c - _FirstVisibleColumn) * _CellWidth;
                if (x >= width)
                    break;
                if (c >= _ColumnLabels.Count || _ColumnLabels[c].Length == 0)
                    continue;

                string label = _ColumnLabels[c];
                if (!SpanColumnLabels)
                {
                    string shown = _CellWidth == 1 ? label.Substring(0, 1) : TextFit.Truncate(label, _CellWidth);
                    surface.DrawText(x, 0, shown, labelStyle);
                    continue;
                }

                int next = c + 1;
                while (next < columns && (next >= _ColumnLabels.Count || _ColumnLabels[next].Length == 0))
                    next++;
                int room = Math.Min(width - x, (next - c) * _CellWidth);
                surface.DrawText(x, 0, TextFit.Truncate(label, room), labelStyle);
            }
        }

        private void DrawLegend(ISurface surface, int gutter, int y, int width, CellStyle labelStyle, CellStyle cellStyle)
        {
            int x = gutter;
            x += surface.DrawText(x, y, TextFit.Truncate(_LegendLowText, Math.Max(0, width - x)), labelStyle);
            int levels = _LevelCount > 0 ? _LevelCount : _Shades.Length;
            for (int level = 0; level < levels && x + 1 < width; level++)
            {
                x++;
                surface.Set(x, y, Cell.Glyph(GlyphOf(level), StyleOf(level, cellStyle), 1));
                x++;
            }

            if (x + 1 < width)
                surface.DrawText(x + 1, y, TextFit.Truncate(_LegendHighText, width - x - 1), labelStyle);
        }

        private int Level(double raw, double min, double max)
        {
            double value = Sanitize(raw, min, max);
            double span = max - min;
            if (_LevelCount == 0)
            {
                double normalized = span <= 0 ? 0.0 : (value - min) / span;
                int index = (int)Math.Round(normalized * (_Shades.Length - 1), MidpointRounding.AwayFromZero);
                return Math.Max(0, Math.Min(_Shades.Length - 1, index));
            }

            if (span <= 0 || value <= min)
                return 0;

            double fraction = (value - min) / span;
            int level = (int)Math.Ceiling(fraction * (_LevelCount - 1) - 1e-9);
            return Math.Max(1, Math.Min(_LevelCount - 1, level));
        }

        private string GlyphOf(int level)
        {
            if (_LevelCount == 0)
                return _Shades[level];
            if (_LevelGlyphs != null)
                return _LevelGlyphs[level];

            int shade = (int)Math.Round((double)level / (_LevelCount - 1) * (_Shades.Length - 1), MidpointRounding.AwayFromZero);
            return _Shades[Math.Max(0, Math.Min(_Shades.Length - 1, shade))];
        }

        private CellStyle StyleOf(int level, CellStyle fallback)
        {
            return _LevelCount > 0 && _LevelStyles != null ? _LevelStyles[level] : fallback;
        }

        private bool TryCellAt(int x, int y, out int row, out int column)
        {
            row = y - _LastHeader;
            column = x < _LastGutter ? -1 : _FirstVisibleColumn + (x - _LastGutter) / _CellWidth;
            return row >= 0 && row < Rows && column >= 0 && column < Columns && column < _FirstVisibleColumn + Math.Max(1, _LastVisibleColumns);
        }

        private bool CanSelect(int row, int column)
        {
            Func<int, int, bool>? predicate = IsCellSelectable;
            return predicate == null || predicate(row, column);
        }

        private void Step(int dr, int dc)
        {
            int r = _SelectedRow + dr;
            int c = _SelectedColumn + dc;
            while (r >= 0 && r < Rows && c >= 0 && c < Columns)
            {
                if (CanSelect(r, c))
                {
                    SetSelection(r, c);
                    return;
                }

                r += dr;
                c += dc;
            }
        }

        private void Edge(int row, bool first)
        {
            int direction = first ? 1 : -1;
            for (int c = first ? 0 : Columns - 1; c >= 0 && c < Columns; c += direction)
            {
                if (CanSelect(row, c))
                {
                    SetSelection(row, c);
                    return;
                }
            }
        }

        private bool Nearest(ref int row, ref int column)
        {
            int total = Rows * Columns;
            int start = row * Columns + column;
            for (int offset = 1; offset < total; offset++)
            {
                int forward = start + offset;
                if (forward < total && CanSelect(forward / Columns, forward % Columns))
                {
                    row = forward / Columns;
                    column = forward % Columns;
                    return true;
                }
            }

            for (int offset = 1; offset < total; offset++)
            {
                int backward = start - offset;
                if (backward >= 0 && CanSelect(backward / Columns, backward % Columns))
                {
                    row = backward / Columns;
                    column = backward % Columns;
                    return true;
                }
            }

            return false;
        }

        private void SetSelection(int row, int column)
        {
            if (row == _SelectedRow && column == _SelectedColumn)
                return;

            _SelectedRow = row;
            _SelectedColumn = column;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private int RowLabelGutter()
        {
            int gutter = 0;
            for (int i = 0; i < _RowLabels.Count; i++)
            {
                if (TextFit.Width(_RowLabels[i]) > gutter)
                    gutter = TextFit.Width(_RowLabels[i]);
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
            return TextFit.PadRight(TextFit.Ellipsize(text, width), width);
        }

        #endregion
    }
}
