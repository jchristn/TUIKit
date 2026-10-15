namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A list that filters its items as the user types, using fuzzy subsequence matching with the
    /// matched characters highlighted. The most-reused TUI interaction — command palettes, file jumps,
    /// pickers. Type to filter, Up/Down to move, and read <see cref="SelectedItem"/> on accept. Items
    /// are of any type <typeparamref name="T"/>; a display selector maps each to the text matched and
    /// shown (the identity when <typeparamref name="T"/> is <see cref="string"/>).
    /// Opt-in features cover real pickers: secondary text (<see cref="Detail"/>), items that cannot be
    /// chosen (<see cref="IsEnabled"/>), checkboxes (<see cref="MultiSelect"/>), results computed from the
    /// query (<see cref="ItemsProvider"/>), Enter to choose (<see cref="ItemActivated"/>), paste
    /// (<see cref="Insert"/>), and theme styles (<see cref="UseThemeStyles"/>).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <remarks>Not thread-safe: use it from the UI thread. The provider runs on the calling thread.</remarks>
    public sealed class FuzzyList<T> : IWidget, IFocusable, IMouseAware, IThemeable, ITextEntry, ITextEntryKeys, IKeyHintSource, IPasteTarget
    {
        #region Private-Members

        private readonly List<T> _Items = new List<T>();
        private readonly List<string> _Labels = new List<string>();
        private readonly List<int> _Filtered = new List<int>();
        private readonly Func<T, string> _Display;
        private readonly IEqualityComparer<T> _Comparer;
        private readonly List<T> _Checked = new List<T>();
        private string _Query = string.Empty;
        private string _QueryPrompt = "> ";
        private string _CheckedGlyph = "[x]";
        private string _UncheckedGlyph = "[ ]";
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private Func<string, IEnumerable<T>>? _ItemsProvider;
        private Theme? _Theme;
        private bool _SearchDetail;
        private Func<T, bool>? _IsEnabled;

        #endregion

        #region Public-Members

        /// <summary>
        /// Raised when Enter (no modifiers) or a double click activates an enabled item. Enter is consumed
        /// only while a handler is attached, so a list without one leaves Enter to its container, as in 1.7.
        /// </summary>
        public event EventHandler<T>? ItemActivated;

        /// <summary>
        /// Raised once per change to the checked set, from Space, Ctrl+A, or <see cref="SetChecked"/>.
        /// </summary>
        public event EventHandler? CheckedChanged;

        /// <summary>
        /// Raised when <see cref="ItemsProvider"/> throws. The previous items stay in place.
        /// </summary>
        public event EventHandler<Exception>? ProviderFailed;

        /// <summary>
        /// Gets or sets the highlight style for the selected row. Defaults to reversed cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style for matched characters. Defaults to bold yellow.
        /// </summary>
        public CellStyle MatchStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(3)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of the query line. Defaults to palette color 6 (cyan), the 1.7 look.
        /// </summary>
        public CellStyle QueryStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of unselected, enabled rows. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of disabled rows (see <see cref="IsEnabled"/>). Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style of <see cref="Detail"/> text on unselected rows. Defaults to palette color 8 (grey).
        /// </summary>
        public CellStyle DetailStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of the check column on unselected rows while <see cref="MultiSelect"/> is on.
        /// Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle CheckStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the text drawn before the query. Defaults to <c>"&gt; "</c>. May be empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string QueryPrompt
        {
            get { return _QueryPrompt; }
            set { _QueryPrompt = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the list follows the theme passed to
        /// <see cref="ApplyTheme"/>. Defaults to false: <see cref="ApplyTheme"/> only remembers the theme, so a
        /// host with <c>ApplyThemeToWidgets</c> does not restyle a 1.7 list. While true, rows draw with the
        /// theme's roles (query from <see cref="Theme.Accent"/>, rows from <see cref="Theme.Text"/>, disabled from
        /// <see cref="Theme.Disabled"/>, detail from <see cref="Theme.Muted"/>, the selected row from
        /// <see cref="Theme.Selection"/>) instead of the style properties, which keep their values, so turning
        /// it off restores them.
        /// </summary>
        public bool UseThemeStyles { get; set; }

        /// <summary>
        /// Gets or sets the secondary text drawn after each label (two spaces, then the detail in
        /// <see cref="DetailStyle"/>), such as an id, a path, or a description, or null for none (the default).
        /// The detail is truncated before the label when the row is narrow.
        /// </summary>
        public Func<T, string>? Detail { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the query also matches <see cref="Detail"/> text, ranked below
        /// a label match of the same score. Defaults to false. Re-filters when changed.
        /// </summary>
        public bool SearchDetail
        {
            get { return _SearchDetail; }
            set
            {
                _SearchDetail = value;
                Refilter();
            }
        }

        /// <summary>
        /// Gets or sets a predicate deciding whether an item can be chosen right now, or null for every item
        /// enabled (the default). Disabled items are listed in <see cref="DisabledStyle"/>; navigation and
        /// clicks skip them, and they cannot be activated or checked from the keyboard or mouse.
        /// </summary>
        public Func<T, bool>? IsEnabled
        {
            get { return _IsEnabled; }
            set
            {
                _IsEnabled = value;
                FixSelection(1);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether items carry checkboxes. Defaults to false. When true,
        /// Space toggles the selected item's check instead of typing a space (so a multi-select query cannot
        /// contain spaces; fuzzy matching makes them unnecessary), Ctrl+A checks every visible enabled item or
        /// unchecks them all when all are checked, and a check column is drawn with <see cref="CheckedGlyph"/>
        /// and <see cref="UncheckedGlyph"/>. In a <see cref="TextField"/> with line-editing keys, Ctrl+A moves
        /// to the start; this list has no caret, so here it always means "check all", as in
        /// <see cref="DataTable{T}"/>.
        /// </summary>
        public bool MultiSelect { get; set; }

        /// <summary>
        /// Gets or sets the glyph drawn for a checked item. Defaults to <c>"[x]"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string CheckedGlyph
        {
            get { return _CheckedGlyph; }
            set { _CheckedGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the glyph drawn for an unchecked item. Defaults to <c>"[ ]"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string UncheckedGlyph
        {
            get { return _UncheckedGlyph; }
            set { _UncheckedGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the query-editing keys Ctrl+U (clear the query) and Ctrl+W
        /// (delete its last word) are on. Defaults to false, where both are left to the container.
        /// </summary>
        public bool UseLineEditingKeys { get; set; }

        /// <summary>
        /// Gets or sets a provider that computes the items for each query, or null for the fixed items given at
        /// construction (the default). When set, every query change replaces the items with the provider's
        /// result for the new query, which is then scored and highlighted as usual; assigning it runs it once
        /// for the current query. It runs on the UI thread, so keep it fast. A provider that throws leaves the
        /// previous items in place and raises <see cref="ProviderFailed"/>.
        /// </summary>
        public Func<string, IEnumerable<T>>? ItemsProvider
        {
            get { return _ItemsProvider; }
            set
            {
                _ItemsProvider = value;
                Refilter();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="GetKeyHints"/> lists the list's keys. Defaults to
        /// true; false returns no hints.
        /// </summary>
        public bool ShowKeyHints { get; set; } = true;

        /// <summary>
        /// Gets or sets the hint shown for leaving the list while typing, or null for
        /// <see cref="KeyHintResolver.LeaveTextHint"/>. Part of <see cref="ITextEntryKeys"/>. Defaults to null.
        /// </summary>
        public KeyHint? LeaveHint { get; set; }

        /// <summary>
        /// Gets a value indicating that printable keys type into the query. Always true. Part of
        /// <see cref="ITextEntry"/>, so the key-hint resolver hides single-letter shortcuts the list would type.
        /// </summary>
        public bool AcceptsText
        {
            get { return true; }
        }

        /// <summary>
        /// Gets or sets the current query. Setting it re-filters the list (and runs <see cref="ItemsProvider"/>).
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
        /// Gets the number of items currently matching the query, enabled or not.
        /// </summary>
        public int MatchCount
        {
            get { return _Filtered.Count; }
        }

        /// <summary>
        /// Gets the selected item, or the type default when nothing matches or every match is disabled. Never a
        /// disabled item.
        /// </summary>
        public T? SelectedItem
        {
            get { return _Selected >= 0 && _Selected < _Filtered.Count ? _Items[_Filtered[_Selected]] : default; }
        }

        /// <summary>
        /// Gets the checked items in original item order. Never null; empty when nothing is checked.
        /// </summary>
        public IReadOnlyList<T> CheckedItems
        {
            get
            {
                List<T> result = new List<T>();
                foreach (T item in _Items)
                {
                    if (IndexOfChecked(item) >= 0 && !result.Any(r => _Comparer.Equals(r, item)))
                        result.Add(item);
                }

                foreach (T item in _Checked)
                {
                    if (!result.Any(r => _Comparer.Equals(r, item)))
                        result.Add(item);
                }

                return result;
            }
        }

        #endregion

        #region Constructors-and-Factories

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
            : this(items, display, EqualityComparer<T>.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuzzyList{T}"/> class with an item comparer, for item
        /// types without value equality. Checks belong to items, compared with <paramref name="comparer"/>, so
        /// they survive query changes and provider refreshes.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <param name="display">A selector mapping an item to its text; may be null only when <typeparamref name="T"/> is <see cref="string"/>.</param>
        /// <param name="comparer">The item equality comparer. Must not be null.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="items"/> or <paramref name="comparer"/> is null, or when
        /// <paramref name="display"/> is null and <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        public FuzzyList(IEnumerable<T> items, Func<T, string>? display, IEqualityComparer<T> comparer)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

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

            _Comparer = comparer;
            SetItemsCore(items);
            Refilter();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Returns whether an item is checked.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> when checked; otherwise <c>false</c>.</returns>
        public bool IsChecked(T item)
        {
            return IndexOfChecked(item) >= 0;
        }

        /// <summary>
        /// Checks or unchecks an item programmatically (disabled items included; only the keyboard and mouse
        /// refuse them), raising <see cref="CheckedChanged"/> when the state changed.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="isChecked">The new state.</param>
        public void SetChecked(T item, bool isChecked)
        {
            if (SetCheckedCore(item, isChecked))
                CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Appends text to the query, for paste: newlines and other controls become spaces. Null or empty is ignored.
        /// </summary>
        /// <param name="text">The text to append.</param>
        public void Insert(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            char[] chars = text!.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsControl(chars[i]))
                    chars[i] = ' ';
            }

            Query = _Query + new string(chars);
        }

        /// <summary>
        /// Appends pasted text to the query through <see cref="Insert"/>. Part of <see cref="IPasteTarget"/>.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null.</param>
        /// <returns>Always <c>true</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public bool HandlePaste(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Insert(text);
            return true;
        }

        /// <summary>
        /// Remembers the theme and, while <see cref="UseThemeStyles"/> is true, draws with it. Part of
        /// <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        /// <summary>
        /// Returns whether the list consumes a chord while it is focused: the navigation keys and Backspace;
        /// Enter while <see cref="ItemActivated"/> has a handler; Ctrl+A with <see cref="MultiSelect"/>; and
        /// Ctrl+U and Ctrl+W with <see cref="UseLineEditingKeys"/>. Part of <see cref="ITextEntryKeys"/>.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns><c>true</c> when the list consumes it.</returns>
        public bool ConsumesChord(KeyChord chord)
        {
            switch (chord.Code)
            {
                case KeyCode.Up:
                case KeyCode.Down:
                case KeyCode.PageUp:
                case KeyCode.PageDown:
                case KeyCode.Home:
                case KeyCode.End:
                case KeyCode.Backspace:
                    return true;
                case KeyCode.Enter:
                    return chord.Modifiers == KeyModifiers.None && ItemActivated != null;
                case KeyCode.Character:
                    if (chord.Modifiers != KeyModifiers.Ctrl)
                        return false;
                    char c = char.ToLowerInvariant((char)chord.Rune);
                    return (c == 'a' && MultiSelect) || ((c == 'u' || c == 'w') && UseLineEditingKeys);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the list's keys for the status bar: Up/Down Move, Enter Choose while <see cref="ItemActivated"/>
        /// has a handler, and with <see cref="MultiSelect"/> Space Check and Ctrl+A Check all. Part of
        /// <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null while <see cref="ShowKeyHints"/> is false.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!ShowKeyHints)
                return null;

            List<KeyHint> hints = new List<KeyHint>();
            if (ItemActivated != null)
                hints.Add(new KeyHint(new KeyChord(KeyCode.Enter, 0, KeyModifiers.None), "Choose", 10).WhileTyping());
            hints.Add(new KeyHint("Up/Down", "Move").WhileTyping());
            if (MultiSelect)
            {
                hints.Add(new KeyHint(new KeyChord(KeyCode.Character, ' ', KeyModifiers.None), "Check").WhileTyping());
                hints.Add(KeyHint.For("ctrl+a", "Check all").WhileTyping());
            }

            return hints;
        }

        /// <summary>
        /// Handles typing (to filter), Backspace (removes the last grapheme), navigation (skipping disabled
        /// items), and the opt-in keys described on <see cref="MultiSelect"/>, <see cref="UseLineEditingKeys"/>,
        /// and <see cref="ItemActivated"/>.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    MoveSelection(-1, 1);
                    return true;
                case KeyCode.Down:
                    MoveSelection(1, 1);
                    return true;
                case KeyCode.PageUp:
                    MoveSelection(-1, Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.PageDown:
                    MoveSelection(1, Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    FixSelection(1);
                    return true;
                case KeyCode.End:
                    _Selected = _Filtered.Count - 1;
                    FixSelection(-1);
                    return true;
                case KeyCode.Backspace:
                    if (_Query.Length > 0)
                        Query = _Query.Substring(0, TextFit.PreviousBoundary(_Query, _Query.Length));
                    return true;
                case KeyCode.Enter:
                    if (key.Modifiers != KeyModifiers.None)
                        return false;
                    return Activate();
                case KeyCode.Character:
                    if (key.Modifiers == KeyModifiers.Ctrl)
                        return HandleCtrl(char.ToLowerInvariant((char)key.Rune));
                    if ((key.Modifiers & KeyModifiers.Ctrl) != 0)
                        return false;
                    if (MultiSelect && key.Rune == ' ' && key.Modifiers == KeyModifiers.None)
                    {
                        ToggleSelected();
                        return true;
                    }

                    Query = _Query + char.ConvertFromUtf32(key.Rune);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Selects the enabled row under the pointer on a left press (toggling its check with
        /// <see cref="MultiSelect"/> when the click lands on the check column), activates it on a double click,
        /// and moves the selection one enabled row per wheel notch. Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event changed the selection; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            int count = _Filtered.Count;
            if (count == 0)
                return false;

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                if (mouse.Button == MouseButton.WheelUp)
                {
                    MoveSelection(-1, 1);
                    return true;
                }

                if (mouse.Button == MouseButton.WheelDown)
                {
                    MoveSelection(1, 1);
                    return true;
                }

                return false;
            }

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                int index = _Top + (mouse.Y - 1);
                if (index >= 0 && index < count && ItemEnabled(_Filtered[index]))
                {
                    _Selected = index;
                    if (mouse.ClickCount >= 2)
                        Activate();
                    else if (MultiSelect && mouse.X < TextFit.Width(_CheckedGlyph))
                        ToggleSelected();
                    return true;
                }
            }

            return false;
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

            Theme? theme = UseThemeStyles ? _Theme : null;
            CellStyle queryStyle = theme != null ? theme.Accent : QueryStyle;
            CellStyle normalStyle = theme != null ? theme.Text : NormalStyle;
            CellStyle disabledStyle = theme != null ? theme.Disabled : DisabledStyle;
            CellStyle detailStyle = theme != null ? theme.Muted : DetailStyle;
            CellStyle highlightStyle = theme != null ? theme.Selection : HighlightStyle;
            CellStyle checkStyle = theme != null ? theme.Text : CheckStyle;

            surface.DrawText(0, 0, _QueryPrompt + _Query, queryStyle);

            int listHeight = height - 1;
            if (listHeight <= 0)
                return;

            _LastViewportHeight = listHeight;

            int anchor = Math.Max(0, _Selected);
            if (anchor < _Top)
                _Top = anchor;
            else if (anchor >= _Top + listHeight)
                _Top = anchor - listHeight + 1;

            for (int row = 0; row < listHeight && _Top + row < _Filtered.Count; row++)
            {
                int filteredIndex = _Top + row;
                int itemIndex = _Filtered[filteredIndex];
                string item = _Labels[itemIndex];
                bool selected = filteredIndex == _Selected;
                bool enabled = ItemEnabled(itemIndex);
                int y = row + 1;

                CellStyle baseStyle = selected ? highlightStyle : (enabled ? normalStyle : disabledStyle);
                if (selected)
                    surface.Fill(new Rect(0, y, width, 1), Cell.Blank(baseStyle));

                int column = 0;
                if (MultiSelect)
                {
                    string glyph = IsChecked(_Items[itemIndex]) ? _CheckedGlyph : _UncheckedGlyph;
                    column += surface.DrawText(0, y, TextFit.Truncate(glyph + " ", width), selected ? baseStyle : checkStyle);
                }

                string? detail = Detail?.Invoke(_Items[itemIndex]);
                int labelRoom = width - column;
                int labelWidth = TextFit.Width(item);
                List<int> positions = new List<int>();
                Matches(item, _Query, positions);
                column = DrawLabel(surface, item, positions, column, y, width, selected, baseStyle, enabled);

                if (!string.IsNullOrEmpty(detail) && column + 2 < width && labelWidth < labelRoom)
                {
                    string fitted = TextFit.Truncate(detail, width - column - 2);
                    if (fitted.Length > 0)
                        surface.DrawText(column + 2, y, fitted, selected ? baseStyle : (enabled ? detailStyle : disabledStyle));
                }
            }
        }

        #endregion

        #region Private-Methods

        private int DrawLabel(ISurface surface, string item, List<int> positions, int column, int y, int width, bool selected, CellStyle baseStyle, bool enabled)
        {
            // Draw cluster by cluster so wide (CJK, emoji) glyphs occupy two cells and never split.
            IReadOnlyList<Grapheme> clusters = Graphemes.Split(item);
            int charIndex = 0;
            for (int i = 0; i < clusters.Count && column < width; i++)
            {
                Grapheme cluster = clusters[i];
                bool matched = false;
                for (int k = 0; k < cluster.Text.Length && !matched; k++)
                    matched = positions.Contains(charIndex + k);

                CellStyle style = matched && !selected && enabled ? MatchStyle : baseStyle;
                charIndex += cluster.Text.Length;
                if (cluster.Width == 0)
                    continue;
                if (column + cluster.Width > width)
                    break;

                column += surface.DrawText(column, y, cluster.Text, style);
            }

            return column;
        }

        private bool HandleCtrl(char c)
        {
            if (c == 'a' && MultiSelect)
            {
                ToggleAllVisible();
                return true;
            }

            if (UseLineEditingKeys && c == 'u')
            {
                Query = string.Empty;
                return true;
            }

            if (UseLineEditingKeys && c == 'w')
            {
                Query = TextWords.RemoveLastWord(_Query);
                return true;
            }

            return false;
        }

        private bool Activate()
        {
            EventHandler<T>? handler = ItemActivated;
            if (handler == null || _Selected < 0 || _Selected >= _Filtered.Count)
                return false;

            int itemIndex = _Filtered[_Selected];
            if (!ItemEnabled(itemIndex))
                return false;

            handler(this, _Items[itemIndex]);
            return true;
        }

        private void ToggleSelected()
        {
            if (_Selected < 0 || _Selected >= _Filtered.Count)
                return;

            int itemIndex = _Filtered[_Selected];
            if (!ItemEnabled(itemIndex))
                return;

            T item = _Items[itemIndex];
            SetChecked(item, !IsChecked(item));
        }

        private void ToggleAllVisible()
        {
            List<T> visible = new List<T>();
            foreach (int index in _Filtered)
            {
                if (ItemEnabled(index))
                    visible.Add(_Items[index]);
            }

            if (visible.Count == 0)
                return;

            bool allChecked = visible.All(IsChecked);
            bool changed = false;
            foreach (T item in visible)
                changed |= SetCheckedCore(item, !allChecked);

            if (changed)
                CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool SetCheckedCore(T item, bool isChecked)
        {
            int index = IndexOfChecked(item);
            if (isChecked == (index >= 0))
                return false;

            if (isChecked)
                _Checked.Add(item);
            else
                _Checked.RemoveAt(index);
            return true;
        }

        private int IndexOfChecked(T item)
        {
            for (int i = 0; i < _Checked.Count; i++)
            {
                if (_Comparer.Equals(_Checked[i], item))
                    return i;
            }

            return -1;
        }

        private bool ItemEnabled(int itemIndex)
        {
            Func<T, bool>? predicate = _IsEnabled;
            return predicate == null || predicate(_Items[itemIndex]);
        }

        private void MoveSelection(int direction, int steps)
        {
            if (_Filtered.Count == 0)
            {
                _Selected = 0;
                return;
            }

            int start = _Selected < 0 ? (direction > 0 ? -1 : _Filtered.Count) : _Selected;
            int target = start;
            int moved = 0;
            int candidate = start + direction;
            while (candidate >= 0 && candidate < _Filtered.Count && moved < steps)
            {
                if (ItemEnabled(_Filtered[candidate]))
                {
                    target = candidate;
                    moved++;
                }

                candidate += direction;
            }

            if (target >= 0 && target < _Filtered.Count)
                _Selected = target;
        }

        private void FixSelection(int direction)
        {
            if (_Filtered.Count == 0)
            {
                _Selected = 0;
                return;
            }

            int start = Math.Max(0, Math.Min(_Selected, _Filtered.Count - 1));
            for (int pass = 0; pass < 2; pass++)
            {
                int dir = pass == 0 ? direction : -direction;
                for (int i = start; i >= 0 && i < _Filtered.Count; i += dir)
                {
                    if (ItemEnabled(_Filtered[i]))
                    {
                        _Selected = i;
                        return;
                    }
                }
            }

            _Selected = -1;
        }

        private void SetItemsCore(IEnumerable<T> items)
        {
            List<T> copy = items.ToList();
            _Items.Clear();
            _Labels.Clear();
            foreach (T item in copy)
            {
                _Items.Add(item);
                _Labels.Add(_Display(item));
            }
        }

        private void Refilter()
        {
            Func<string, IEnumerable<T>>? provider = _ItemsProvider;
            if (provider != null)
            {
                try
                {
                    IEnumerable<T>? result = provider(_Query);
                    SetItemsCore(result ?? Enumerable.Empty<T>());
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    ProviderFailed?.Invoke(this, ex);
                }
            }

            _Filtered.Clear();
            List<int> scores = new List<int>();
            List<int> positions = new List<int>();
            for (int i = 0; i < _Items.Count; i++)
            {
                positions.Clear();
                if (Matches(_Labels[i], _Query, positions))
                {
                    _Filtered.Add(i);
                    scores.Add(Score(positions) * 2);
                    continue;
                }

                Func<T, string>? detail = Detail;
                if (_SearchDetail && detail != null && Matches(detail(_Items[i]) ?? string.Empty, _Query, positions))
                {
                    _Filtered.Add(i);
                    scores.Add(Score(positions) * 2 + 1);
                }
            }

            // Stable sort filtered by score ascending (fewer gaps first; a detail match after a label match of the same score).
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
            FixSelection(1);
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

        #endregion
    }
}
