namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;

    internal sealed class PaneLine
    {
        private StyledText _Content;
        private int _CachedWidth = -1;
        private IReadOnlyList<StyledText>? _CachedRows;
        private string? _MarkdownSource;
        private MarkdownRenderOptions? _MarkdownOptions;

        internal long Id { get; }

        internal StyledText Content
        {
            get { return _Content; }
            set
            {
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
            _MarkdownSource = source ?? throw new ArgumentNullException(nameof(source));
            _MarkdownOptions = options ?? throw new ArgumentNullException(nameof(options));
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
                    _CachedRows = TextWrapper.Wrap(_Content, width);

                _CachedWidth = width;
            }

            return _CachedRows;
        }
    }
}
