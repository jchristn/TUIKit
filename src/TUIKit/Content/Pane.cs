namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// A persistent, thread-safe region of scrolling text content. Any thread may call
    /// <see cref="Write(string)"/> or <see cref="WriteLine(string)"/>; writes are ordered first-in
    /// first-out within the pane. Content can be updated in place through the handle returned by
    /// <c>WriteLine</c>, and a smart scroll lock detaches the viewport when the user scrolls up and
    /// re-attaches at the bottom. A pane is also an <see cref="IWidget"/>, so it can be bound to a
    /// layout region like any other widget.
    /// </summary>
    /// <remarks>
    /// All members are thread-safe. Rendering reads pane state under the same lock used by writers,
    /// so a frame never captures a half-written batch.
    /// </remarks>
    public sealed class Pane : IWidget
    {
        private readonly string _Id;
        private readonly object _Sync = new object();
        private readonly List<PaneLine> _Lines = new List<PaneLine>();
        private StyledText _Current = StyledText.Empty;
        private long _NextId = 1;
        private int _Capacity = 5000;
        private int _MaxLineLength = 8192;
        private bool _Attached = true;
        private int _ViewTop;
        private int _LastTotalRows;
        private int _LastHeight;
        private int _NewSinceDetached;
        private long _Version;
        private int _BatchDepth;

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
        /// output). False while the user has scrolled up.
        /// </summary>
        public bool IsAtBottom
        {
            get { lock (_Sync) { return _Attached; } }
        }

        /// <summary>
        /// Gets the number of new lines committed since the viewport detached from the bottom. Zero
        /// while attached. Drives the "N new" indicator.
        /// </summary>
        public int NewSinceDetached
        {
            get { lock (_Sync) { return _NewSinceDetached; } }
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
                _Attached = true;
                _ViewTop = 0;
                _NewSinceDetached = 0;
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
                if (_Attached)
                {
                    _Attached = false;
                    _ViewTop = Math.Max(0, _LastTotalRows - _LastHeight);
                }

                _ViewTop = Math.Max(0, _ViewTop - lines);
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
                if (_Attached)
                    return;

                _ViewTop += lines;
                int maxTop = Math.Max(0, _LastTotalRows - _LastHeight);
                if (_ViewTop >= maxTop)
                {
                    _ViewTop = maxTop;
                    _Attached = true;
                    _NewSinceDetached = 0;
                }

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
                _Attached = true;
                _NewSinceDetached = 0;
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

                List<StyledText> rows = new List<StyledText>();
                for (int i = 0; i < _Lines.Count; i++)
                {
                    IReadOnlyList<StyledText> wrapped = TextWrapper.Wrap(_Lines[i].Content, width);
                    for (int w = 0; w < wrapped.Count; w++)
                        rows.Add(wrapped[w]);
                }

                if (_Current.ToPlainString().Length > 0)
                {
                    IReadOnlyList<StyledText> wrapped = TextWrapper.Wrap(_Current, width);
                    for (int w = 0; w < wrapped.Count; w++)
                        rows.Add(wrapped[w]);
                }

                _LastTotalRows = rows.Count;
                _LastHeight = height;

                int maxTop = Math.Max(0, rows.Count - height);
                int top = _Attached ? maxTop : Math.Min(Math.Max(_ViewTop, 0), maxTop);
                _ViewTop = top;

                for (int r = 0; r < height && top + r < rows.Count; r++)
                    surface.DrawStyledText(0, r, rows[top + r], background);
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

            if (!_Attached)
                _NewSinceDetached++;

            Bump();
            return new PaneLineHandle(this, id);
        }

        private void Bump()
        {
            _Version++;
        }
    }
}
