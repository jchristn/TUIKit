namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// Projects a stream of text and keyed status lines onto a scrollback <see cref="Pane"/>. Streaming
    /// text is buffered into the current block and rendered as Markdown incrementally while it arrives:
    /// each completed source line is committed once, and only the trailing partial line is re-rendered
    /// per chunk, so headings, lists, and code blocks keep their structure while streaming (see
    /// <see cref="RenderMarkdownWhileStreaming"/>). <see cref="FinalizeBlock"/> commits the last line and
    /// starts a new block; the final pane content equals <see cref="MarkdownRenderer.Render(string, MarkdownStyles)"/>
    /// over the whole block. Separately,
    /// named lines can be created once and updated in place — for example a task line flipped from
    /// "running…" to "done" — without the caller knowing pane internals. This is deliberately generic:
    /// it moves text and status lines, with no assumptions about what produced them.
    /// </summary>
    /// <remarks>All members are thread-safe, so a background producer may write while the render thread draws the pane.</remarks>
    public sealed class StreamingTranscript
    {
        private readonly Pane _Pane;
        private readonly object _Sync = new object();
        private readonly Dictionary<string, PaneLineHandle> _Tracked = new Dictionary<string, PaneLineHandle>(StringComparer.Ordinal);
        private readonly StringBuilder _Block = new StringBuilder();
        private PaneLineHandle? _LiveLine;
        private MarkdownStyles _Styles = MarkdownStyles.Default;
        private bool _RenderWhileStreaming = true;
        private bool _InCodeBlock;
        private int _Processed;
        private int _Emitted;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingTranscript"/> class over a pane.
        /// </summary>
        /// <param name="pane">The scrollback pane to project onto. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pane"/> is null.</exception>
        public StreamingTranscript(Pane pane)
        {
            _Pane = pane ?? throw new ArgumentNullException(nameof(pane));
        }

        /// <summary>
        /// Gets the underlying pane.
        /// </summary>
        public Pane Pane
        {
            get { return _Pane; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether streamed text is rendered as Markdown line by line
        /// while it arrives. Defaults to true. When false, the original behavior applies: the whole block
        /// shows on one live line with newlines collapsed to spaces until <see cref="FinalizeBlock"/>.
        /// Change it only between blocks.
        /// </summary>
        public bool RenderMarkdownWhileStreaming
        {
            get { lock (_Sync) { return _RenderWhileStreaming; } }
            set { lock (_Sync) { _RenderWhileStreaming = value; } }
        }

        /// <summary>
        /// Gets or sets the Markdown element styles, for example <see cref="MarkdownStyles.FromTheme"/>.
        /// Defaults to <see cref="MarkdownStyles.Default"/>. Applies to lines rendered after the change.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownStyles Styles
        {
            get { lock (_Sync) { return _Styles; } }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                lock (_Sync) { _Styles = value; }
            }
        }

        /// <summary>
        /// Appends text to the current block. With <see cref="RenderMarkdownWhileStreaming"/> on, every
        /// completed line is rendered as Markdown and committed, and the trailing partial line is shown
        /// rendered on a live line that is replaced as more text arrives. With it off, newlines in the
        /// accumulated block collapse to spaces on a single live line until <see cref="FinalizeBlock"/>.
        /// </summary>
        /// <param name="text">The text to append. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void AppendText(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            lock (_Sync)
            {
                _Block.Append(text);
                if (_RenderWhileStreaming)
                {
                    StreamLines(false);
                    return;
                }

                string line = CollapseToLine(_Block.ToString());
                if (_LiveLine == null)
                    _LiveLine = _Pane.WriteLine(line);
                else
                    _LiveLine.Update(line);
            }
        }

        /// <summary>
        /// Renders the buffered block as Markdown into the pane and starts a new block. The live line is
        /// replaced by the first rendered line and any remaining lines are appended below it. A block
        /// with no buffered text is a no-op.
        /// </summary>
        public void FinalizeBlock()
        {
            lock (_Sync)
            {
                if (_RenderWhileStreaming)
                {
                    if (_Block.Length > 0)
                    {
                        StreamLines(true);
                        if (_Emitted == 0)
                            _Pane.WriteLine(StyledText.Empty);
                    }

                    _Block.Clear();
                    _LiveLine = null;
                    _InCodeBlock = false;
                    _Processed = 0;
                    _Emitted = 0;
                    return;
                }

                string markdown = _Block.ToString();
                _Block.Clear();

                if (markdown.Length == 0)
                {
                    _LiveLine = null;
                    return;
                }

                IReadOnlyList<StyledText> lines = MarkdownRenderer.Render(markdown, _Styles);
                int start = 0;
                if (_LiveLine != null && lines.Count > 0)
                {
                    _LiveLine.Update(lines[0]);
                    start = 1;
                }

                for (int i = start; i < lines.Count; i++)
                    _Pane.WriteLine(lines[i]);

                _LiveLine = null;
            }
        }

        /// <summary>
        /// Creates a named line in the pane that can be updated in place, or returns the existing handle
        /// when the key is already tracked.
        /// </summary>
        /// <param name="key">The line key. Must not be null.</param>
        /// <returns>The line handle for the key.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public PaneLineHandle Track(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            lock (_Sync)
            {
                if (_Tracked.TryGetValue(key, out PaneLineHandle? existing))
                    return existing;

                PaneLineHandle handle = _Pane.WriteLine(string.Empty);
                _Tracked[key] = handle;
                return handle;
            }
        }

        /// <summary>
        /// Updates a tracked line's content in place.
        /// </summary>
        /// <param name="key">The line key. Must not be null.</param>
        /// <param name="content">The new content. Must not be null.</param>
        /// <returns><c>true</c> when the line was still on screen and updated; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="content"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when <paramref name="key"/> was never tracked.</exception>
        public bool Update(string key, string content)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            lock (_Sync)
            {
                if (!_Tracked.TryGetValue(key, out PaneLineHandle? handle))
                    throw new KeyNotFoundException("No tracked line for key '" + key + "'.");

                return handle.Update(content);
            }
        }

        /// <summary>
        /// Updates a tracked line's content in place with styled text.
        /// </summary>
        /// <param name="key">The line key. Must not be null.</param>
        /// <param name="content">The new content. Must not be null.</param>
        /// <returns><c>true</c> when the line was still on screen and updated; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="content"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when <paramref name="key"/> was never tracked.</exception>
        public bool Update(string key, StyledText content)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            lock (_Sync)
            {
                if (!_Tracked.TryGetValue(key, out PaneLineHandle? handle))
                    throw new KeyNotFoundException("No tracked line for key '" + key + "'.");

                return handle.Update(content);
            }
        }

        // Commits every completed source line after _Processed, then shows the trailing partial line on the
        // live line (or, when finishing, commits it too). Must be called under _Sync.
        private void StreamLines(bool finish)
        {
            string block = _Block.ToString();
            while (true)
            {
                int newline = block.IndexOf('\n', _Processed);
                if (newline < 0)
                    break;

                string line = block.Substring(_Processed, newline - _Processed);
                _Processed = newline + 1;
                CommitLine(line.EndsWith("\r", StringComparison.Ordinal) ? line.Substring(0, line.Length - 1) : line);
            }

            string partial = block.Substring(_Processed);
            if (finish)
            {
                CommitLine(partial.Replace("\r", string.Empty));
                _Processed = block.Length;
                return;
            }

            if (partial.Length == 0)
                return;

            bool inCode = _InCodeBlock;
            StyledText? rendered = MarkdownRenderer.RenderLine(partial.Replace("\r", string.Empty), _Styles, ref inCode);
            if (rendered == null)
            {
                _LiveLine?.Remove();
                _LiveLine = null;
            }
            else if (_LiveLine == null)
            {
                _LiveLine = _Pane.WriteLine(rendered);
            }
            else
            {
                _LiveLine.Update(rendered);
            }
        }

        private void CommitLine(string line)
        {
            StyledText? rendered = MarkdownRenderer.RenderLine(line, _Styles, ref _InCodeBlock);
            if (rendered == null)
            {
                _LiveLine?.Remove();
            }
            else
            {
                if (_LiveLine != null)
                    _LiveLine.Update(rendered);
                else
                    _Pane.WriteLine(rendered);

                _Emitted++;
            }

            _LiveLine = null;
        }

        private static string CollapseToLine(string text)
        {
            return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
