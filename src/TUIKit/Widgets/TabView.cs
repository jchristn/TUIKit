namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A tabbed container that shows one of several child widgets with a tab strip along the top.
    /// Tab or Right activates the next tab; Left activates the previous. Clicking a tab header
    /// activates it, and the header under the pointer renders with <see cref="HoverStyle"/> while
    /// hover tracking is on. Mouse events below the strip are forwarded to the active tab's content
    /// (see <see cref="ForwardMouse"/>).
    /// </summary>
    /// <remarks>
    /// With <see cref="ForwardKeys"/> set the tab view becomes a hierarchical focus scope
    /// (<see cref="IFocusContainer"/>): keys go to the active tab's content first, Ctrl+PageDown and
    /// Ctrl+PageUp switch tabs, Left and Right switch tabs only when the content does not consume them,
    /// and an unconsumed Tab bubbles out to the parent focus ring instead of switching tabs. Not
    /// thread-safe: use it from the UI loop.
    /// </remarks>
    public sealed class TabView : IWidget, IFocusable, IMouseAware, IFocusContainer, IFocusAware, IThemeable
    {
        private readonly List<string> _Names = new List<string>();
        private readonly List<IWidget> _Widgets = new List<IWidget>();
        private int _Active;
        private int _HoverTab = -1;
        private bool _Focused;
        private bool _ChildHovered;

        /// <summary>
        /// Raised after the active tab changes, from a key, a click, or <see cref="Activate"/>. The
        /// arguments carry the old and new <see cref="ActiveIndex"/>.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? ActiveTabChanged;

        /// <summary>
        /// Gets or sets the style of the active tab. Defaults to reversed cyan.
        /// </summary>
        public CellStyle ActiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of inactive tabs. Defaults to muted.
        /// </summary>
        public CellStyle InactiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of an inactive tab header under the pointer. The active tab keeps
        /// <see cref="ActiveStyle"/> while hovered. Defaults to underlined default text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Underline);

        /// <summary>
        /// Gets or sets a value indicating whether keys are forwarded to the active tab's content first,
        /// making the tab view a hierarchical focus scope. Defaults to false, which keeps the original
        /// behavior (Tab, Left, and Right always switch tabs and the content never sees keys).
        /// </summary>
        public bool ForwardKeys { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether mouse events below the tab strip are forwarded to the
        /// active tab's content (in content-local coordinates) when it implements <see cref="IMouseAware"/>.
        /// Defaults to true.
        /// </summary>
        public bool ForwardMouse { get; set; } = true;

        /// <summary>
        /// Gets the zero-based index of the active tab, or -1 when there are no tabs.
        /// </summary>
        public int ActiveIndex
        {
            get { return _Widgets.Count == 0 ? -1 : _Active; }
        }

        /// <summary>
        /// Gets the number of tabs.
        /// </summary>
        public int Count
        {
            get { return _Widgets.Count; }
        }

        /// <summary>
        /// Gets the tab labels in order. Never null.
        /// </summary>
        public IReadOnlyList<string> TabNames
        {
            get { return _Names; }
        }

        /// <summary>
        /// Gets the active tab's content, or null when there are no tabs.
        /// </summary>
        public IWidget? ActiveContent
        {
            get { return _Widgets.Count == 0 ? null : _Widgets[_Active]; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                IWidget? content = ActiveContent;
                if (content is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return content as IFocusable ?? this;
            }
        }

        /// <summary>
        /// Adds a tab.
        /// </summary>
        /// <param name="name">The tab label. Must not be null.</param>
        /// <param name="widget">The tab content. Must not be null.</param>
        /// <returns>This tab view, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public TabView Add(string name, IWidget widget)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            _Names.Add(name);
            _Widgets.Add(widget);
            return this;
        }

        /// <summary>
        /// Renames a tab.
        /// </summary>
        /// <param name="index">The zero-based tab index.</param>
        /// <param name="name">The new label. Must not be null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public void SetTabName(int index, string name)
        {
            if (index < 0 || index >= _Names.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _Names[index] = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Activates a tab by index.
        /// </summary>
        /// <param name="index">The zero-based tab index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public void Activate(int index)
        {
            if (index < 0 || index >= _Widgets.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            SetActive(index);
        }

        /// <summary>
        /// Switches tabs with Tab/Right (next) and Left (previous). With <see cref="ForwardKeys"/> set, the
        /// active content gets the key first (see the class remarks).
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (_Widgets.Count == 0)
                return false;

            if (ForwardKeys)
                return HandleForwardedKey(key);

            if (key.Code == KeyCode.Tab || key.Code == KeyCode.Right)
            {
                SetActive((_Active + 1) % _Widgets.Count);
                return true;
            }

            if (key.Code == KeyCode.Left)
            {
                SetActive((_Active - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (ForwardKeys && ActiveContent is IFocusContainer container)
                return container.MoveFocus(forward);

            return false;
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            if (ForwardKeys && ActiveContent is IFocusContainer container)
                container.FocusEdge(first);
        }

        /// <summary>
        /// Tracks focus and, with <see cref="ForwardKeys"/> set, forwards it to the active content so its
        /// caret or highlight follows. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the tab view gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (ForwardKeys && ActiveContent is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme to the tab strip (<see cref="ActiveStyle"/> from <see cref="Theme.Selection"/>,
        /// <see cref="InactiveStyle"/> from <see cref="Theme.Muted"/>, <see cref="HoverStyle"/> from
        /// <see cref="Theme.Text"/> underlined) and forwards it to every tab's content.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            ActiveStyle = theme.Selection;
            InactiveStyle = theme.Muted;
            HoverStyle = theme.Text.WithAttribute(CellAttributes.Underline, true);
            for (int i = 0; i < _Widgets.Count; i++)
                ThemeApplier.Apply(_Widgets[i], theme);
        }

        /// <summary>
        /// Activates the tab header under a left press and tracks the hovered header for
        /// <see cref="HoverStyle"/> rendering. Enter/Move/Leave events over the strip are observed but
        /// never consumed. Events below the strip go to the active content when <see cref="ForwardMouse"/>
        /// is set.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event was consumed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (ForwardMouse && mouse.Y >= 1 && mouse.Kind != MouseEventKind.Leave)
            {
                _HoverTab = -1;
                _ChildHovered = true;
                if (ActiveContent is IMouseAware child)
                    return child.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount));

                return false;
            }

            if (_ChildHovered && (mouse.Kind == MouseEventKind.Leave || mouse.Y < 1))
            {
                _ChildHovered = false;
                if (ActiveContent is IMouseAware child)
                    child.HandleMouse(new MouseEvent(MouseEventKind.Leave, MouseButton.None, mouse.X, mouse.Y - 1, mouse.Modifiers, 0));
            }

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button == MouseButton.Left)
                    {
                        int pressed = TabIndexAt(mouse.X, mouse.Y);
                        if (pressed >= 0)
                        {
                            SetActive(pressed);
                            return true;
                        }
                    }

                    return false;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _HoverTab = TabIndexAt(mouse.X, mouse.Y);
                    return false;
                case MouseEventKind.Leave:
                    _HoverTab = -1;
                    return false;
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

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0 || _Widgets.Count == 0)
                return;

            int cursor = 0;
            for (int i = 0; i < _Names.Count && cursor < width; i++)
            {
                string label = " " + _Names[i] + " ";
                CellStyle style = i == _Active ? ActiveStyle : (i == _HoverTab ? HoverStyle : InactiveStyle);
                cursor += surface.DrawText(cursor, 0, label, style);
                cursor += surface.DrawText(cursor, 0, " ", InactiveStyle);
            }

            if (height > 1)
                _Widgets[_Active].Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }

        private bool HandleForwardedKey(KeyEvent key)
        {
            bool ctrlOnly = key.Modifiers == KeyModifiers.Ctrl;
            if (ctrlOnly && key.Code == KeyCode.PageDown)
            {
                SetActive((_Active + 1) % _Widgets.Count);
                return true;
            }

            if (ctrlOnly && key.Code == KeyCode.PageUp)
            {
                SetActive((_Active - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            if (ActiveContent is IFocusable focusable && FocusScope.IsFocusable(focusable) && focusable.HandleKey(key))
                return true;

            if (key.Modifiers == KeyModifiers.None && key.Code == KeyCode.Right)
            {
                SetActive((_Active + 1) % _Widgets.Count);
                return true;
            }

            if (key.Modifiers == KeyModifiers.None && key.Code == KeyCode.Left)
            {
                SetActive((_Active - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            return false;
        }

        private void SetActive(int index)
        {
            if (index == _Active)
                return;

            int before = _Active;
            IWidget previous = _Widgets[before];
            _Active = index;

            if (ForwardKeys)
            {
                if (previous is IFocusAware previousAware)
                    previousAware.OnFocusChanged(false);
                if (_Widgets[index] is IFocusAware nextAware)
                    nextAware.OnFocusChanged(_Focused);
            }

            ActiveTabChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, index));
        }

        private int TabIndexAt(int x, int y)
        {
            if (y != 0 || x < 0)
                return -1;

            // Mirrors the render pass: each header occupies " name " followed by a one-cell gap.
            int cursor = 0;
            for (int i = 0; i < _Names.Count; i++)
            {
                int headerWidth = TextFit.Width(_Names[i]) + 2;
                if (x >= cursor && x < cursor + headerWidth)
                    return i;

                cursor += headerWidth + 1;
            }

            return -1;
        }
    }
}
