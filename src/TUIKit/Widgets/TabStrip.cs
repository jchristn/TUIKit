namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A one-row strip of tab headers without content, for applications that host the tab content
    /// themselves (a router, or screens built lazily). It has the strip behavior of <see cref="TabView"/>:
    /// Left and Right (and Ctrl+PgUp and Ctrl+PgDn) switch tabs, a click activates a header, the header
    /// under the pointer uses <see cref="HoverStyle"/>, and while the strip has focus the active tab is
    /// drawn with <see cref="FocusedTabStyle"/> and wrapped in <see cref="TabFocusPrefix"/> and
    /// <see cref="TabFocusSuffix"/>. Keys it does not use, such as Down and Tab, are not consumed, so the
    /// container or host moves focus on. <see cref="TabView"/> draws its own strip with this widget.
    /// </summary>
    /// <remarks>
    /// Every tab is drawn as prefix, name, suffix, followed by a one-cell gap. Use a focus prefix and
    /// suffix the same width as <see cref="TabPrefix"/> and <see cref="TabSuffix"/> (for example
    /// <c>"&gt;"</c>/<c>"&lt;"</c> with <c>" "</c>/<c>" "</c>, or <c>"["</c>/<c>"]"</c>) so moving focus
    /// never shifts tab positions. Not thread-safe: use it on the UI thread.
    /// </remarks>
    public sealed class TabStrip : IWidget, IFocusable, IFocusAware, IMouseAware, IThemeable, IKeyHintSource
    {
        private readonly List<string> _Names = new List<string>();
        private int _Active;
        private int _HoverTab = -1;
        private bool _Focused;
        private string _TabPrefix = " ";
        private string _TabSuffix = " ";
        private string _TabFocusPrefix = ">";
        private string _TabFocusSuffix = " ";

        /// <summary>
        /// Raised after the active tab changes, with the previous and new index.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? ActiveTabChanged;

        /// <summary>
        /// Gets or sets the style of the active tab. Defaults to reversed cyan.
        /// </summary>
        public CellStyle ActiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of inactive tabs and the gaps between tabs. Defaults to muted.
        /// </summary>
        public CellStyle InactiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of an inactive tab under the pointer. Defaults to underlined text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Underline);

        /// <summary>
        /// Gets or sets the style of the active tab while the strip has focus, or null to add bold and
        /// underline to <see cref="ActiveStyle"/>, so the cue never depends on color alone. Defaults to null.
        /// </summary>
        public CellStyle? FocusedTabStyle { get; set; }

        /// <summary>
        /// Gets or sets the text drawn before every tab's name, except the active tab while the strip has
        /// focus. Defaults to a space. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabPrefix
        {
            get { return _TabPrefix; }
            set { _TabPrefix = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the text drawn after every tab's name, except the active tab while the strip has
        /// focus. Defaults to a space. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabSuffix
        {
            get { return _TabSuffix; }
            set { _TabSuffix = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the text drawn before the active tab's name while the strip has focus. Defaults to
        /// <c>"&gt;"</c>. Empty falls back to <see cref="TabPrefix"/>, so only the style marks focus. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabFocusPrefix
        {
            get { return _TabFocusPrefix; }
            set { _TabFocusPrefix = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the text drawn after the active tab's name while the strip has focus. Defaults to a
        /// space. Empty falls back to <see cref="TabSuffix"/>. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TabFocusSuffix
        {
            get { return _TabFocusSuffix; }
            set { _TabFocusSuffix = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets the tab names in order. Never null.
        /// </summary>
        public IReadOnlyList<string> Tabs
        {
            get { return _Names; }
        }

        /// <summary>
        /// Gets the number of tabs.
        /// </summary>
        public int Count
        {
            get { return _Names.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the strip has focus.
        /// </summary>
        public bool IsFocused
        {
            get { return _Focused; }
        }

        /// <summary>
        /// Gets or sets the index of the active tab; -1 while there are no tabs. Setting it raises
        /// <see cref="ActiveTabChanged"/> when it changes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to <see cref="Count"/> - 1.</exception>
        public int ActiveIndex
        {
            get { return _Names.Count == 0 ? -1 : _Active; }
            set
            {
                if (value < 0 || value >= _Names.Count)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Active index must be within [0, Count).");

                SetActive(value, true);
            }
        }

        /// <summary>
        /// Adds a tab at the end.
        /// </summary>
        /// <param name="name">The tab name. Must not be null.</param>
        /// <returns>This strip, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public TabStrip Add(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            _Names.Add(name);
            return this;
        }

        /// <summary>
        /// Renames a tab.
        /// </summary>
        /// <param name="index">The tab index.</param>
        /// <param name="name">The new name. Must not be null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public void SetTabName(int index, string name)
        {
            if (index < 0 || index >= _Names.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _Names[index] = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the strip's own key hints: Left/Right to switch tabs while focused with more than one tab.
        /// </summary>
        /// <returns>The hints. Never null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            if (_Focused && _Names.Count > 1)
                hints.Add(new KeyHint("Left/Right", "Switch tab", 5));
            return hints;
        }

        /// <summary>
        /// Switches tabs with Left and Right (wrapping) and Ctrl+PgUp and Ctrl+PgDn. Other keys, including
        /// Down and Tab, are not consumed.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key switched tabs; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (_Names.Count == 0)
                return false;

            bool plain = key.Modifiers == KeyModifiers.None;
            bool ctrlOnly = key.Modifiers == KeyModifiers.Ctrl;
            if ((plain && key.Code == KeyCode.Right) || (ctrlOnly && key.Code == KeyCode.PageDown))
            {
                SetActive((_Active + 1) % _Names.Count, true);
                return true;
            }

            if ((plain && key.Code == KeyCode.Left) || (ctrlOnly && key.Code == KeyCode.PageUp))
            {
                SetActive((_Active - 1 + _Names.Count) % _Names.Count, true);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Records whether the strip has focus.
        /// </summary>
        /// <param name="focused"><c>true</c> when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
        }

        /// <summary>
        /// Applies a theme: the active tab from <see cref="Theme.Selection"/>, the focused tab from
        /// <see cref="Theme.TabFocusedRole"/> when registered, inactive tabs from <see cref="Theme.Muted"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            ActiveStyle = theme.Selection;
            FocusedTabStyle = theme.HasStyle(Theme.TabFocusedRole) ? theme.GetStyle(Theme.TabFocusedRole) : (CellStyle?)null;
            InactiveStyle = theme.Muted;
            HoverStyle = theme.Text.WithAttribute(CellAttributes.Underline, true);
        }

        /// <summary>
        /// Activates the tab under a left press and tracks the hovered header. Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns><c>true</c> when a press activated a tab; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button != MouseButton.Left)
                        return false;
                    int pressed = TabIndexAt(mouse.X, mouse.Y, _Focused);
                    if (pressed < 0)
                        return false;
                    SetActive(pressed, true);
                    return true;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _HoverTab = TabIndexAt(mouse.X, mouse.Y, _Focused);
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
            return new Size(available.Width, available.Height > 0 ? 1 : 0);
        }

        /// <summary>
        /// Draws the headers on the first row.
        /// </summary>
        /// <param name="surface">The surface. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            RenderStrip(surface, _Focused);
        }

        internal int HoverTab
        {
            get { return _HoverTab; }
            set { _HoverTab = value; }
        }

        internal void RenderStrip(ISurface surface, bool showFocus)
        {
            int width = surface.Size.Width;
            if (width <= 0 || surface.Size.Height <= 0 || _Names.Count == 0)
                return;

            int cursor = 0;
            for (int i = 0; i < _Names.Count && cursor < width; i++)
            {
                bool focusedTab = i == _Active && showFocus;
                CellStyle style = i == _Active ? ActiveStyle : (i == _HoverTab ? HoverStyle : InactiveStyle);
                if (focusedTab)
                    style = FocusedTabStyle ?? ActiveStyle.WithAttribute(CellAttributes.Bold, true).WithAttribute(CellAttributes.Underline, true);

                cursor += surface.DrawText(cursor, 0, Label(i, focusedTab), style);
                cursor += surface.DrawText(cursor, 0, " ", InactiveStyle);
            }
        }

        internal int TabIndexAt(int x, int y, bool showFocus)
        {
            if (y != 0 || x < 0)
                return -1;

            int cursor = 0;
            for (int i = 0; i < _Names.Count; i++)
            {
                int headerWidth = TextFit.Width(Label(i, i == _Active && showFocus));
                if (x >= cursor && x < cursor + headerWidth)
                    return i;

                cursor += headerWidth + 1;
            }

            return -1;
        }

        internal bool SetActive(int index, bool raise)
        {
            if (index == _Active)
                return false;

            int before = _Active;
            _Active = index;
            if (raise)
                ActiveTabChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, index));
            return true;
        }

        private string Label(int index, bool focused)
        {
            if (!focused)
                return _TabPrefix + _Names[index] + _TabSuffix;

            string prefix = _TabFocusPrefix.Length > 0 ? _TabFocusPrefix : _TabPrefix;
            string suffix = _TabFocusSuffix.Length > 0 ? _TabFocusSuffix : _TabSuffix;
            return prefix + _Names[index] + suffix;
        }
    }
}
