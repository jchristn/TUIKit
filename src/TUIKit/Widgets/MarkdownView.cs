namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A widget that shows Markdown laid out at its own width: tables sized from their content, wrapped lists
    /// and quotes that keep their indent, and highlighted code. Use it for Markdown that is not part of a
    /// scrolling log, such as a help page, a details panel, or a modal body; for a transcript use
    /// <see cref="Pane.WriteMarkdown(string, MarkdownRenderOptions)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view scrolls with the mouse wheel, with <see cref="ScrollOffset"/> and the scroll methods, and, while
    /// it has keyboard focus, with Up/Down, PageUp/PageDown, and Home/End. It is focusable but not a Tab stop by
    /// default; set <see cref="IsFocusStop"/> to make Tab reach it. <see cref="SetSearch"/> highlights and finds
    /// text, <see cref="UseThemeStyles"/> makes it follow the application theme, and <see cref="TailFollow"/>
    /// keeps a growing document at its end.
    /// </para>
    /// <para>
    /// Layout is cached per width and recomputed when the width, <see cref="Markdown"/>, or
    /// <see cref="Options"/> changes. The options are copied when set; after editing a copy obtained from
    /// <see cref="Options"/> in place, call <see cref="Invalidate"/>. Not thread-safe: use it from the UI thread.
    /// </para>
    /// </remarks>
    public sealed class MarkdownView : IWidget, IScrollExtent, IMouseAware, IFocusable, IFocusStop, IFocusAware, IKeyHintSource, IThemeable
    {
        private string _Markdown;
        private MarkdownRenderOptions _Options;
        private MarkdownRenderOptions? _ThemedOptions;
        private Theme? _Theme;
        private bool _UseThemeStyles;
        private int _ScrollOffset;
        private int _WheelStep = 3;
        private int _CachedWidth = -1;
        private IReadOnlyList<StyledText>? _CachedRows;
        private int _ViewportHeight;
        private bool _IsFocused;
        private string? _Search;
        private int _MatchRow = -1;
        private CellStyle _SearchStyle = SearchHighlighter.DefaultStyle;
        private readonly TailFollow _Follow = new TailFollow();
        private int _CountedRows = -1;
        private int _CountedWidth = -1;
        private bool _ContentChanged;
        private Rect _IndicatorRect;
        private TailFollowMode _SeenMode = TailFollowMode.Never;

        /// <summary>
        /// Initializes an empty view with default <see cref="MarkdownRenderOptions"/>.
        /// </summary>
        public MarkdownView()
            : this(string.Empty, new MarkdownRenderOptions())
        {
        }

        /// <summary>
        /// Initializes a view showing the given Markdown with default <see cref="MarkdownRenderOptions"/>.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
        public MarkdownView(string markdown)
            : this(markdown, new MarkdownRenderOptions())
        {
        }

        /// <summary>
        /// Initializes a view showing the given Markdown with the given options, which are copied.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="options">How the Markdown should look. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public MarkdownView(string markdown, MarkdownRenderOptions options)
        {
            _Markdown = markdown ?? throw new ArgumentNullException(nameof(markdown));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _Options = options.Clone();
            _Follow.Mode = TailFollowMode.Never;
        }

        /// <summary>
        /// Gets or sets the Markdown source. Defaults to empty. Setting it keeps <see cref="ScrollOffset"/>, which
        /// is clamped at the next render, unless <see cref="TailFollow"/> is following the end. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Markdown
        {
            get { return _Markdown; }
            set
            {
                _Markdown = value ?? throw new ArgumentNullException(nameof(value));
                _ContentChanged = true;
                DropLayout();
            }
        }

        /// <summary>
        /// Gets or sets how the Markdown should look. The value is copied on set, so later changes to the
        /// instance you passed have no effect. Getting returns the view's own copy; after changing it in place,
        /// call <see cref="Invalidate"/>. Must not be null.
        /// </summary>
        /// <remarks>
        /// These are the caller's base options. While <see cref="UseThemeStyles"/> is true and a theme has been
        /// applied, the view renders with <c>Options.WithTheme(theme)</c> instead, but this property still returns
        /// the base options, so reading and reassigning it never captures themed values.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownRenderOptions Options
        {
            get { return _Options; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _Options = value.Clone();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the view follows the application theme. Defaults to false,
        /// which renders exactly with <see cref="Options"/>. While true and once a theme has been applied
        /// (<see cref="ApplyTheme"/>, called by the host and by <see cref="ThemeApplier"/>), the view renders with
        /// <see cref="MarkdownRenderOptions.WithTheme"/> of <see cref="Options"/>: theme colors without theme
        /// backgrounds, plus ASCII glyphs on a theme with <see cref="Theme.UseAsciiBorders"/>. Each theme is
        /// derived from the base options, so switching from an ASCII theme back to a Unicode one restores the base
        /// glyphs. While false, an applied theme is only recorded, so setting this true later applies it at once;
        /// setting it false again returns to the base options.
        /// </summary>
        public bool UseThemeStyles
        {
            get { return _UseThemeStyles; }
            set
            {
                if (_UseThemeStyles == value)
                    return;

                _UseThemeStyles = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the style used to clear the view before drawing. Defaults to <see cref="CellStyle.Default"/>.
        /// Caller-owned: a theme never changes it.
        /// </summary>
        public CellStyle Background { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the first rendered row shown at the top of the view. Defaults to 0. Must be 0 or greater.
        /// After a render the value is clamped at once against <see cref="ContentHeight"/> and
        /// <see cref="ViewportHeight"/>, so the last rows stay visible; before the first render it is stored as given
        /// and clamped by the first render.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int ScrollOffset
        {
            get { return _ScrollOffset; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ScrollOffset must be 0 or greater.");

                MoveTo(value);
            }
        }

        /// <summary>
        /// Gets or sets the number of rows one mouse wheel notch scrolls. Defaults to 3. Minimum 1, maximum 100.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 100.</exception>
        public int WheelStep
        {
            get { return _WheelStep; }
            set
            {
                if (value < 1 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "WheelStep must be between 1 and 100.");

                _WheelStep = value;
            }
        }

        /// <summary>
        /// Gets the number of rows the Markdown occupies in the last layout, made by a render, a measure, or
        /// <see cref="RowsAt"/>. It is 0 before the first of those and after <see cref="Markdown"/> or the options
        /// change, until the next layout. To know the height up front, call <c>RowsAt(width).Count</c>.
        /// </summary>
        public int ContentHeight
        {
            get { return _CachedRows == null ? 0 : _CachedRows.Count; }
        }

        /// <summary>
        /// Gets the height of the last render in rows, or 0 before the first render. Paging moves by this height
        /// minus one row.
        /// </summary>
        public int ViewportHeight
        {
            get { return _ViewportHeight; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Tab and Shift+Tab stop on the view. Defaults to false: traversal
        /// skips it and the host never gives it focus on its own, while <see cref="FocusScope.SetFocus(IFocusable)"/>
        /// and a click on a view bound directly to a region still focus it. Set it to true to make the view a Tab
        /// stop, for example a help page the user reads with the keyboard. Part of <see cref="IFocusStop"/>.
        /// </summary>
        public bool IsFocusStop { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="GetKeyHints"/> describes the scrolling keys. Defaults
        /// to true. False always returns no hints.
        /// </summary>
        public bool ShowKeyHints { get; set; } = true;

        /// <summary>
        /// Gets a value indicating whether the view has keyboard focus, as last reported through
        /// <see cref="OnFocusChanged"/>. False for a new view.
        /// </summary>
        public bool IsFocused
        {
            get { return _IsFocused; }
        }

        /// <summary>
        /// Gets the active search term set by <see cref="SetSearch"/>, or null when no search is set.
        /// </summary>
        public string? SearchText
        {
            get { return _Search; }
        }

        /// <summary>
        /// Gets the tail-follow state that keeps a growing document at its end, the same type and contract as
        /// <see cref="Pane.TailFollow"/>. Never null. A new view sets its mode to <see cref="TailFollowMode.Never"/>,
        /// so the offset is kept when <see cref="Markdown"/> changes, as in earlier versions. With
        /// <see cref="TailFollowMode.FollowAtBottom"/> the view stays at the end while it is there, counts rows
        /// that arrive while the user has scrolled up, and shows the "new below" indicator on its last row; a click
        /// on the indicator returns to the end. With <see cref="TailFollowMode.AlwaysFollow"/> it always shows the
        /// end. Row counts are compared at the same width only: a resize is never counted as new content, and when
        /// not following, a resize keeps the same row number at the top, clamped to the new end.
        /// </summary>
        public TailFollow TailFollow
        {
            get { return _Follow; }
        }

        /// <summary>
        /// Discards the cached layout so the next render lays the Markdown out again. Call it after changing the
        /// <see cref="Options"/> instance in place.
        /// </summary>
        public void Invalidate()
        {
            _ThemedOptions = null;
            DropLayout();
        }

        /// <summary>
        /// Returns the rendered rows at a width, from the cache when the width has not changed.
        /// </summary>
        /// <param name="width">The width in columns. Must be 1 or greater.</param>
        /// <returns>The rendered rows. Never null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        public IReadOnlyList<StyledText> RowsAt(int width)
        {
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be 1 or greater.");

            if (_CachedRows == null || _CachedWidth != width)
            {
                _CachedRows = MarkdownRenderer.RenderCore(_Markdown, width, EffectiveOptions(), CancellationToken.None);
                _CachedWidth = width;
            }

            return _CachedRows;
        }

        /// <summary>
        /// Scrolls by a number of rows, clamped to the content: negative moves up, positive moves down. Before the
        /// first render the result is only floored at 0 and the first render clamps it.
        /// </summary>
        /// <param name="rows">The rows to move. Any value.</param>
        public void ScrollBy(int rows)
        {
            long target = (long)_ScrollOffset + rows;
            MoveTo((int)Math.Max(0, Math.Min(int.MaxValue, target)));
        }

        /// <summary>
        /// Scrolls to the first row.
        /// </summary>
        public void ScrollToTop()
        {
            MoveTo(0);
        }

        /// <summary>
        /// Scrolls so the last rows are visible. Before the first render the next render shows the end.
        /// </summary>
        public void ScrollToEnd()
        {
            MoveTo(int.MaxValue);
        }

        /// <summary>
        /// Sets the search term to highlight, or null or empty to clear it. Matching is case-insensitive over the
        /// plain text of the rendered rows, so it finds what the user sees, not Markdown syntax. Matches are
        /// highlighted on the next render with the style of <see cref="Theme.SearchMatchRole"/> (black on palette
        /// yellow until a theme registers it); use <see cref="FindNext"/> and <see cref="FindPrevious"/> to move
        /// between them.
        /// </summary>
        /// <param name="query">The search term, or null or empty to clear.</param>
        public void SetSearch(string? query)
        {
            _Search = string.IsNullOrEmpty(query) ? null : query;
            _MatchRow = -1;
        }

        /// <summary>
        /// Scrolls to the next row matching the search term, wrapping around. The search starts after the current
        /// match when it is visible, otherwise after the top visible row. Before the first render the rows are laid
        /// out at the last layout width, or 80 columns, so the found row may move once the real width is known.
        /// </summary>
        /// <returns><c>true</c> when a match was found; <c>false</c> when none was found or no search is set.</returns>
        public bool FindNext()
        {
            return FindDirectional(true);
        }

        /// <summary>
        /// Scrolls to the previous row matching the search term, wrapping around. See <see cref="FindNext"/>.
        /// </summary>
        /// <returns><c>true</c> when a match was found; <c>false</c> when none was found or no search is set.</returns>
        public bool FindPrevious()
        {
            return FindDirectional(false);
        }

        /// <summary>
        /// Always returns false: the view has no focused sub-region.
        /// </summary>
        /// <param name="rect">Always <see cref="Rect"/> default.</param>
        /// <returns><c>false</c>.</returns>
        public bool TryGetFocusRect(out Rect rect)
        {
            rect = default(Rect);
            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (available.Width < 1 || available.Height < 1)
                return new Size(0, 0);

            int rows = RowsAt(available.Width).Count;
            return new Size(available.Width, Math.Min(available.Height, rows));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            _IndicatorRect = default(Rect);
            if (width <= 0 || height <= 0)
                return;

            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(Background));
            IReadOnlyList<StyledText> rows = RowsAt(width);
            _ViewportHeight = height;
            int maxOffset = Math.Max(0, rows.Count - height);
            if (_Follow.Mode != _SeenMode)
            {
                // Turning follow on attaches when the view is already at the end, as a pane does.
                _SeenMode = _Follow.Mode;
                if (_SeenMode != TailFollowMode.Never)
                    _Follow.OnViewportMoved(Math.Min(_ScrollOffset, maxOffset), maxOffset);
            }

            CountGrowth(rows.Count, width);
            if (_Follow.Mode != TailFollowMode.Never && _Follow.IsFollowing)
                _ScrollOffset = maxOffset;
            else if (_ScrollOffset > maxOffset)
                _ScrollOffset = maxOffset;

            for (int y = 0; y < height && _ScrollOffset + y < rows.Count; y++)
            {
                StyledText row = rows[_ScrollOffset + y];
                if (_Search != null)
                    row = SearchHighlighter.Highlight(row, _Search, _SearchStyle);

                surface.DrawStyledText(0, y, row, Background);
            }

            string? indicator = _Follow.Mode == TailFollowMode.Never ? null : _Follow.IndicatorText;
            if (indicator != null)
            {
                int indicatorWidth = Math.Min(width, TUIKit.Unicode.TextFit.Width(indicator));
                int x = width - indicatorWidth;
                _IndicatorRect = new Rect(x, height - 1, indicatorWidth, 1);
                surface.DrawText(x, height - 1, TUIKit.Unicode.TextFit.Ellipsize(indicator, indicatorWidth), Background.WithAttribute(CellAttributes.Reverse, true).WithAttribute(CellAttributes.Bold, true));
            }
        }

        /// <summary>
        /// Handles the scrolling keys while the view has focus: Up and Down move one row, PageUp and PageDown move
        /// <see cref="ViewportHeight"/> minus one row (at least one), and Home and End go to the top and the bottom.
        /// A key is consumed only when it moved the view or the content is taller than the viewport, so a short
        /// document never swallows keys a parent container wants. While a search term is set, F3 and Shift+F3 call
        /// <see cref="FindNext"/> and <see cref="FindPrevious"/> and are consumed. All other keys return false.
        /// Part of <see cref="IFocusable"/>.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.F3)
            {
                if (_Search == null)
                    return false;

                if ((key.Modifiers & KeyModifiers.Shift) != 0)
                    FindPrevious();
                else
                    FindNext();

                return true;
            }

            int page = Math.Max(1, _ViewportHeight - 1);
            int target;
            switch (key.Code)
            {
                case KeyCode.Up:
                    target = _ScrollOffset - 1;
                    break;
                case KeyCode.Down:
                    target = _ScrollOffset + 1;
                    break;
                case KeyCode.PageUp:
                    target = _ScrollOffset - page;
                    break;
                case KeyCode.PageDown:
                    target = _ScrollOffset + page;
                    break;
                case KeyCode.Home:
                    target = 0;
                    break;
                case KeyCode.End:
                    target = int.MaxValue;
                    break;
                default:
                    return false;
            }

            int before = _ScrollOffset;
            MoveTo(Math.Max(0, target));
            return _ScrollOffset != before || IsScrollable();
        }

        /// <summary>
        /// Records whether the view has keyboard focus. Called by the host focus ring, <see cref="FocusScope"/>, and
        /// <see cref="FocusManager"/>. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the view gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            _IsFocused = focused;
        }

        /// <summary>
        /// Returns hints for the scrolling keys ("Up/Down Scroll", "PgUp/PgDn Page", "Home/End Top/Bottom", and
        /// "F3 Next match" while a search term is set) while the view is focused and its content is taller than the
        /// viewport. Returns null otherwise, and always when <see cref="ShowKeyHints"/> is false. Part of
        /// <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!ShowKeyHints || !_IsFocused || !IsScrollable())
                return null;

            List<KeyHint> hints = new List<KeyHint>
            {
                new KeyHint("Up/Down", "Scroll"),
                new KeyHint("PgUp/PgDn", "Page"),
                new KeyHint("Home/End", "Top/Bottom"),
            };

            if (_Search != null)
                hints.Add(new KeyHint("F3", "Next match"));

            return hints;
        }

        /// <summary>
        /// Records the theme and, while <see cref="UseThemeStyles"/> is true, restyles the view with it. The search
        /// highlight always follows the theme's <see cref="Theme.SearchMatchRole"/>. <see cref="Background"/> is
        /// never changed. Part of <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            _Theme = theme;
            _SearchStyle = theme.Resolve(Theme.SearchMatchRole, SearchHighlighter.DefaultStyle);
            if (_UseThemeStyles)
                Invalidate();
            else
                _ThemedOptions = null;
        }

        /// <summary>
        /// Scrolls by <see cref="WheelStep"/> rows on a wheel event, clamped at once, and returns to the end on a
        /// left click on the "new below" indicator. Part of <see cref="IMouseAware"/>.
        /// </summary>
        /// <param name="mouse">The mouse event in view-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event scrolled the view; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Button)
            {
                case MouseButton.WheelUp:
                    ScrollBy(-_WheelStep);
                    return true;
                case MouseButton.WheelDown:
                    ScrollBy(_WheelStep);
                    return true;
                case MouseButton.Left:
                    if (mouse.Kind == MouseEventKind.Press && _IndicatorRect.Width > 0 && _IndicatorRect.Contains(new Point(mouse.X, mouse.Y)))
                    {
                        _Follow.ReturnToTail();
                        MoveTo(int.MaxValue);
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private MarkdownRenderOptions EffectiveOptions()
        {
            if (!_UseThemeStyles || _Theme == null)
                return _Options;

            if (_ThemedOptions == null)
                _ThemedOptions = _Options.WithTheme(_Theme);

            return _ThemedOptions;
        }

        private void DropLayout()
        {
            _CachedRows = null;
            _CachedWidth = -1;
            _MatchRow = -1;
        }

        private bool IsScrollable()
        {
            return _CachedRows != null && _ViewportHeight > 0 && _CachedRows.Count > _ViewportHeight;
        }

        private bool HasLayoutForViewport()
        {
            return _CachedRows != null && _ViewportHeight > 0;
        }

        // Moves the top row, clamping against the last layout when one exists, and reports the move to the
        // tail-follow state.
        private void MoveTo(int offset)
        {
            if (!HasLayoutForViewport())
            {
                _ScrollOffset = offset == int.MaxValue ? int.MaxValue : Math.Max(0, offset);
                if (offset == int.MaxValue)
                    _Follow.ReturnToTail();

                return;
            }

            int maxOffset = Math.Max(0, _CachedRows!.Count - _ViewportHeight);
            _ScrollOffset = Math.Min(Math.Max(0, offset), maxOffset);
            _Follow.OnViewportMoved(_ScrollOffset, maxOffset);
        }

        // Compares the row count with the previous layout at the same width and reports growth or shrinkage
        // caused by a Markdown change. A resize is never counted.
        private void CountGrowth(int rows, int width)
        {
            if (_ContentChanged && _CountedRows >= 0 && _CountedWidth == width && _Follow.Mode != TailFollowMode.Never)
            {
                int delta = rows - _CountedRows;
                if (delta > 0)
                    _Follow.OnContentAppended(delta);
                else if (delta < 0)
                    _Follow.OnContentRemoved(-delta);
            }

            _ContentChanged = false;
            _CountedRows = rows;
            _CountedWidth = width;
        }

        private bool FindDirectional(bool down)
        {
            if (_Search == null)
                return false;

            int width = _CachedWidth > 0 ? _CachedWidth : 80;
            IReadOnlyList<StyledText> rows = RowsAt(width);
            if (rows.Count == 0)
                return false;

            int visible = Math.Max(1, _ViewportHeight);
            int start = _MatchRow >= _ScrollOffset && _MatchRow < _ScrollOffset + visible ? _MatchRow : _ScrollOffset;
            if (start >= rows.Count)
                start = rows.Count - 1;

            for (int step = 1; step <= rows.Count; step++)
            {
                int index = down
                    ? (start + step) % rows.Count
                    : (((start - step) % rows.Count) + rows.Count) % rows.Count;

                if (SearchHighlighter.Matches(rows[index], _Search))
                {
                    int maxOffset = Math.Max(0, rows.Count - visible);
                    _ScrollOffset = Math.Min(index, maxOffset);
                    _MatchRow = index;
                    _Follow.OnJumpedAway();
                    return true;
                }
            }

            return false;
        }
    }
}
