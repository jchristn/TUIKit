namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Theming;

    /// <summary>
    /// A container that lays out child panes along one axis and frames each of them, with neighbours
    /// sharing one border line and the focused pane drawn whole in the focus style: a filter row above a
    /// grid above a detail strip, or a list beside a transcript. A <see cref="FramedStack"/> nested inside
    /// another joins its lines with the parent's, so stacks inside stacks form a grid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Children are sized with <see cref="StackSize"/>. When the fixed lengths and minimums do not fit,
    /// children are dropped from the end: a dropped child is not drawn, is skipped by focus, and loses
    /// focus if it had it (see <see cref="FocusScope.RepairFocus"/>). Hidden children
    /// (<see cref="IHideable"/>) take no space.
    /// </para>
    /// <para>
    /// Focus runs on a <see cref="FocusScope"/> that starts inactive: Tab and Shift+Tab move between the
    /// focusable children (descending into containers), and focus leaves the stack at either end so the
    /// host moves on. Children that are not <see cref="IFocusable"/> are laid out and framed but never
    /// focused. A left click on a child focuses it and reaches the child in its own content coordinates.
    /// <see cref="Overlay"/> shows a drawer or inline panel over part of the stack; while shown it takes
    /// focus and the focused frame. Key hints need nothing from the stack: the focused child is on the
    /// focus path, so its own <see cref="IKeyHintSource"/> hints are used.
    /// </para>
    /// <para>Not thread-safe: build and use it on the UI thread.</para>
    /// </remarks>
    public sealed class FramedStack : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware, IThemeable, IFocusChildren
    {
        private readonly List<FramedStackSlot> _Slots = new List<FramedStackSlot>();
        private readonly FocusScope _Scope = new FocusScope(false);
        private FocusFrameOptions _FrameOptions = new FocusFrameOptions { JoinBorders = true };
        private bool _Focused;
        private IWidget? _Overlay;
        private Rect _LastOuter;
        private Rect _OverlayOuter;
        private Rect _OverlayContent;
        private FramedStackSlot? _Hovered;
        private bool _OverlayHovered;
        private bool _PathFocusedForOverlay;

        /// <summary>
        /// Initializes a new instance of the <see cref="FramedStack"/> class.
        /// </summary>
        /// <param name="orientation">The axis children are laid out along:
        /// <see cref="SplitOrientation.Vertical"/> (the default) stacks them top to bottom,
        /// <see cref="SplitOrientation.Horizontal"/> places them left to right.</param>
        public FramedStack(SplitOrientation orientation = SplitOrientation.Vertical)
        {
            Orientation = orientation;
        }

        /// <summary>
        /// Gets or sets the axis children are laid out along. <see cref="SplitOrientation.Vertical"/> stacks
        /// them top to bottom; <see cref="SplitOrientation.Horizontal"/> places them left to right.
        /// </summary>
        public SplitOrientation Orientation { get; set; }

        /// <summary>
        /// Gets or sets the frame options. Defaults to a new <see cref="FocusFrameOptions"/> with
        /// <see cref="FocusFrameOptions.JoinBorders"/> on, so neighbours share their border line and the
        /// focused frame is drawn whole (<see cref="FocusFrameOptions.FocusedJoinMode"/>). Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public FocusFrameOptions FrameOptions
        {
            get { return _FrameOptions; }
            set { _FrameOptions = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether children are framed. Defaults to true. When false,
        /// children are laid out edge to edge with no lines and the stack shows no focus of its own.
        /// </summary>
        public bool ShowFrames { get; set; } = true;

        /// <summary>
        /// Gets or sets the style of unfocused frames. Defaults to dim gray (palette 8).
        /// </summary>
        public CellStyle FrameStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of the focused frame. Defaults to bold bright yellow (palette 11).
        /// </summary>
        public CellStyle FocusedFrameStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(11)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of the focused frame's title, or null to use <see cref="FocusedFrameStyle"/>.
        /// Defaults to null.
        /// </summary>
        public CellStyle? FocusedTitleStyle { get; set; }

        /// <summary>
        /// Gets or sets the style used to clear the stack's area before drawing. Defaults to
        /// <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle BackgroundStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets a value indicating whether frames use ASCII glyphs (heavy ASCII while focused).
        /// Defaults to false. <see cref="ApplyTheme"/> copies <see cref="Theme.UseAsciiBorders"/>.
        /// </summary>
        public bool AsciiFrames { get; set; }

        /// <summary>
        /// Gets the number of children.
        /// </summary>
        public int Count
        {
            get { return _Slots.Count; }
        }

        /// <summary>
        /// Gets the children in layout order. Never null.
        /// </summary>
        public IReadOnlyList<IWidget> Children
        {
            get
            {
                List<IWidget> children = new List<IWidget>(_Slots.Count);
                for (int i = 0; i < _Slots.Count; i++)
                    children.Add(_Slots[i].Widget);
                return children;
            }
        }

        /// <summary>
        /// Gets the child that holds the stack's focus (the overlay while one is shown), or null.
        /// </summary>
        public IWidget? FocusedWidget
        {
            get
            {
                if (_Overlay != null)
                    return _Overlay;

                return (_Scope.FocusedChild as FramedStackSlot)?.Widget;
            }
        }

        /// <summary>
        /// Gets or sets a widget shown over part of the stack, such as a drawer or an inline panel, or null
        /// for none. While shown it is framed (when <see cref="ShowFrames"/> is on), takes the stack's focus
        /// and the focused frame, and receives keys and clicks inside it; the children behind it draw
        /// plain. A click outside it closes it. Setting null closes it and gives focus back to the child
        /// that had it. Defaults to null.
        /// </summary>
        public IWidget? Overlay
        {
            get { return _Overlay; }
            set
            {
                if (ReferenceEquals(value, _Overlay))
                    return;

                IWidget? previous = _Overlay;
                _Overlay = value;
                if (previous is IFocusAware previousAware)
                    previousAware.OnFocusChanged(false);

                if (value == null)
                {
                    _OverlayOuter = default;
                    _OverlayContent = default;
                    _Scope.OnFocusChanged(_Focused);
                    return;
                }

                if (previous == null)
                    _Scope.OnFocusChanged(false);
                if (value is IFocusAware aware)
                    aware.OnFocusChanged(_Focused);
                if (value is IFocusContainer container)
                    container.FocusEdge(true);
            }
        }

        /// <summary>
        /// Gets or sets where <see cref="Overlay"/> goes: a function from the stack's rectangle to the
        /// overlay's outer rectangle (frame included), in the same coordinates; the result is clipped to
        /// the stack. Null (the default) places it as a drawer over the right half.
        /// </summary>
        public Func<Rect, Rect>? OverlayRect { get; set; }

        /// <summary>
        /// Gets or sets the title drawn on the overlay's frame, or null. Defaults to null.
        /// </summary>
        public string? OverlayTitle { get; set; }

        /// <summary>
        /// Gets the focused leaf (see <see cref="IFocusContainer.FocusedLeaf"/>).
        /// </summary>
        public IFocusable? FocusedLeaf
        {
            get
            {
                if (_Overlay != null)
                {
                    if (_Overlay is IFocusContainer container)
                        return container.FocusedLeaf ?? container;
                    return _Overlay as IFocusable ?? this;
                }

                return _Scope.FocusedLeaf;
            }
        }

        /// <summary>
        /// Gets the next node on the focus path: the overlay while one is shown, otherwise the stack's
        /// focus scope (whose own focused child is the focused pane).
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _Overlay != null ? _Overlay as IFocusable : _Scope; }
        }

        bool IFocusChildren.HasFocusableChild
        {
            get { return _Overlay != null ? FocusScope.IsFocusable(_Overlay) : ((IFocusChildren)_Scope).HasFocusableChild; }
        }

        bool IFocusChildren.HasTabStopChild
        {
            get { return _Overlay != null ? FocusScope.IsTabStop(_Overlay) : ((IFocusChildren)_Scope).HasTabStopChild; }
        }

        /// <summary>
        /// Adds a child at the end of the stack. The first focusable child added holds the stack's focus.
        /// </summary>
        /// <param name="child">The child. Must not be null and must not already be in the stack.</param>
        /// <param name="size">How the child is sized along the axis. Must not be null.</param>
        /// <param name="title">An optional title for the child's frame, or null.</param>
        /// <returns>This stack, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> or
        /// <paramref name="size"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="child"/> is already in the stack.</exception>
        public FramedStack Add(IWidget child, StackSize size, string? title = null)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (size == null)
                throw new ArgumentNullException(nameof(size));
            if (Find(child) != null)
                throw new ArgumentException("The widget is already in this stack.", nameof(child));

            FramedStackSlot slot = new FramedStackSlot(child, size, title) { LaidOut = true };
            _Slots.Add(slot);
            if (child is IFocusable)
                _Scope.Add(slot);
            return this;
        }

        /// <summary>
        /// Removes a child. When it held focus, focus moves to the next focusable child.
        /// </summary>
        /// <param name="child">The child. Must not be null.</param>
        /// <returns><c>true</c> when the child was removed; <c>false</c> when it was not in the stack.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        public bool Remove(IWidget child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            FramedStackSlot? slot = Find(child);
            if (slot == null)
                return false;

            _Slots.Remove(slot);
            if (child is IFocusable)
                _Scope.Remove(slot);
            if (ReferenceEquals(_Hovered, slot))
                _Hovered = null;
            return true;
        }

        /// <summary>
        /// Gives the stack's focus to a child, as a click on it would. The child need not be a tab stop.
        /// </summary>
        /// <param name="child">The child. Must not be null; must be in the stack and focusable.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="child"/> is not in the stack or
        /// does not implement <see cref="IFocusable"/>.</exception>
        public void Focus(IWidget child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            FramedStackSlot? slot = Find(child);
            if (slot == null)
                throw new ArgumentException("The widget is not in this stack.", nameof(child));
            if (!(child is IFocusable))
                throw new ArgumentException("The widget is not focusable.", nameof(child));

            _Scope.SetFocus(slot);
        }

        /// <summary>
        /// Returns the rectangle a child's content was given in the last render, in this stack's
        /// coordinates, or an empty rectangle when the child was not laid out (or before the first render).
        /// </summary>
        /// <param name="child">The child. Must not be null.</param>
        /// <returns>The content rectangle; may be empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        public Rect ContentRectOf(IWidget child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            FramedStackSlot? slot = Find(child);
            if (slot == null || !slot.LaidOut)
                return new Rect(0, 0, 0, 0);

            return new Rect(slot.Content.X - _LastOuter.X, slot.Content.Y - _LastOuter.Y, slot.Content.Width, slot.Content.Height);
        }

        /// <summary>
        /// Routes a key to the overlay while one is shown, otherwise to the focused child, then uses Tab
        /// and Shift+Tab to move between children. Returns false at either end so focus can leave the stack.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (_Overlay != null)
                return _Overlay is IFocusable overlay && overlay.HandleKey(key);

            return _Scope.HandleKey(key);
        }

        /// <summary>
        /// Moves focus to the next or previous focusable child (see <see cref="IFocusContainer.MoveFocus"/>).
        /// While an overlay is shown, only the overlay's own traversal is used.
        /// </summary>
        /// <param name="forward"><c>true</c> for Tab, <c>false</c> for Shift+Tab.</param>
        /// <returns><c>true</c> when focus moved inside the stack; <c>false</c> when it should leave.</returns>
        public bool MoveFocus(bool forward)
        {
            if (_Overlay != null)
                return _Overlay is IFocusContainer container && container.MoveFocus(forward);

            return _Scope.MoveFocus(forward);
        }

        /// <summary>
        /// Focuses the first or last focusable child (see <see cref="IFocusContainer.FocusEdge"/>).
        /// </summary>
        /// <param name="first"><c>true</c> for the first child, <c>false</c> for the last.</param>
        public void FocusEdge(bool first)
        {
            if (_Overlay != null)
            {
                if (_Overlay is IFocusContainer container)
                    container.FocusEdge(first);
                return;
            }

            _Scope.FocusEdge(first);
        }

        /// <summary>
        /// Records whether the stack is on the active focus path and tells the focused child (or the
        /// overlay) so only the leaf on the active path shows focus.
        /// </summary>
        /// <param name="focused"><c>true</c> when the stack gained focus.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (_Overlay != null)
            {
                if (_Overlay is IFocusAware aware)
                    aware.OnFocusChanged(focused);
                return;
            }

            _Scope.OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme: frame styles from <see cref="Theme.Border"/>, <see cref="FocusFrame.FocusedStyle"/>,
        /// and <see cref="Theme.FocusTitleRole"/>, ASCII frames from <see cref="Theme.UseAsciiBorders"/>, the
        /// background from <see cref="Theme.Text"/>, and the theme to every child and the overlay.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            FrameStyle = theme.Border;
            FocusedFrameStyle = FocusFrame.FocusedStyle(theme);
            FocusedTitleStyle = theme.Resolve(Theme.FocusTitleRole, FocusedFrameStyle);
            AsciiFrames = theme.UseAsciiBorders;
            BackgroundStyle = theme.Text;
            for (int i = 0; i < _Slots.Count; i++)
                ThemeApplier.Apply(_Slots[i].Widget, theme);
            ThemeApplier.Apply(_Overlay, theme);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <summary>
        /// Lays out and draws the children, their frames (unfocused first, the focused one last and whole),
        /// and the overlay.
        /// </summary>
        /// <param name="surface">The surface. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Rect full = new Rect(0, 0, surface.Size.Width, surface.Size.Height);
            surface.Fill(full, Cell.Blank(BackgroundStyle));
            if (full.IsEmpty)
                return;

            List<FramedStackFrame> frames = new List<FramedStackFrame>();
            List<FramedStackSlot> leaves = new List<FramedStackSlot>();
            List<FramedStack> overlays = new List<FramedStack>();
            LayoutInto(full, _Focused, frames, leaves, overlays);

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].Focused == (pass == 1))
                        DrawFrame(surface, frames[i]);
                }
            }

            for (int i = 0; i < leaves.Count; i++)
            {
                Rect content = leaves[i].Content;
                if (content.Width > 0 && content.Height > 0)
                    leaves[i].Widget.Render(new SurfaceView(surface, content));
            }

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].Focused)
                    FocusFrame.ApplyNarrowFocus(surface, frames[i].Outer, true, frames[i].Owner.FrameOptions);
            }

            for (int i = 0; i < overlays.Count; i++)
                overlays[i].DrawOverlay(surface);
        }

        /// <summary>
        /// Routes the mouse: while an overlay is shown, events inside it go to it and a press outside
        /// closes it; otherwise a left press focuses the child under the pointer, and events inside a
        /// child's content area reach the child (when it is <see cref="IMouseAware"/>) in the child's own
        /// coordinates. A press on a line shared by two children belongs to the child before the line.
        /// Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns><c>true</c> when the event was consumed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            Point point = new Point(mouse.X + _LastOuter.X, mouse.Y + _LastOuter.Y);
            bool press = mouse.Kind == MouseEventKind.Press;
            if (mouse.Kind == MouseEventKind.Leave)
            {
                LeaveHovered(mouse);
                LeaveOverlay(mouse);
                return false;
            }

            if (_Overlay != null)
            {
                if (_OverlayOuter.Contains(point))
                {
                    LeaveHovered(mouse);
                    _OverlayHovered = true;
                    if (_OverlayContent.Contains(point) && _Overlay is IMouseAware overlayMouse)
                        overlayMouse.HandleMouse(Translate(mouse, point, _OverlayContent));
                    return true;
                }

                LeaveOverlay(mouse);
                if (press)
                    Overlay = null;
            }

            FramedStackSlot? slot = SlotAt(point);
            if (!ReferenceEquals(slot, _Hovered))
                LeaveHovered(mouse);
            _Hovered = slot;
            if (slot == null)
                return false;

            bool handled = false;
            if (press && mouse.Button == MouseButton.Left && slot.Widget is IFocusable && FocusScope.IsFocusable(slot))
            {
                _Scope.SetFocus(slot);
                handled = true;
            }

            if (slot.Content.Contains(point) && slot.Widget is IMouseAware child)
                handled |= child.HandleMouse(Translate(mouse, point, slot.Content));

            return handled;
        }

        internal void LayoutInto(Rect outer, bool pathFocused, List<FramedStackFrame> frames, List<FramedStackSlot> leaves, List<FramedStack> overlays)
        {
            _LastOuter = outer;
            bool vertical = Orientation == SplitOrientation.Vertical;
            int axis = vertical ? outer.Height : outer.Width;

            List<FramedStackSlot> candidates = new List<FramedStackSlot>();
            for (int i = 0; i < _Slots.Count; i++)
            {
                FramedStackSlot slot = _Slots[i];
                bool hidden = slot.Widget is IHideable hideable && !hideable.IsVisible;
                slot.LaidOut = false;
                slot.Outer = default;
                slot.Content = default;
                if (!hidden)
                    candidates.Add(slot);
            }

            int[] lengths = Distribute(candidates, axis, ShowFrames);
            bool showFocus = pathFocused && _Overlay == null;
            int position = vertical ? outer.Y : outer.X;
            for (int i = 0; i < lengths.Length; i++)
            {
                FramedStackSlot slot = candidates[i];
                int span = ShowFrames ? lengths[i] + 2 : lengths[i];
                Rect slotOuter = vertical
                    ? new Rect(outer.X, position, outer.Width, span)
                    : new Rect(position, outer.Y, span, outer.Height);
                position += ShowFrames ? lengths[i] + 1 : lengths[i];

                slot.LaidOut = true;
                slot.Outer = slotOuter;
                bool childFocused = showFocus && ReferenceEquals(_Scope.FocusedChild, slot);
                if (ShowFrames && slot.Widget is FramedStack nested && nested.ShowFrames)
                {
                    // A nested stack draws into this child's whole area, so its lines land on ours and join.
                    slot.Content = slotOuter;
                    nested.LayoutInto(slotOuter, childFocused, frames, leaves, overlays);
                    continue;
                }

                if (ShowFrames)
                {
                    frames.Add(new FramedStackFrame(this, slotOuter, slot.Title, childFocused));
                    slot.Content = FocusFrame.ContentRect(slotOuter, _FrameOptions);
                }
                else
                {
                    slot.Content = slotOuter;
                }

                leaves.Add(slot);
            }

            if (_Overlay != null)
            {
                Func<Rect, Rect> place = OverlayRect ?? DefaultOverlayRect;
                _OverlayOuter = place(outer).Intersect(outer);
                _OverlayContent = ShowFrames ? FocusFrame.ContentRect(_OverlayOuter, _FrameOptions) : _OverlayOuter;
                _PathFocusedForOverlay = pathFocused;
                overlays.Add(this);
            }
        }

        private void DrawOverlay(ISurface surface)
        {
            IWidget? overlay = _Overlay;
            if (overlay == null || _OverlayOuter.IsEmpty)
                return;

            surface.Fill(_OverlayOuter, Cell.Blank(BackgroundStyle));
            if (ShowFrames)
                DrawFrameCore(surface, _OverlayOuter, OverlayTitle, _PathFocusedForOverlay, false);
            if (_OverlayContent.Width > 0 && _OverlayContent.Height > 0)
                overlay.Render(new SurfaceView(surface, _OverlayContent));
            if (ShowFrames)
                FocusFrame.ApplyNarrowFocus(surface, _OverlayOuter, _PathFocusedForOverlay, _FrameOptions);
        }

        private static void DrawFrame(ISurface surface, FramedStackFrame frame)
        {
            frame.Owner.DrawFrameCore(surface, frame.Outer, frame.Title, frame.Focused, frame.Owner.FrameOptions.JoinBorders);
        }

        private void DrawFrameCore(ISurface surface, Rect outer, string? title, bool focused, bool join)
        {
            FocusFrame.DrawCore(
                surface,
                outer,
                focused,
                _FrameOptions.FocusedBorder,
                _FrameOptions.UnfocusedBorder,
                FocusedFrameStyle,
                FrameStyle,
                FocusedTitleStyle ?? FocusedFrameStyle,
                AsciiFrames,
                _FrameOptions,
                title,
                join);
        }

        private static Rect DefaultOverlayRect(Rect area)
        {
            int width = area.Width - (area.Width / 2);
            return new Rect(area.X + (area.Width / 2), area.Y, width, area.Height);
        }

        // Fixed children first; the rest is shared by weight, with minimums enforced; leftovers from
        // rounding go to the last weighted child. Children are dropped from the end until the fixed
        // lengths and minimums fit. Returns one length per laid-out child, in order.
        private static int[] Distribute(List<FramedStackSlot> candidates, int axis, bool frames)
        {
            int count = candidates.Count;
            int available = 0;
            while (count > 0)
            {
                available = axis - (frames ? count + 1 : 0);
                int required = 0;
                for (int i = 0; i < count; i++)
                    required += candidates[i].Size.Minimum;
                if (required <= available)
                    break;
                count--;
            }

            int[] lengths = new int[count];
            if (count == 0)
                return lengths;

            int rest = available;
            List<int> pool = new List<int>();
            for (int i = 0; i < count; i++)
            {
                StackSize size = candidates[i].Size;
                if (size.Kind == StackSizeKind.Fixed)
                {
                    lengths[i] = size.Length;
                    rest -= size.Length;
                }
                else
                {
                    pool.Add(i);
                }
            }

            if (pool.Count == 0)
                return lengths;

            int lastWeighted = pool[pool.Count - 1];
            while (pool.Count > 0)
            {
                long totalWeight = 0;
                for (int p = 0; p < pool.Count; p++)
                    totalWeight += candidates[pool[p]].Size.Weight;

                List<int> below = new List<int>();
                for (int p = 0; p < pool.Count; p++)
                {
                    StackSize size = candidates[pool[p]].Size;
                    if (rest * (long)size.Weight / totalWeight < size.Minimum)
                        below.Add(pool[p]);
                }

                if (below.Count == 0)
                    break;

                for (int b = 0; b < below.Count; b++)
                {
                    lengths[below[b]] = candidates[below[b]].Size.Minimum;
                    rest -= candidates[below[b]].Size.Minimum;
                    pool.Remove(below[b]);
                }
            }

            if (pool.Count > 0)
            {
                long totalWeight = 0;
                for (int p = 0; p < pool.Count; p++)
                    totalWeight += candidates[pool[p]].Size.Weight;

                int used = 0;
                for (int p = 0; p < pool.Count; p++)
                {
                    int share = (int)(rest * (long)candidates[pool[p]].Size.Weight / totalWeight);
                    lengths[pool[p]] = share;
                    used += share;
                }

                rest -= used;
            }

            if (rest > 0)
                lengths[lastWeighted] += rest;

            return lengths;
        }

        private FramedStackSlot? Find(IWidget child)
        {
            for (int i = 0; i < _Slots.Count; i++)
            {
                if (ReferenceEquals(_Slots[i].Widget, child))
                    return _Slots[i];
            }

            return null;
        }

        private FramedStackSlot? SlotAt(Point point)
        {
            FramedStackSlot? edge = null;
            for (int i = 0; i < _Slots.Count; i++)
            {
                FramedStackSlot slot = _Slots[i];
                if (!slot.LaidOut)
                    continue;
                if (slot.Content.Contains(point))
                    return slot;
                if (edge == null && slot.Outer.Contains(point))
                    edge = slot;
            }

            return edge;
        }

        private void LeaveHovered(MouseEvent mouse)
        {
            FramedStackSlot? hovered = _Hovered;
            _Hovered = null;
            if (hovered != null && hovered.Widget is IMouseAware child)
                child.HandleMouse(new MouseEvent(MouseEventKind.Leave, MouseButton.None, mouse.X, mouse.Y, mouse.Modifiers, 0));
        }

        private void LeaveOverlay(MouseEvent mouse)
        {
            if (!_OverlayHovered)
                return;

            _OverlayHovered = false;
            if (_Overlay is IMouseAware overlay)
                overlay.HandleMouse(new MouseEvent(MouseEventKind.Leave, MouseButton.None, mouse.X, mouse.Y, mouse.Modifiers, 0));
        }

        private static MouseEvent Translate(MouseEvent mouse, Point point, Rect content)
        {
            return new MouseEvent(mouse.Kind, mouse.Button, point.X - content.X, point.Y - content.Y, mouse.Modifiers, mouse.ClickCount);
        }
    }
}
