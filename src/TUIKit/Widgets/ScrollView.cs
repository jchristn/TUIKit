namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A container that hosts a child widget larger than the visible region and scrolls it both
    /// vertically and horizontally, drawing scrollbars when the content overflows. The child is
    /// rendered at its full content size into an off-screen buffer, and a window of it is blitted into
    /// the viewport.
    /// </summary>
    public sealed class ScrollView : IWidget
    {
        private readonly IWidget _Child;
        private int _ContentWidth;
        private int _ContentHeight;
        private int _ScrollX;
        private int _ScrollY;

        /// <summary>
        /// Gets or sets a value indicating whether a vertical scrollbar is drawn when content
        /// overflows vertically. Defaults to true.
        /// </summary>
        public bool ShowVerticalScrollbar { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether a horizontal scrollbar is drawn when content
        /// overflows horizontally. Defaults to true.
        /// </summary>
        public bool ShowHorizontalScrollbar { get; set; } = true;

        /// <summary>
        /// Gets the current horizontal scroll offset in cells.
        /// </summary>
        public int ScrollX
        {
            get { return _ScrollX; }
        }

        /// <summary>
        /// Gets the current vertical scroll offset in cells.
        /// </summary>
        public int ScrollY
        {
            get { return _ScrollY; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollView"/> class.
        /// </summary>
        /// <param name="child">The child widget to scroll. Must not be null.</param>
        /// <param name="contentWidth">The full content width in cells. Must be greater than zero.</param>
        /// <param name="contentHeight">The full content height in cells. Must be greater than zero.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a content dimension is not positive.</exception>
        public ScrollView(IWidget child, int contentWidth, int contentHeight)
        {
            _Child = child ?? throw new ArgumentNullException(nameof(child));
            if (contentWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentWidth), contentWidth, "Content width must be greater than zero.");
            if (contentHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(contentHeight), contentHeight, "Content height must be greater than zero.");

            _ContentWidth = contentWidth;
            _ContentHeight = contentHeight;
        }

        /// <summary>
        /// Sets the content size, for content that changes size.
        /// </summary>
        /// <param name="width">The content width. Must be greater than zero.</param>
        /// <param name="height">The content height. Must be greater than zero.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public void SetContentSize(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");

            _ContentWidth = width;
            _ContentHeight = height;
        }

        /// <summary>
        /// Scrolls by the supplied deltas. Positive values move toward the end.
        /// </summary>
        /// <param name="dx">The horizontal delta in cells.</param>
        /// <param name="dy">The vertical delta in cells.</param>
        public void ScrollBy(int dx, int dy)
        {
            _ScrollX += dx;
            _ScrollY += dy;
            if (_ScrollX < 0)
                _ScrollX = 0;
            if (_ScrollY < 0)
                _ScrollY = 0;
        }

        /// <summary>
        /// Scrolls to an absolute position, clamped to the content.
        /// </summary>
        /// <param name="x">The horizontal offset in cells.</param>
        /// <param name="y">The vertical offset in cells.</param>
        public void ScrollTo(int x, int y)
        {
            _ScrollX = Math.Max(0, x);
            _ScrollY = Math.Max(0, y);
        }

        /// <summary>
        /// Handles arrow and page keys to scroll.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    ScrollBy(0, -1);
                    return true;
                case KeyCode.Down:
                    ScrollBy(0, 1);
                    return true;
                case KeyCode.Left:
                    ScrollBy(-1, 0);
                    return true;
                case KeyCode.Right:
                    ScrollBy(1, 0);
                    return true;
                case KeyCode.PageUp:
                    ScrollBy(0, -10);
                    return true;
                case KeyCode.PageDown:
                    ScrollBy(0, 10);
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

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int viewWidth = surface.Size.Width;
            int viewHeight = surface.Size.Height;
            if (viewWidth <= 0 || viewHeight <= 0)
                return;

            bool needVertical = ShowVerticalScrollbar && _ContentHeight > viewHeight;
            bool needHorizontal = ShowHorizontalScrollbar && _ContentWidth > viewWidth;
            int innerWidth = Math.Max(1, viewWidth - (needVertical ? 1 : 0));
            int innerHeight = Math.Max(1, viewHeight - (needHorizontal ? 1 : 0));

            int maxX = Math.Max(0, _ContentWidth - innerWidth);
            int maxY = Math.Max(0, _ContentHeight - innerHeight);
            if (_ScrollX > maxX)
                _ScrollX = maxX;
            if (_ScrollY > maxY)
                _ScrollY = maxY;

            CellBuffer content = new CellBuffer(_ContentWidth, _ContentHeight);
            _Child.Render(new BufferSurface(content));

            for (int y = 0; y < innerHeight; y++)
            {
                for (int x = 0; x < innerWidth; x++)
                {
                    int cx = _ScrollX + x;
                    int cy = _ScrollY + y;
                    if (cx < _ContentWidth && cy < _ContentHeight)
                        surface.Set(x, y, content.Get(cx, cy));
                }
            }

            if (needVertical)
                DrawVerticalScrollbar(surface, innerWidth, innerHeight, maxY);
            if (needHorizontal)
                DrawHorizontalScrollbar(surface, innerWidth, innerHeight, maxX);
        }

        private void DrawVerticalScrollbar(ISurface surface, int column, int height, int maxY)
        {
            CellStyle track = CellStyle.Default.WithForeground(Color.FromPalette(8));
            CellStyle thumb = CellStyle.Default.WithForeground(Color.FromPalette(7));
            int thumbSize = Math.Max(1, height * height / _ContentHeight);
            int thumbPos = maxY > 0 ? (height - thumbSize) * _ScrollY / maxY : 0;

            for (int y = 0; y < height; y++)
            {
                bool onThumb = y >= thumbPos && y < thumbPos + thumbSize;
                surface.Set(column, y, Cell.Glyph(onThumb ? "█" : "│", onThumb ? thumb : track, 1));
            }
        }

        private void DrawHorizontalScrollbar(ISurface surface, int width, int row, int maxX)
        {
            CellStyle track = CellStyle.Default.WithForeground(Color.FromPalette(8));
            CellStyle thumb = CellStyle.Default.WithForeground(Color.FromPalette(7));
            int thumbSize = Math.Max(1, width * width / _ContentWidth);
            int thumbPos = maxX > 0 ? (width - thumbSize) * _ScrollX / maxX : 0;

            for (int x = 0; x < width; x++)
            {
                bool onThumb = x >= thumbPos && x < thumbPos + thumbSize;
                surface.Set(x, row, Cell.Glyph(onThumb ? "█" : "─", onThumb ? thumb : track, 1));
            }
        }
    }
}
