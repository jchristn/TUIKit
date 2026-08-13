namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A vertical list whose items can each be checked or unchecked independently, for choosing several
    /// options at once. Up/Down move the cursor (the view scrolls to keep it visible), Space toggles the
    /// item under the cursor, and a configurable key toggles every item at once. Items are of any type
    /// <typeparamref name="T"/>; a display selector maps each to its label (the identity when
    /// <typeparamref name="T"/> is <see cref="string"/>).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public sealed class CheckList<T> : IWidget, IFocusable, IFocusAware
    {
        private readonly List<T> _Items = new List<T>();
        private readonly List<bool> _Checked = new List<bool>();
        private readonly Func<T, string> _Display;
        private int _Selected;
        private int _Top;

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckList{T}"/> class.
        /// </summary>
        /// <param name="items">The items. Must not be null; individual items may be null.</param>
        /// <param name="display">
        /// A selector mapping an item to its display label. May be null only when <typeparamref name="T"/>
        /// is <see cref="string"/>, in which case the item itself is used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="items"/> is null, or when <paramref name="display"/> is null and
        /// <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        public CheckList(IEnumerable<T> items, Func<T, string>? display = null)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (display == null)
            {
                if (typeof(T) != typeof(string))
                    throw new ArgumentNullException(nameof(display), "A display selector is required when T is not string.");

                _Display = item => (string)(object)item!;
            }
            else
            {
                _Display = display;
            }

            foreach (T item in items)
            {
                _Items.Add(item);
                _Checked.Add(false);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the list is focused. When focused the cursor row is
        /// drawn with a solid highlight; otherwise it is drawn as bold text. Defaults to true. Set by
        /// the host focus ring through <see cref="IFocusAware"/>.
        /// </summary>
        public bool IsFocused { get; set; } = true;

        /// <summary>
        /// Gets the items. Never null.
        /// </summary>
        public IReadOnlyList<T> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Gets the zero-based index of the cursor, or -1 when the list is empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Items.Count == 0 ? -1 : _Selected; }
        }

        /// <summary>
        /// Gets the glyph drawn for a checked item. Defaults to "[x]".
        /// </summary>
        public string CheckedGlyph { get; set; } = "[x]";

        /// <summary>
        /// Gets the glyph drawn for an unchecked item. Defaults to "[ ]".
        /// </summary>
        public string UncheckedGlyph { get; set; } = "[ ]";

        /// <summary>
        /// Gets or sets the key that toggles every item at once. Defaults to the 'a' character.
        /// </summary>
        public KeyEvent ToggleAllKey { get; set; } = KeyEvent.Char('a');

        /// <summary>
        /// Gets or sets the highlight style for the cursor row when focused. Defaults to black on cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default
            .WithForeground(Color.FromRgb(0, 0, 0))
            .WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style for a checked item's glyph and label. Defaults to bold green.
        /// </summary>
        public CellStyle CheckedStyle { get; set; } = CellStyle.Default
            .WithForeground(Color.FromPalette(2))
            .WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets the zero-based indices of the checked items, in order. Never null.
        /// </summary>
        public IReadOnlyList<int> CheckedIndices
        {
            get
            {
                List<int> result = new List<int>();
                for (int i = 0; i < _Checked.Count; i++)
                {
                    if (_Checked[i])
                        result.Add(i);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the checked items, in order. Never null.
        /// </summary>
        public IReadOnlyList<T> CheckedItems
        {
            get
            {
                List<T> result = new List<T>();
                for (int i = 0; i < _Items.Count; i++)
                {
                    if (_Checked[i])
                        result.Add(_Items[i]);
                }

                return result;
            }
        }

        /// <summary>
        /// Reports whether the item at the supplied index is checked.
        /// </summary>
        /// <param name="index">The zero-based item index.</param>
        /// <returns><c>true</c> when checked; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public bool IsChecked(int index)
        {
            if (index < 0 || index >= _Items.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside the item range.");

            return _Checked[index];
        }

        /// <summary>
        /// Sets the checked state of the item at the supplied index.
        /// </summary>
        /// <param name="index">The zero-based item index.</param>
        /// <param name="value">The new checked state.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetChecked(int index, bool value)
        {
            if (index < 0 || index >= _Items.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside the item range.");

            _Checked[index] = value;
        }

        /// <summary>
        /// Toggles every item to checked when any is currently unchecked, otherwise clears them all.
        /// </summary>
        public void ToggleAll()
        {
            bool anyUnchecked = false;
            for (int i = 0; i < _Checked.Count; i++)
            {
                if (!_Checked[i])
                {
                    anyUnchecked = true;
                    break;
                }
            }

            for (int i = 0; i < _Checked.Count; i++)
                _Checked[i] = anyUnchecked;
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (_Items.Count == 0)
                return false;

            if (key.Equals(ToggleAllKey))
            {
                ToggleAll();
                return true;
            }

            switch (key.Code)
            {
                case KeyCode.Up:
                    _Selected = _Selected > 0 ? _Selected - 1 : 0;
                    return true;
                case KeyCode.Down:
                    _Selected = _Selected < _Items.Count - 1 ? _Selected + 1 : _Items.Count - 1;
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    return true;
                case KeyCode.End:
                    _Selected = _Items.Count - 1;
                    return true;
                case KeyCode.Character:
                    if (key.Rune == ' ')
                    {
                        _Checked[_Selected] = !_Checked[_Selected];
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int width = 0;
            for (int i = 0; i < _Items.Count; i++)
            {
                int labelWidth = TUIKit.Unicode.Graphemes.MeasureWidth(_Display(_Items[i]));
                int rowWidth = 4 + labelWidth;
                if (rowWidth > width)
                    width = rowWidth;
            }

            return new Size(Math.Min(width, available.Width), Math.Min(_Items.Count, available.Height));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int height = surface.Size.Height;
            int width = surface.Size.Width;
            if (height <= 0 || width <= 0)
                return;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + height)
                _Top = _Selected - height + 1;

            for (int row = 0; row < height; row++)
            {
                int index = _Top + row;
                if (index >= _Items.Count)
                    break;

                bool isCursor = index == _Selected;
                bool isChecked = _Checked[index];
                string glyph = isChecked ? CheckedGlyph : UncheckedGlyph;
                string label = _Display(_Items[index]);
                string line = glyph + " " + label;
                if (TUIKit.Unicode.Graphemes.MeasureWidth(line) > width)
                    line = Fit(line, width);

                CellStyle style;
                if (isCursor && IsFocused)
                    style = HighlightStyle;
                else if (isChecked)
                    style = CheckedStyle;
                else
                    style = isCursor ? CellStyle.Default.WithAttribute(CellAttributes.Bold, true) : CellStyle.Default;

                if (isCursor && IsFocused)
                    surface.Fill(new Rect(0, row, width, 1), Cell.Blank(HighlightStyle));

                surface.DrawText(0, row, line, style);
            }
        }

        private static string Fit(string text, int maxWidth)
        {
            if (maxWidth < 1)
                return string.Empty;

            string clipped = text;
            while (clipped.Length > 0 && TUIKit.Unicode.Graphemes.MeasureWidth(clipped) > maxWidth)
                clipped = clipped.Substring(0, clipped.Length - 1);

            return clipped;
        }
    }
}
