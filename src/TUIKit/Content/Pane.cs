namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using TUIKit;
    using TUIKit.Input;
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
    public sealed class Pane : IWidget, IMouseAware
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
        private CellStyle _SearchStyle = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(3));

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
                        row = HighlightMatches(row, _Search!, _SearchStyle);

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
        /// line-oriented (non-TTY) output and for building a selection source.
        /// </summary>
        /// <returns>The committed lines as plain strings. Never null.</returns>
        public IReadOnlyList<string> SnapshotPlainLines()
        {
            lock (_Sync)
            {
                List<string> result = new List<string>(_Lines.Count);
                for (int i = 0; i < _Lines.Count; i++)
                    result.Add(_Lines[i].Content.ToPlainString());

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

            long id = _NextId++;
            _Lines.Add(new PaneLine(id, content));

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

                    if (rows[index].ToPlainString().IndexOf(_Search!, StringComparison.OrdinalIgnoreCase) >= 0)
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

        private static StyledText HighlightMatches(StyledText row, string query, CellStyle highlight)
        {
            List<char> chars = new List<char>();
            List<CellStyle> styles = new List<CellStyle>();
            IReadOnlyList<StyledSpan> spans = row.Spans;
            for (int s = 0; s < spans.Count; s++)
            {
                string text = spans[s].Text;
                for (int c = 0; c < text.Length; c++)
                {
                    chars.Add(text[c]);
                    styles.Add(spans[s].Style);
                }
            }

            string plain = new string(chars.ToArray());
            int from = 0;
            bool any = false;
            while (from <= plain.Length - query.Length)
            {
                int index = plain.IndexOf(query, from, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    break;

                any = true;
                for (int i = index; i < index + query.Length && i < styles.Count; i++)
                    styles[i] = highlight;

                from = index + query.Length;
            }

            if (!any)
                return row;

            List<StyledSpan> result = new List<StyledSpan>();
            StringBuilder run = new StringBuilder();
            CellStyle current = CellStyle.Default;
            bool has = false;
            for (int i = 0; i < chars.Count; i++)
            {
                if (!has)
                {
                    current = styles[i];
                    has = true;
                }
                else if (styles[i] != current)
                {
                    result.Add(new StyledSpan(run.ToString(), current));
                    run.Clear();
                    current = styles[i];
                }

                run.Append(chars[i]);
            }

            if (has)
                result.Add(new StyledSpan(run.ToString(), current));

            return new StyledText(result);
        }

        private void Bump()
        {
            _Version++;
        }
    }
}
