namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A group of mutually exclusive options. Vertical groups use Up/Down; horizontal groups use
    /// Left/Right. Home/End jump to the first and last option. Use only from the UI loop.
    /// </summary>
    public sealed class RadioGroup : IWidget, IFocusable, IMouseAware, IEnableable, IChangeNotifier, IThemeable, IValueSnapshot
    {
        private readonly string[] _Options;
        private int _Selected;
        private bool _Enabled = true;
        private RadioGroupOrientation _Orientation;
        private string _SelectedMarker = "(o)";
        private string _UnselectedMarker = "( )";
        private string _Separator = " / ";

        /// <summary>Gets or sets the layout axis. Defaults to Vertical.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined orientation.</exception>
        public RadioGroupOrientation Orientation
        {
            get { return _Orientation; }
            set
            {
                if (value != RadioGroupOrientation.Vertical && value != RadioGroupOrientation.Horizontal)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _Orientation = value;
            }
        }

        /// <summary>Gets or sets the selected marker. Defaults to (o). Must not be null.</summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string SelectedMarker
        {
            get { return _SelectedMarker; }
            set { _SelectedMarker = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>Gets or sets the unselected marker. Defaults to ( ). Must not be null.</summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string UnselectedMarker
        {
            get { return _UnselectedMarker; }
            set { _UnselectedMarker = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>Gets or sets the horizontal separator. Defaults to " / ". Must not be null.</summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Separator
        {
            get { return _Separator; }
            set { _Separator = value ?? throw new ArgumentNullException(nameof(value)); }
        }

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
            get { return Array.AsReadOnly(_Options); }
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> is empty or contains null.</exception>
        public RadioGroup(string[] options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.Length == 0)
                throw new ArgumentException("At least one option is required.", nameof(options));

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null)
                    throw new ArgumentException("Options must not contain null.", nameof(options));
            }
            _Options = (string[])options.Clone();
        }

        /// <summary>
        /// Changes the selection with arrows along the layout axis and Home/End (first and last option).
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            switch (key.Code)
            {
                case KeyCode.Left when Orientation == RadioGroupOrientation.Horizontal:
                case KeyCode.Up when Orientation == RadioGroupOrientation.Vertical:
                    SetSelected(Math.Max(0, _Selected - 1));
                    return true;
                case KeyCode.Right when Orientation == RadioGroupOrientation.Horizontal:
                case KeyCode.Down when Orientation == RadioGroupOrientation.Vertical:
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
        /// Selects the clicked option on a left press (rows vertically, cell spans horizontally) and moves the selection
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

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && mouse.X >= 0 && mouse.Y >= 0)
            {
                if (Orientation == RadioGroupOrientation.Vertical && mouse.Y < _Options.Length)
                {
                    SetSelected(mouse.Y);
                    return true;
                }
                if (Orientation == RadioGroupOrientation.Horizontal && mouse.Y == 0)
                {
                    int x = 0;
                    for (int i = 0; i < _Options.Length; i++)
                    {
                        int width = TextFit.Width(OptionText(i));
                        if (mouse.X >= x && mouse.X < x + width)
                        {
                            SetSelected(i);
                            return true;
                        }
                        x += width + TextFit.Width(Separator);
                    }
                }
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
            return new Size(available.Width, Math.Min(available.Height, Orientation == RadioGroupOrientation.Horizontal ? 1 : _Options.Length));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            if (NormalStyle.Background.Kind != ColorKind.Default)
                surface.Fill(new Rect(0, 0, surface.Size.Width, surface.Size.Height), Cell.Blank(NormalStyle));

            int x = 0;
            for (int i = 0; i < _Options.Length; i++)
            {
                int y = Orientation == RadioGroupOrientation.Horizontal ? 0 : i;
                if (y >= surface.Size.Height || x >= surface.Size.Width)
                    break;
                CellStyle style = i == _Selected ? SelectedStyle.Over(NormalStyle) : NormalStyle;
                if (!_Enabled)
                    style = DisabledStyle.Over(style);
                string text = OptionText(i);
                surface.DrawText(x, y, text, style);
                if (Orientation == RadioGroupOrientation.Horizontal)
                {
                    x += TextFit.Width(text);
                    if (i + 1 < _Options.Length)
                    {
                        surface.DrawText(x, y, Separator, _Enabled ? NormalStyle : DisabledStyle.Over(NormalStyle));
                        x += TextFit.Width(Separator);
                    }
                }
            }
        }

        private string OptionText(int index)
        {
            return (index == _Selected ? SelectedMarker : UnselectedMarker) + " " + _Options[index];
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

        /// <summary>
        /// Gets the selected index as the snapshot used by <see cref="Form"/> snapshot dirty tracking. Part of
        /// <see cref="IValueSnapshot"/>.
        /// </summary>
        /// <returns>The snapshot. Never null.</returns>
        public string? GetSnapshot()
        {
            return SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
