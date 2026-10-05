namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A vertical list of selectable items with keyboard navigation and scrolling. The selected item is
    /// highlighted; the view scrolls to keep it visible. Items are of any type <typeparamref name="T"/>;
    /// a display selector maps each to its label (the identity when <typeparamref name="T"/> is
    /// <see cref="string"/>), so the selection can be read back as the original object.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public sealed class ListView<T> : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable, IKeyHintSource
    {
        private readonly List<T> _Items = new List<T>();
        private readonly Func<T, string> _Display;
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private int _HoverIndex = -1;
        private bool _Enabled = true;
        private bool _RevealSelection;
        private Rect _IndicatorRect;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListView{T}"/> class.
        /// </summary>
        /// <param name="display">
        /// A selector mapping an item to its display label. May be null only when
        /// <typeparamref name="T"/> is <see cref="string"/>, in which case the item itself is used.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="display"/> is null and <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        public ListView(Func<T, string>? display = null)
        {
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
        }

        /// <summary>
        /// Gets or sets a value indicating whether the list is focused. When focused the selected item is
        /// drawn with a solid highlight bar; when not focused it is drawn as bold text only, so the
        /// selection stays visible without competing with the focused widget. Defaults to true. Set
        /// automatically by the host focus ring and <see cref="FocusManager"/> through <see cref="IFocusAware"/>.
        /// </summary>
        public bool IsFocused { get; set; } = true;

        /// <summary>
        /// Raised after the selected index changes, whether from a key, the mouse, or a programmatic call
        /// (<see cref="Select"/>, <see cref="SetItems"/>, the navigation methods). The arguments carry the
        /// old and new <see cref="SelectedIndex"/> (-1 when empty). Raised on the thread that made the change.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? SelectionChanged;

        /// <summary>
        /// Raised after any selection change; the untyped companion of <see cref="SelectionChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Raised with the selected index when the user activates an item with Enter or a double click.
        /// While no handler is attached Enter is not consumed (it falls through to the host, as before).
        /// </summary>
        public event Action<int>? ItemActivated;

        /// <summary>
        /// Gets or sets a value indicating whether the list accepts input. A disabled list renders with
        /// <see cref="DisabledStyle"/> and ignores keys and the mouse. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets the style composed over <see cref="NormalStyle"/> while the list is disabled.
        /// Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Applies a theme: <see cref="NormalStyle"/> from <see cref="Theme.Text"/>, the highlight from
        /// <see cref="Theme.Accent"/>, and <see cref="DisabledStyle"/> from <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            HighlightColor = theme.Accent.Foreground;
            DisabledStyle = theme.Disabled;
        }

        /// <summary>
        /// Selects the item at an index, clamped to the valid range, and scrolls it into view on the next
        /// render. A no-op when the list is empty.
        /// </summary>
        /// <param name="index">The zero-based index.</param>
        public void Select(int index)
        {
            if (_Items.Count == 0)
                return;

            SetSelected(Math.Max(0, Math.Min(_Items.Count - 1, index)));
        }

        /// <summary>
        /// Updates the focused state so the selection highlight reflects focus on the next frame. Part of
        /// <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the list has gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        /// <summary>
        /// Gets the items. Never null.
        /// </summary>
        public IReadOnlyList<T> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Gets the zero-based index of the selected item, or -1 when the list is empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Items.Count == 0 ? -1 : _Selected; }
        }

        /// <summary>
        /// Gets the selected item, or the type default when the list is empty.
        /// </summary>
        public T? SelectedItem
        {
            get { return _Items.Count == 0 ? default : _Items[_Selected]; }
        }

        /// <summary>
        /// Gets or sets the highlight color for the selected item. Defaults to palette cyan.
        /// </summary>
        public Color HighlightColor { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Gets or sets the base style applied to unselected items and the surface fill. The selected
        /// and hover styles are composed over this style, so a background set here shows through
        /// consistently. Defaults to <see cref="CellStyle.Default"/>; assign a style with a background
        /// to give the list a solid background.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of the non-selected row under the pointer while hover tracking is
        /// on. The selected row keeps its selection style when hovered. Defaults to underlined
        /// default text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Underline);

        /// <summary>
        /// Gets or sets the follow-the-bottom behavior for a list that grows with <see cref="Append"/> (a
        /// chat, a log, a feed), or null for none. While following, appended items keep the last row in
        /// view; moving the selection only stops following when it scrolls the list up (selecting a
        /// visible row never does), End resumes, and while not following a "N new below" indicator on
        /// the last row counts arrivals and returns to the bottom when clicked. Defaults to null, which
        /// keeps the earlier behavior (the view simply keeps the selection visible).
        /// </summary>
        public TailFollow? TailFollow { get; set; }

        /// <summary>
        /// Appends one item, keeping the current selection. With <see cref="TailFollow"/> set and
        /// following, the list shows its new last row.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Append(T item)
        {
            int before = SelectedIndex;
            _Items.Add(item);
            TailFollow?.OnContentAppended(1);
            RaiseIfChanged(before);
        }

        /// <summary>
        /// Appends several items, keeping the current selection. With <see cref="TailFollow"/> set and
        /// following, the list shows its new last row.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public void AppendRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            int before = SelectedIndex;
            int count = _Items.Count;
            _Items.AddRange(items);
            TailFollow?.OnContentAppended(_Items.Count - count);
            RaiseIfChanged(before);
        }

        /// <summary>
        /// Replaces the list items and resets the selection to the first item. Raises
        /// <see cref="SelectionChanged"/> when the selected index or the selected item changed.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public void SetItems(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            int before = SelectedIndex;
            T? beforeItem = SelectedItem;
            _Items.Clear();
            _Items.AddRange(items);
            _Selected = 0;
            _Top = 0;
            TailFollow?.Reset();
            if (before == SelectedIndex && before >= 0 && !EqualityComparer<T>.Default.Equals(beforeItem!, SelectedItem!))
            {
                // Same index, different item: the selection still changed for anyone showing its details.
                SelectionChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, before));
                Changed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                RaiseIfChanged(before);
            }
        }

        /// <summary>
        /// Moves the selection down by one item.
        /// </summary>
        public void SelectNext()
        {
            if (_Items.Count == 0)
                return;

            SetSelected(Math.Min(_Items.Count - 1, _Selected + 1));
        }

        /// <summary>
        /// Moves the selection up by one item.
        /// </summary>
        public void SelectPrevious()
        {
            if (_Items.Count == 0)
                return;

            SetSelected(Math.Max(0, _Selected - 1));
        }

        /// <summary>
        /// Moves the selection to the first item. A no-op when the list is empty.
        /// </summary>
        public void SelectFirst()
        {
            if (_Items.Count == 0)
                return;

            SetSelected(0);
        }

        /// <summary>
        /// Moves the selection to the last item. A no-op when the list is empty.
        /// </summary>
        public void SelectLast()
        {
            if (_Items.Count == 0)
                return;

            SetSelected(_Items.Count - 1);
        }

        /// <summary>
        /// Moves the selection down by one page — the height of the list's viewport as of the most
        /// recent render (at least one row) — clamped to the last item.
        /// </summary>
        public void PageDown()
        {
            if (_Items.Count == 0)
                return;

            SetSelected(Math.Min(_Items.Count - 1, _Selected + Math.Max(1, _LastViewportHeight)));
        }

        /// <summary>
        /// Moves the selection up by one page — the height of the list's viewport as of the most recent
        /// render (at least one row) — clamped to the first item.
        /// </summary>
        public void PageUp()
        {
            if (_Items.Count == 0)
                return;

            SetSelected(Math.Max(0, _Selected - Math.Max(1, _LastViewportHeight)));
        }

        /// <summary>
        /// Gets the list's keys for the status bar: moving the selection, jumping to either end, and
        /// <c>Enter</c> when <see cref="ItemActivated"/> has a handler. Empty while disabled or empty.
        /// Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints. Never null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            if (!_Enabled || _Items.Count == 0)
                return hints;

            if (ItemActivated != null)
                hints.Add(new KeyHint(new KeyChord(KeyCode.Enter, 0, KeyModifiers.None), "Open", 10));
            hints.Add(new KeyHint("Up/Down", "Move"));
            hints.Add(new KeyHint("Home/End", "First/last"));
            return hints;
        }

        /// <summary>
        /// Handles Up/Down (single step), PageUp/PageDown (by the viewport height), and Home/End (first
        /// and last item) navigation keys.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled)
                return false;

            switch (key.Code)
            {
                case KeyCode.Enter:
                    Action<int>? activated = ItemActivated;
                    if (activated == null || _Items.Count == 0 || key.Modifiers != KeyModifiers.None)
                        return false;

                    activated(_Selected);
                    return true;
                case KeyCode.Up:
                    SelectPrevious();
                    return true;
                case KeyCode.Down:
                    SelectNext();
                    return true;
                case KeyCode.PageUp:
                    PageUp();
                    return true;
                case KeyCode.PageDown:
                    PageDown();
                    return true;
                case KeyCode.Home:
                    SelectFirst();
                    return true;
                case KeyCode.End:
                    SelectLast();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Selects the row under a left press, steps the selection with the wheel, and tracks the
        /// hovered row for <see cref="HoverStyle"/> rendering. Enter/Move/Leave events are observed
        /// but never consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press or wheel changed the selection; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!_Enabled)
                return false;

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button == MouseButton.Left && TailFollow != null && _IndicatorRect.Width > 0 && _IndicatorRect.Contains(new Point(mouse.X, mouse.Y)))
                    {
                        TailFollow.ReturnToTail();
                        _Top = Math.Max(0, _Items.Count - _LastViewportHeight);
                        return true;
                    }

                    if (mouse.Button == MouseButton.Left)
                    {
                        int pressed = RowIndexAt(mouse.Y);
                        if (pressed >= 0)
                        {
                            SetSelected(pressed);
                            if (mouse.ClickCount >= 2)
                                ItemActivated?.Invoke(pressed);
                            return true;
                        }
                    }

                    return false;
                case MouseEventKind.Wheel:
                    if (mouse.Button == MouseButton.WheelUp)
                    {
                        SelectPrevious();
                        return true;
                    }

                    if (mouse.Button == MouseButton.WheelDown)
                    {
                        SelectNext();
                        return true;
                    }

                    return false;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _HoverIndex = RowIndexAt(mouse.Y);
                    return false;
                case MouseEventKind.Leave:
                    _HoverIndex = -1;
                    return false;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Items.Count));
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

            _LastViewportHeight = height;

            TailFollow? follow = TailFollow;
            if (follow == null)
            {
                RevealSelection(height);
            }
            else
            {
                // Only a viewport move counts: keeping a changed selection visible may scroll, which is
                // reported; selecting a row that is already visible leaves the viewport (and following) alone.
                int maxTop = Math.Max(0, _Items.Count - height);
                if (_RevealSelection)
                {
                    if (follow.IsFollowing)
                        _Top = maxTop;
                    int before = _Top;
                    RevealSelection(height);
                    if (_Top != before || !follow.IsFollowing)
                        follow.OnViewportMoved(Math.Min(_Top, maxTop), maxTop);
                }
                else if (follow.IsFollowing)
                {
                    _Top = maxTop;
                }

                _Top = Math.Max(0, Math.Min(_Top, maxTop));
            }

            _RevealSelection = false;

            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(NormalStyle));

            for (int row = 0; row < height && _Top + row < _Items.Count; row++)
            {
                int index = _Top + row;
                bool selected = index == _Selected;
                CellStyle style;
                if (!_Enabled)
                {
                    style = selected
                        ? DisabledStyle.Over(NormalStyle).WithAttribute(CellAttributes.Bold, true)
                        : DisabledStyle.Over(NormalStyle);
                }
                else if (selected && IsFocused)
                {
                    style = NormalStyle.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(HighlightColor);
                    surface.Fill(new Rect(0, row, width, 1), Cell.Blank(style));
                }
                else if (selected)
                {
                    style = NormalStyle.WithForeground(HighlightColor).WithAttribute(CellAttributes.Bold, true);
                }
                else if (index == _HoverIndex)
                {
                    style = HoverStyle.Over(NormalStyle);
                }
                else
                {
                    style = NormalStyle;
                }

                surface.DrawText(0, row, _Display(_Items[index]), style);
            }

            _IndicatorRect = default;
            string? indicator = follow?.IndicatorText;
            if (indicator != null)
            {
                int indicatorWidth = Math.Min(width, TUIKit.Unicode.TextFit.Width(indicator));
                _IndicatorRect = new Rect(width - indicatorWidth, height - 1, indicatorWidth, 1);
                surface.DrawText(width - indicatorWidth, height - 1, TUIKit.Unicode.TextFit.Ellipsize(indicator, indicatorWidth), NormalStyle.WithAttribute(CellAttributes.Reverse, true).WithAttribute(CellAttributes.Bold, true));
            }
        }

        private void RevealSelection(int height)
        {
            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + height)
                _Top = _Selected - height + 1;
        }

        private void SetSelected(int index)
        {
            int before = SelectedIndex;
            _Selected = index;
            _RevealSelection = true;
            RaiseIfChanged(before);
        }

        private void RaiseIfChanged(int before)
        {
            int after = SelectedIndex;
            if (before == after)
                return;

            SelectionChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, after));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private int RowIndexAt(int y)
        {
            if (y < 0)
                return -1;

            int index = _Top + y;
            return index < _Items.Count ? index : -1;
        }
    }
}
