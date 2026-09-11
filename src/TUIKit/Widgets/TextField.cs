namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A single-line text input widget with a caret, suitable for modal forms.
    /// </summary>
    public sealed class TextField : IWidget, IFocusable, IFocusAware
    {
        private string _Value = string.Empty;
        private int _Caret;
        private char _MaskChar;

        /// <summary>
        /// Gets or sets a value indicating whether the field is focused and renders a caret.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets the base style used to paint the field: the surface fill and the value text
        /// share this style, and the caret is drawn as this style with reverse video so it inverts
        /// against whatever foreground and background are set here. Defaults to
        /// <see cref="CellStyle.Default"/> (the terminal's default colors); assign a style with a
        /// background to give the field a solid background.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the character used to obscure the value when rendering, for secret input such
        /// as passwords, API keys, or bearer tokens. When <c>'\0'</c> (the default) the value renders
        /// as typed. When set to a visible character (for example <c>'•'</c>) every value character is
        /// drawn as that mask character, including the glyph shown under the caret, while the underlying
        /// <see cref="Value"/> and all editing and caret behavior remain unchanged.
        /// </summary>
        public char MaskChar
        {
            get { return _MaskChar; }
            set { _MaskChar = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the field masks its rendered value. Returns <c>true</c> when
        /// <see cref="MaskChar"/> is set to a non-null character.
        /// </summary>
        public bool IsMasked
        {
            get { return _MaskChar != '\0'; }
        }

        /// <summary>
        /// Updates the focused state so the caret shows or hides on the next frame. Part of
        /// <see cref="IFocusAware"/>; called by the host and <see cref="FocusManager"/> on focus changes.
        /// </summary>
        /// <param name="focused"><c>true</c> when the field has gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

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
        /// Inserts literal text at the caret, as produced by a bracketed paste. Control characters are
        /// dropped so a multi-line or newline-terminated clipboard payload (common when copying an access
        /// key, secret, or token) collapses into the single line this field holds; the caret advances past
        /// the inserted run.
        /// </summary>
        /// <param name="text">The text to insert. Null is treated as empty.</param>
        public void Insert(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(text!.Length);
            foreach (char c in text!)
            {
                // Keep printable characters (including any Unicode above the C0/C1 control ranges); drop
                // CR, LF, Tab, and other controls that a single-line field cannot represent.
                if (c >= ' ' && c != '\x7F' && !(c >= '\x80' && c <= '\x9F'))
                    builder.Append(c);
            }

            if (builder.Length == 0)
                return;

            string sanitized = builder.ToString();
            _Value = _Value.Insert(_Caret, sanitized);
            _Caret += sanitized.Length;
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

            surface.Fill(new Rect(0, 0, surface.Size.Width, 1), Cell.Blank(NormalStyle));
            string display = _MaskChar == '\0' ? _Value : new string(_MaskChar, _Value.Length);
            surface.DrawText(0, 0, display, NormalStyle);

            if (IsFocused && _Caret <= surface.Size.Width)
            {
                string underGlyph;
                if (_Caret < _Value.Length)
                    underGlyph = _MaskChar == '\0' ? _Value[_Caret].ToString() : _MaskChar.ToString();
                else
                    underGlyph = " ";
                surface.Set(_Caret, 0, Cell.Glyph(underGlyph, NormalStyle.WithAttribute(CellAttributes.Reverse, true), 1));
            }
        }
    }
}
