namespace TUIKit.Content
{
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A committed pane line: its stable identifier, its styled content, and a cache of the content
    /// wrapped to the most recent render width so a frame does not rewrap unchanged scrollback.
    /// </summary>
    internal sealed class PaneLine
    {
        private StyledText _Content;
        private int _CachedWidth = -1;
        private IReadOnlyList<StyledText>? _CachedRows;

        internal long Id { get; }

        internal StyledText Content
        {
            get { return _Content; }
            set
            {
                _Content = value;
                _CachedRows = null;
                _CachedWidth = -1;
            }
        }

        internal PaneLine(long id, StyledText content)
        {
            Id = id;
            _Content = content;
        }

        internal IReadOnlyList<StyledText> Wrapped(int width)
        {
            if (_CachedRows == null || _CachedWidth != width)
            {
                _CachedRows = TextWrapper.Wrap(_Content, width);
                _CachedWidth = width;
            }

            return _CachedRows;
        }
    }
}
