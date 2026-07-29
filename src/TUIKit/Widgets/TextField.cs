namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A single-line text input widget with a caret, suitable for modal forms.
    /// </summary>
    public sealed class TextField : IWidget, IFocusable
    {
        private string _Value = string.Empty;
        private int _Caret;

        /// <summary>
        /// Gets or sets a value indicating whether the field is focused and renders a caret.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets the field value. Setting places the caret at the end. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Value
        {
            get { return _Value; }
            set
            {
                _Value = value ?? throw new ArgumentNullException(nameof(value));
                _Caret = _Value.Length;
            }
        }

        /// <summary>
        /// Handles editing and caret keys.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Character:
                    if ((key.Modifiers & KeyModifiers.Ctrl) != 0)
                        return false;
                    string s = char.ConvertFromUtf32(key.Rune);
                    _Value = _Value.Insert(_Caret, s);
                    _Caret += s.Length;
                    return true;
                case KeyCode.Backspace:
                    if (_Caret > 0)
                    {
                        _Value = _Value.Remove(_Caret - 1, 1);
                        _Caret--;
                    }

                    return true;
                case KeyCode.Delete:
                    if (_Caret < _Value.Length)
                        _Value = _Value.Remove(_Caret, 1);
                    return true;
                case KeyCode.Left:
                    if (_Caret > 0)
                        _Caret--;
                    return true;
                case KeyCode.Right:
                    if (_Caret < _Value.Length)
                        _Caret++;
                    return true;
                case KeyCode.Home:
                    _Caret = 0;
                    return true;
                case KeyCode.End:
                    _Caret = _Value.Length;
                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            surface.Fill(new Rect(0, 0, surface.Size.Width, 1), Cell.Blank(CellStyle.Default));
            surface.DrawText(0, 0, _Value, CellStyle.Default);

            if (IsFocused && _Caret <= surface.Size.Width)
            {
                string underGlyph = _Caret < _Value.Length ? _Value[_Caret].ToString() : " ";
                surface.Set(_Caret, 0, Cell.Glyph(underGlyph, CellStyle.Default.WithAttribute(CellAttributes.Reverse, true), 1));
            }
        }
    }
}
