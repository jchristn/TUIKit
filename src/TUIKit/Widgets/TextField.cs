namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A single-line text input widget with a caret, suitable for modal forms. Editing and caret movement
    /// step over whole grapheme clusters, the caret is placed by terminal column (so CJK and emoji text
    /// line up), and a value wider than the field scrolls horizontally to keep the caret visible.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class TextField : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable
    {
        private string _Value = string.Empty;
        private int _Caret;
        private char _MaskChar;
        private int _ScrollColumn;
        private bool _Enabled = true;

        /// <summary>
        /// Raised after <see cref="Value"/> changes, whether from typing, a paste, or a programmatic set.
        /// Not raised when a set leaves the value unchanged.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<string>>? ValueChanged;

        /// <summary>
        /// Raised after any value change; the untyped companion of <see cref="ValueChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

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
        /// Gets or sets the style composed over <see cref="NormalStyle"/> while the field is disabled or
        /// read-only. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style of <see cref="Placeholder"/> text. Defaults to dim text.
        /// </summary>
        public CellStyle PlaceholderStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets hint text shown while the value is empty. Null or empty shows nothing. Defaults to null.
        /// </summary>
        public string? Placeholder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the field accepts input. A disabled field renders with
        /// <see cref="DisabledStyle"/>, shows no caret, and ignores keys, pastes, and the mouse. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the value is read-only. A read-only field still takes
        /// focus and moves its caret (so the value can be scrolled and inspected) but refuses edits and
        /// pastes. Programmatic sets of <see cref="Value"/> still apply. Defaults to false.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the character used to obscure the value when rendering, for secret input such
        /// as passwords, API keys, or bearer tokens. When <c>'\0'</c> (the default) the value renders
        /// as typed. When set to a visible character (for example <c>'*'</c>) every value character is
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
        /// Gets the caret position as a UTF-16 index into <see cref="Value"/>.
        /// </summary>
        public int CaretIndex
        {
            get { return _Caret; }
        }

        /// <summary>
        /// Gets the first visible column of the value, which moves when the value is wider than the field.
        /// </summary>
        public int ScrollColumn
        {
            get { return _ScrollColumn; }
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
        /// Gets or sets the field value. Setting places the caret at the end and raises
        /// <see cref="ValueChanged"/> when the value differs. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Value
        {
            get { return _Value; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                string before = _Value;
                _Value = value;
                _Caret = _Value.Length;
                RaiseIfChanged(before);
            }
        }

        /// <summary>
        /// Applies a theme: <see cref="NormalStyle"/> from <see cref="Theme.Text"/>,
        /// <see cref="DisabledStyle"/> from <see cref="Theme.Disabled"/>, and
        /// <see cref="PlaceholderStyle"/> from <see cref="Theme.Muted"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            DisabledStyle = theme.Disabled;
            PlaceholderStyle = theme.Muted;
        }

        /// <summary>
        /// Inserts literal text at the caret, as produced by a bracketed paste. Control characters are
        /// dropped so a multi-line or newline-terminated clipboard payload (common when copying an access
        /// key, secret, or token) collapses into the single line this field holds; the caret advances past
        /// the inserted run. Ignored while the field is disabled or read-only.
        /// </summary>
        /// <param name="text">The text to insert. Null is treated as empty.</param>
        public void Insert(string? text)
        {
            if (string.IsNullOrEmpty(text) || !_Enabled || IsReadOnly)
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

            string before = _Value;
            string sanitized = builder.ToString();
            _Value = _Value.Insert(_Caret, sanitized);
            _Caret += sanitized.Length;
            RaiseIfChanged(before);
        }

        /// <summary>
        /// Handles editing and caret keys. Returns <c>false</c> for every key while disabled; while
        /// read-only only caret keys are consumed.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            string before = _Value;
            switch (key.Code)
            {
                case KeyCode.Character:
                    if ((key.Modifiers & KeyModifiers.Ctrl) != 0 || IsReadOnly)
                        return false;
                    string s = char.ConvertFromUtf32(key.Rune);
                    _Value = _Value.Insert(_Caret, s);
                    _Caret += s.Length;
                    RaiseIfChanged(before);
                    return true;
                case KeyCode.Backspace:
                    if (IsReadOnly)
                        return false;
                    if (_Caret > 0)
                    {
                        int start = TextFit.PreviousBoundary(_Value, _Caret);
                        _Value = _Value.Remove(start, _Caret - start);
                        _Caret = start;
                        RaiseIfChanged(before);
                    }

                    return true;
                case KeyCode.Delete:
                    if (IsReadOnly)
                        return false;
                    if (_Caret < _Value.Length)
                    {
                        int end = TextFit.NextBoundary(_Value, _Caret);
                        _Value = _Value.Remove(_Caret, end - _Caret);
                        RaiseIfChanged(before);
                    }

                    return true;
                case KeyCode.Left:
                    if (_Caret > 0)
                        _Caret = TextFit.PreviousBoundary(_Value, _Caret);
                    return true;
                case KeyCode.Right:
                    if (_Caret < _Value.Length)
                        _Caret = TextFit.NextBoundary(_Value, _Caret);
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

        /// <summary>
        /// Positions the caret at the clicked column on a left press, accounting for horizontal scroll and
        /// wide glyphs (clamped to the value length). Other mouse events are not consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a left press positioned the caret; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!_Enabled)
                return false;

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                _Caret = TextFit.IndexAtColumn(DisplayText(), Math.Max(0, mouse.X) + _ScrollColumn);
                if (_Caret > _Value.Length)
                    _Caret = _Value.Length;
                return true;
            }

            return false;
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

            int width = surface.Size.Width;
            if (width <= 0 || surface.Size.Height <= 0)
                return;

            bool dimmed = !_Enabled || IsReadOnly;
            CellStyle style = dimmed ? DisabledStyle.Over(NormalStyle) : NormalStyle;
            surface.Fill(new Rect(0, 0, width, 1), Cell.Blank(style));

            if (_Value.Length == 0 && !string.IsNullOrEmpty(Placeholder))
            {
                surface.DrawText(0, 0, TextFit.Truncate(Placeholder, width), PlaceholderStyle.Over(NormalStyle));
                _ScrollColumn = 0;
            }

            string display = DisplayText();
            int caretColumn = TextFit.ColumnOf(display, _Caret);
            bool showCaret = IsFocused && _Enabled;

            // Keep the caret inside the field: one spare column on the right so it can sit after the text.
            int usable = Math.Max(1, width - (showCaret ? 1 : 0));
            if (caretColumn < _ScrollColumn)
                _ScrollColumn = caretColumn;
            else if (caretColumn > _ScrollColumn + usable)
                _ScrollColumn = caretColumn - usable;

            int displayWidth = TextFit.Width(display);
            if (_ScrollColumn > 0 && displayWidth - _ScrollColumn < usable)
                _ScrollColumn = Math.Max(0, Math.Min(caretColumn, displayWidth - usable));

            if (display.Length > 0)
                surface.DrawText(0, 0, TextFit.Slice(display, _ScrollColumn, width), style);

            if (showCaret)
            {
                int x = caretColumn - _ScrollColumn;
                if (x >= 0 && x < width)
                {
                    string underGlyph = " ";
                    int glyphWidth = 1;
                    if (_Caret < display.Length)
                    {
                        int end = TextFit.NextBoundary(display, _Caret);
                        underGlyph = display.Substring(_Caret, end - _Caret);
                        glyphWidth = Math.Max(1, TextFit.Width(underGlyph));
                        if (x + glyphWidth > width)
                        {
                            underGlyph = " ";
                            glyphWidth = 1;
                        }
                    }

                    CellStyle caretStyle = NormalStyle.WithAttribute(CellAttributes.Reverse, true);
                    surface.Set(x, 0, Cell.Glyph(underGlyph, caretStyle, glyphWidth));
                    if (glyphWidth == 2)
                        surface.Set(x + 1, 0, Cell.Continuation(caretStyle));
                }
            }
        }

        private string DisplayText()
        {
            return _MaskChar == '\0' ? _Value : new string(_MaskChar, _Value.Length);
        }

        private void RaiseIfChanged(string before)
        {
            if (string.Equals(before, _Value, StringComparison.Ordinal))
                return;

            ValueChanged?.Invoke(this, new ValueChangedEventArgs<string>(before, _Value));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
