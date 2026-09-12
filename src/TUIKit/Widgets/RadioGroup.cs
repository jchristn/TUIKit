namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A vertical group of mutually exclusive options. Up/Down change the selection and Home/End jump
    /// to the first and last option.
    /// </summary>
    public sealed class RadioGroup : IWidget, IFocusable, IMouseAware
    {
        private readonly string[] _Options;
        private int _Selected;

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
        /// Gets the zero-based index of the selected option.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Selected; }
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
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                case KeyCode.Down:
                    _Selected = Math.Min(_Options.Length - 1, _Selected + 1);
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    return true;
                case KeyCode.End:
                    _Selected = _Options.Length - 1;
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

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && mouse.Y >= 0 && mouse.Y < _Options.Length)
            {
                _Selected = mouse.Y;
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel && mouse.Button == MouseButton.WheelUp)
            {
                _Selected = Math.Max(0, _Selected - 1);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel && mouse.Button == MouseButton.WheelDown)
            {
                _Selected = Math.Min(_Options.Length - 1, _Selected + 1);
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
                surface.DrawText(0, i, mark + _Options[i], style);
            }
        }
    }
}
