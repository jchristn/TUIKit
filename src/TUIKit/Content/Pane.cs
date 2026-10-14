namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// A persistent, thread-safe region of scrolling text content. Any thread may call
    /// <see cref="Write(string)"/> or <see cref="WriteLine(string)"/>; writes are ordered first-in
    /// first-out within the pane. Content can be updated in place through the handle returned by
    /// <c>WriteLine</c>, and a smart scroll lock detaches the viewport when the user scrolls up and
    /// re-attaches at the bottom (see <see cref="TailFollow"/>); while detached, a "N new below"
    /// indicator on the last row counts what arrived and a click on it returns to the bottom. A pane is also an <see cref="IWidget"/>, so it can be bound to a
    /// layout region like any other widget.
    /// </summary>
    /// <remarks>
    /// All members are thread-safe. Rendering reads pane state under the same lock used by writers,
    /// so a frame never captures a half-written batch.
    /// </remarks>
    public sealed class Pane : IWidget, IMouseAware, IThemeable
    {
        private readonly string _Id;
        private readonly object _Sync = new object();
        private readonly List<PaneLine> _Lines = new List<PaneLine>();
        private StyledText _Current = StyledText.Empty;
        private long _NextId = 1;
        private int _Capacity = 5000;
        private int _MaxLineLength = 8192;
        private readonly TailFollow _Follow = new TailFollow();
        private long _FirstNewId;
        private Rect _IndicatorRect;
        private int _ViewTop;
        private int _LastTotalRows;
        private int _LastHeight;
        private int _LastWidth;
        private long _Version;
        private int _BatchDepth;
        private string? _Search;
        private CellStyle _SearchStyle = SearchHighlighter.DefaultStyle;

        /// <summary>
        /// Gets the pane identifier. Never null or empty.
        /// </summary>
        public string Id
        {
            get { return _Id; }
        }

        /// <summary>
        /// Gets or sets the scrollback capacity in committed lines. Zero means unbounded. When the
        /// limit is exceeded the oldest line is evicted. Defaults to 5000. Must be zero or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int ScrollbackCapacityLines
        {
            get { lock (_Sync) { return _Capacity; } }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Capacity must be zero or greater.");
                lock (_Sync) { _Capacity = value; }
            }
        }

        /// <summary>
        /// Gets or sets the maximum length in characters of a single committed line. A longer line is
        /// truncated with an ellipsis, guarding against a runaway producer emitting one enormous line.
        /// Defaults to 8192. Must be greater than zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a non-positive value.</exception>
        public int MaxLineLength
        {
            get { lock (_Sync) { return _MaxLineLength; } }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum line length must be greater than zero.");
                lock (_Sync) { _MaxLineLength = value; }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the viewport is attached to the bottom (following new
        /// output). False while the user has scrolled up. Same as <see cref="TailFollow.IsFollowing"/>.
        /// </summary>
        public bool IsAtBottom
        {
            get { return _Follow.IsFollowing; }
        }

        /// <summary>
        /// Gets the number of new lines committed since the viewport detached from the bottom. Zero
        /// while attached. Drives the "N new below" indicator. Same as <see cref="TailFollow.NewItemsBelow"/>.
        /// </summary>
        public int NewSinceDetached
        {
            get { return _Follow.NewItemsBelow; }
        }

        /// <summary>
        /// Gets the follow-the-bottom state that drives the scroll lock: its mode (for example
        /// <see cref="TailFollowMode.AlwaysFollow"/> for a log that must never fall behind), the
        /// indicator text and visibility, and the <see cref="TailFollow.FollowingChanged"/> event. Never
        /// null. Selecting text never detaches the pane; only scrolling does.
        /// </summary>
        public TailFollow TailFollow
        {
            get { return _Follow; }
        }

        /// <summary>
        /// Gets a monotonically increasing version stamp that changes whenever pane content or scroll
        /// state changes. A renderer can compare it to decide whether a repaint is needed.
        /// </summary>
        public long Version
        {
            get { lock (_Sync) { return _Version; } }
        }

        /// <summary>
        /// Gets the number of committed lines currently retained.
        /// </summary>
        public int LineCount
        {
            get { lock (_Sync) { return _Lines.Count; } }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pane"/> class.
        /// </summary>
        /// <param name="id">The pane identifier. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        public Pane(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Pane id must not be null or empty.", nameof(id));

            _Id = id;
        }

        /// <summary>
        /// Appends text to the current, not-yet-terminated line without starting a new line. Used for
        /// partial-line token streaming.
        /// </summary>
        /// <param name="text">The text to append. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void Write(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Write(Text.From(text));
        }

        /// <summary>
        /// Appends styled text to the current, not-yet-terminated line.
        /// </summary>
        /// <param name="text">The styled text to append. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void Write(StyledText text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            lock (_Sync)
            {
                _Current = _Current.Append(text);
                Bump();
            }
        }

        /// <summary>
        /// Commits an empty line.
        /// </summary>
        /// <returns>A handle to the committed line.</returns>
        public PaneLineHandle WriteLine()
        {
            return WriteLine(StyledText.Empty);
        }

        /// <summary>
        /// Appends plain text to the current line and commits it.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <returns>A handle to the committed line.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public PaneLineHandle WriteLine(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return WriteLine(Text.From(text));
        }

        /// <summary>
        /// Appends styled text to the current line and commits it.
        /// </summary>
        /// <param name="text">The styled text. Must not be null.</param>
        /// <returns>A handle to the committed line.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public PaneLineHandle WriteLine(StyledText text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            lock (_Sync)
            {
                StyledText line = _Current.Append(text);
                _Current = StyledText.Empty;
                return CommitLine(line);
            }
        }

        /// <summary>
        /// Removes all content and re-attaches the viewport to the bottom.
        /// </summary>
        public void Clear()
        {
            lock (_Sync)
            {
                _Lines.Clear();
                _Current = StyledText.Empty;
                _Follow.Reset();
                _ViewTop = 0;
                Bump();
            }
        }

        /// <summary>
        /// Begins an atomic batch. Writes made before the returned scope is disposed render together.
        /// </summary>
        /// <returns>A disposable batch scope.</returns>
        public PaneBatch BeginBatch()
        {
            Monitor.Enter(_Sync);
            _BatchDepth++;
            return new PaneBatch(this);
        }

        /// <summary>
        /// Scrolls the viewport up (toward older content), detaching from the bottom.
        /// </summary>
        /// <param name="lines">The number of visual rows to scroll. Values of zero or less are ignored.</param>
        public void ScrollUp(int lines)
        {
            if (lines <= 0)
                return;

            lock (_Sync)
            {
                int maxTop = Math.Max(0, _LastTotalRows - _LastHeight);
                if (_Follow.IsFollowing)
                    _ViewTop = maxTop;

                _ViewTop = Math.Max(0, _ViewTop - lines);
                _Follow.OnViewportMoved(_ViewTop, maxTop);
                Bump();
            }
        }

        /// <summary>
        /// Scrolls the viewport down (toward newer content), re-attaching when the bottom is reached.
        /// </summary>
        /// <param name="lines">The number of visual rows to scroll. Values of zero or less are ignored.</param>
        public void ScrollDown(int lines)
        {
            if (lines <= 0)
                return;

            lock (_Sync)
            {
                if (_Follow.IsFollowing)
                    return;

                _ViewTop += lines;
                int maxTop = Math.Max(0, _LastTotalRows - _LastHeight);
                if (_ViewTop >= maxTop)
                    _ViewTop = maxTop;

                _Follow.OnViewportMoved(_ViewTop, maxTop);
                Bump();
            }
        }

        /// <summary>
        /// Re-attaches the viewport to the bottom and clears the new-line indicator.
        /// </summary>
        public void ScrollToBottom()
        {
            lock (_Sync)
            {
                _Follow.ReturnToTail();
                _ViewTop = Math.Max(0, _LastTotalRows - _LastHeight);
                Bump();
            }
        }

        /// <summary>
        /// Gets or sets the background style used for blank cells when the pane is rendered through
        /// the <see cref="IWidget"/> interface (for example when bound to a region). Defaults to the
        /// default style.
        /// </summary>
        public CellStyle Background { get; set; } = CellStyle.Default;

        /// <summary>
        /// Appends styled text parsed from inline markup and commits it as a line.
        /// </summary>
        /// <param name="markup">The markup source (see <see cref="Markup"/>). Must not be null.</param>
        /// <returns>A handle to the committed line.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markup"/> is null.</exception>
        public PaneLineHandle WriteMarkup(string markup)
        {
            if (markup == null)
                throw new ArgumentNullException(nameof(markup));

            return WriteLine(Markup.Parse(markup));
        }

        /// <summary>
        /// Commits a block of Markdown, laid out with default <see cref="MarkdownRenderOptions"/>, that reflows
        /// whenever the pane's width changes. See <see cref="WriteMarkdown(string, MarkdownRenderOptions)"/>.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <returns>A handle to the committed block; <see cref="PaneLineHandle.UpdateMarkdown(string)"/> replaces its source.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
        public PaneLineHandle WriteMarkdown(string markdown)
        {
            return WriteMarkdown(markdown, new MarkdownRenderOptions());
        }

        /// <summary>
        /// Commits a block of Markdown that is laid out at the pane's width each time it renders, so tables,
        /// wrapped list items, and quotes reflow when the terminal is resized. The block counts as one committed
        /// line for <see cref="ScrollbackCapacityLines"/> and the "N new below" indicator. Text written with
        /// <see cref="Write(string)"/> and not yet committed is committed first, as its own line.
        /// <see cref="MaxLineLength"/> applies to each source line. The options are copied, so changing
        /// <paramref name="options"/> afterwards does not affect the block.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="options">How the block should look. Must not be null.</param>
        /// <returns>A handle to the committed block; <see cref="PaneLineHandle.UpdateMarkdown(string)"/> replaces its source.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> or <paramref name="options"/> is null.</exception>
        public PaneLineHandle WriteMarkdown(string markdown, MarkdownRenderOptions options)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            MarkdownRenderOptions snapshot = options.Clone();
            lock (_Sync)
            {
                if (_Current.Width > 0)
                {
                    StyledText pending = _Current;
                    _Current = StyledText.Empty;
                    CommitLine(pending);
                }

                PaneLine paneLine = new PaneLine(_NextId++, StyledText.Empty);
                paneLine.SetMarkdown(LimitLineLengths(markdown), snapshot);
                return CommitPaneLine(paneLine);
            }
        }

        /// <summary>
        /// Scrolls the viewport in response to a mouse wheel event. Part of <see cref="IMouseAware"/>;
        /// the host forwards wheel events when the pointer is over the pane so the user can scroll
        /// scrollback without a key binding.
        /// </summary>
        /// <param name="mouse">The mouse event in pane-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a wheel event scrolled the pane; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                bool onIndicator;
                lock (_Sync)
                    onIndicator = _IndicatorRect.Width > 0 && _IndicatorRect.Contains(new Point(mouse.X, mouse.Y));

                if (onIndicator)
                {
                    ScrollToBottom();
                    return true;
                }
            }

            switch (mouse.Button)
            {
                case MouseButton.WheelUp:
                    ScrollUp(3);
                    return true;
                case MouseButton.WheelDown:
                    ScrollDown(3);
                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <summary>
        /// Renders the pane using its <see cref="Background"/> style. Part of the <see cref="IWidget"/>
        /// contract.
        /// </summary>
        /// <param name="surface">The surface to render into. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface)
        {
            Render(surface, Background);
        }

        /// <summary>
        /// Renders the visible portion of the pane into the supplied surface.
        /// </summary>
        /// <param name="surface">The surface to render into. Must not be null.</param>
        /// <param name="background">The background style for blank cells.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface, CellStyle background)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            lock (_Sync)
            {
                int width = surface.Size.Width;
                int height = surface.Size.Height;
                surface.Fill(new Rect(0, 0, width, height), Cell.Blank(background));

                if (width <= 0 || height <= 0)
                    return;

                // Wrapped rows are cached per line and width, so only new or changed lines rewrap, and
                // only the visible window is materialized.
                int totalRows = CountRows(width);
                _LastTotalRows = totalRows;
                _LastHeight = height;
                _LastWidth = width;

                int maxTop = Math.Max(0, totalRows - height);
                int top = _Follow.IsFollowing ? maxTop : Math.Min(Math.Max(_ViewTop, 0), maxTop);
                _ViewTop = top;

                List<StyledText> rows = VisibleRows(width, top, height);
                for (int r = 0; r < rows.Count; r++)
                {
                    StyledText row = rows[r];
                    if (!string.IsNullOrEmpty(_Search))
                        row = SearchHighlighter.Highlight(row, _Search!, _SearchStyle);

                    surface.DrawStyledText(0, r, row, background);
                }

                _IndicatorRect = default;
                string? indicator = _Follow.IndicatorText;
                if (indicator != null)
                {
                    int indicatorWidth = Math.Min(width, TUIKit.Unicode.TextFit.Width(indicator));
                    int x = width - indicatorWidth;
                    _IndicatorRect = new Rect(x, height - 1, indicatorWidth, 1);
                    surface.DrawText(x, height - 1, TUIKit.Unicode.TextFit.Ellipsize(indicator, indicatorWidth), background.WithAttribute(CellAttributes.Reverse, true).WithAttribute(CellAttributes.Bold, true));
                }
            }
        }

        /// <summary>
        /// Returns a snapshot of the committed lines as plain text, oldest first. Useful for
        /// line-oriented (non-TTY) output and for building a selection source. A Markdown block written with
        /// <see cref="WriteMarkdown(string, MarkdownRenderOptions)"/> contributes one entry per rendered row, laid
        /// out at the width of the last render (80 before the first render).
        /// </summary>
        /// <returns>The committed lines as plain strings. Never null.</returns>
        public IReadOnlyList<string> SnapshotPlainLines()
        {
            lock (_Sync)
            {
                List<string> result = new List<string>(_Lines.Count);
                int width = _LastWidth > 0 ? _LastWidth : 80;
                for (int i = 0; i < _Lines.Count; i++)
                {
                    if (!_Lines[i].IsMarkdown)
                    {
                        result.Add(_Lines[i].Content.ToPlainString());
                        continue;
                    }

                    IReadOnlyList<StyledText> rows = _Lines[i].Wrapped(width);
                    for (int r = 0; r < rows.Count; r++)
                        result.Add(rows[r].ToPlainString());
                }

                return result;
            }
        }

        internal bool UpdateLine(long id, StyledText content)
        {
            lock (_Sync)
            {
                for (int i = _Lines.Count - 1; i >= 0; i--)
                {
                    if (_Lines[i].Id == id)
                    {
                        _Lines[i].Content = content;
                        Bump();
                        return true;
                    }
                }

                return false;
            }
        }

        internal bool UpdateMarkdownLine(long id, string markdown, MarkdownRenderOptions? options)
        {
            lock (_Sync)
            {
                for (int i = _Lines.Count - 1; i >= 0; i--)
                {
                    if (_Lines[i].Id == id)
                    {
                        MarkdownRenderOptions resolved = options ?? _Lines[i].MarkdownOptions ?? new MarkdownRenderOptions();
                        _Lines[i].SetMarkdown(LimitLineLengths(markdown), resolved);
                        Bump();
                        return true;
                    }
                }

                return false;
            }
        }

        internal bool RemoveLine(long id)
        {
            lock (_Sync)
            {
                for (int i = _Lines.Count - 1; i >= 0; i--)
                {
                    if (_Lines[i].Id == id)
                    {
                        _Lines.RemoveAt(i);
                        if (_FirstNewId > 0 && id >= _FirstNewId && _Follow.NewItemsBelow > 0)
                            _Follow.OnContentRemoved(1);
                        Bump();
                        return true;
                    }
                }

                return false;
            }
        }

        internal void EndBatch()
        {
            _BatchDepth--;
            if (_BatchDepth <= 0)
            {
                _BatchDepth = 0;
                Bump();
            }

            Monitor.Exit(_Sync);
        }

        private PaneLineHandle CommitLine(StyledText line)
        {
            StyledText content = line;
            string plain = content.ToPlainString();
            if (plain.Length > _MaxLineLength)
                content = Text.From(plain.Substring(0, _MaxLineLength) + "…");

            return CommitPaneLine(new PaneLine(_NextId++, content));
        }

        private PaneLineHandle CommitPaneLine(PaneLine paneLine)
        {
            long id = paneLine.Id;
            _Lines.Add(paneLine);

            if (_Capacity > 0 && _Lines.Count > _Capacity)
                _Lines.RemoveAt(0);

            // Remember the first line counted as "new below", so removing a counted line can lower the
            // count; a line that arrived while following was already seen.
            if (_Follow.OnContentAppended(1))
                _FirstNewId = 0;
            else if (_Follow.NewItemsBelow == 1)
                _FirstNewId = id;

            Bump();
            return new PaneLineHandle(this, id);
        }

        // Applies MaxLineLength to each source line of a Markdown block. Must be called under _Sync.
        private string LimitLineLengths(string markdown)
        {
            if (markdown.Length <= _MaxLineLength)
                return markdown;

            string[] lines = markdown.Split('\n');
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > _MaxLineLength)
                {
                    lines[i] = lines[i].Substring(0, _MaxLineLength) + "…";
                    changed = true;
                }
            }

            return changed ? string.Join("\n", lines) : markdown;
        }

        /// <summary>
        /// Gets the active search term set by <see cref="SetSearch"/>, or null when no search is set. Thread-safe.
        /// </summary>
        public string? SearchText
        {
            get { lock (_Sync) { return _Search; } }
        }

        /// <summary>
        /// Applies a theme to the pane's search highlight, the only part of a pane that follows the theme. The
        /// highlight becomes the style registered under <see cref="Theme.SearchMatchRole"/>, or black on palette
        /// yellow (the style used before themes reached the pane) when the theme does not register it. Part of
        /// <see cref="IThemeable"/>; the host calls it when the pane is bound and when the theme changes.
        /// Thread-safe.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            CellStyle style = theme.Resolve(Theme.SearchMatchRole, SearchHighlighter.DefaultStyle);
            lock (_Sync)
            {
                _SearchStyle = style;
                Bump();
            }
        }

        /// <summary>
        /// Sets the search term to highlight in the pane, or null/empty to clear it. Matches are
        /// highlighted on the next render; use <see cref="FindNext"/> and <see cref="FindPrevious"/> to
        /// scroll between them.
        /// </summary>
        /// <param name="query">The search term, or null/empty to clear.</param>
        public void SetSearch(string? query)
        {
            lock (_Sync)
            {
                _Search = string.IsNullOrEmpty(query) ? null : query;
                Bump();
            }
        }

        /// <summary>
        /// Scrolls to the next line matching the current search term, wrapping around.
        /// </summary>
        /// <returns><c>true</c> when a match was found; otherwise <c>false</c>.</returns>
        public bool FindNext()
        {
            return FindDirectional(true);
        }

        /// <summary>
        /// Scrolls to the previous line matching the current search term, wrapping around.
        /// </summary>
        /// <returns><c>true</c> when a match was found; otherwise <c>false</c>.</returns>
        public bool FindPrevious()
        {
            return FindDirectional(false);
        }

        private bool FindDirectional(bool down)
        {
            lock (_Sync)
            {
                if (string.IsNullOrEmpty(_Search))
                    return false;

                int width = _LastWidth > 0 ? _LastWidth : 80;
                List<StyledText> rows = BuildRows(width);
                if (rows.Count == 0)
                    return false;

                int start = _ViewTop;
                for (int step = 1; step <= rows.Count; step++)
                {
                    int index = down
                        ? (start + step) % rows.Count
                        : (((start - step) % rows.Count) + rows.Count) % rows.Count;

                    if (SearchHighlighter.Matches(rows[index], _Search!))
                    {
                        int maxTop = Math.Max(0, rows.Count - Math.Max(1, _LastHeight));
                        _ViewTop = Math.Min(index, maxTop);
                        _Follow.DetachAtJump();
                        Bump();
                        return true;
                    }
                }

                return false;
            }
        }

        private int CountRows(int width)
        {
            int total = 0;
            for (int i = 0; i < _Lines.Count; i++)
                total += _Lines[i].Wrapped(width).Count;

            if (_Current.Spans.Count > 0 && _Current.ToPlainString().Length > 0)
                total += TextWrapper.Wrap(_Current, width).Count;

            return total;
        }

        private List<StyledText> VisibleRows(int width, int top, int height)
        {
            List<StyledText> rows = new List<StyledText>(height);
            int row = 0;
            for (int i = 0; i < _Lines.Count && rows.Count < height; i++)
            {
                IReadOnlyList<StyledText> wrapped = _Lines[i].Wrapped(width);
                if (row + wrapped.Count <= top)
                {
                    row += wrapped.Count;
                    continue;
                }

                for (int w = 0; w < wrapped.Count && rows.Count < height; w++, row++)
                {
                    if (row >= top)
                        rows.Add(wrapped[w]);
                }
            }

            if (rows.Count < height && _Current.Spans.Count > 0 && _Current.ToPlainString().Length > 0)
            {
                IReadOnlyList<StyledText> wrapped = TextWrapper.Wrap(_Current, width);
                for (int w = 0; w < wrapped.Count && rows.Count < height; w++, row++)
                {
                    if (row >= top)
                        rows.Add(wrapped[w]);
                }
            }

            return rows;
        }

        private List<StyledText> BuildRows(int width)
        {
            List<StyledText> rows = new List<StyledText>();
            for (int i = 0; i < _Lines.Count; i++)
            {
                IReadOnlyList<StyledText> wrapped = _Lines[i].Wrapped(width);
                for (int w = 0; w < wrapped.Count; w++)
                    rows.Add(wrapped[w]);
            }

            if (_Current.ToPlainString().Length > 0)
            {
                IReadOnlyList<StyledText> wrapped = TextWrapper.Wrap(_Current, width);
                for (int w = 0; w < wrapped.Count; w++)
                    rows.Add(wrapped[w]);
            }

            return rows;
        }

        private void Bump()
        {
            _Version++;
        }
    }
}
