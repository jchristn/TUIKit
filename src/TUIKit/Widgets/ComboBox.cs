namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// An editable text field with a filtered suggestion list (combo box). Typing filters the
    /// suggestions (case-insensitive substring match) and opens the list below the field; Down/Up move the
    /// highlight, Enter or a click accepts the highlighted suggestion, Escape closes the list. With
    /// <see cref="AllowFreeText"/> off, the value is restricted to the suggestions: an unmatched entry is
    /// reverted when focus leaves or Enter is pressed.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class ComboBox : IWidget, IFocusable, IFocusAware, IMouseAware, IEnableable, IChangeNotifier, IThemeable
    {
        private readonly TextField _Field = new TextField();
        private readonly List<string> _Items = new List<string>();
        private readonly List<string> _Filtered = new List<string>();
        private int _Highlight;
        private int _Top;
        private bool _Open;
        private int _MaxVisibleItems = 6;
        private string _Committed = string.Empty;

        /// <summary>
        /// Raised after <see cref="Value"/> changes, while typing or when a suggestion is accepted.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<string>>? ValueChanged;

        /// <summary>
        /// Raised after any value change; the untyped companion of <see cref="ValueChanged"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComboBox"/> class.
        /// </summary>
        /// <param name="items">The suggestions. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public ComboBox(IEnumerable<string> items)
        {
            SetItems(items);
            _Field.ValueChanged += (sender, e) =>
            {
                ValueChanged?.Invoke(this, e);
                Changed?.Invoke(this, EventArgs.Empty);
            };
        }

        /// <summary>
        /// Gets or sets the value. Setting raises <see cref="ValueChanged"/> when it differs. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Value
        {
            get { return _Field.Value; }
            set
            {
                _Field.Value = value ?? throw new ArgumentNullException(nameof(value));
                _Committed = value;
            }
        }

        /// <summary>
        /// Gets the suggestions. Never null.
        /// </summary>
        public IReadOnlyList<string> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Gets the suggestions matching the current value. Never null.
        /// </summary>
        public IReadOnlyList<string> Filtered
        {
            get { return _Filtered; }
        }

        /// <summary>
        /// Gets a value indicating whether the suggestion list is open.
        /// </summary>
        public bool IsOpen
        {
            get { return _Open; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether values outside the suggestions are allowed. Defaults to true.
        /// </summary>
        public bool AllowFreeText { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of visible suggestions. Defaults to 6. Must be at least 1.
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
        /// Gets or sets a value indicating whether the box accepts input. Defaults to true.
        /// </summary>
        public bool IsEnabled
        {
            get { return _Field.IsEnabled; }
            set
            {
                _Field.IsEnabled = value;
                if (!value)
                    _Open = false;
            }
        }

        /// <summary>
        /// Gets or sets hint text shown while the value is empty.
        /// </summary>
        public string? Placeholder
        {
            get { return _Field.Placeholder; }
            set { _Field.Placeholder = value; }
        }

        /// <summary>
        /// Gets or sets the style of the highlighted suggestion. Defaults to black on cyan (palette 6).
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of the suggestion list. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle ListStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets the underlying text field, for styling.
        /// </summary>
        public TextField Field
        {
            get { return _Field; }
        }

        /// <summary>
        /// Replaces the suggestions.
        /// </summary>
        /// <param name="items">The suggestions. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public void SetItems(IEnumerable<string> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _Items.Clear();
            foreach (string item in items)
            {
                if (item != null)
                    _Items.Add(item);
            }

            Refilter();
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _Field.OnFocusChanged(focused);
            if (!focused)
            {
                _Open = false;
                EnforceRestriction();
            }
        }

        /// <summary>
        /// Applies a theme to the field and list.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            _Field.ApplyTheme(theme);
            HighlightStyle = theme.Selection;
            ListStyle = theme.Text;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (!_Field.IsEnabled)
                return false;

            switch (key.Code)
            {
                case KeyCode.Down:
                    if (!_Open)
                    {
                        Refilter();
                        _Open = _Filtered.Count > 0;
                    }
                    else
                    {
                        _Highlight = Math.Min(_Filtered.Count - 1, _Highlight + 1);
                    }

                    return true;
                case KeyCode.Up:
                    if (_Open)
                    {
                        _Highlight = Math.Max(0, _Highlight - 1);
                        return true;
                    }

                    return false;
                case KeyCode.Escape:
                    if (_Open)
                    {
                        _Open = false;
                        return true;
                    }

                    return false;
                case KeyCode.Enter:
                    if (_Open && _Highlight < _Filtered.Count)
                    {
                        Accept(_Filtered[_Highlight]);
                        return true;
                    }

                    EnforceRestriction();
                    _Committed = _Field.Value;
                    return false;
                case KeyCode.Tab:
                    _Open = false;
                    return false;
                default:
                    string before = _Field.Value;
                    bool handled = _Field.HandleKey(key);
                    if (!string.Equals(before, _Field.Value, StringComparison.Ordinal))
                    {
                        Refilter();
                        _Open = _Filtered.Count > 0 && _Field.Value.Length > 0;
                    }

                    return handled;
            }
        }

        /// <summary>
        /// Inserts pasted text into the field.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        public void Insert(string? text)
        {
            _Field.Insert(text);
            Refilter();
        }

        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Y == 0)
                return _Field.HandleMouse(mouse);

            if (_Open && mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                int index = _Top + mouse.Y - 1;
                if (index >= 0 && index < _Filtered.Count)
                {
                    Accept(_Filtered[index]);
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int height = 1 + (_Open ? Math.Min(_Filtered.Count, _MaxVisibleItems) : 0);
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

            _Field.Render(new SurfaceView(surface, new Rect(0, 0, width, 1)));
            if (!_Open || height < 2)
                return;

            int rows = Math.Min(Math.Min(_Filtered.Count, _MaxVisibleItems), height - 1);
            if (_Highlight < _Top)
                _Top = _Highlight;
            else if (_Highlight >= _Top + rows)
                _Top = _Highlight - rows + 1;

            for (int r = 0; r < rows && _Top + r < _Filtered.Count; r++)
            {
                int index = _Top + r;
                CellStyle style = index == _Highlight ? HighlightStyle : ListStyle;
                surface.Fill(new Rect(0, r + 1, width, 1), Cell.Blank(style));
                surface.DrawText(1, r + 1, TextFit.Ellipsize(_Filtered[index], Math.Max(0, width - 1)), style);
            }
        }

        private void Accept(string value)
        {
            _Field.Value = value;
            _Committed = value;
            _Open = false;
            Refilter();
        }

        private void EnforceRestriction()
        {
            if (AllowFreeText || _Field.Value.Length == 0)
                return;

            for (int i = 0; i < _Items.Count; i++)
            {
                if (string.Equals(_Items[i], _Field.Value, StringComparison.OrdinalIgnoreCase))
                {
                    _Field.Value = _Items[i];
                    return;
                }
            }

            _Field.Value = _Committed;
        }

        private void Refilter()
        {
            _Filtered.Clear();
            string query = _Field.Value;
            for (int i = 0; i < _Items.Count; i++)
            {
                if (query.Length == 0 || _Items[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    _Filtered.Add(_Items[i]);
            }

            _Highlight = 0;
            _Top = 0;
        }
    }
}
