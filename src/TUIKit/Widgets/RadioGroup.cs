namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A vertical group of mutually exclusive options. Up/Down change the selection and Home/End jump
    /// to the first and last option.
    /// </summary>
    public sealed class RadioGroup : IWidget, IFocusable, IMouseAware, IEnableable, IChangeNotifier, IThemeable
    {
        private readonly string[] _Options;
        private int _Selected;
        private bool _Enabled = true;

        /// <summary>
        /// Raised after the selected option changes, whether from a key, a click, or a programmatic set of
        /// <see cref="SelectedIndex"/>. The arguments carry the old and new index.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? SelectionChanged;

        /// <summary>
        /// Raised after any selection change; the untyped companion of <see cref="SelectionChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets or sets a value indicating whether the group accepts input. A disabled group renders with
        /// <see cref="DisabledStyle"/> and ignores keys and the mouse. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets the style composed over <see cref="NormalStyle"/> while disabled. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets the options. Never null.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Options
        {
            get { return _Options; }
        }

        /// <summary>
        /// Applies a theme: <see cref="NormalStyle"/> from <see cref="Theme.Text"/>,
        /// <see cref="SelectedStyle"/> from <see cref="Theme.Accent"/>, and <see cref="DisabledStyle"/> from
        /// <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            SelectedStyle = theme.Accent;
            DisabledStyle = theme.Disabled;
        }

        /// <summary>
        /// Gets or sets the base style applied to unselected options and the surface fill. Defaults to
        /// <see cref="CellStyle.Default"/>; assign a style with a background to give the group a solid
        /// background.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style applied to the selected option. It is composed over
        /// <see cref="NormalStyle"/>, so a background set on <see cref="NormalStyle"/> shows through
        /// unless this style sets its own. Defaults to a cyan (palette 6) foreground.
        /// </summary>
        public CellStyle SelectedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the zero-based index of the selected option. Setting clamps to the valid range and
        /// raises <see cref="SelectionChanged"/> when the index changes.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Selected; }
            set { SetSelected(Math.Max(0, Math.Min(_Options.Length - 1, value))); }
        }

        /// <summary>
        /// Gets the selected option text.
        /// </summary>
        public string SelectedOption
        {
            get { return _Options[_Selected]; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RadioGroup"/> class.
        /// </summary>
        /// <param name="options">The options. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> is empty.</exception>
        public RadioGroup(string[] options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.Length == 0)
                throw new ArgumentException("At least one option is required.", nameof(options));

            _Options = options;
        }

        /// <summary>
        /// Changes the selection with Up/Down (one option) and Home/End (first and last option).
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            switch (key.Code)
            {
                case KeyCode.Up:
                    SetSelected(Math.Max(0, _Selected - 1));
                    return true;
                case KeyCode.Down:
                    SetSelected(Math.Min(_Options.Length - 1, _Selected + 1));
                    return true;
                case KeyCode.Home:
                    SetSelected(0);
                    return true;
                case KeyCode.End:
                    SetSelected(_Options.Length - 1);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Selects the clicked option on a left press (each option occupies one row) and moves the selection
        /// one option per wheel notch. Coordinates are widget-local. Other mouse events are not consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event changed the selection; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!_Enabled)
                return false;

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && mouse.Y >= 0 && mouse.Y < _Options.Length)
            {
                SetSelected(mouse.Y);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel && mouse.Button == MouseButton.WheelUp)
            {
                SetSelected(Math.Max(0, _Selected - 1));
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel && mouse.Button == MouseButton.WheelDown)
            {
                SetSelected(Math.Min(_Options.Length - 1, _Selected + 1));
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Options.Length));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            if (NormalStyle.Background.Kind != ColorKind.Default)
                surface.Fill(new Rect(0, 0, surface.Size.Width, surface.Size.Height), Cell.Blank(NormalStyle));

            for (int i = 0; i < _Options.Length && i < surface.Size.Height; i++)
            {
                string mark = i == _Selected ? "(o) " : "( ) ";
                CellStyle style = i == _Selected
                    ? SelectedStyle.Over(NormalStyle)
                    : NormalStyle;
                if (!_Enabled)
                    style = DisabledStyle.Over(style);
                surface.DrawText(0, i, mark + _Options[i], style);
            }
        }

        private void SetSelected(int index)
        {
            if (index == _Selected)
                return;

            int before = _Selected;
            _Selected = index;
            SelectionChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, index));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
