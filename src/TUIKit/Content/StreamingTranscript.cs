namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// Projects a stream of text and keyed status lines onto a scrollback <see cref="Pane"/>. Streaming
    /// text is buffered into the current block and shown on a single live line as it arrives; calling
    /// <see cref="FinalizeBlock"/> re-renders the buffered block as Markdown into the pane. Separately,
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
        /// Appends text to the current block and updates the single live line that shows it as it
        /// streams. Newlines in the accumulated block collapse to spaces on the live line; the block is
        /// rendered with its structure intact by <see cref="FinalizeBlock"/>.
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
                string markdown = _Block.ToString();
                _Block.Clear();

                if (markdown.Length == 0)
                {
                    _LiveLine = null;
                    return;
                }

                IReadOnlyList<StyledText> lines = MarkdownRenderer.Render(markdown);
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

        private static string CollapseToLine(string text)
        {
            return text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
