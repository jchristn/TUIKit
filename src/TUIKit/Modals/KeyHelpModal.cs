namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A key-help overlay: key bindings grouped by category in two aligned columns (keys and what they
    /// do). Up/Down/PageUp/PageDown/Home/End or the wheel scroll; typing filters by keys, description, or
    /// category (Backspace edits the filter); Escape, Enter, F1, or <c>?</c> (with an empty filter) close.
    /// Build it from a <see cref="CommandRegistry"/> (<see cref="FromCommands"/>) and/or add rows with
    /// <see cref="Add"/>. Completes with null.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class KeyHelpModal : DialogModal, IThemeable
    {
        private readonly List<KeyHelpEntry> _Entries = new List<KeyHelpEntry>();
        private readonly List<string> _Lines = new List<string>();
        private readonly List<bool> _Headings = new List<bool>();
        private string _Filter = string.Empty;
        private int _Top;
        private int _LastRows = 1;
        private int _VisibleRows = 18;

        /// <summary>
        /// Gets or sets the heading style. Defaults to bold cyan (palette 6).
        /// </summary>
        public CellStyle HeadingStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the key column style. Defaults to yellow (palette 3).
        /// </summary>
        public CellStyle KeyStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(3));

        /// <summary>
        /// Gets or sets the number of visible rows. Defaults to 18. Must be at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int VisibleRows
        {
            get { return _VisibleRows; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Must be at least 1.");
                _VisibleRows = value;
            }
        }

        /// <summary>
        /// Gets the entries. Never null.
        /// </summary>
        public IReadOnlyList<KeyHelpEntry> Entries
        {
            get { return _Entries; }
        }

        /// <summary>
        /// Gets the filter text.
        /// </summary>
        public string Filter
        {
            get { return _Filter; }
        }

        /// <summary>
        /// Gets the number of entries matching the filter.
        /// </summary>
        public int MatchCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _Entries.Count; i++)
                {
                    if (Matches(_Entries[i]))
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Gets the first visible line index (scroll position).
        /// </summary>
        public int ScrollTop
        {
            get { return _Top; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyHelpModal"/> class.
        /// </summary>
        /// <param name="title">The title. Defaults to "Keyboard Shortcuts".</param>
        public KeyHelpModal(string title = "Keyboard Shortcuts")
        {
            Title = title;
            FooterHint = " type to filter  Esc close ";
            MinContentWidth = 30;
            MaxContentWidth = 90;
        }

        /// <summary>
        /// Builds a help overlay from commands that have a key chord, grouped by command category.
        /// </summary>
        /// <param name="commands">The commands. Must not be null.</param>
        /// <param name="style">The chord label style. Defaults to ASCII.</param>
        /// <returns>The modal.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="commands"/> is null.</exception>
        public static KeyHelpModal FromCommands(IEnumerable<Command> commands, KeyLabelStyle style = KeyLabelStyle.Ascii)
        {
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            KeyHelpModal modal = new KeyHelpModal();
            foreach (Command command in commands)
            {
                if (command != null && command.Chord.HasValue)
                    modal.Add(command.Category, command.Chord.Value.ToLabel(style), command.Title);
            }

            return modal;
        }

        /// <summary>
        /// Adds a row.
        /// </summary>
        /// <param name="category">The category. Must not be null.</param>
        /// <param name="keys">The key label. Must not be null.</param>
        /// <param name="description">The description. Must not be null.</param>
        /// <returns>This modal, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public KeyHelpModal Add(string category, string keys, string description)
        {
            _Entries.Add(new KeyHelpEntry(category, keys, description));
            return this;
        }

        /// <summary>
        /// Sets the filter text.
        /// </summary>
        /// <param name="filter">The filter. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
        public void SetFilter(string filter)
        {
            _Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _Top = 0;
        }

        /// <summary>
        /// Applies a theme.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            BorderStyleColor = theme.Accent;
            BackgroundStyle = theme.Text;
            ApplyBorderTheme(theme);
            HeadingStyle = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            KeyStyle = theme.Warning;
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                case KeyCode.Enter:
                case KeyCode.F1:
                    RequestClose(null);
                    return true;
                case KeyCode.Up:
                    _Top = Math.Max(0, _Top - 1);
                    return true;
                case KeyCode.Down:
                    _Top++;
                    return true;
                case KeyCode.PageUp:
                    _Top = Math.Max(0, _Top - _LastRows);
                    return true;
                case KeyCode.PageDown:
                    _Top += _LastRows;
                    return true;
                case KeyCode.Home:
                    _Top = 0;
                    return true;
                case KeyCode.End:
                    _Top = int.MaxValue / 2;
                    return true;
                case KeyCode.Backspace:
                    if (_Filter.Length > 0)
                        SetFilter(_Filter.Substring(0, TextFit.PreviousBoundary(_Filter, _Filter.Length)));
                    return true;
                case KeyCode.Character:
                    if ((key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt)) != 0)
                        return true;
                    if (key.Rune == '?' && _Filter.Length == 0)
                    {
                        RequestClose(null);
                        return true;
                    }

                    SetFilter(_Filter + char.ConvertFromUtf32(key.Rune));
                    return true;
                default:
                    return true;
            }
        }

        /// <inheritdoc/>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Kind == MouseEventKind.Wheel)
                _Top = mouse.Button == MouseButton.WheelUp ? Math.Max(0, _Top - 3) : _Top + 3;

            return true;
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            int keys = KeyColumnWidth();
            int description = 10;
            for (int i = 0; i < _Entries.Count; i++)
                description = Math.Max(description, TextFit.Width(_Entries[i].Description));

            return Math.Min(availableWidth, keys + 2 + description);
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return 1 + _VisibleRows;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            int height = content.Size.Height;
            string filterLine = _Filter.Length > 0 ? "Filter: " + _Filter : "Type to filter";
            content.DrawText(0, 0, TextFit.Ellipsize(filterLine, width), BackgroundStyle.WithAttribute(CellAttributes.Dim, _Filter.Length == 0));

            BuildLines(width);
            int rows = Math.Max(1, height - 1);
            _LastRows = rows;
            _Top = Math.Max(0, Math.Min(_Top, Math.Max(0, _Lines.Count - rows)));
            if (_Lines.Count == 0)
            {
                content.DrawText(0, 1, TextFit.Ellipsize("No matching shortcuts.", width), BackgroundStyle.WithAttribute(CellAttributes.Dim, true));
                return;
            }

            int keyWidth = KeyColumnWidth();
            for (int r = 0; r < rows && _Top + r < _Lines.Count; r++)
            {
                int index = _Top + r;
                if (_Headings[index])
                {
                    content.DrawText(0, r + 1, TextFit.Ellipsize(_Lines[index], width), HeadingStyle);
                    continue;
                }

                string line = _Lines[index];
                int split = line.IndexOf('\t');
                string keys = split >= 0 ? line.Substring(0, split) : line;
                string description = split >= 0 ? line.Substring(split + 1) : string.Empty;
                content.DrawText(1, r + 1, TextFit.PadRight(keys, Math.Min(keyWidth, Math.Max(0, width - 1))), KeyStyle);
                int x = 1 + keyWidth + 1;
                if (x < width)
                    content.DrawText(x, r + 1, TextFit.Ellipsize(description, width - x), BackgroundStyle);
            }
        }

        private void BuildLines(int width)
        {
            _Lines.Clear();
            _Headings.Clear();
            List<string> categories = new List<string>();
            for (int i = 0; i < _Entries.Count; i++)
            {
                if (Matches(_Entries[i]) && !categories.Contains(_Entries[i].Category))
                    categories.Add(_Entries[i].Category);
            }

            for (int c = 0; c < categories.Count; c++)
            {
                if (c > 0)
                {
                    _Lines.Add(string.Empty);
                    _Headings.Add(true);
                }

                _Lines.Add(categories[c]);
                _Headings.Add(true);
                for (int i = 0; i < _Entries.Count; i++)
                {
                    if (_Entries[i].Category == categories[c] && Matches(_Entries[i]))
                    {
                        _Lines.Add(_Entries[i].Keys + "\t" + _Entries[i].Description);
                        _Headings.Add(false);
                    }
                }
            }
        }

        private int KeyColumnWidth()
        {
            int width = 4;
            for (int i = 0; i < _Entries.Count; i++)
                width = Math.Max(width, TextFit.Width(_Entries[i].Keys));

            return Math.Min(width, 24);
        }

        private bool Matches(KeyHelpEntry entry)
        {
            if (_Filter.Length == 0)
                return true;

            return entry.Keys.IndexOf(_Filter, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Description.IndexOf(_Filter, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Category.IndexOf(_Filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
