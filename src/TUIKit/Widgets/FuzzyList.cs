namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A list that filters its items as the user types, using fuzzy subsequence matching with the
    /// matched characters highlighted. The most-reused TUI interaction — command palettes, file jumps,
    /// pickers. Type to filter, Up/Down to move, and read <see cref="SelectedItem"/> on accept. Items
    /// are of any type <typeparamref name="T"/>; a display selector maps each to the text matched and
    /// shown (the identity when <typeparamref name="T"/> is <see cref="string"/>).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public sealed class FuzzyList<T> : IWidget, IFocusable
    {
        private readonly List<T> _Items = new List<T>();
        private readonly List<string> _Labels = new List<string>();
        private readonly List<int> _Filtered = new List<int>();
        private readonly Func<T, string> _Display;
        private string _Query = string.Empty;
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;

        /// <summary>
        /// Gets or sets the highlight style for the selected row. Defaults to reversed cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style for matched characters. Defaults to bold yellow.
        /// </summary>
        public CellStyle MatchStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(3)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the current query. Setting it re-filters the list.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Query
        {
            get { return _Query; }
            set
            {
                _Query = value ?? throw new ArgumentNullException(nameof(value));
                Refilter();
            }
        }

        /// <summary>
        /// Gets the number of items currently matching the query.
        /// </summary>
        public int MatchCount
        {
            get { return _Filtered.Count; }
        }

        /// <summary>
        /// Gets the selected item, or the type default when nothing matches.
        /// </summary>
        public T? SelectedItem
        {
            get { return _Filtered.Count == 0 ? default : _Items[_Filtered[_Selected]]; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuzzyList{T}"/> class.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <param name="display">
        /// A selector mapping an item to the text matched and shown. May be null only when
        /// <typeparamref name="T"/> is <see cref="string"/>, in which case the item itself is used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="items"/> is null, or when <paramref name="display"/> is null and
        /// <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        public FuzzyList(IEnumerable<T> items, Func<T, string>? display = null)
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
                _Labels.Add(_Display(item));
            }

            Refilter();
        }

        /// <summary>
        /// Handles typing (to filter) and Up/Down (to navigate).
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
                    _Selected = Math.Min(_Filtered.Count - 1, _Selected + 1);
                    return true;
                case KeyCode.PageUp:
                    _Selected = Math.Max(0, _Selected - Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.PageDown:
                    _Selected = Math.Min(Math.Max(0, _Filtered.Count - 1), _Selected + Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    return true;
                case KeyCode.End:
                    _Selected = Math.Max(0, _Filtered.Count - 1);
                    return true;
                case KeyCode.Backspace:
                    if (_Query.Length > 0)
                        Query = _Query.Substring(0, _Query.Length - 1);
                    return true;
                case KeyCode.Character:
                    if ((key.Modifiers & KeyModifiers.Ctrl) != 0)
                        return false;
                    Query = _Query + char.ConvertFromUtf32(key.Rune);
                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            surface.DrawText(0, 0, "> " + _Query, CellStyle.Default.WithForeground(Color.FromPalette(6)));

            int listHeight = height - 1;
            if (listHeight <= 0)
                return;

            _LastViewportHeight = listHeight;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + listHeight)
                _Top = _Selected - listHeight + 1;

            for (int row = 0; row < listHeight && _Top + row < _Filtered.Count; row++)
            {
                int filteredIndex = _Top + row;
                string item = _Labels[_Filtered[filteredIndex]];
                bool selected = filteredIndex == _Selected;
                int y = row + 1;

                CellStyle baseStyle = selected ? HighlightStyle : CellStyle.Default;
                if (selected)
                    surface.Fill(new Rect(0, y, width, 1), Cell.Blank(baseStyle));

                List<int> positions = new List<int>();
                Matches(item, _Query, positions);
                for (int i = 0; i < item.Length && i < width; i++)
                {
                    bool matched = positions.Contains(i);
                    CellStyle style = matched && !selected ? MatchStyle : baseStyle;
                    surface.Set(i, y, Cell.Glyph(item[i].ToString(), style, 1));
                }
            }
        }

        private void Refilter()
        {
            _Filtered.Clear();
            List<int> scores = new List<int>();
            List<int> positions = new List<int>();
            for (int i = 0; i < _Items.Count; i++)
            {
                positions.Clear();
                if (Matches(_Labels[i], _Query, positions))
                {
                    _Filtered.Add(i);
                    scores.Add(Score(positions));
                }
            }

            // Stable sort filtered by score ascending (fewer gaps first).
            for (int i = 1; i < _Filtered.Count; i++)
            {
                int item = _Filtered[i];
                int score = scores[i];
                int j = i - 1;
                while (j >= 0 && scores[j] > score)
                {
                    _Filtered[j + 1] = _Filtered[j];
                    scores[j + 1] = scores[j];
                    j--;
                }

                _Filtered[j + 1] = item;
                scores[j + 1] = score;
            }

            if (_Selected >= _Filtered.Count)
                _Selected = Math.Max(0, _Filtered.Count - 1);
        }

        private static bool Matches(string item, string query, List<int> positions)
        {
            positions.Clear();
            if (query.Length == 0)
                return true;

            int q = 0;
            for (int i = 0; i < item.Length && q < query.Length; i++)
            {
                if (char.ToLowerInvariant(item[i]) == char.ToLowerInvariant(query[q]))
                {
                    positions.Add(i);
                    q++;
                }
            }

            return q == query.Length;
        }

        private static int Score(List<int> positions)
        {
            if (positions.Count == 0)
                return 0;

            int gaps = positions[0];
            for (int i = 1; i < positions.Count; i++)
                gaps += positions[i] - positions[i - 1] - 1;

            return gaps;
        }
    }
}
