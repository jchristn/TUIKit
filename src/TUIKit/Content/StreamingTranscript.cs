namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;
    using TUIKit.Compatibility;

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
        private StringBuilder _Partial = new StringBuilder();
        private bool _HasLineInput;
        private bool _CanDefer;
        private bool _HeldPipeRow;
        private int _Emitted;
        private MarkdownRenderOptions? _RenderOptions;
        private MarkdownRenderOptions? _BlockOptions;
        private PaneLineHandle? _MarkdownBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingTranscript"/> class over a pane.
        /// </summary>
        /// <param name="pane">The scrollback pane to project onto. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pane"/> is null.</exception>
        public StreamingTranscript(Pane pane)
        {
            RuntimeCompat.ThrowIfNull(pane, nameof(pane));
            _Pane = pane;
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
        /// Gets or sets the Markdown element styles, for example <see cref="MarkdownStyles.FromTheme(Theming.Theme)"/>.
        /// Defaults to <see cref="MarkdownStyles.Default"/>. Applies to lines rendered after the change.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownStyles Styles
        {
            get { lock (_Sync) { return _Styles; } }
            set
            {
                RuntimeCompat.ThrowIfNull(value, nameof(value));

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
            RuntimeCompat.ThrowIfNull(text, nameof(text));

            lock (_Sync)
            {
                if (_RenderOptions != null && _BlockOptions == null)
                    _BlockOptions = _RenderOptions;

                if (_BlockOptions == null && _RenderWhileStreaming)
                {
                    StreamLines(text);
                    return;
                }

                _Block.Append(text);
                if (_RenderWhileStreaming)
                {
                    if (!_HeldPipeRow || text.IndexOf('\n') >= 0)
                        StreamMarkdownBlock();
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
        /// Gets or sets the width-aware rendering options, or null (the default) for the 1.5.0 line-by-line
        /// behavior. When set, each block is a single Markdown block in the pane (see
        /// <see cref="Pane.WriteMarkdown(string, MarkdownRenderOptions)"/>) that is laid out at the pane's width,
        /// so tables size their columns and the block reflows on resize. While streaming, the block is laid out
        /// again with everything received so far on each <see cref="AppendText"/>, which costs time linear in the
        /// block's size; that is fine for model replies of a few kilobytes. The text shown while streaming is
        /// <see cref="MarkdownRenderer.StreamingPrefix"/> of what has arrived: a half-received table row is held
        /// back until its newline arrives, and a table header until its delimiter row arrives, so a table never
        /// shows as text first. The options are copied on set, and a block in progress keeps the options it started with.
        /// </summary>
        public MarkdownRenderOptions? RenderOptions
        {
            get { lock (_Sync) { return _RenderOptions; } }
            set { lock (_Sync) { _RenderOptions = value?.Clone(); } }
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
                if (_BlockOptions != null)
                {
                    FinalizeMarkdownBlock(_BlockOptions);
                    return;
                }

                if (_RenderWhileStreaming)
                {
                    if (_HasLineInput)
                    {
                        CommitLine(_Partial.ToString().Replace("\r", string.Empty));
                        if (_Emitted == 0)
                            _Pane.WriteLine(StyledText.Empty);
                    }

                    _Block.Clear();
                    _LiveLine = null;
                    _InCodeBlock = false;
                    _Partial = new StringBuilder();
                    _HasLineInput = false;
                    _CanDefer = false;
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
            RuntimeCompat.ThrowIfNull(key, nameof(key));

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
            RuntimeCompat.ThrowIfNull(key, nameof(key));
            RuntimeCompat.ThrowIfNull(content, nameof(content));

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
            RuntimeCompat.ThrowIfNull(key, nameof(key));
            RuntimeCompat.ThrowIfNull(content, nameof(content));

            lock (_Sync)
            {
                if (!_Tracked.TryGetValue(key, out PaneLineHandle? handle))
                    throw new KeyNotFoundException("No tracked line for key '" + key + "'.");

                return handle.Update(content);
            }
        }

        private void StreamLines(string chunk)
        {
            if (chunk.Length == 0)
                return;
            _HasLineInput = true;
            int start = 0;
            int newline;
            while ((newline = chunk.IndexOf('\n', start)) >= 0)
            {
                lock (_Partial)
                    _Partial.Append(chunk, start, newline - start);
                string line = _Partial.ToString();
                CommitLine(line.EndsWith("\r", StringComparison.Ordinal) ? line.Substring(0, line.Length - 1) : line);
                _Partial = new StringBuilder();
                _CanDefer = false;
                start = newline + 1;
            }
            if (start == chunk.Length)
                return;

            StringBuilder pending = _Partial;
            lock (pending)
                pending.Append(chunk, start, chunk.Length - start);
            bool inCode = _InCodeBlock;
            if (_CanDefer && _LiveLine != null)
            {
                // Capture a prefix, not the transcript lock: readers hold the pane lock.
                int length = pending.Length;
                MarkdownStyles styles = _Styles;
                _Pane.DeferLine(_LiveLine.Id, () =>
                {
                    string snapshot;
                    lock (pending)
                        snapshot = pending.ToString(0, length);
                    return MarkdownRenderer.RenderLine(snapshot.Replace("\r", string.Empty), styles, ref inCode)!;
                });
                return;
            }
            string partial = pending.ToString();
            string trimmed = partial.Replace("\r", string.Empty).TrimStart();
            _CanDefer = trimmed.Length >= 3 && !trimmed.StartsWith("```", StringComparison.Ordinal);
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

        // Shows everything received so far as one Markdown block. Must be called under _Sync.
        private void StreamMarkdownBlock()
        {
            string block = _Block.ToString();
            _HeldPipeRow = MarkdownRenderer.StartsWithPipe(block, block.LastIndexOf('\n') + 1, block.Length);
            string source = MarkdownRenderer.StreamingPrefix(block);

            if (source.Trim().Length == 0)
                return;

            if (_MarkdownBlock == null)
                _MarkdownBlock = _Pane.WriteMarkdown(source, _BlockOptions!);
            else
                _MarkdownBlock.UpdateMarkdown(source);
        }

        // Commits the whole buffered block as Markdown and starts a new block. Must be called under _Sync.
        private void FinalizeMarkdownBlock(MarkdownRenderOptions options)
        {
            string markdown = _Block.ToString();
            _Block.Clear();
            if (markdown.Length > 0)
            {
                if (_MarkdownBlock != null)
                    _MarkdownBlock.UpdateMarkdown(markdown);
                else if (_LiveLine != null)
                    _LiveLine.UpdateMarkdown(markdown, options);
                else
                    _Pane.WriteMarkdown(markdown, options);
            }

            _MarkdownBlock = null;
            _LiveLine = null;
            _BlockOptions = null;
            _InCodeBlock = false;
            _HeldPipeRow = false;
            _Emitted = 0;
        }

        private static string CollapseToLine(string text)
        {
            return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
