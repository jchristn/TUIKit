namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;

    /// <summary>
    /// Caches width-aware Markdown layouts for apps that draw many Markdown blocks inside their own widgets: a chat
    /// transcript of message bubbles, a list with rendered descriptions, or a details panel with several Markdown
    /// fields. <see cref="GetRows"/> returns the cached rows for a text, width, and options instance, or renders
    /// and stores them, so the same text is not laid out again on every frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry is identified by the Markdown text (ordinal comparison), the width, and the options instance
    /// (reference comparison). Keep and reuse one options instance: a new instance, such as each result of
    /// <see cref="MarkdownRenderOptions.WithTheme"/>, misses every entry made with the old one, so call
    /// <c>WithTheme</c> once per theme change, not once per frame. After changing an options instance in place,
    /// call <see cref="Clear"/>.
    /// </para>
    /// <para>
    /// The cache holds one width at a time: a request at a width with no entries first drops every entry at other
    /// widths, so resizing never leaves a stale copy per width behind. An app that lays the same text out at two
    /// widths at once (for example a list and a preview) uses one cache per surface. Beyond that, the least
    /// recently used entry is evicted once <see cref="Capacity"/> is reached.
    /// </para>
    /// <para>
    /// Thread-safe: renders may be prepared off the UI thread. Layout runs outside the lock, so two threads that
    /// miss on the same entry at once may both render it; the first stored result is kept and returned to both.
    /// The options passed in must not be mutated while a render is using them.
    /// </para>
    /// </remarks>
    public sealed class MarkdownLayoutCache
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<MarkdownLayoutCacheKey, LinkedListNode<MarkdownLayoutCacheKey>> _Entries =
            new Dictionary<MarkdownLayoutCacheKey, LinkedListNode<MarkdownLayoutCacheKey>>();
        private readonly LinkedList<MarkdownLayoutCacheKey> _Recency = new LinkedList<MarkdownLayoutCacheKey>();
        private int _Capacity = 256;
        private int _Width = -1;

        /// <summary>
        /// Gets or sets the maximum number of cached layouts. Defaults to 256. Minimum 1, maximum 1,000,000.
        /// Lowering it evicts the least recently used entries at once. Thread-safe.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 1,000,000.</exception>
        public int Capacity
        {
            get { lock (_Sync) { return _Capacity; } }
            set
            {
                if (value < 1 || value > 1000000)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Capacity must be between 1 and 1,000,000.");

                lock (_Sync)
                {
                    _Capacity = value;
                    TrimLocked();
                }
            }
        }

        /// <summary>
        /// Gets the number of cached layouts. Thread-safe.
        /// </summary>
        public int Count
        {
            get { lock (_Sync) { return _Entries.Count; } }
        }

        /// <summary>
        /// Gets the width of the cached layouts, or 0 when the cache is empty. Thread-safe.
        /// </summary>
        public int Width
        {
            get { lock (_Sync) { return _Entries.Count == 0 ? 0 : _Width; } }
        }

        /// <summary>
        /// Returns the layout of Markdown at a width with the given options, from the cache when it holds one, or
        /// renders it with <see cref="MarkdownRenderer.Render(string, int, MarkdownRenderOptions)"/> and stores it.
        /// A hit returns the same list instance as the call that stored it. Thread-safe.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="width">The available width in columns. Must be 1 or greater. A width other than the
        /// cached one drops every cached entry first.</param>
        /// <param name="options">How the output should look. Must not be null. Compared by reference: reuse one
        /// instance, and call <see cref="Clear"/> after changing it in place.</param>
        /// <returns>The rendered rows, at least one. Never null. Do not modify.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        public IReadOnlyList<StyledText> GetRows(string markdown, int width, MarkdownRenderOptions options)
        {
            return GetRowsCore(markdown, width, options, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously returns the layout of Markdown at a width, as <see cref="GetRows"/> does, checking the
        /// token before the lookup and between top-level blocks of a render. A cancelled render stores nothing.
        /// The work runs on the calling thread until it completes. Thread-safe.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="width">The available width in columns. Must be 1 or greater.</param>
        /// <param name="options">How the output should look. Must not be null. Compared by reference.</param>
        /// <param name="token">Cancels the render.</param>
        /// <returns>The rendered rows, at least one. Never null. Do not modify.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="token"/> is cancelled.</exception>
        public Task<IReadOnlyList<StyledText>> GetRowsAsync(string markdown, int width, MarkdownRenderOptions options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(GetRowsCore(markdown, width, options, token));
        }

        /// <summary>
        /// Removes every cached layout. Call it after changing an options instance in place. Thread-safe.
        /// </summary>
        public void Clear()
        {
            lock (_Sync)
            {
                _Entries.Clear();
                _Recency.Clear();
                _Width = -1;
            }
        }

        private IReadOnlyList<StyledText> GetRowsCore(string markdown, int width, MarkdownRenderOptions options, CancellationToken token)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be 1 or greater.");

            MarkdownLayoutCacheKey probe = new MarkdownLayoutCacheKey(markdown, options);
            lock (_Sync)
            {
                if (width == _Width && _Entries.TryGetValue(probe, out LinkedListNode<MarkdownLayoutCacheKey>? hit))
                {
                    _Recency.Remove(hit);
                    _Recency.AddFirst(hit);
                    return hit.Value.Rows!;
                }
            }

            IReadOnlyList<StyledText> rows = MarkdownRenderer.RenderCore(markdown, width, options, token);
            lock (_Sync)
            {
                if (width != _Width)
                {
                    _Entries.Clear();
                    _Recency.Clear();
                    _Width = width;
                }
                else if (_Entries.TryGetValue(probe, out LinkedListNode<MarkdownLayoutCacheKey>? raced))
                {
                    return raced.Value.Rows!;
                }

                probe.Rows = rows;
                LinkedListNode<MarkdownLayoutCacheKey> node = _Recency.AddFirst(probe);
                _Entries[probe] = node;
                TrimLocked();
                return rows;
            }
        }

        private void TrimLocked()
        {
            while (_Entries.Count > _Capacity && _Recency.Last != null)
            {
                LinkedListNode<MarkdownLayoutCacheKey> oldest = _Recency.Last;
                _Recency.RemoveLast();
                _Entries.Remove(oldest.Value);
            }
        }
    }
}
