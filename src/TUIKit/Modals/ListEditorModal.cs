namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A single-screen editor for an ordered list of <typeparamref name="T"/>. Items are added through
    /// an inline text field that is parsed, validated, and previewed live; the selected item can be
    /// removed; and — when enabled — items can be reordered with Alt+Up / Alt+Down. Enter finishes and
    /// Escape cancels. The result is delivered through <see cref="Modal.Completion"/> /
    /// <c>ShowAsync&lt;IReadOnlyList&lt;T&gt;&gt;</c>: a fresh list on finish, or <c>null</c> on cancel.
    /// A <c>null</c> means "leave the caller's data alone" and is distinct from an empty list, which is
    /// a real "no items" state. The input collection is copied and never mutated. This type is not
    /// thread-safe; drive it from the UI loop.
    /// </summary>
    /// <typeparam name="T">The list item type.</typeparam>
    public sealed class ListEditorModal<T> : DialogModal
    {
        private readonly List<T> _Items = new List<T>();
        private readonly Func<T, string> _Display;
        private readonly ListEditorOptions<T> _Options;
        private readonly IEqualityComparer<T> _Comparer;
        private readonly ListView<T> _List;
        private readonly TextField _Field = new TextField();
        private bool _Adding;
        private string? _Error;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListEditorModal{T}"/> class.
        /// </summary>
        /// <param name="initialItems">Items to start from; copied, never mutated. Must not be null.</param>
        /// <param name="display">Renders an item's primary text. Must not be null.</param>
        /// <param name="options">Behavior and key bindings; defaults are applied when null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="initialItems"/> or <paramref name="display"/> is null.</exception>
        public ListEditorModal(
            IEnumerable<T> initialItems,
            Func<T, string> display,
            ListEditorOptions<T>? options = null)
        {
            if (initialItems == null)
                throw new ArgumentNullException(nameof(initialItems));

            _Display = display ?? throw new ArgumentNullException(nameof(display));
            _Options = options ?? new ListEditorOptions<T>();
            _Comparer = _Options.DedupeComparer ?? EqualityComparer<T>.Default;
            _Items.AddRange(initialItems);

            _List = new ListView<T>(RowText);
            _List.SetItems(_Items);
            _Field.IsFocused = false;
            UpdateFooter();
        }

        /// <summary>
        /// Gets the number of items currently in the editor.
        /// </summary>
        public int Count
        {
            get { return _Items.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the inline add field is currently open.
        /// </summary>
        public bool IsAdding
        {
            get { return _Adding; }
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (_Adding)
                return HandleAddKey(key);

            return HandleBrowseKey(key);
        }

        /// <inheritdoc/>
        public override bool HandlePaste(string text)
        {
            if (!_Adding)
                return false;

            _Field.Insert(text);
            _Error = null;
            return true;
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            int width = TextWidth("Items (" + _Items.Count + "):");

            if (_Options.Help != null)
            {
                foreach (string line in _Options.Help)
                    width = Math.Max(width, TextWidth(line));
            }

            if (_Items.Count == 0)
                width = Math.Max(width, TextWidth(_Options.EmptyText));

            for (int i = 0; i < _Items.Count; i++)
                width = Math.Max(width, TextWidth("  " + RowText(_Items[i])));

            width = Math.Max(width, TextWidth(_Options.AddPrompt + ": ") + 16);
            return Math.Min(Math.Max(width, 24), availableWidth);
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            int help = _Options.Help != null ? _Options.Help.Count : 0;
            int listRows = Math.Min(Math.Max(1, _Items.Count), 12);
            int height = help + (help > 0 ? 1 : 0) + 1 + listRows;
            if (_Adding)
                height += 2;

            return height;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            int width = content.Size.Width;
            int height = content.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            int y = 0;

            if (_Options.Help != null)
            {
                CellStyle helpStyle = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);
                foreach (string line in _Options.Help)
                {
                    if (y >= height)
                        return;
                    content.DrawText(0, y, Truncate(line, width), helpStyle);
                    y++;
                }

                if (y < height)
                    y++;
            }

            if (y >= height)
                return;
            content.DrawText(0, y, Truncate("Items (" + _Items.Count + "):", width), CellStyle.Default.WithAttribute(CellAttributes.Bold, true));
            y++;

            int reserved = _Adding ? 2 : 0;
            int listHeight = Math.Max(0, height - y - reserved);

            if (_Items.Count == 0)
            {
                if (listHeight > 0)
                    content.DrawText(0, y, Truncate(_Options.EmptyText, width), CellStyle.Default.WithAttribute(CellAttributes.Dim, true));
            }
            else if (listHeight > 0)
            {
                SurfaceView listView = new SurfaceView(content, new Rect(0, y, width, listHeight));
                _List.Render(listView);
            }

            y += listHeight;

            if (_Adding && y + 1 < height)
            {
                string prompt = _Options.AddPrompt + ": ";
                int promptWidth = TextWidth(prompt);
                content.DrawText(0, y, prompt, CellStyle.Default.WithForeground(Color.FromPalette(6)));
                if (promptWidth < width)
                {
                    SurfaceView fieldView = new SurfaceView(content, new Rect(promptWidth, y, width - promptWidth, 1));
                    _Field.Render(fieldView);
                }

                y++;
                RenderPreview(content, y, width);
            }
        }

        private bool HandleBrowseKey(KeyEvent key)
        {
            UpdateFooter();

            if (key.Code == KeyCode.Enter)
            {
                if (!_Options.AllowEmpty && _Items.Count == 0)
                    return true;

                Close(new List<T>(_Items));
                return true;
            }

            if (key.Code == KeyCode.Escape)
                return HandleDismiss(key, null);

            KeyChord chord = KeyChord.FromKeyEvent(key);

            if (_Options.Parse != null && (chord.Equals(_Options.AddKey) || chord.Equals(_Options.AddKeyAlt)))
            {
                _Adding = true;
                _Error = null;
                _Field.Value = string.Empty;
                _Field.IsFocused = true;
                UpdateFooter();
                return true;
            }

            if (chord.Equals(_Options.RemoveKey) || chord.Equals(_Options.RemoveKeyAlt))
            {
                RemoveSelected();
                return true;
            }

            bool alt = (key.Modifiers & KeyModifiers.Alt) == KeyModifiers.Alt;
            if (_Options.AllowReorder && alt && key.Code == KeyCode.Up)
            {
                MoveSelected(-1);
                return true;
            }

            if (_Options.AllowReorder && alt && key.Code == KeyCode.Down)
            {
                MoveSelected(1);
                return true;
            }

            return _List.HandleKey(key);
        }

        private bool HandleAddKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter)
            {
                Commit();
                return true;
            }

            if (key.Code == KeyCode.Escape)
            {
                _Adding = false;
                _Error = null;
                _Field.Value = string.Empty;
                _Field.IsFocused = false;
                UpdateFooter();
                return true;
            }

            bool handled = _Field.HandleKey(key);
            if (handled)
                _Error = null;

            return true;
        }

        private void Commit()
        {
            if (_Options.Parse == null)
            {
                _Adding = false;
                return;
            }

            ParseResult<T> result = _Options.Parse(_Field.Value);
            if (!result.Ok)
            {
                _Error = result.Error;
                return;
            }

            if (!_Options.AllowDuplicates && ContainsItem(result.Value))
            {
                _Error = "Duplicate item.";
                return;
            }

            _Items.Add(result.Value);
            SelectIndex(_Items.Count - 1);
            _Adding = false;
            _Error = null;
            _Field.Value = string.Empty;
            _Field.IsFocused = false;
            UpdateFooter();
        }

        private bool ContainsItem(T value)
        {
            for (int i = 0; i < _Items.Count; i++)
            {
                if (_Comparer.Equals(_Items[i], value))
                    return true;
            }

            return false;
        }

        private void RemoveSelected()
        {
            int sel = _List.SelectedIndex;
            if (sel < 0)
                return;

            _Items.RemoveAt(sel);
            SelectIndex(Math.Min(sel, _Items.Count - 1));
        }

        private void MoveSelected(int delta)
        {
            int sel = _List.SelectedIndex;
            int target = sel + delta;
            if (sel < 0 || target < 0 || target >= _Items.Count)
                return;

            T temp = _Items[sel];
            _Items[sel] = _Items[target];
            _Items[target] = temp;
            SelectIndex(target);
        }

        private void SelectIndex(int index)
        {
            _List.SetItems(_Items);
            for (int i = 0; i < index; i++)
                _List.SelectNext();
        }

        private void RenderPreview(ISurface content, int y, int width)
        {
            if (y >= content.Size.Height)
                return;

            string? message;
            CellStyle style;
            if (_Error != null)
            {
                message = "✗ " + _Error;
                style = CellStyle.Default.WithForeground(Color.FromPalette(1));
            }
            else if (_Field.Value.Length == 0 || _Options.Parse == null)
            {
                message = null;
                style = CellStyle.Default;
            }
            else
            {
                ParseResult<T> preview = _Options.Parse(_Field.Value);
                if (preview.Ok)
                {
                    string described = _Options.Describe != null ? _Options.Describe(preview.Value) : _Display(preview.Value);
                    message = "→ " + described;
                    style = CellStyle.Default.WithForeground(Color.FromPalette(2));
                }
                else
                {
                    message = "✗ " + (preview.Error ?? "Invalid input.");
                    style = CellStyle.Default.WithForeground(Color.FromPalette(1));
                }
            }

            if (message != null)
                content.DrawText(0, y, Truncate(message, width), style);
        }

        private void UpdateFooter()
        {
            if (_Adding)
            {
                FooterHint = "Enter: commit · Esc: back";
                return;
            }

            List<string> parts = new List<string>();
            if (_Options.Parse != null)
                parts.Add("a: add");
            parts.Add("d: remove");
            if (_Options.AllowReorder)
                parts.Add("Alt+↑/↓: move");
            parts.Add("Enter: done");
            parts.Add("Esc: cancel");
            FooterHint = string.Join(" · ", parts);
        }

        private string RowText(T item)
        {
            string primary = _Display(item);
            if (_Options.Describe == null)
                return primary;

            return primary + "  " + _Options.Describe(item);
        }

        private static int TextWidth(string text)
        {
            return TUIKit.Unicode.Graphemes.MeasureWidth(text);
        }
    }
}
