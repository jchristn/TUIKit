namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A push button drawn as <c>[ Label ]</c>. Enter, Space, or a left click raises <see cref="Clicked"/>.
    /// The focused button renders with <see cref="FocusedStyle"/>; a disabled button renders with
    /// <see cref="DisabledStyle"/> and ignores input. Use <see cref="ButtonRow"/> to lay out several.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class Button : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IThemeable, ITooltipProvider
    {
        private string _Label;
        private bool _Enabled = true;
        private bool _Hovered;

        /// <summary>
        /// Raised when the button is activated by Enter, Space, a click, or <see cref="Click"/>.
        /// </summary>
        public event EventHandler? Clicked;

        /// <summary>
        /// Gets or sets the label. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Label
        {
            get { return _Label; }
            set { _Label = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets an optional tooltip shown when the pointer rests on the button, or null.
        /// </summary>
        public string? TooltipText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the button is focused. Set by the host and focus scopes.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the button accepts input. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets the style of an unfocused button. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of the focused button. Defaults to black on cyan (palette 6).
        /// </summary>
        public CellStyle FocusedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of a hovered, unfocused button. Defaults to bold.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of a disabled button. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Initializes a new instance of the <see cref="Button"/> class.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="onClick">An optional handler attached to <see cref="Clicked"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public Button(string label, Action? onClick = null)
        {
            _Label = label ?? throw new ArgumentNullException(nameof(label));
            if (onClick != null)
                Clicked += (sender, e) => onClick();
        }

        /// <summary>
        /// Gets the rendered width in columns: the label plus four (brackets and padding).
        /// </summary>
        public int Width
        {
            get { return TextFit.Width(_Label) + 4; }
        }

        /// <summary>
        /// Activates the button programmatically. A no-op while disabled.
        /// </summary>
        /// <returns><c>true</c> when <see cref="Clicked"/> was raised.</returns>
        public bool Click()
        {
            if (!_Enabled)
                return false;

            Clicked?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        /// <summary>
        /// Applies a theme: <see cref="NormalStyle"/> from <see cref="Theme.Text"/>,
        /// <see cref="FocusedStyle"/> from the <see cref="Theme.ButtonFocusedRole"/> style or
        /// <see cref="Theme.Selection"/>, <see cref="HoverStyle"/> from <see cref="Theme.Accent"/>, and
        /// <see cref="DisabledStyle"/> from <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            FocusedStyle = theme.Resolve(Theme.ButtonFocusedRole, theme.Selection);
            HoverStyle = theme.Accent;
            DisabledStyle = theme.Disabled;
        }

        /// <inheritdoc/>
        public string? GetTooltip(int x, int y)
        {
            return TooltipText;
        }

        /// <summary>
        /// Activates on Enter or Space. Returns <c>false</c> while disabled.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key activated the button.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled || key.Modifiers != KeyModifiers.None)
                return false;

            if (key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && key.Rune == ' '))
                return Click();

            return false;
        }

        /// <summary>
        /// Activates on a left press and tracks hover.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press activated the button.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    return mouse.Button == MouseButton.Left && Click();
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _Hovered = true;
                    return false;
                case MouseEventKind.Leave:
                    _Hovered = false;
                    return false;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, Width), available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            if (surface.Size.Width <= 0 || surface.Size.Height <= 0)
                return;

            CellStyle style;
            if (!_Enabled)
                style = DisabledStyle.Over(NormalStyle);
            else if (IsFocused)
                style = FocusedStyle;
            else if (_Hovered)
                style = HoverStyle.Over(NormalStyle);
            else
                style = NormalStyle;

            string text = "[ " + _Label + " ]";
            surface.DrawText(0, 0, TextFit.Ellipsize(text, surface.Size.Width), style);
        }
    }
}
