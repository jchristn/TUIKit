namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;
    using TUIKit.Compatibility;

    internal sealed class PaneLine
    {
        private StyledText _Content;
        private Func<StyledText>? _PendingContent;

        internal void DeferContent(Func<StyledText> render)
        {
            Content = StyledText.Empty;
            _PendingContent = render;
        }
        private int _CachedWidth = -1;
        private IReadOnlyList<StyledText>? _CachedRows;
        private string? _MarkdownSource;
        private MarkdownRenderOptions? _MarkdownOptions;

        internal long Id { get; }

        internal StyledText Content
        {
            get
            {
                if (_PendingContent != null)
                {
                    _Content = _PendingContent();
                    _PendingContent = null;
                }
                return _Content;
            }
            set
            {
                _PendingContent = null;
                _Content = value;
                _MarkdownSource = null;
                _MarkdownOptions = null;
                _CachedRows = null;
                _CachedWidth = -1;
            }
        }

        internal bool IsMarkdown
        {
            get { return _MarkdownSource != null; }
        }

        internal MarkdownRenderOptions? MarkdownOptions
        {
            get { return _MarkdownOptions; }
        }

        internal PaneLine(long id, StyledText content)
        {
            Id = id;
            _Content = content;
        }

        internal void SetMarkdown(string source, MarkdownRenderOptions options)
        {
            RuntimeCompat.ThrowIfNull(source, nameof(source));
            RuntimeCompat.ThrowIfNull(options, nameof(options));

            _MarkdownSource = source;
            _MarkdownOptions = options;
            _PendingContent = null;
            _Content = StyledText.Empty;
            _CachedRows = null;
            _CachedWidth = -1;
        }

        // A Markdown line lays its source out at the given width, so a resize (a new width) reflows it.
        internal IReadOnlyList<StyledText> Wrapped(int width)
        {
            if (_CachedRows == null || _CachedWidth != width)
            {
                if (_MarkdownSource != null && _MarkdownOptions != null)
                    _CachedRows = MarkdownRenderer.RenderCore(_MarkdownSource, Math.Max(1, width), _MarkdownOptions, CancellationToken.None);
                else
                    _CachedRows = TextWrapper.Wrap(Content, width);

                _CachedWidth = width;
            }

            return _CachedRows;
        }
    }
}
