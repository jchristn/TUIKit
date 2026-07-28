namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A multi-line text editor widget with caret movement, insertion and deletion, newline handling,
    /// a kill ring, and undo/redo. Drive it by forwarding key events to <see cref="HandleKey"/>. This
    /// is the interactive composer in the example harness.
    /// </summary>
    public sealed class TextEditor : IWidget
    {
        private readonly List<string> _Lines = new List<string> { string.Empty };
        private readonly Stack<EditorSnapshot> _Undo = new Stack<EditorSnapshot>();
        private readonly Stack<EditorSnapshot> _Redo = new Stack<EditorSnapshot>();
        private int _Row;
        private int _Column;
        private string _KillRing = string.Empty;
        private int _MaxUndo = 200;

        /// <summary>
        /// Gets or sets a value indicating whether the editor is focused and should render a caret.
        /// </summary>
        public bool IsFocused { get; set; }

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
            }
        }

        /// <summary>
        /// Handles a key event, performing the corresponding edit or caret movement.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            bool ctrl = (key.Modifiers & KeyModifiers.Ctrl) != 0;

            if (ctrl && key.Code == KeyCode.Character)
            {
                switch (key.Rune)
                {
                    case 'z':
                        Undo();
                        return true;
                    case 'y':
                        Redo();
                        return true;
                    case 'k':
                        KillToEndOfLine();
                        return true;
                    case 'u':
                        Yank();
                        return true;
                    default:
                        return true;
                }
            }

            switch (key.Code)
            {
                case KeyCode.Character:
                    InsertText(char.ConvertFromUtf32(key.Rune));
                    return true;
                case KeyCode.Enter:
                    InsertNewline();
                    return true;
                case KeyCode.Backspace:
                    Backspace();
                    return true;
                case KeyCode.Delete:
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
        }

        /// <summary>
        /// Inserts a newline at the caret.
        /// </summary>
        public void InsertNewline()
        {
            PushUndo();
            NewlineRaw();
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
                _Lines[_Row] = _Lines[_Row].Remove(_Column - 1, 1);
                _Column--;
            }
            else
            {
                int prevLength = _Lines[_Row - 1].Length;
                _Lines[_Row - 1] += _Lines[_Row];
                _Lines.RemoveAt(_Row);
                _Row--;
                _Column = prevLength;
            }
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
                _Lines[_Row] = line.Remove(_Column, 1);
            }
            else if (_Row < _Lines.Count - 1)
            {
                PushUndo();
                _Lines[_Row] += _Lines[_Row + 1];
                _Lines.RemoveAt(_Row + 1);
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
        /// Undoes the last edit.
        /// </summary>
        public void Undo()
        {
            if (_Undo.Count == 0)
                return;

            _Redo.Push(Capture());
            Restore(_Undo.Pop());
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
        }

        /// <summary>
        /// Moves the caret left.
        /// </summary>
        public void MoveLeft()
        {
            if (_Column > 0)
                _Column--;
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
                _Column++;
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
                _Row--;
                _Column = Math.Min(_Column, _Lines[_Row].Length);
            }
        }

        /// <summary>
        /// Moves the caret down.
        /// </summary>
        public void MoveDown()
        {
            if (_Row < _Lines.Count - 1)
            {
                _Row++;
                _Column = Math.Min(_Column, _Lines[_Row].Length);
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Lines.Count));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int height = surface.Size.Height;
            int width = surface.Size.Width;
            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(CellStyle.Default));

            int top = 0;
            if (_Row >= height)
                top = _Row - height + 1;

            for (int row = 0; row < height && top + row < _Lines.Count; row++)
                surface.DrawText(0, row, _Lines[top + row], CellStyle.Default);

            if (IsFocused)
            {
                int caretScreenRow = _Row - top;
                if (caretScreenRow >= 0 && caretScreenRow < height && _Column <= width)
                {
                    Cell under = _Column < _Lines[_Row].Length
                        ? Cell.Glyph(_Lines[_Row][_Column].ToString(), CellStyle.Default, 1)
                        : Cell.Blank(CellStyle.Default);
                    surface.Set(_Column, caretScreenRow, Cell.Glyph(under.Grapheme, under.Style.WithAttribute(CellAttributes.Reverse, true), 1));
                }
            }
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
