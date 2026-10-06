namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// Divides its region between two child widgets along one axis, drawing an optional divider
    /// between them. The split position is a ratio in [<see cref="MinRatio"/>, <see cref="MaxRatio"/>]
    /// that the user can drag with the arrow keys (Left/Right when horizontal, Up/Down when vertical),
    /// giving resizable panes. Because a child may itself be a <see cref="SplitView"/>, arbitrarily
    /// nested layouts compose from this one widget. Dragging the divider with the left button also
    /// resizes the split.
    /// </summary>
    /// <remarks>
    /// With <see cref="ForwardKeys"/> set the split view becomes a hierarchical focus scope
    /// (<see cref="IFocusContainer"/>): keys go to the focused pane first, Tab and Shift+Tab move between
    /// the panes (descending into nested containers and bubbling out at either end), a click focuses the
    /// pane under the pointer, and the arrow keys resize only when held with <see cref="ResizeModifiers"/>
    /// or when the focused pane does not consume them. Not thread-safe: use it from the UI loop.
    /// </remarks>
    public sealed class SplitView : IWidget, IFocusable, IMouseAware, IFocusContainer, IFocusAware, IThemeable, IFocusPathNode
    {
        private readonly IWidget _First;
        private readonly IWidget _Second;
        private int _LastFirstExtent;
        private int _LastDivider;
        private double _Ratio;
        private double _MinRatio = 0.1;
        private double _MaxRatio = 0.9;
        private double _ResizeStep = 0.05;
        private int _FocusedPane;
        private bool _Focused;
        private bool _Dragging;
        private int _LastExtent;
        private Rect _FirstInner;
        private Rect _SecondInner;
        private FocusFrameOptions _FrameOptions = new FocusFrameOptions();

        /// <summary>
        /// Gets or sets a value indicating whether keys are forwarded to the focused pane first, making the
        /// split view a hierarchical focus scope. Defaults to false, which keeps the original behavior (the
        /// arrow keys always resize and the panes never see keys).
        /// </summary>
        public bool ForwardKeys { get; set; }

        /// <summary>
        /// Gets or sets the modifiers that, held with an arrow key, always resize the split while
        /// <see cref="ForwardKeys"/> is set (before the focused pane sees the key). Defaults to Ctrl+Shift.
        /// </summary>
        public KeyModifiers ResizeModifiers { get; set; } = KeyModifiers.Ctrl | KeyModifiers.Shift;

        /// <summary>
        /// Gets or sets the style of the divider line. Defaults to a grey (palette 8) foreground.
        /// </summary>
        public CellStyle DividerStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets the first (left or top) child.
        /// </summary>
        public IWidget First
        {
            get { return _First; }
        }

        /// <summary>
        /// Gets the second (right or bottom) child.
        /// </summary>
        public IWidget Second
        {
            get { return _Second; }
        }

        /// <summary>
        /// Gets the child that holds focus one level down, or null when <see cref="ForwardKeys"/> is off (keys resize the split) or the focused pane is not focusable. Part of
        /// <see cref="IFocusPathNode"/>; the host uses it to build <see cref="FocusPath"/>.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return ForwardKeys ? Pane(_FocusedPane) as IFocusable : null; }
        }

        /// <summary>
        /// Gets or sets the focused pane while <see cref="ForwardKeys"/> is set: 0 for the first child, 1 for
        /// the second. Values are clamped to that range.
        /// </summary>
        public int FocusedPane
        {
            get { return _FocusedPane; }
            set { SetPane(value <= 0 ? 0 : 1); }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                IWidget pane = Pane(_FocusedPane);
                if (pane is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return pane as IFocusable ?? this;
            }
        }

        /// <summary>Gets or sets the split orientation.</summary>
        public SplitOrientation Orientation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether each pane is drawn inside a <see cref="FocusFrame"/>:
        /// the pane holding focus gets the heavy focused border and the other a normal border, so the user
        /// can see which side keys go to. A pane counts as focused while the split view holds focus with
        /// <see cref="ForwardKeys"/> set and that pane is <see cref="FocusedPane"/>. The frame takes the
        /// pane's outer ring of cells in both states, so moving focus never shifts content. With
        /// <see cref="FocusFrameOptions.JoinBorders"/> set on <see cref="FrameOptions"/>, the two frames share
        /// the divider line and meet in tee glyphs, and the focused frame is drawn whole over it. Defaults to
        /// false.
        /// </summary>
        public bool ShowPaneFrames { get; set; }

        /// <summary>
        /// Gets or sets the border and title style of the focused pane frame. Defaults to bold yellow
        /// (palette 11); <see cref="ApplyTheme"/> sets it from <see cref="Theme.FocusBorderRole"/>.
        /// </summary>
        public CellStyle FocusedFrameStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(11)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the border style of an unfocused pane frame. Defaults to gray (palette 8);
        /// <see cref="ApplyTheme"/> sets it from <see cref="Theme.Border"/>.
        /// </summary>
        public CellStyle FrameStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets a value indicating whether pane frames use ASCII glyphs (heavy ASCII while
        /// focused). Defaults to false; <see cref="ApplyTheme"/> copies <see cref="Theme.UseAsciiBorders"/>.
        /// </summary>
        public bool AsciiFrames { get; set; }

        /// <summary>
        /// Gets or sets the pane frame options (border styles, narrow fallback). Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public FocusFrameOptions FrameOptions
        {
            get { return _FrameOptions; }
            set { _FrameOptions = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>Gets or sets whether a divider line is drawn between the panes. Defaults to true.</summary>
        public bool ShowDivider { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum split ratio. Values are clamped to the range 0.0 through 1.0.
        /// Defaults to 0.1.
        /// </summary>
        public double MinRatio
        {
            get { return _MinRatio; }
            set { _MinRatio = value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value; }
        }

        /// <summary>
        /// Gets or sets the maximum split ratio. Values are clamped to the range 0.0 through 1.0.
        /// Defaults to 0.9.
        /// </summary>
        public double MaxRatio
        {
            get { return _MaxRatio; }
            set { _MaxRatio = value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value; }
        }

        /// <summary>
        /// Gets or sets the amount the ratio changes per resize key. Must be greater than 0.0 and no
        /// greater than 1.0. Defaults to 0.05.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not in the range (0.0, 1.0].</exception>
        public double ResizeStep
        {
            get { return _ResizeStep; }
            set
            {
                if (value <= 0.0 || value > 1.0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Resize step must be greater than 0.0 and no greater than 1.0.");

                _ResizeStep = value;
            }
        }

        /// <summary>
        /// Gets the current split ratio: the fraction of the area given to the first child.
        /// </summary>
        public double Ratio
        {
            get { return _Ratio; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitView"/> class.
        /// </summary>
        /// <param name="orientation">The split orientation.</param>
        /// <param name="first">The first (left or top) child. Must not be null.</param>
        /// <param name="second">The second (right or bottom) child. Must not be null.</param>
        /// <param name="ratio">The initial split ratio (0–1). Clamped to the min/max range.</param>
        /// <exception cref="ArgumentNullException">Thrown when a child is null.</exception>
        public SplitView(SplitOrientation orientation, IWidget first, IWidget second, double ratio = 0.5)
        {
            _First = first ?? throw new ArgumentNullException(nameof(first));
            _Second = second ?? throw new ArgumentNullException(nameof(second));
            Orientation = orientation;
            _Ratio = Clamp(ratio);
        }

        /// <summary>
        /// Resizes the split with the arrow keys aligned to the orientation.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key resized the split; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (ForwardKeys)
                return HandleForwardedKey(key);

            return HandleResizeKey(key);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (!ForwardKeys)
                return false;

            if (Pane(_FocusedPane) is IFocusContainer container && FocusScope.IsFocusable(container) && container.MoveFocus(forward))
                return true;

            int target = forward ? _FocusedPane + 1 : _FocusedPane - 1;
            while (target >= 0 && target <= 1)
            {
                if (Pane(target) is IFocusable focusable && FocusScope.IsFocusable(focusable))
                {
                    SetPane(target);
                    if (focusable is IFocusContainer entered)
                        entered.FocusEdge(forward);
                    return true;
                }

                target += forward ? 1 : -1;
            }

            return false;
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            if (!ForwardKeys)
                return;

            int pane = first ? 0 : 1;
            if (!(Pane(pane) is IFocusable candidate) || !FocusScope.IsFocusable(candidate))
                pane = 1 - pane;

            SetPane(pane);
            if (Pane(pane) is IFocusContainer container)
                container.FocusEdge(first);
        }

        /// <summary>
        /// Tracks focus and, with <see cref="ForwardKeys"/> set, forwards it to the focused pane. Part of
        /// <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the split view gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (ForwardKeys && Pane(_FocusedPane) is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme: <see cref="DividerStyle"/> from <see cref="Theme.Border"/>, then forwards the
        /// theme to both children.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            DividerStyle = theme.Border;
            FrameStyle = theme.Border;
            FocusedFrameStyle = FocusFrame.FocusedStyle(theme);
            AsciiFrames = theme.UseAsciiBorders;
            ThemeApplier.Apply(_First, theme);
            ThemeApplier.Apply(_Second, theme);
        }

        private bool HandleForwardedKey(KeyEvent key)
        {
            bool arrow = key.Code == KeyCode.Left || key.Code == KeyCode.Right || key.Code == KeyCode.Up || key.Code == KeyCode.Down;
            if (arrow && ResizeModifiers != KeyModifiers.None && key.Modifiers == ResizeModifiers)
                return HandleResizeKey(KeyEvent.Special(key.Code));

            if (Pane(_FocusedPane) is IFocusable focusable && FocusScope.IsFocusable(focusable) && focusable.HandleKey(key))
                return true;

            if (key.Code == KeyCode.Tab && (key.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
                return MoveFocus((key.Modifiers & KeyModifiers.Shift) == 0);

            if (arrow && key.Modifiers == KeyModifiers.None)
                return HandleResizeKey(key);

            return false;
        }

        private IWidget Pane(int index)
        {
            return index == 0 ? _First : _Second;
        }

        private void SetPane(int pane)
        {
            if (pane == _FocusedPane)
                return;

            IWidget previous = Pane(_FocusedPane);
            _FocusedPane = pane;
            if (ForwardKeys)
            {
                if (previous is IFocusAware previousAware)
                    previousAware.OnFocusChanged(false);
                if (Pane(pane) is IFocusAware nextAware)
                    nextAware.OnFocusChanged(_Focused);
            }
        }

        private bool HandleResizeKey(KeyEvent key)
        {
            if (Orientation == SplitOrientation.Horizontal)
            {
                if (key.Code == KeyCode.Left)
                {
                    _Ratio = Clamp(_Ratio - ResizeStep);
                    return true;
                }

                if (key.Code == KeyCode.Right)
                {
                    _Ratio = Clamp(_Ratio + ResizeStep);
                    return true;
                }
            }
            else
            {
                if (key.Code == KeyCode.Up)
                {
                    _Ratio = Clamp(_Ratio - ResizeStep);
                    return true;
                }

                if (key.Code == KeyCode.Down)
                {
                    _Ratio = Clamp(_Ratio + ResizeStep);
                    return true;
                }
            }

            return false;
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

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            bool join = ShowPaneFrames && _FrameOptions.JoinBorders;
            int divider = ShowDivider || join ? 1 : 0;
            _LastDivider = divider;
            _LastExtent = Orientation == SplitOrientation.Horizontal ? width : height;

            if (Orientation == SplitOrientation.Horizontal)
            {
                int usable = width - divider;
                if (usable < 2)
                {
                    _LastFirstExtent = width;
                    _LastDivider = 0;
                    RenderPane(surface, 0, new Rect(0, 0, width, height));
                    _SecondInner = default;
                    return;
                }

                int firstWidth = Clamp(usable, (int)Math.Round(_Ratio * usable));
                int secondWidth = usable - firstWidth;
                _LastFirstExtent = firstWidth;

                if (join)
                {
                    RenderJoined(surface, new Rect(0, 0, firstWidth + 1, height), new Rect(firstWidth, 0, secondWidth + 1, height));
                    return;
                }

                RenderPane(surface, 0, new Rect(0, 0, firstWidth, height));
                if (divider > 0)
                    surface.Fill(new Rect(firstWidth, 0, 1, height), Cell.Glyph("\u2502", DividerStyle, 1));
                RenderPane(surface, 1, new Rect(firstWidth + divider, 0, secondWidth, height));
            }
            else
            {
                int usable = height - divider;
                if (usable < 2)
                {
                    _LastFirstExtent = height;
                    _LastDivider = 0;
                    RenderPane(surface, 0, new Rect(0, 0, width, height));
                    _SecondInner = default;
                    return;
                }

                int firstHeight = Clamp(usable, (int)Math.Round(_Ratio * usable));
                int secondHeight = usable - firstHeight;
                _LastFirstExtent = firstHeight;

                if (join)
                {
                    RenderJoined(surface, new Rect(0, 0, width, firstHeight + 1), new Rect(0, firstHeight, width, secondHeight + 1));
                    return;
                }

                RenderPane(surface, 0, new Rect(0, 0, width, firstHeight));
                if (divider > 0)
                    surface.Fill(new Rect(0, firstHeight, width, 1), Cell.Glyph("\u2500", DividerStyle, 1));
                RenderPane(surface, 1, new Rect(0, firstHeight + divider, width, secondHeight));
            }
        }

        private void RenderPane(ISurface surface, int pane, Rect rect)
        {
            Rect inner = rect;
            if (ShowPaneFrames)
            {
                bool focused = _Focused && ForwardKeys && pane == _FocusedPane;
                FocusFrame.Draw(surface, rect, focused, FocusedFrameStyle, FrameStyle, AsciiFrames, _FrameOptions);
                inner = InnerRect(rect);
            }

            if (pane == 0)
                _FirstInner = inner;
            else
                _SecondInner = inner;

            if (inner.Width > 0 && inner.Height > 0)
                Pane(pane).Render(new SurfaceView(surface, inner));
            if (ShowPaneFrames)
                FocusFrame.ApplyNarrowFocus(surface, rect, _Focused && ForwardKeys && pane == _FocusedPane, _FrameOptions);
        }

        // Joined frames share the divider line: both frames include it, the unfocused one is drawn first
        // and the focused one last, so the shared line takes the focused weight and meets in tees.
        private void RenderJoined(ISurface surface, Rect firstFrame, Rect secondFrame)
        {
            int focused = _Focused && ForwardKeys ? _FocusedPane : -1;
            int firstDrawn = focused == 0 ? 1 : 0;
            for (int step = 0; step < 2; step++)
            {
                int pane = step == 0 ? firstDrawn : 1 - firstDrawn;
                FocusFrame.Draw(surface, pane == 0 ? firstFrame : secondFrame, pane == focused, FocusedFrameStyle, FrameStyle, AsciiFrames, _FrameOptions);
            }

            _FirstInner = InnerRect(firstFrame);
            _SecondInner = InnerRect(secondFrame);
            if (_FirstInner.Width > 0 && _FirstInner.Height > 0)
                _First.Render(new SurfaceView(surface, _FirstInner));
            if (_SecondInner.Width > 0 && _SecondInner.Height > 0)
                _Second.Render(new SurfaceView(surface, _SecondInner));
            FocusFrame.ApplyNarrowFocus(surface, firstFrame, focused == 0, _FrameOptions);
            FocusFrame.ApplyNarrowFocus(surface, secondFrame, focused == 1, _FrameOptions);
        }

        private Rect InnerRect(Rect rect)
        {
            return FocusFrame.ContentRect(rect, _FrameOptions);
        }

        /// <summary>
        /// Routes the mouse to the pane under the pointer, forwarding it (in that pane's local coordinates) to
        /// the child when the child is itself mouse-aware. A click on the divider is ignored. Coordinates are
        /// widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a pane consumed the event; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            int position = Orientation == SplitOrientation.Horizontal ? mouse.X : mouse.Y;

            // Divider drag: a left press on the divider starts it, moves with the button held resize, and
            // the release ends it.
            if (_Dragging)
            {
                if (mouse.Kind == MouseEventKind.Release || mouse.Kind == MouseEventKind.Leave)
                {
                    _Dragging = false;
                    return true;
                }

                if (mouse.Kind == MouseEventKind.Move)
                {
                    int usable = _LastExtent - _LastDivider;
                    if (usable >= 2)
                        _Ratio = Clamp((double)position / usable);
                    return true;
                }
            }

            if (_LastDivider > 0 && position == _LastFirstExtent && mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                _Dragging = true;
                return true;
            }

            int secondStart = _LastFirstExtent + _LastDivider;
            if (position < _LastFirstExtent)
                return ForwardToPane(0, mouse);
            if (position >= secondStart)
                return ForwardToPane(1, mouse);

            return false;
        }

        private bool ForwardToPane(int pane, MouseEvent mouse)
        {
            bool press = mouse.Kind == MouseEventKind.Press;
            if (ForwardKeys && press && Pane(pane) is IFocusable focusable && FocusScope.IsFocusable(focusable))
                SetPane(pane);

            Rect inner = pane == 0 ? _FirstInner : _SecondInner;
            if (!inner.Contains(new Point(mouse.X, mouse.Y)))
            {
                if (mouse.Kind == MouseEventKind.Leave && inner.Width > 0 && inner.Height > 0)
                {
                    int x = Math.Min(Math.Max(mouse.X, inner.X), inner.X + inner.Width - 1);
                    int y = Math.Min(Math.Max(mouse.Y, inner.Y), inner.Y + inner.Height - 1);
                    return ForwardTo(Pane(pane), mouse, x - inner.X, y - inner.Y);
                }

                return press && ShowPaneFrames && ForwardKeys;
            }

            return ForwardTo(Pane(pane), mouse, mouse.X - inner.X, mouse.Y - inner.Y);
        }

        private static bool ForwardTo(IWidget child, MouseEvent mouse, int localX, int localY)
        {
            if (child is IMouseAware aware)
                return aware.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, localX, localY, mouse.Modifiers, mouse.ClickCount));

            return false;
        }

        private double Clamp(double ratio)
        {
            double min = Math.Min(_MinRatio, _MaxRatio);
            double max = Math.Max(_MinRatio, _MaxRatio);
            if (ratio < min)
                return min;
            if (ratio > max)
                return max;

            return ratio;
        }

        private static int Clamp(int usable, int value)
        {
            if (value < 1)
                return 1;
            if (value > usable - 1)
                return usable - 1;

            return value;
        }
    }
}
