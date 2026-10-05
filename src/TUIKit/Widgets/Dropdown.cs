namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A drop-down picker (select box). Closed, it shows the selected item with a marker on the right and
    /// Up/Down step the selection directly. Enter, Space, Alt+Down, F4, or a click opens an inline list
    /// below the value (the widget's measured height grows, so containers such as <see cref="Form"/> make
    /// room); in the list Up/Down/PageUp/PageDown/Home/End move the highlight, typing a character jumps to
    /// the next item starting with it, Enter or a click picks, and Escape closes without changing the
    /// selection. Losing focus closes the list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class Dropdown<T> : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable
    {
        private readonly List<T> _Items = new List<T>();
        private readonly Func<T, string> _Display;
        private int _Selected = -1;
        private int _Highlight;
        private int _Top;
        private bool _Open;
        private bool _Enabled = true;
        private int _MaxVisibleItems = 8;

        /// <summary>
        /// Raised after the selected index changes, from input or a programmatic set.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<int>>? SelectionChanged;

        /// <summary>
        /// Raised after any selection change; the untyped companion of <see cref="SelectionChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Dropdown{T}"/> class.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <param name="display">A selector mapping an item to its label; may be null only when T is string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null, or <paramref name="display"/> is null and T is not string.</exception>
        public Dropdown(IEnumerable<T> items, Func<T, string>? display = null)
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

            _Items.AddRange(items);
        }

        /// <summary>
        /// Gets the items. Never null.
        /// </summary>
        public IReadOnlyList<T> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Gets or sets the selected index, or -1 for no selection. Setting clamps to [-1, Count) and
        /// raises <see cref="SelectionChanged"/> when it changes.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Selected; }
            set { SetSelected(Math.Max(-1, Math.Min(_Items.Count - 1, value))); }
        }

        /// <summary>
        /// Gets the selected item, or the type default when nothing is selected.
        /// </summary>
        public T? SelectedItem
        {
            get { return _Selected >= 0 ? _Items[_Selected] : default; }
        }

        /// <summary>
        /// Gets a value indicating whether the list is open.
        /// </summary>
        public bool IsOpen
        {
            get { return _Open; }
        }

        /// <summary>
        /// Gets or sets text shown when nothing is selected. Defaults to empty.
        /// </summary>
        public string Placeholder { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum number of list rows shown while open. Defaults to 8. Must be at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int MaxVisibleItems
        {
            get { return _MaxVisibleItems; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Must be at least 1.");
                _MaxVisibleItems = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the picker accepts input. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Enabled; }
            set
            {
                _Enabled = value;
                if (!value)
                    _Open = false;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the picker is focused.
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets the style of the closed value. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of the value while focused. Defaults to underlined.
        /// </summary>
        public CellStyle FocusedStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Underline, true);

        /// <summary>
        /// Gets or sets the style of the highlighted list row. Defaults to black on cyan (palette 6).
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of the open list. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle ListStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style composed over the value while disabled. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the marker drawn at the right of the closed value. Defaults to a small down
        /// triangle (U+25BE).
        /// </summary>
        public string Marker { get; set; } = "\u25BE";

        /// <summary>
        /// Replaces the items. The selection is kept when the same item (by equality) is still present,
        /// otherwise cleared.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public void SetItems(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            T? previous = SelectedItem;
            bool had = _Selected >= 0;
            _Items.Clear();
            _Items.AddRange(items);
            int index = had ? _Items.FindIndex(i => EqualityComparer<T>.Default.Equals(i, previous!)) : -1;
            int before = _Selected;
            _Selected = index;
            if (before != index)
            {
                SelectionChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, index));
                Changed?.Invoke(this, EventArgs.Empty);
            }

            _Highlight = Math.Max(0, index);
            _Top = 0;
        }

        /// <summary>
        /// Opens the list. A no-op while disabled or empty.
        /// </summary>
        public void Open()
        {
            if (!_Enabled || _Items.Count == 0)
                return;

            _Open = true;
            _Highlight = Math.Max(0, _Selected);
        }

        /// <summary>
        /// Closes the list without changing the selection.
        /// </summary>
        public void Close()
        {
            _Open = false;
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
            if (!focused)
                _Open = false;
        }

        /// <summary>
        /// Applies a theme: value from <see cref="Theme.Text"/>, focus from <see cref="Theme.Accent"/>,
        /// highlight from <see cref="Theme.Selection"/>, list from <see cref="Theme.Text"/>, and disabled
        /// from <see cref="Theme.Disabled"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            NormalStyle = theme.Text;
            FocusedStyle = theme.Accent.WithAttribute(CellAttributes.Underline, true);
            HighlightStyle = theme.Selection;
            ListStyle = theme.Text;
            DisabledStyle = theme.Disabled;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Enabled || _Items.Count == 0)
                return false;

            if (!_Open)
            {
                bool opener = key.Code == KeyCode.Enter
                    || (key.Code == KeyCode.Character && key.Rune == ' ' && key.Modifiers == KeyModifiers.None)
                    || key.Code == KeyCode.F4
                    || (key.Code == KeyCode.Down && key.Modifiers == KeyModifiers.Alt);
                if (opener)
                {
                    Open();
                    return true;
                }

                if (key.Modifiers != KeyModifiers.None)
                    return false;

                switch (key.Code)
                {
                    case KeyCode.Up:
                        SetSelected(Math.Max(0, _Selected - 1));
                        return true;
                    case KeyCode.Down:
                        SetSelected(Math.Min(_Items.Count - 1, _Selected + 1));
                        return true;
                    case KeyCode.Home:
                        SetSelected(0);
                        return true;
                    case KeyCode.End:
                        SetSelected(_Items.Count - 1);
                        return true;
                    default:
                        return false;
                }
            }

            switch (key.Code)
            {
                case KeyCode.Escape:
                    _Open = false;
                    return true;
                case KeyCode.Enter:
                    SetSelected(_Highlight);
                    _Open = false;
                    return true;
                case KeyCode.Up:
                    _Highlight = Math.Max(0, _Highlight - 1);
                    return true;
                case KeyCode.Down:
                    _Highlight = Math.Min(_Items.Count - 1, _Highlight + 1);
                    return true;
                case KeyCode.PageUp:
                    _Highlight = Math.Max(0, _Highlight - _MaxVisibleItems);
                    return true;
                case KeyCode.PageDown:
                    _Highlight = Math.Min(_Items.Count - 1, _Highlight + _MaxVisibleItems);
                    return true;
                case KeyCode.Home:
                    _Highlight = 0;
                    return true;
                case KeyCode.End:
                    _Highlight = _Items.Count - 1;
                    return true;
                case KeyCode.Tab:
                    _Open = false;
                    return false;
                case KeyCode.Character:
                    if (key.Modifiers == KeyModifiers.None || key.Modifiers == KeyModifiers.Shift)
                        JumpTo(char.ConvertFromUtf32(key.Rune));
                    return true;
                default:
                    return true;
            }
        }

        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (!_Enabled)
                return false;

            if (mouse.Kind == MouseEventKind.Wheel && _Open)
            {
                if (mouse.Button == MouseButton.WheelUp)
                    _Highlight = Math.Max(0, _Highlight - 1);
                else if (mouse.Button == MouseButton.WheelDown)
                    _Highlight = Math.Min(_Items.Count - 1, _Highlight + 1);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Move && _Open && mouse.Y >= 1)
            {
                int hovered = _Top + mouse.Y - 1;
                if (hovered < _Items.Count)
                    _Highlight = hovered;
                return false;
            }

            if (mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left)
                return false;

            if (mouse.Y == 0)
            {
                if (_Open)
                    _Open = false;
                else
                    Open();
                return true;
            }

            if (_Open)
            {
                int index = _Top + mouse.Y - 1;
                if (index >= 0 && index < _Items.Count)
                {
                    SetSelected(index);
                    _Open = false;
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int height = 1 + (_Open ? Math.Min(_Items.Count, _MaxVisibleItems) : 0);
            return new Size(available.Width, Math.Min(available.Height, height));
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

            CellStyle valueStyle = !_Enabled ? DisabledStyle.Over(NormalStyle) : (IsFocused ? FocusedStyle.Over(NormalStyle) : NormalStyle);
            surface.Fill(new Rect(0, 0, width, 1), Cell.Blank(NormalStyle));
            string value = _Selected >= 0 ? _Display(_Items[_Selected]) : Placeholder;
            int markerWidth = TextFit.Width(Marker);
            surface.DrawText(0, 0, TextFit.Ellipsize(value, Math.Max(0, width - markerWidth - 1)), valueStyle);
            if (width > markerWidth)
                surface.DrawText(width - markerWidth, 0, Marker, valueStyle);

            if (!_Open || height < 2)
                return;

            int rows = Math.Min(Math.Min(_Items.Count, _MaxVisibleItems), height - 1);
            if (_Highlight < _Top)
                _Top = _Highlight;
            else if (_Highlight >= _Top + rows)
                _Top = _Highlight - rows + 1;

            for (int r = 0; r < rows && _Top + r < _Items.Count; r++)
            {
                int index = _Top + r;
                CellStyle style = index == _Highlight ? HighlightStyle : ListStyle;
                surface.Fill(new Rect(0, r + 1, width, 1), Cell.Blank(style));
                string mark = index == _Selected ? "\u2022 " : "  ";
                surface.DrawText(0, r + 1, TextFit.Ellipsize(mark + _Display(_Items[index]), width), style);
            }
        }

        private void JumpTo(string prefix)
        {
            for (int offset = 1; offset <= _Items.Count; offset++)
            {
                int index = (_Highlight + offset) % _Items.Count;
                if (_Display(_Items[index]).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    _Highlight = index;
                    return;
                }
            }
        }

        private void SetSelected(int index)
        {
            if (index == _Selected)
                return;

            int before = _Selected;
            _Selected = index;
            SelectionChanged?.Invoke(this, new ValueChangedEventArgs<int>(before, index));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
