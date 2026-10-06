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
    /// and an unconsumed Tab bubbles out to the parent focus ring instead of switching tabs.
    /// Add <see cref="StripFocusStop"/> to make the tab strip its own focus stop, so the user can tell
    /// whether arrows will switch tabs or move inside the content: the selected tab shows
    /// <see cref="FocusedTabStyle"/> and <see cref="TabFocusMarker"/> only while the strip holds focus
    /// (<see cref="IsStripFocused"/>). Not thread-safe: use it from the UI loop.
    /// </remarks>
    public sealed class TabView : IWidget, IFocusable, IMouseAware, IFocusContainer, IFocusAware, IThemeable, IFocusPathNode, IKeyHintSource
    {
        private readonly TabStrip _Strip = new TabStrip();
        private readonly List<IWidget> _Widgets = new List<IWidget>();
        private bool _Focused;
        private bool _ChildHovered;
        private bool _StripFocused = true;

        /// <summary>
        /// Raised after the active tab changes, from a key, a click, or <see cref="Activate"/>. The
        /// arguments carry the old and new <see cref="ActiveIndex"/>.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? ActiveTabChanged;

        /// <summary>
        /// Gets or sets the style of the active tab. Defaults to reversed cyan.
        /// </summary>
        public CellStyle ActiveStyle
        {
            get { return _Strip.ActiveStyle; }
            set { _Strip.ActiveStyle = value; }
        }

        /// <summary>
        /// Gets or sets the style of inactive tabs. Defaults to muted.
        /// </summary>
        public CellStyle InactiveStyle
        {
            get { return _Strip.InactiveStyle; }
            set { _Strip.InactiveStyle = value; }
        }

        /// <summary>
        /// Gets or sets the style of an inactive tab header under the pointer. The active tab keeps
        /// <see cref="ActiveStyle"/> while hovered. Defaults to underlined default text.
        /// </summary>
        public CellStyle HoverStyle
        {
            get { return _Strip.HoverStyle; }
            set { _Strip.HoverStyle = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether keys are forwarded to the active tab's content first,
        /// making the tab view a hierarchical focus scope. Defaults to false, which keeps the original
        /// behavior (Tab, Left, and Right always switch tabs and the content never sees keys).
        /// </summary>
        public bool ForwardKeys { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tab strip is a focus stop of its own when
        /// <see cref="ForwardKeys"/> is set. Focus entering the tab view lands on the strip, where Left
        /// and Right switch tabs; Tab, Down, or Enter moves into the active content; Shift+Tab from the
        /// content's first stop returns to the strip. Clicking a tab focuses the strip and clicking the
        /// content focuses it. Defaults to false, which keeps the 1.3 behavior (keys go straight to the
        /// content). Has no effect while <see cref="ForwardKeys"/> is off, where the strip always holds
        /// focus.
        /// </summary>
        public bool StripFocusStop { get; set; }

        /// <summary>
        /// Gets a value indicating whether keys currently go to the tab strip (switching tabs) rather
        /// than to the active content. True while the tab view holds focus and either
        /// <see cref="ForwardKeys"/> is off or <see cref="StripFocusStop"/> is on and the strip is the
        /// current stop.
        /// </summary>
        public bool IsStripFocused
        {
            get { return _Focused && (!ForwardKeys || (StripFocusStop && _StripFocused)); }
        }

        /// <summary>
        /// Gets or sets the style of the selected tab while the tab strip holds focus
        /// (<see cref="IsStripFocused"/>), or null to derive it from <see cref="ActiveStyle"/> by adding
        /// bold and underline, so the cue never depends on color alone. <see cref="ApplyTheme"/> sets it
        /// from <see cref="Theme.TabFocusedRole"/> when the theme registers that role. Defaults to null.
        /// </summary>
        public CellStyle? FocusedTabStyle
        {
            get { return _Strip.FocusedTabStyle; }
            set { _Strip.FocusedTabStyle = value; }
        }

        /// <summary>
        /// Gets or sets the marker drawn in place of the selected tab's left padding while the strip
        /// holds focus. Defaults to <c>"&gt;"</c>. It must be empty (no marker) or exactly one cell wide,
        /// so focus never changes the width of the tab strip.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the value is wider than one cell.</exception>
        public string TabFocusMarker
        {
            get { return _Strip.TabFocusPrefix; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (TextFit.Width(value) > 1)
                    throw new ArgumentException("The tab focus marker must be empty or one cell wide.", nameof(value));

                _Strip.TabFocusPrefix = value;
            }
        }

        /// <summary>
        /// Gets or sets the text drawn before every tab's name, except the selected tab while the strip
        /// holds focus. Defaults to a space, the 1.4.0 padding. Never null. See <see cref="TabStrip.TabPrefix"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabPrefix
        {
            get { return _Strip.TabPrefix; }
            set { _Strip.TabPrefix = value; }
        }

        /// <summary>
        /// Gets or sets the text drawn after every tab's name, except the selected tab while the strip
        /// holds focus. Defaults to a space. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabSuffix
        {
            get { return _Strip.TabSuffix; }
            set { _Strip.TabSuffix = value; }
        }

        /// <summary>
        /// Gets or sets the text drawn before the selected tab's name while the strip holds focus. Defaults
        /// to <c>"&gt;"</c>; <see cref="TabFocusMarker"/> is a shorthand for it. Empty falls back to
        /// <see cref="TabPrefix"/>. Unlike <see cref="TabFocusMarker"/>, any width is allowed; keep it the
        /// width of <see cref="TabPrefix"/> so tabs do not shift. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabFocusPrefix
        {
            get { return _Strip.TabFocusPrefix; }
            set { _Strip.TabFocusPrefix = value; }
        }

        /// <summary>
        /// Gets or sets the text drawn after the selected tab's name while the strip holds focus. Defaults
        /// to a space. Empty falls back to <see cref="TabSuffix"/>. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabFocusSuffix
        {
            get { return _Strip.TabFocusSuffix; }
            set { _Strip.TabFocusSuffix = value; }
        }

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
            get { return _Widgets.Count == 0 ? -1 : _Strip.ActiveIndex; }
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
            get { return _Strip.Tabs; }
        }

        /// <summary>
        /// Gets the active tab's content, or null when there are no tabs.
        /// </summary>
        public IWidget? ActiveContent
        {
            get { return _Widgets.Count == 0 ? null : _Widgets[_Strip.ActiveIndex]; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                if (ContentBypassed)
                    return this;

                IWidget? content = ActiveContent;
                if (content is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return content as IFocusable ?? this;
            }
        }

        /// <summary>
        /// Gets the active content when keys go to it, or null while the tab strip holds focus (see
        /// <see cref="IsStripFocused"/>) or the content is not focusable. Part of
        /// <see cref="IFocusPathNode"/>.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get
            {
                if (!ForwardKeys || ContentBypassed)
                    return null;

                return ActiveContent as IFocusable;
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

            _Strip.Add(name);
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
            if (index < 0 || index >= _Widgets.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _Strip.SetTabName(index, name ?? throw new ArgumentNullException(nameof(name)));
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
        /// Gets the tab view's keys for the status bar: how to switch tabs, and, while the strip holds
        /// focus with <see cref="StripFocusStop"/>, how to enter the content. Empty with fewer than two
        /// tabs and no strip stop. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints. Never null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            if (_Widgets.Count == 0)
                return hints;

            if (IsStripFocused)
            {
                if (_Widgets.Count > 1)
                    hints.Add(new KeyHint("Left/Right", "Switch tab", 5));
                if (ForwardKeys && StripFocusStop && ActiveContent is IFocusable)
                    hints.Add(new KeyHint(new KeyChord(KeyCode.Tab, 0, KeyModifiers.None), "Enter tab", 10));
            }
            else if (ForwardKeys && _Widgets.Count > 1)
            {
                hints.Add(new KeyHint("Ctrl+PgUp/PgDn", "Switch tab"));
            }

            return hints;
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
                SetActive((_Strip.ActiveIndex + 1) % _Widgets.Count);
                return true;
            }

            if (key.Code == KeyCode.Left)
            {
                SetActive((_Strip.ActiveIndex - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (!ForwardKeys)
                return false;

            if (StripFocusStop)
            {
                if (_StripFocused)
                    return forward && EnterContent(true);

                if (ActiveContent is IFocusContainer inner && FocusScope.IsFocusable(inner) && inner.MoveFocus(forward))
                    return true;

                if (!forward)
                {
                    FocusStrip();
                    return true;
                }

                return false;
            }

            if (ActiveContent is IFocusContainer container)
                return container.MoveFocus(forward);

            return false;
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            if (!ForwardKeys)
                return;

            if (StripFocusStop)
            {
                if (first || !EnterContent(false))
                    FocusStrip();
                return;
            }

            if (ActiveContent is IFocusContainer container)
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
            if (ForwardKeys && !ContentBypassed && ActiveContent is IFocusAware aware)
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

            _Strip.ApplyTheme(theme);
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
                _Strip.HoverTab = -1;
                _ChildHovered = true;
                if (mouse.Kind == MouseEventKind.Press && ForwardKeys && StripFocusStop && _StripFocused)
                    EnterContent(true);
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
                            if (ForwardKeys && StripFocusStop)
                                FocusStrip();
                            SetActive(pressed);
                            return true;
                        }
                    }

                    return false;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _Strip.HoverTab = TabIndexAt(mouse.X, mouse.Y);
                    return false;
                case MouseEventKind.Leave:
                    _Strip.HoverTab = -1;
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

            _Strip.RenderStrip(surface, IsStripFocused);

            if (height > 1)
                _Widgets[_Strip.ActiveIndex].Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }

        private bool ContentBypassed
        {
            get { return ForwardKeys && StripFocusStop && _StripFocused; }
        }

        private bool EnterContent(bool first)
        {
            if (!(ActiveContent is IFocusable content) || !FocusScope.IsFocusable(content))
                return false;

            if (_StripFocused)
            {
                _StripFocused = false;
                if (content is IFocusAware aware)
                    aware.OnFocusChanged(_Focused);
            }

            if (content is IFocusContainer container)
                container.FocusEdge(first);

            return true;
        }

        private void FocusStrip()
        {
            if (_StripFocused)
                return;

            _StripFocused = true;
            if (ActiveContent is IFocusAware aware)
                aware.OnFocusChanged(false);
        }

        private bool HandleStripKey(KeyEvent key)
        {
            bool plain = key.Modifiers == KeyModifiers.None;
            bool ctrlOnly = key.Modifiers == KeyModifiers.Ctrl;
            if ((plain && key.Code == KeyCode.Right) || (ctrlOnly && key.Code == KeyCode.PageDown))
            {
                SetActive((_Strip.ActiveIndex + 1) % _Widgets.Count);
                return true;
            }

            if ((plain && key.Code == KeyCode.Left) || (ctrlOnly && key.Code == KeyCode.PageUp))
            {
                SetActive((_Strip.ActiveIndex - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            if (plain && (key.Code == KeyCode.Tab || key.Code == KeyCode.Down || key.Code == KeyCode.Enter))
                return EnterContent(true);

            return false;
        }

        private bool HandleForwardedKey(KeyEvent key)
        {
            if (StripFocusStop && _StripFocused)
                return HandleStripKey(key);

            bool ctrlOnly = key.Modifiers == KeyModifiers.Ctrl;
            if (ctrlOnly && key.Code == KeyCode.PageDown)
            {
                SetActive((_Strip.ActiveIndex + 1) % _Widgets.Count);
                return true;
            }

            if (ctrlOnly && key.Code == KeyCode.PageUp)
            {
                SetActive((_Strip.ActiveIndex - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            if (ActiveContent is IFocusable focusable && FocusScope.IsFocusable(focusable) && focusable.HandleKey(key))
                return true;

            if (StripFocusStop && key.Code == KeyCode.Tab && key.Modifiers == KeyModifiers.Shift)
            {
                FocusStrip();
                return true;
            }

            if (key.Modifiers == KeyModifiers.None && key.Code == KeyCode.Right)
            {
                SetActive((_Strip.ActiveIndex + 1) % _Widgets.Count);
                return true;
            }

            if (key.Modifiers == KeyModifiers.None && key.Code == KeyCode.Left)
            {
                SetActive((_Strip.ActiveIndex - 1 + _Widgets.Count) % _Widgets.Count);
                return true;
            }

            return false;
        }

        private void SetActive(int index)
        {
            int before = _Strip.ActiveIndex;
            if (index == before)
                return;

            IWidget previous = _Widgets[before];
            _Strip.SetActive(index, false);

            if (ForwardKeys)
            {
                if (previous is IFocusAware previousAware)
                    previousAware.OnFocusChanged(false);
                if (_Widgets[index] is IFocusAware nextAware)
                    nextAware.OnFocusChanged(_Focused && !ContentBypassed);
            }

            ActiveTabChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, index));
        }

        private int TabIndexAt(int x, int y)
        {
            return _Strip.TabIndexAt(x, y, IsStripFocused);
        }
    }
}
