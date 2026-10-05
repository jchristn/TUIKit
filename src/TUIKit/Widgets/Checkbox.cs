namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A labeled checkbox. Space or Enter toggles it, as does a left click. While hover tracking is
    /// on, the checkbox renders with <see cref="HoverStyle"/> when the pointer is over it.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class Checkbox : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable
    {
        private string _Label;
        private bool _Hovered;
        private bool _Checked;
        private bool _Enabled = true;

        /// <summary>
        /// Raised after <see cref="Checked"/> changes, whether from a key, a click, or a programmatic set.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<bool>>? CheckedChanged;

        /// <summary>
        /// Raised after any change; the untyped companion of <see cref="CheckedChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is checked. Setting a different value
        /// raises <see cref="CheckedChanged"/>.
        /// </summary>
        public bool Checked
        {
            get { return _Checked; }
            set
            {
                if (_Checked == value)
                    return;

                _Checked = value;
                CheckedChanged?.Invoke(this, new ValueChangedEventArgs<bool>(!value, value));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

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
        /// Gets or sets a value indicating whether the checkbox accepts input. A disabled checkbox renders
        /// with <see cref="DisabledStyle"/> and ignores keys and the mouse. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is focused. Set by the host and
        /// <see cref="FocusManager"/>; when true the box renders with <see cref="FocusedStyle"/>.
        /// Defaults to false.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets the base style. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style composed over <see cref="NormalStyle"/> while focused. Defaults to
        /// <see cref="CellStyle.Default"/> (no visible change), preserving the original look.
        /// </summary>
        public CellStyle FocusedStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style composed over <see cref="NormalStyle"/> while disabled. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style used while the pointer is over the checkbox. Defaults to bold
        /// default text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Bold);

        /// <summary>
        /// Initializes a new instance of the <see cref="Checkbox"/> class.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="isChecked">The initial checked state. Defaults to false.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public Checkbox(string label, bool isChecked = false)
        {
            _Label = label ?? throw new ArgumentNullException(nameof(label));
            _Checked = isChecked;
        }

        /// <summary>
        /// Updates the focused state. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when focus was gained; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        /// <summary>
        /// Applies a theme: <see cref="NormalStyle"/> from <see cref="Theme.Text"/>,
        /// <see cref="FocusedStyle"/> from <see cref="Theme.Accent"/>, and <see cref="DisabledStyle"/> from
        /// <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            FocusedStyle = theme.Accent;
            DisabledStyle = theme.Disabled;
        }

        /// <summary>
        /// Toggles the checkbox on Space or Enter. Returns <c>false</c> while disabled.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key toggled the checkbox; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            if (key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && key.Rune == ' '))
            {
                Checked = !Checked;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Toggles the checkbox on a left press and tracks hover for <see cref="HoverStyle"/>
        /// rendering. Enter/Move/Leave events are observed but never consumed. Ignored while disabled.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press toggled the checkbox; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button == MouseButton.Left && _Enabled)
                    {
                        Checked = !Checked;
                        return true;
                    }

                    return false;
                case MouseEventKind.Enter:
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
            return new Size(Math.Min(available.Width, TextFit.Width(_Label) + 4), available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            string mark = Checked ? "[x] " : "[ ] ";
            CellStyle style;
            if (!_Enabled)
                style = DisabledStyle.Over(NormalStyle);
            else if (_Hovered)
                style = HoverStyle.Over(NormalStyle);
            else if (IsFocused)
                style = FocusedStyle.Over(NormalStyle);
            else
                style = NormalStyle;

            surface.DrawText(0, 0, mark + _Label, style);
        }
    }
}
