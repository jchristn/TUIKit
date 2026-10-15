namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A single-line text input widget with a caret, suitable for modal forms. Editing and caret movement
    /// step over whole grapheme clusters, the caret is placed by terminal column (so CJK and emoji text
    /// line up), and a value wider than the field scrolls horizontally to keep the caret visible.
    /// Opt-in features cover login and settings screens: shell line-editing keys
    /// (<see cref="UseLineEditingKeys"/>), a prefilled value the first keystroke replaces
    /// (<see cref="SelectAll"/>), a reveal key for masked values (<see cref="RevealKey"/>), Enter to submit
    /// (<see cref="Submitted"/>), a length limit (<see cref="MaxLength"/>), and validation
    /// (<see cref="Validator"/>).
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class TextField : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable, ITextEntry, ITextEntryKeys, IKeyHintSource, IPasteTarget
    {
        #region Private-Members

        private string _Value = string.Empty;
        private int _Caret;
        private char _MaskChar;
        private int _ScrollColumn;
        private bool _Enabled = true;
        private bool _AllSelected;
        private CellStyle? _SelectionStyle;
        private int _MaxLength;
        private string? _Error;

        #endregion

        #region Public-Members

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
        /// Raised when Enter (with no modifiers) is pressed. Enter is consumed only while a handler is
        /// attached, so a field without one leaves Enter to its container, as in 1.7. The field does not
        /// validate on submit; call <see cref="Validate"/> from the handler when wanted.
        /// </summary>
        public event EventHandler? Submitted;

        /// <summary>
        /// Raised when <see cref="Error"/> changes, from <see cref="Validate"/> or
        /// <see cref="ValidateOnChange"/>. Not raised when the error stays the same.
        /// </summary>
        public event EventHandler? ErrorChanged;

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
        /// Gets or sets the style the value is drawn in while it is selected (<see cref="IsAllSelected"/>).
        /// Defaults to <see cref="NormalStyle"/> with reverse video, following later changes to
        /// <see cref="NormalStyle"/> until assigned.
        /// </summary>
        public CellStyle SelectionStyle
        {
            get { return _SelectionStyle ?? NormalStyle.WithAttribute(CellAttributes.Reverse, true); }
            set { _SelectionStyle = value; }
        }

        /// <summary>
        /// Gets or sets the style composed over the value text while <see cref="Error"/> is set, or null
        /// to draw the value unchanged (the default). The field never draws the error message itself; a
        /// <see cref="Form"/> or the application does.
        /// </summary>
        public CellStyle? ErrorStyle { get; set; }

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
        /// Gets a value indicating whether printable keys are inserted as text: true while enabled and not
        /// read-only. Part of <see cref="ITextEntry"/>.
        /// </summary>
        public bool AcceptsText
        {
            get { return _Enabled && !IsReadOnly; }
        }

        /// <summary>
        /// Gets or sets the character used to obscure the value when rendering, for secret input such
        /// as passwords, API keys, or bearer tokens. When <c>'\0'</c> (the default) the value renders
        /// as typed. When set to a visible character (for example <c>'*'</c>) each grapheme of the value
        /// is drawn as one mask character, one column wide, including the glyph shown under the caret,
        /// while the underlying <see cref="Value"/> and all editing and caret behavior remain unchanged.
        /// <see cref="IsRevealed"/> shows the value as typed without clearing the mask.
        /// </summary>
        public char MaskChar
        {
            get { return _MaskChar; }
            set { _MaskChar = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the field has a mask configured. Returns <c>true</c> when
        /// <see cref="MaskChar"/> is set to a non-null character, whether or not <see cref="IsRevealed"/> is on.
        /// </summary>
        public bool IsMasked
        {
            get { return _MaskChar != '\0'; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a masked value is drawn as typed. While true,
        /// <see cref="MaskChar"/> is ignored when drawing; <see cref="IsMasked"/> still reports the mask.
        /// Has no visible effect on an unmasked field. Defaults to false.
        /// </summary>
        public bool IsRevealed { get; set; }

        /// <summary>
        /// Gets or sets the chord that toggles <see cref="IsRevealed"/> on a masked field, or null for no key
        /// (the default). Ctrl+R is the common choice. On an unmasked field the chord is not consumed.
        /// </summary>
        public KeyChord? RevealKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the shell line-editing keys are on. Defaults to false,
        /// where the field behaves exactly as in 1.7 (Ctrl+Left and Ctrl+Right move one grapheme). When
        /// true: Ctrl+A and Ctrl+E move to the start and end; Ctrl+U deletes from the start to the caret
        /// and Ctrl+K from the caret to the end; Ctrl+W deletes the word before the caret (trailing spaces
        /// first, then non-space graphemes); Ctrl+Left and Ctrl+Right move to the previous and next word
        /// start. Words are whitespace-separated runs of graphemes. Deleting chords are refused while
        /// <see cref="IsReadOnly"/>; moving chords still apply.
        /// </summary>
        public bool UseLineEditingKeys { get; set; }

        /// <summary>
        /// Gets or sets the maximum value length in graphemes, or 0 for no limit (the default). Typing at
        /// the limit is refused and <see cref="Insert"/> keeps only the leading graphemes that fit. Setting
        /// <see cref="Value"/> programmatically is never truncated, so loading a stored value never loses data.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0.</exception>
        public int MaxLength
        {
            get { return _MaxLength; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "MaxLength must be zero or greater.");
                _MaxLength = value;
            }
        }

        /// <summary>
        /// Gets or sets the validator: it receives the value and returns an error message, or null when the
        /// value is valid. Null (the default) means every value is valid.
        /// </summary>
        public Func<string, string?>? Validator { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether every value change re-runs <see cref="Validate"/>.
        /// Defaults to false: errors are computed only when <see cref="Validate"/> is called.
        /// </summary>
        public bool ValidateOnChange { get; set; }

        /// <summary>
        /// Gets the error stored by the last <see cref="Validate"/>, or null when the value was valid or has
        /// not been validated.
        /// </summary>
        public string? Error
        {
            get { return _Error; }
        }

        /// <summary>
        /// Gets a value indicating whether the whole value is selected, so the next printable key or paste
        /// replaces it. Set by <see cref="SelectAll"/>; cleared by caret movement, edits, and setting
        /// <see cref="Value"/>.
        /// </summary>
        public bool IsAllSelected
        {
            get { return _AllSelected; }
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
        /// Gets or sets the field value. Setting places the caret at the end, clears the selection, and
        /// raises <see cref="ValueChanged"/> when the value differs. Never truncated by
        /// <see cref="MaxLength"/>. Must not be null.
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
                _AllSelected = false;
                RaiseIfChanged(before);
            }
        }

        /// <summary>
        /// Gets or sets the hint shown in the status bar for leaving this field while typing (for example
        /// <c>Esc</c> Back), or null for <see cref="KeyHintResolver.LeaveTextHint"/>. Part of
        /// <see cref="ITextEntryKeys"/>. Defaults to null.
        /// </summary>
        public KeyHint? LeaveHint { get; set; }

        #endregion

        #region Public-Methods

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
        /// Selects the whole value, so the next printable key or paste replaces it and Backspace or Delete
        /// clears it. Typical use after loading a remembered value: <c>field.Value = saved; field.SelectAll();</c>.
        /// Has no effect on an empty value.
        /// </summary>
        public void SelectAll()
        {
            _AllSelected = _Value.Length > 0;
            if (_AllSelected)
                _Caret = _Value.Length;
        }

        /// <summary>
        /// Clears the selection without changing the value.
        /// </summary>
        public void ClearSelection()
        {
            _AllSelected = false;
        }

        /// <summary>
        /// Runs <see cref="Validator"/> on the current value, stores the result in <see cref="Error"/>, and
        /// raises <see cref="ErrorChanged"/> when it changed. A null validator clears the error.
        /// </summary>
        /// <returns><c>true</c> when the value is valid; otherwise <c>false</c>.</returns>
        public bool Validate()
        {
            Func<string, string?>? validator = Validator;
            string? error = validator == null ? null : validator(_Value);
            if (string.IsNullOrEmpty(error))
                error = null;
            SetError(error);
            return error == null;
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
        /// the inserted run. While the value is selected, the text replaces it. With
        /// <see cref="MaxLength"/> set, only the leading graphemes that fit are kept. Ignored while the field
        /// is disabled or read-only.
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

            InsertRun(builder.ToString());
        }

        /// <summary>
        /// Inserts pasted text through <see cref="Insert"/>. Part of <see cref="IPasteTarget"/>.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null.</param>
        /// <returns><c>true</c> when the field accepts text (enabled and not read-only); otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public bool HandlePaste(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (!AcceptsText)
                return false;

            Insert(text);
            return true;
        }

        /// <summary>
        /// Returns whether the field consumes a chord while it accepts text: Backspace, Delete, Left,
        /// Right, Home, and End; the line-editing chords while <see cref="UseLineEditingKeys"/> is on;
        /// <see cref="RevealKey"/> on a masked field; and Enter (no modifiers) only while
        /// <see cref="Submitted"/> has a handler. Without a handler Enter, Tab, Esc, and Up and Down reach
        /// the application. Part of <see cref="ITextEntryKeys"/>.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns><c>true</c> when the field consumes it.</returns>
        public bool ConsumesChord(KeyChord chord)
        {
            if (!_Enabled)
                return false;

            if (RevealKey.HasValue && IsMasked && chord.Equals(RevealKey.Value))
                return true;

            if (UseLineEditingKeys && TryLineEditChord(chord.Code, chord.Rune, chord.Modifiers, out bool edits))
                return !edits || !IsReadOnly;

            switch (chord.Code)
            {
                case KeyCode.Backspace:
                case KeyCode.Delete:
                    return !IsReadOnly;
                case KeyCode.Left:
                case KeyCode.Right:
                case KeyCode.Home:
                case KeyCode.End:
                    return true;
                case KeyCode.Enter:
                    return chord.Modifiers == KeyModifiers.None && Submitted != null;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the field's own hints: Show or Hide for <see cref="RevealKey"/> on a masked field, and Enter
        /// Submit while <see cref="Submitted"/> has a handler. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null when neither applies (the 1.7 status bar is unchanged).</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!_Enabled)
                return null;

            List<KeyHint>? hints = null;
            if (RevealKey.HasValue && IsMasked)
            {
                hints = new List<KeyHint>();
                hints.Add(new KeyHint(RevealKey.Value, IsRevealed ? "Hide" : "Show").WhileTyping());
            }

            if (Submitted != null)
            {
                hints = hints ?? new List<KeyHint>();
                hints.Add(new KeyHint(new KeyChord(KeyCode.Enter, 0, KeyModifiers.None), "Submit", 10).WhileTyping());
            }

            return hints;
        }

        /// <summary>
        /// Handles editing and caret keys. Returns <c>false</c> for every key while disabled; while
        /// read-only only caret keys are consumed. See <see cref="UseLineEditingKeys"/>,
        /// <see cref="RevealKey"/>, and <see cref="Submitted"/> for the opt-in keys.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            if (RevealKey.HasValue && IsMasked && KeyChord.FromKeyEvent(key).Equals(RevealKey.Value))
            {
                IsRevealed = !IsRevealed;
                return true;
            }

            if (UseLineEditingKeys && TryLineEditChord(key.Code, key.Rune, key.Modifiers, out bool edits))
                return HandleLineEdit(key, edits);

            string before = _Value;
            switch (key.Code)
            {
                case KeyCode.Character:
                    if ((key.Modifiers & KeyModifiers.Ctrl) != 0 || IsReadOnly)
                        return false;
                    string s = char.ConvertFromUtf32(key.Rune);
                    if (_AllSelected)
                    {
                        _Value = string.Empty;
                        _Caret = 0;
                        _AllSelected = false;
                    }

                    if (_MaxLength > 0 && TextWords.GraphemeCount(_Value) >= _MaxLength)
                    {
                        RaiseIfChanged(before);
                        return true;
                    }

                    _Value = _Value.Insert(_Caret, s);
                    _Caret += s.Length;
                    RaiseIfChanged(before);
                    return true;
                case KeyCode.Backspace:
                    if (IsReadOnly)
                        return false;
                    if (ClearSelectedValue(before))
                        return true;
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
                    if (ClearSelectedValue(before))
                        return true;
                    if (_Caret < _Value.Length)
                    {
                        int end = TextFit.NextBoundary(_Value, _Caret);
                        _Value = _Value.Remove(_Caret, end - _Caret);
                        RaiseIfChanged(before);
                    }

                    return true;
                case KeyCode.Left:
                    _AllSelected = false;
                    if (_Caret > 0)
                        _Caret = TextFit.PreviousBoundary(_Value, _Caret);
                    return true;
                case KeyCode.Right:
                    _AllSelected = false;
                    if (_Caret < _Value.Length)
                        _Caret = TextFit.NextBoundary(_Value, _Caret);
                    return true;
                case KeyCode.Home:
                    _AllSelected = false;
                    _Caret = 0;
                    return true;
                case KeyCode.End:
                    _AllSelected = false;
                    _Caret = _Value.Length;
                    return true;
                case KeyCode.Enter:
                    EventHandler? submitted = Submitted;
                    if (key.Modifiers != KeyModifiers.None || submitted == null)
                        return false;
                    submitted(this, EventArgs.Empty);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Positions the caret at the clicked column on a left press, accounting for horizontal scroll and
        /// wide glyphs (clamped to the value length), and clears the selection. Other mouse events are not consumed.
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
                int displayIndex = TextFit.IndexAtColumn(DisplayText(), Math.Max(0, mouse.X) + _ScrollColumn);
                _Caret = ToValueIndex(displayIndex);
                if (_Caret > _Value.Length)
                    _Caret = _Value.Length;
                _AllSelected = false;
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
            int caretIndex = ToDisplayIndex(_Caret);
            int caretColumn = TextFit.ColumnOf(display, caretIndex);
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

            CellStyle textStyle = style;
            if (_AllSelected)
                textStyle = SelectionStyle;
            else if (_Error != null && ErrorStyle.HasValue)
                textStyle = ErrorStyle.Value.Over(style);

            if (display.Length > 0)
                surface.DrawText(0, 0, TextFit.Slice(display, _ScrollColumn, width), textStyle);

            if (showCaret)
            {
                int x = caretColumn - _ScrollColumn;
                if (x >= 0 && x < width)
                {
                    string underGlyph = " ";
                    int glyphWidth = 1;
                    if (display.Length == 0 && !string.IsNullOrEmpty(Placeholder))
                    {
                        // An empty field shows its placeholder; keep its first letter visible under the caret.
                        string placeholder = Placeholder!;
                        string first = placeholder.Substring(0, TextFit.NextBoundary(placeholder, 0));
                        if (TextFit.Width(first) == 1)
                            underGlyph = first;
                    }
                    else if (caretIndex < display.Length)
                    {
                        int end = TextFit.NextBoundary(display, caretIndex);
                        underGlyph = display.Substring(caretIndex, end - caretIndex);
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

        #endregion

        #region Private-Methods

        private static bool TryLineEditChord(KeyCode code, int rune, KeyModifiers modifiers, out bool edits)
        {
            edits = false;
            if (modifiers != KeyModifiers.Ctrl)
                return false;

            if (code == KeyCode.Left || code == KeyCode.Right)
                return true;

            if (code != KeyCode.Character)
                return false;

            switch (char.ToLowerInvariant((char)rune))
            {
                case 'a':
                case 'e':
                    return true;
                case 'u':
                case 'k':
                case 'w':
                    edits = true;
                    return true;
                default:
                    return false;
            }
        }

        private bool HandleLineEdit(KeyEvent key, bool edits)
        {
            if (edits && IsReadOnly)
                return false;

            string before = _Value;
            if (edits && ClearSelectedValue(before))
                return true;

            _AllSelected = false;
            if (key.Code == KeyCode.Left)
            {
                _Caret = TextWords.PreviousWordStart(_Value, _Caret);
                return true;
            }

            if (key.Code == KeyCode.Right)
            {
                _Caret = TextWords.NextWordStart(_Value, _Caret);
                return true;
            }

            switch (char.ToLowerInvariant((char)key.Rune))
            {
                case 'a':
                    _Caret = 0;
                    break;
                case 'e':
                    _Caret = _Value.Length;
                    break;
                case 'u':
                    _Value = _Value.Substring(_Caret);
                    _Caret = 0;
                    break;
                case 'k':
                    _Value = _Value.Substring(0, _Caret);
                    break;
                case 'w':
                    int start = TextWords.PreviousWordStart(_Value, _Caret);
                    _Value = _Value.Remove(start, _Caret - start);
                    _Caret = start;
                    break;
            }

            RaiseIfChanged(before);
            return true;
        }

        private bool ClearSelectedValue(string before)
        {
            if (!_AllSelected)
                return false;

            _AllSelected = false;
            _Value = string.Empty;
            _Caret = 0;
            RaiseIfChanged(before);
            return true;
        }

        private void InsertRun(string run)
        {
            string before = _Value;
            if (_AllSelected)
            {
                _Value = string.Empty;
                _Caret = 0;
                _AllSelected = false;
            }

            if (_MaxLength > 0)
            {
                int room = _MaxLength - TextWords.GraphemeCount(_Value);
                if (room <= 0)
                {
                    RaiseIfChanged(before);
                    return;
                }

                run = run.Substring(0, TextWords.IndexOfGrapheme(run, room));
            }

            _Value = _Value.Insert(_Caret, run);
            _Caret += run.Length;
            RaiseIfChanged(before);
        }

        private bool ShowsMask()
        {
            return _MaskChar != '\0' && !IsRevealed;
        }

        private string DisplayText()
        {
            return ShowsMask() ? new string(_MaskChar, TextWords.GraphemeCount(_Value)) : _Value;
        }

        private int ToDisplayIndex(int valueIndex)
        {
            if (!ShowsMask())
                return valueIndex;

            int i = 0;
            int count = 0;
            while (i < valueIndex && i < _Value.Length)
            {
                i = TextFit.NextBoundary(_Value, i);
                count++;
            }

            return count;
        }

        private int ToValueIndex(int displayIndex)
        {
            return ShowsMask() ? TextWords.IndexOfGrapheme(_Value, displayIndex) : displayIndex;
        }

        private void SetError(string? error)
        {
            if (string.Equals(_Error, error, StringComparison.Ordinal))
                return;

            _Error = error;
            ErrorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseIfChanged(string before)
        {
            if (string.Equals(before, _Value, StringComparison.Ordinal))
                return;

            ValueChanged?.Invoke(this, new ValueChangedEventArgs<string>(before, _Value));
            Changed?.Invoke(this, EventArgs.Empty);
            if (ValidateOnChange)
                Validate();
        }

        #endregion
    }
}
