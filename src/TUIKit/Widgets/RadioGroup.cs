namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A vertical group of mutually exclusive options. Up/Down change the selection.
    /// </summary>
    public sealed class RadioGroup : IWidget
    {
        private readonly string[] _Options;
        private int _Selected;

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
        /// Changes the selection with Up/Down.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Up)
            {
                _Selected = Math.Max(0, _Selected - 1);
                return true;
            }

            if (key.Code == KeyCode.Down)
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

            for (int i = 0; i < _Options.Length && i < surface.Size.Height; i++)
            {
                string mark = i == _Selected ? "(o) " : "( ) ";
                CellStyle style = i == _Selected
                    ? CellStyle.Default.WithForeground(Color.FromPalette(6))
                    : CellStyle.Default;
                surface.DrawText(0, i, mark + _Options[i], style);
            }
        }
    }
}
