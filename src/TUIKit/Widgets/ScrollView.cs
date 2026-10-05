namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A container that hosts a child widget larger than the visible region and scrolls it both
    /// vertically and horizontally, drawing scrollbars when the content overflows. The child is
    /// rendered at its full content size into an off-screen buffer, and a window of it is blitted into
    /// the viewport. Mouse events other than the wheel are forwarded to the child in content
    /// coordinates (see <see cref="ForwardMouse"/>).
    /// </summary>
    /// <remarks>
    /// With <see cref="ForwardKeys"/> set the scroll view becomes a hierarchical focus scope
    /// (<see cref="IFocusContainer"/>): keys go to the child first and scroll the view only when the child
    /// does not consume them; Tab traversal descends into the child when it is a container. Combine with
    /// <see cref="IScrollExtent"/> on the child so the focused part stays visible. Not thread-safe: use it
    /// from the UI loop.
    /// </remarks>
    public sealed class ScrollView : IWidget, IFocusable, IMouseAware, IFocusContainer, IFocusAware, IThemeable, IFocusPathNode
    {
        private readonly IWidget _Child;
        private int _ContentWidth;
        private int _ContentHeight;
        private int _ScrollX;
        private int _ScrollY;
        private int _LastViewportHeight;
        private int _LastViewportWidth;

        /// <summary>
        /// Gets or sets a value indicating whether keys go to the child first. Defaults to false, which
        /// keeps the original behavior (the view consumes its scroll keys and the child never sees keys).
        /// </summary>
        public bool ForwardKeys { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether non-wheel mouse events inside the viewport are forwarded
        /// to the child, translated into content coordinates, when it implements <see cref="IMouseAware"/>.
        /// Wheel events always scroll the view. Defaults to true.
        /// </summary>
        public bool ForwardMouse { get; set; } = true;

        /// <summary>
        /// Gets the child that holds focus one level down, or null when <see cref="ForwardKeys"/> is off (keys scroll the view) or the child is not focusable. Part of
        /// <see cref="IFocusPathNode"/>; the host uses it to build <see cref="FocusPath"/>.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return ForwardKeys ? _Child as IFocusable : null; }
        }

        /// <summary>
        /// Gets the hosted child.
        /// </summary>
        public IWidget Child
        {
            get { return _Child; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                if (_Child is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return _Child as IFocusable ?? this;
            }
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            return ForwardKeys && _Child is IFocusContainer container && container.MoveFocus(forward);
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            if (ForwardKeys && _Child is IFocusContainer container)
                container.FocusEdge(first);
        }

        /// <summary>
        /// Forwards focus changes to the child while <see cref="ForwardKeys"/> is set. Part of
        /// <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the view gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            if (ForwardKeys && _Child is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme: <see cref="TrackStyle"/> from <see cref="Theme.Border"/> and
        /// <see cref="ThumbStyle"/> from <see cref="Theme.Accent"/>, then forwards it to the child.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            TrackStyle = theme.Border;
            ThumbStyle = theme.Accent;
            ThemeApplier.Apply(_Child, theme);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the view scrolls to keep the child's focused region
        /// visible when the child implements <see cref="IScrollExtent"/>. Defaults to true.
        /// </summary>
        public bool AutoScrollToFocus { get; set; } = true;

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
        /// Gets or sets the style of the scrollbar track (the groove behind the thumb). Applies to
        /// both the vertical and horizontal scrollbars. Defaults to a grey (palette 8) foreground.
        /// </summary>
        public CellStyle TrackStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of the scrollbar thumb (the draggable indicator). Applies to both
        /// the vertical and horizontal scrollbars. Defaults to a light-grey (palette 7) foreground.
        /// </summary>
        public CellStyle ThumbStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(7));

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
        /// Scrolls the minimum amount so the vertical range [<paramref name="top"/>,
        /// <paramref name="top"/> + <paramref name="height"/>) is visible, based on the most recent
        /// viewport height. Out-of-range values are clamped rather than throwing; the call is a no-op
        /// until the view has been rendered at least once.
        /// </summary>
        /// <param name="top">The top of the range in content coordinates.</param>
        /// <param name="height">The height of the range in cells.</param>
        public void EnsureVisible(int top, int height)
        {
            if (_LastViewportHeight <= 0)
                return;

            int clampedTop = Math.Max(0, top);
            int clampedHeight = Math.Max(0, height);

            if (clampedTop < _ScrollY)
                _ScrollY = clampedTop;
            else if (clampedTop + clampedHeight > _ScrollY + _LastViewportHeight)
                _ScrollY = Math.Max(0, clampedTop + clampedHeight - _LastViewportHeight);
        }

        /// <summary>
        /// Handles arrow keys (one cell), PageUp/PageDown (ten rows), and Home/End (jump to the top or
        /// bottom of the content) to scroll vertically, and Left/Right to scroll horizontally.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (ForwardKeys && _Child is IFocusable focusable && FocusScope.IsFocusable(focusable) && focusable.HandleKey(key))
                return true;

            if (ForwardKeys && key.Code == KeyCode.Tab)
                return false;

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
                case KeyCode.Home:
                    // Jump to the top of the content. The horizontal offset is left unchanged.
                    ScrollTo(_ScrollX, 0);
                    return true;
                case KeyCode.End:
                    // Jump to the bottom; Render clamps the offset to the last full viewport.
                    ScrollTo(_ScrollX, _ContentHeight);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Scrolls vertically in response to a mouse wheel event. Part of <see cref="IMouseAware"/>;
        /// the host forwards wheel events when the pointer is over this view.
        /// </summary>
        /// <param name="mouse">The mouse event in view-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a wheel event scrolled the view; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (ForwardMouse && mouse.Kind != MouseEventKind.Wheel && _Child is IMouseAware child)
            {
                if (mouse.Kind == MouseEventKind.Leave || (mouse.X >= 0 && mouse.Y >= 0
                    && (_LastViewportWidth <= 0 || mouse.X < _LastViewportWidth)
                    && (_LastViewportHeight <= 0 || mouse.Y < _LastViewportHeight)))
                {
                    return child.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X + _ScrollX, mouse.Y + _ScrollY, mouse.Modifiers, mouse.ClickCount));
                }

                return false;
            }

            switch (mouse.Button)
            {
                case MouseButton.WheelUp:
                    ScrollBy(0, -3);
                    return true;
                case MouseButton.WheelDown:
                    ScrollBy(0, 3);
                    return true;
                case MouseButton.WheelLeft:
                    ScrollBy(-3, 0);
                    return true;
                case MouseButton.WheelRight:
                    ScrollBy(3, 0);
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
            _LastViewportHeight = innerHeight;
            _LastViewportWidth = innerWidth;

            CellBuffer content = new CellBuffer(_ContentWidth, _ContentHeight);
            _Child.Render(new BufferSurface(content));

            if (AutoScrollToFocus && _Child is IScrollExtent extent && extent.TryGetFocusRect(out Rect focus))
                EnsureVisible(focus.Y, focus.Height);

            int maxX = Math.Max(0, _ContentWidth - innerWidth);
            int maxY = Math.Max(0, _ContentHeight - innerHeight);
            if (_ScrollX > maxX)
                _ScrollX = maxX;
            if (_ScrollY > maxY)
                _ScrollY = maxY;

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
            CellStyle track = TrackStyle;
            CellStyle thumb = ThumbStyle;
            int thumbSize = Math.Max(1, height * height / _ContentHeight);
            int thumbPos = maxY > 0 ? (height - thumbSize) * _ScrollY / maxY : 0;

            for (int y = 0; y < height; y++)
            {
                bool onThumb = y >= thumbPos && y < thumbPos + thumbSize;
                surface.Set(column, y, Cell.Glyph(onThumb ? "\u2588" : "\u2502", onThumb ? thumb : track, 1));
            }
        }

        private void DrawHorizontalScrollbar(ISurface surface, int width, int row, int maxX)
        {
            CellStyle track = TrackStyle;
            CellStyle thumb = ThumbStyle;
            int thumbSize = Math.Max(1, width * width / _ContentWidth);
            int thumbPos = maxX > 0 ? (width - thumbSize) * _ScrollX / maxX : 0;

            for (int x = 0; x < width; x++)
            {
                bool onThumb = x >= thumbPos && x < thumbPos + thumbSize;
                surface.Set(x, row, Cell.Glyph(onThumb ? "\u2588" : "\u2500", onThumb ? thumb : track, 1));
            }
        }
    }
}
