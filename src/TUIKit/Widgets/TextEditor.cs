namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A multi-line text editor widget with caret movement, insertion and deletion, newline handling,
    /// a kill ring, and undo/redo. Drive it by forwarding key events to <see cref="HandleKey"/>. This
    /// is the interactive composer in the example harness. Caret placement, wrapping, and horizontal
    /// scrolling are measured in terminal columns, and caret movement and deletion step over whole
    /// grapheme clusters, so CJK and emoji text edit correctly. Without <see cref="WordWrap"/> long lines
    /// scroll horizontally to keep the caret visible.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class TextEditor : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable, ITextEntry, ITextEntryKeys, IKeyHintSource
    {
        private readonly List<string> _Lines = new List<string> { string.Empty };
        private readonly Stack<EditorSnapshot> _Undo = new Stack<EditorSnapshot>();
        private readonly Stack<EditorSnapshot> _Redo = new Stack<EditorSnapshot>();
        private int _Row;
        private int _Column;
        private string _KillRing = string.Empty;
        private int _MaxUndo = 200;
        private int _ScrollColumn;
        private bool _Enabled = true;

        // The first visible line index and viewport height captured on the last render, so a click can map a
        // screen row back to a text row.
        private int _LastTop;
        private int _LastHeight = 1;

        // The visual (wrapped) rows painted on the last word-wrapped render, parallel arrays of the logical
        // line each visual row belongs to plus its start column and length within that line. Used to map a
        // click back to a logical (row, column) when WordWrap is on.
        private readonly List<int> _VisLogical = new List<int>();
        private readonly List<int> _VisStart = new List<int>();
        private readonly List<int> _VisLen = new List<int>();

        /// <summary>
        /// Gets or sets a value indicating whether the editor is focused and should render a caret.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Raised after the text changes, whether from typing, a paste through <see cref="InsertText"/>,
        /// undo/redo, or a programmatic set of <see cref="Text"/>. Raised once per operation, and not
        /// raised when a set leaves the text unchanged.
        /// </summary>
        public event EventHandler? TextChanged;

        /// <summary>
        /// Raised after any text change; the <see cref="IChangeNotifier"/> companion of <see cref="TextChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets or sets a value indicating whether the editor accepts input. A disabled editor renders with
        /// <see cref="DisabledStyle"/>, shows no caret, and ignores keys and the mouse. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the text is read-only. A read-only editor still moves
        /// its caret and scrolls, but editing keys are not consumed (they fall through to the host).
        /// Programmatic calls (<see cref="Text"/>, <see cref="InsertText"/>) still apply. Defaults to false.
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
        /// Gets or sets the hint shown in the status bar for leaving this field while typing (for example
        /// <c>Esc</c> Back), or null for <see cref="KeyHintResolver.LeaveTextHint"/>. Part of
        /// <see cref="ITextEntryKeys"/>. Defaults to null.
        /// </summary>
        public KeyHint? LeaveHint { get; set; }

        /// <summary>
        /// Returns whether the editor consumes a chord while it accepts text: Enter, Backspace, Delete,
        /// the arrows, Home, End, its Ctrl+Z/Y/K/U editing chords, and any other Ctrl+letter while
        /// <see cref="ConsumeUnboundControlKeys"/> is on. Part of <see cref="ITextEntryKeys"/>.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns><c>true</c> when the editor consumes it.</returns>
        public bool ConsumesChord(KeyChord chord)
        {
            if (!_Enabled)
                return false;

            bool ctrl = (chord.Modifiers & KeyModifiers.Ctrl) != 0;
            if (ctrl && chord.Code == KeyCode.Character)
            {
                int rune = char.ToLowerInvariant((char)chord.Rune);
                if (rune == 'z' || rune == 'y' || rune == 'k' || rune == 'u')
                    return !IsReadOnly;
                return ConsumeUnboundControlKeys;
            }

            switch (chord.Code)
            {
                case KeyCode.Enter:
                case KeyCode.Backspace:
                case KeyCode.Delete:
                    return !IsReadOnly;
                case KeyCode.Left:
                case KeyCode.Right:
                case KeyCode.Up:
                case KeyCode.Down:
                case KeyCode.Home:
                case KeyCode.End:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the editor's own shortcuts for the status bar: undo and redo while editable. Part of
        /// <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints. Never null; empty while read-only or disabled.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!AcceptsText)
                return new KeyHint[0];

            return new KeyHint[]
            {
                KeyHint.For("ctrl+z", "Undo"),
                KeyHint.For("ctrl+y", "Redo")
            };
        }

        /// <summary>
        /// Gets or sets a value indicating whether Ctrl+letter chords the editor does not bind (anything
        /// other than Ctrl+Z, Ctrl+Y, Ctrl+K, Ctrl+U) are consumed. Defaults to true, the original behavior;
        /// set it to false so unbound chords such as a command palette key fall through to the host.
        /// </summary>
        public bool ConsumeUnboundControlKeys { get; set; } = true;

        /// <summary>
        /// Gets or sets the style composed over <see cref="NormalStyle"/> while disabled or read-only.
        /// Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets the first visible column of unwrapped text, which moves when the caret leaves the viewport
        /// horizontally. Always zero while <see cref="WordWrap"/> is on.
        /// </summary>
        public int ScrollColumn
        {
            get { return WordWrap ? 0 : _ScrollColumn; }
        }

        /// <summary>
        /// Gets the number of logical lines. Always at least one.
        /// </summary>
        public int LineCount
        {
            get { return _Lines.Count; }
        }

        /// <summary>
        /// Applies a theme: <see cref="NormalStyle"/> from <see cref="Theme.Text"/> and
        /// <see cref="DisabledStyle"/> from <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            DisabledStyle = theme.Disabled;
        }

        /// <summary>
        /// Gets or sets the base style used to paint the editor: the surface fill and the text share
        /// this style, and the caret is drawn as this style with reverse video so it inverts against
        /// whatever foreground and background are set here. Defaults to <see cref="CellStyle.Default"/>
        /// (the terminal's default colors); assign a style with a background to give the editor a solid
        /// background — for example <c>CellStyle.Default.WithBackground(Color.FromRgb(0x2A, 0x2A, 0x2A))</c>
        /// for a dark grey composer.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets a value indicating whether long logical lines are wrapped to the render width instead
        /// of clipping at the right edge. Wrapping breaks after spaces where possible and hard-breaks words
        /// longer than the width; every character keeps its position, so the caret stays accurate. Defaults to
        /// <c>false</c> (each logical line renders on one row). Hosts that grow to fit content should size
        /// using <see cref="VisualLineCount"/> when this is on.
        /// </summary>
        public bool WordWrap { get; set; }

        /// <summary>
        /// Updates the focused state so the caret shows or hides on the next frame. Part of
        /// <see cref="IFocusAware"/>; called by the host and <see cref="FocusManager"/> on focus changes.
        /// </summary>
        /// <param name="focused"><c>true</c> when the editor has gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        /// <summary>
        /// Gets the caret row (zero-based).
        /// </summary>
        public int CaretRow
        {
            get { return _Row; }
        }

        /// <summary>
        /// Gets the caret column (zero-based).
        /// </summary>
        public int CaretColumn
        {
            get { return _Column; }
        }

        /// <summary>
        /// Gets or sets the full text, with lines joined by newlines. Setting resets the caret to the
        /// end. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Text
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < _Lines.Count; i++)
                {
                    if (i > 0)
                        builder.Append('\n');
                    builder.Append(_Lines[i]);
                }

                return builder.ToString();
            }

            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                string before = Text;
                _Lines.Clear();
                string[] parts = value.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                for (int i = 0; i < parts.Length; i++)
                    _Lines.Add(parts[i]);

                if (_Lines.Count == 0)
                    _Lines.Add(string.Empty);

                _Row = _Lines.Count - 1;
                _Column = _Lines[_Row].Length;
                _Undo.Clear();
                _Redo.Clear();
                if (!string.Equals(before, Text, StringComparison.Ordinal))
                    RaiseChanged();
            }
        }

        /// <summary>
        /// Handles a key event, performing the corresponding edit or caret movement.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            bool ctrl = (key.Modifiers & KeyModifiers.Ctrl) != 0;
            bool editable = !IsReadOnly;

            if (ctrl && key.Code == KeyCode.Character)
            {
                switch (key.Rune)
                {
                    case 'z':
                        if (!editable)
                            return false;
                        Undo();
                        return true;
                    case 'y':
                        if (!editable)
                            return false;
                        Redo();
                        return true;
                    case 'k':
                        if (!editable)
                            return false;
                        KillToEndOfLine();
                        return true;
                    case 'u':
                        if (!editable)
                            return false;
                        Yank();
                        return true;
                    default:
                        return ConsumeUnboundControlKeys;
                }
            }

            switch (key.Code)
            {
                case KeyCode.Character:
                    if (!editable)
                        return false;
                    InsertText(char.ConvertFromUtf32(key.Rune));
                    return true;
                case KeyCode.Enter:
                    if (!editable)
                        return false;
                    InsertNewline();
                    return true;
                case KeyCode.Backspace:
                    if (!editable)
                        return false;
                    Backspace();
                    return true;
                case KeyCode.Delete:
                    if (!editable)
                        return false;
                    DeleteForward();
                    return true;
                case KeyCode.Left:
                    MoveLeft();
                    return true;
                case KeyCode.Right:
                    MoveRight();
                    return true;
                case KeyCode.Up:
                    MoveUp();
                    return true;
                case KeyCode.Down:
                    MoveDown();
                    return true;
                case KeyCode.Home:
                    _Column = 0;
                    return true;
                case KeyCode.End:
                    _Column = _Lines[_Row].Length;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Inserts text at the caret, expanding embedded newlines.
        /// </summary>
        /// <param name="text">The text to insert. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void InsertText(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (text.Length == 0)
                return;

            PushUndo();
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            int start = 0;
            while (true)
            {
                int newline = normalized.IndexOf('\n', start);
                string segment = newline < 0 ? normalized.Substring(start) : normalized.Substring(start, newline - start);
                InsertRaw(segment);
                if (newline < 0)
                    break;

                NewlineRaw();
                start = newline + 1;
            }

            RaiseChanged();
        }

        /// <summary>
        /// Inserts a newline at the caret.
        /// </summary>
        public void InsertNewline()
        {
            PushUndo();
            NewlineRaw();
            RaiseChanged();
        }

        /// <summary>
        /// Deletes the character before the caret, joining lines when at the start of a line.
        /// </summary>
        public void Backspace()
        {
            if (_Column == 0 && _Row == 0)
                return;

            PushUndo();
            if (_Column > 0)
            {
                int start = TextFit.PreviousBoundary(_Lines[_Row], _Column);
                _Lines[_Row] = _Lines[_Row].Remove(start, _Column - start);
                _Column = start;
            }
            else
            {
                int prevLength = _Lines[_Row - 1].Length;
                _Lines[_Row - 1] += _Lines[_Row];
                _Lines.RemoveAt(_Row);
                _Row--;
                _Column = prevLength;
            }

            RaiseChanged();
        }

        /// <summary>
        /// Deletes the character at the caret, joining lines when at the end of a line.
        /// </summary>
        public void DeleteForward()
        {
            string line = _Lines[_Row];
            if (_Column < line.Length)
            {
                PushUndo();
                int end = TextFit.NextBoundary(line, _Column);
                _Lines[_Row] = line.Remove(_Column, end - _Column);
                RaiseChanged();
            }
            else if (_Row < _Lines.Count - 1)
            {
                PushUndo();
                _Lines[_Row] += _Lines[_Row + 1];
                _Lines.RemoveAt(_Row + 1);
                RaiseChanged();
            }
        }

        /// <summary>
        /// Kills text from the caret to the end of the line into the kill ring.
        /// </summary>
        public void KillToEndOfLine()
        {
            string line = _Lines[_Row];
            if (_Column >= line.Length)
                return;

            PushUndo();
            _KillRing = line.Substring(_Column);
            _Lines[_Row] = line.Substring(0, _Column);
            RaiseChanged();
        }

        /// <summary>
        /// Inserts the kill-ring contents at the caret.
        /// </summary>
        public void Yank()
        {
            if (_KillRing.Length == 0)
                return;

            InsertText(_KillRing);
        }

        /// <summary>
        /// Moves the caret to the next occurrence of the query after the caret, wrapping around.
        /// </summary>
        /// <param name="query">The text to find. Ignored when null or empty.</param>
        /// <returns><c>true</c> when a match was found; otherwise <c>false</c>.</returns>
        public bool Find(string query)
        {
            if (string.IsNullOrEmpty(query))
                return false;

            int rows = _Lines.Count;
            for (int i = 0; i <= rows; i++)
            {
                int row = (_Row + i) % rows;
                int from = i == 0 ? _Column + 1 : 0;
                if (from > _Lines[row].Length)
                    continue;

                int index = _Lines[row].IndexOf(query, Math.Min(from, _Lines[row].Length), StringComparison.Ordinal);
                if (index >= 0)
                {
                    _Row = row;
                    _Column = index;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Replaces every occurrence of a string with another, recording a single undo step.
        /// </summary>
        /// <param name="find">The text to find. Ignored when null or empty.</param>
        /// <param name="replace">The replacement text. Must not be null.</param>
        /// <returns>The number of replacements made.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="replace"/> is null.</exception>
        public int ReplaceAll(string find, string replace)
        {
            if (replace == null)
                throw new ArgumentNullException(nameof(replace));
            if (string.IsNullOrEmpty(find))
                return 0;

            int total = 0;
            for (int r = 0; r < _Lines.Count; r++)
            {
                int pos = 0;
                while ((pos = _Lines[r].IndexOf(find, pos, StringComparison.Ordinal)) >= 0)
                {
                    total++;
                    pos += find.Length;
                }
            }

            if (total == 0)
                return 0;

            PushUndo();
            for (int r = 0; r < _Lines.Count; r++)
                _Lines[r] = _Lines[r].Replace(find, replace);

            _Row = Math.Min(_Row, _Lines.Count - 1);
            _Column = Math.Min(_Column, _Lines[_Row].Length);
            RaiseChanged();
            return total;
        }

        /// <summary>
        /// Undoes the last edit.
        /// </summary>
        public void Undo()
        {
            if (_Undo.Count == 0)
                return;

            _Redo.Push(Capture());
            Restore(_Undo.Pop());
            RaiseChanged();
        }

        /// <summary>
        /// Redoes the last undone edit.
        /// </summary>
        public void Redo()
        {
            if (_Redo.Count == 0)
                return;

            _Undo.Push(Capture());
            Restore(_Redo.Pop());
            RaiseChanged();
        }

        /// <summary>
        /// Moves the caret left.
        /// </summary>
        public void MoveLeft()
        {
            if (_Column > 0)
                _Column = TextFit.PreviousBoundary(_Lines[_Row], _Column);
            else if (_Row > 0)
            {
                _Row--;
                _Column = _Lines[_Row].Length;
            }
        }

        /// <summary>
        /// Moves the caret right.
        /// </summary>
        public void MoveRight()
        {
            if (_Column < _Lines[_Row].Length)
                _Column = TextFit.NextBoundary(_Lines[_Row], _Column);
            else if (_Row < _Lines.Count - 1)
            {
                _Row++;
                _Column = 0;
            }
        }

        /// <summary>
        /// Moves the caret up.
        /// </summary>
        public void MoveUp()
        {
            if (_Row > 0)
            {
                int visualColumn = TextFit.ColumnOf(_Lines[_Row], _Column);
                _Row--;
                _Column = TextFit.IndexAtColumn(_Lines[_Row], visualColumn);
            }
        }

        /// <summary>
        /// Moves the caret down.
        /// </summary>
        public void MoveDown()
        {
            if (_Row < _Lines.Count - 1)
            {
                int visualColumn = TextFit.ColumnOf(_Lines[_Row], _Column);
                _Row++;
                _Column = TextFit.IndexAtColumn(_Lines[_Row], visualColumn);
            }
        }

        /// <summary>
        /// Positions the caret at the clicked row and column on a left press (mapping the screen row through
        /// the current scroll offset), and scrolls the caret one line per wheel notch. Coordinates are
        /// widget-local. Other mouse events are not consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event moved the caret; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!_Enabled)
                return false;

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                if (WordWrap && _VisLogical.Count > 0)
                {
                    int vi = Math.Max(0, Math.Min(_LastTop + mouse.Y, _VisLogical.Count - 1));
                    _Row = _VisLogical[vi];
                    string segment = _Lines[_Row].Substring(_VisStart[vi], _VisLen[vi]);
                    _Column = _VisStart[vi] + TextFit.IndexAtColumn(segment, Math.Max(0, mouse.X));
                    _Column = Math.Min(_Column, _Lines[_Row].Length);
                    return true;
                }

                int row = Math.Max(0, Math.Min(_LastTop + mouse.Y, _Lines.Count - 1));
                _Row = row;
                _Column = TextFit.IndexAtColumn(_Lines[row], Math.Max(0, mouse.X) + _ScrollColumn);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                if (mouse.Button == MouseButton.WheelUp)
                {
                    MoveUp();
                    return true;
                }

                if (mouse.Button == MouseButton.WheelDown)
                {
                    MoveDown();
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int lines = WordWrap ? VisualLineCount(available.Width) : _Lines.Count;
            return new Size(available.Width, Math.Min(available.Height, lines));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int height = surface.Size.Height;
            int width = surface.Size.Width;
            CellStyle textStyle = !_Enabled || IsReadOnly ? DisabledStyle.Over(NormalStyle) : NormalStyle;
            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(textStyle));
            if (width <= 0 || height <= 0)
                return;

            if (WordWrap)
            {
                RenderWrapped(surface, width, height, textStyle);
                return;
            }

            int top = 0;
            if (_Row >= height)
                top = _Row - height + 1;

            _LastTop = top;
            _LastHeight = height;

            // Horizontal scroll: keep the caret column inside the viewport (one spare column at the right
            // so the caret can sit after the last glyph).
            int caretColumn = TextFit.ColumnOf(_Lines[_Row], _Column);
            int usable = Math.Max(1, width - 1);
            if (caretColumn < _ScrollColumn)
                _ScrollColumn = caretColumn;
            else if (caretColumn > _ScrollColumn + usable)
                _ScrollColumn = caretColumn - usable;

            for (int row = 0; row < height && top + row < _Lines.Count; row++)
                surface.DrawText(0, row, TextFit.Slice(_Lines[top + row], _ScrollColumn, width), textStyle);

            if (IsFocused && _Enabled)
                DrawCaret(surface, _Lines[_Row], _Column, caretColumn - _ScrollColumn, _Row - top, width, height);
        }

        private void DrawCaret(ISurface surface, string line, int index, int x, int y, int width, int height)
        {
            if (y < 0 || y >= height || x < 0 || x >= width)
                return;

            string glyph = " ";
            int glyphWidth = 1;
            if (index < line.Length)
            {
                int end = TextFit.NextBoundary(line, index);
                glyph = line.Substring(index, end - index);
                glyphWidth = TextFit.Width(glyph);
                if (glyphWidth < 1 || x + glyphWidth > width)
                {
                    glyph = " ";
                    glyphWidth = 1;
                }
            }

            CellStyle caretStyle = NormalStyle.WithAttribute(CellAttributes.Reverse, true);
            surface.Set(x, y, Cell.Glyph(glyph, caretStyle, glyphWidth));
            if (glyphWidth == 2)
                surface.Set(x + 1, y, Cell.Continuation(caretStyle));
        }

        /// <summary>
        /// Returns the number of visual rows the text occupies when wrapped to <paramref name="width"/>. When
        /// <see cref="WordWrap"/> is off or the width is not positive, this is the logical line count. Hosts
        /// use it to size a composer that grows to fit its wrapped content.
        /// </summary>
        /// <param name="width">The wrap width in columns.</param>
        /// <returns>The visual row count (at least one).</returns>
        public int VisualLineCount(int width)
        {
            if (!WordWrap || width <= 0)
            {
                return _Lines.Count;
            }

            int total = 0;
            for (int i = 0; i < _Lines.Count; i++)
            {
                total += SegmentCount(_Lines[i], width);
            }

            return total;
        }

        // Rebuilds the visual-row layout, scrolls it to keep the caret in view, and paints it.
        private void RenderWrapped(ISurface surface, int width, int height, CellStyle textStyle)
        {
            _VisLogical.Clear();
            _VisStart.Clear();
            _VisLen.Clear();

            int caretVisual = 0;
            int caretColumn = 0;
            bool caretFound = false;

            for (int r = 0; r < _Lines.Count; r++)
            {
                string line = _Lines[r];
                int firstIndex = _VisLogical.Count;
                ForEachSegment(line, width, (start, len) =>
                {
                    _VisLogical.Add(r);
                    _VisStart.Add(start);
                    _VisLen.Add(len);
                });

                if (r == _Row && !caretFound)
                {
                    for (int k = firstIndex; k < _VisLogical.Count; k++)
                    {
                        int start = _VisStart[k];
                        int len = _VisLen[k];
                        bool lastSegOfLine = k + 1 >= _VisLogical.Count || _VisLogical[k + 1] != r;
                        if (_Column < start + len || (lastSegOfLine && _Column <= start + len))
                        {
                            caretVisual = k;
                            caretColumn = TextFit.ColumnOf(line.Substring(start, len), _Column - start);
                            caretFound = true;
                            break;
                        }
                    }
                }
            }

            int total = _VisLogical.Count;
            int top = 0;
            if (caretFound && caretVisual >= height)
            {
                top = caretVisual - height + 1;
            }

            _LastTop = top;
            _LastHeight = height;

            for (int row = 0; row < height && top + row < total; row++)
            {
                int vi = top + row;
                surface.DrawText(0, row, _Lines[_VisLogical[vi]].Substring(_VisStart[vi], _VisLen[vi]), textStyle);
            }

            if (IsFocused && _Enabled && caretFound)
            {
                string seg = _Lines[_VisLogical[caretVisual]].Substring(_VisStart[caretVisual], _VisLen[caretVisual]);
                DrawCaret(surface, seg, _Column - _VisStart[caretVisual], Math.Min(caretColumn, width - 1), caretVisual - top, width, height);
            }
        }

        private static int SegmentCount(string line, int width)
        {
            int count = 0;
            ForEachSegment(line, width, (start, len) => count++);
            return count;
        }

        // Emits the (startColumn, length) of each visual segment of a logical line wrapped to width, preserving
        // every character: it breaks after the last space within the width when there is one, otherwise
        // hard-breaks at the width. An empty line emits a single zero-length segment.
        private static void ForEachSegment(string line, int width, Action<int, int> emit)
        {
            int length = line.Length;
            if (length == 0)
            {
                emit(0, 0);
                return;
            }

            IReadOnlyList<Grapheme> clusters = Graphemes.Split(line);
            int segStart = 0;
            int segWidth = 0;
            int index = 0;
            int lastSpaceEnd = -1;
            for (int i = 0; i < clusters.Count; i++)
            {
                Grapheme cluster = clusters[i];
                if (segWidth + cluster.Width > width && index > segStart)
                {
                    // Break after the last space within the width when there is one, otherwise hard-break.
                    int breakAt = lastSpaceEnd > segStart + 1 ? lastSpaceEnd : index;
                    emit(segStart, breakAt - segStart);
                    segWidth = TextFit.Width(line.Substring(breakAt, index - breakAt));
                    segStart = breakAt;
                    lastSpaceEnd = -1;
                }

                segWidth += cluster.Width;
                index += cluster.Text.Length;
                if (cluster.Text == " ")
                    lastSpaceEnd = index;
            }

            emit(segStart, length - segStart);
        }

        private void RaiseChanged()
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void InsertRaw(string segment)
        {
            if (segment.Length == 0)
                return;

            _Lines[_Row] = _Lines[_Row].Insert(_Column, segment);
            _Column += segment.Length;
        }

        private void NewlineRaw()
        {
            string line = _Lines[_Row];
            string left = line.Substring(0, _Column);
            string right = line.Substring(_Column);
            _Lines[_Row] = left;
            _Lines.Insert(_Row + 1, right);
            _Row++;
            _Column = 0;
        }

        private void PushUndo()
        {
            _Undo.Push(Capture());
            _Redo.Clear();
            while (_Undo.Count > _MaxUndo)
            {
                EditorSnapshot[] kept = _Undo.ToArray();
                _Undo.Clear();
                for (int i = kept.Length - 2; i >= 0; i--)
                    _Undo.Push(kept[i]);
                break;
            }
        }

        private EditorSnapshot Capture()
        {
            return new EditorSnapshot(_Lines, _Row, _Column);
        }

        private void Restore(EditorSnapshot snapshot)
        {
            _Lines.Clear();
            _Lines.AddRange(snapshot.Lines);
            _Row = Math.Min(snapshot.Row, _Lines.Count - 1);
            _Column = Math.Min(snapshot.Column, _Lines[_Row].Length);
        }
    }
}
