namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;
    using TUIKit.Widgets;

    /// <summary>
    /// A command palette: a query line over a ranked list of commands showing each command's title,
    /// category, and key chord. Typing filters (prefix matches rank above substring matches, which rank
    /// above subsequence matches, across title, category, id, and slash aliases); Up/Down/PageUp/PageDown
    /// move; Enter or a click runs the command; Escape cancels. Completes with the chosen
    /// <see cref="Command"/>, or null. Disabled commands are hidden unless <see cref="ShowDisabled"/> is set.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class CommandPaletteModal : DialogModal, IThemeable
    {
        private readonly List<Command> _Commands = new List<Command>();
        private readonly List<Command> _Filtered = new List<Command>();
        private readonly TextField _Query = new TextField { IsFocused = true };
        private int _Selected;
        private int _Top;
        private int _VisibleRows = 12;
        private bool _ShowDisabled;

        /// <summary>
        /// Gets or sets a value indicating whether the chosen command's handler runs when the palette
        /// closes. Defaults to true; set it to false to only receive the command as the result.
        /// </summary>
        public bool RunOnSelect { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether disabled commands are listed (dimmed, not runnable).
        /// Defaults to false.
        /// </summary>
        public bool ShowDisabled
        {
            get { return _ShowDisabled; }
            set
            {
                _ShowDisabled = value;
                Refilter();
            }
        }

        /// <summary>
        /// Gets or sets the number of list rows. Defaults to 12. Must be at least 1.
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
        /// Gets or sets the chord label style. Defaults to <see cref="KeyLabelStyle.Ascii"/>.
        /// </summary>
        public KeyLabelStyle ChordStyle { get; set; } = KeyLabelStyle.Ascii;

        /// <summary>
        /// Gets or sets the highlighted row style. Defaults to black on cyan (palette 6).
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of categories and chords. Defaults to grey (palette 8).
        /// </summary>
        public CellStyle MutedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets the query text.
        /// </summary>
        public string Query
        {
            get { return _Query.Value; }
        }

        /// <summary>
        /// Gets the commands matching the query, best first. Never null.
        /// </summary>
        public IReadOnlyList<Command> Filtered
        {
            get { return _Filtered; }
        }

        /// <summary>
        /// Gets the highlighted command, or null when nothing matches.
        /// </summary>
        public Command? Highlighted
        {
            get { return _Selected >= 0 && _Selected < _Filtered.Count ? _Filtered[_Selected] : null; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandPaletteModal"/> class.
        /// </summary>
        /// <param name="commands">The commands. Must not be null.</param>
        /// <param name="title">The title. Defaults to "Command Palette".</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="commands"/> is null.</exception>
        public CommandPaletteModal(IEnumerable<Command> commands, string title = "Command Palette")
        {
            if (commands == null)
                throw new ArgumentNullException(nameof(commands));

            foreach (Command command in commands)
            {
                if (command != null)
                    _Commands.Add(command);
            }

            Title = title;
            FooterHint = " Enter run  Esc close ";
            MinContentWidth = 30;
            MaxContentWidth = 80;
            Refilter();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandPaletteModal"/> class over a registry.
        /// </summary>
        /// <param name="registry">The registry. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
        public CommandPaletteModal(CommandRegistry registry)
            : this((registry ?? throw new ArgumentNullException(nameof(registry))).Commands)
        {
        }

        /// <summary>
        /// Sets the query text and refilters.
        /// </summary>
        /// <param name="query">The query. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is null.</exception>
        public void SetQuery(string query)
        {
            _Query.Value = query ?? throw new ArgumentNullException(nameof(query));
            Refilter();
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
            HighlightStyle = theme.Selection;
            MutedStyle = theme.Muted;
            _Query.ApplyTheme(theme);
        }

        /// <summary>
        /// Scores how well a command matches a query: 0 for no match, higher is better. Exposed for tests
        /// and for applications that build their own palette.
        /// </summary>
        /// <param name="command">The command. Must not be null.</param>
        /// <param name="query">The query. Must not be null.</param>
        /// <returns>The score.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static int Score(Command command, string query)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            string q = query.Trim();
            if (q.Length == 0)
                return 1;

            int best = Math.Max(ScoreText(command.Title, q) * 2, ScoreText(command.Category + " " + command.Title, q));
            best = Math.Max(best, ScoreText(command.Id, q));
            for (int i = 0; i < command.SlashAliases.Count; i++)
                best = Math.Max(best, ScoreText(command.SlashAliases[i], q));

            return best;
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                    RequestClose(null);
                    return true;
                case KeyCode.Enter:
                    Choose(_Selected);
                    return true;
                case KeyCode.Up:
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                case KeyCode.Down:
                    _Selected = Math.Min(Math.Max(0, _Filtered.Count - 1), _Selected + 1);
                    return true;
                case KeyCode.PageUp:
                    _Selected = Math.Max(0, _Selected - _VisibleRows);
                    return true;
                case KeyCode.PageDown:
                    _Selected = Math.Min(Math.Max(0, _Filtered.Count - 1), _Selected + _VisibleRows);
                    return true;
                default:
                    string before = _Query.Value;
                    _Query.HandleKey(key);
                    if (!string.Equals(before, _Query.Value, StringComparison.Ordinal))
                        Refilter();
                    return true;
            }
        }

        /// <inheritdoc/>
        public override bool HandlePaste(string text)
        {
            _Query.Insert(text);
            Refilter();
            return true;
        }

        /// <inheritdoc/>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            Rect content = ContentBounds;
            if (mouse.Kind == MouseEventKind.Wheel)
            {
                _Selected = mouse.Button == MouseButton.WheelUp ? Math.Max(0, _Selected - 1) : Math.Min(Math.Max(0, _Filtered.Count - 1), _Selected + 1);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && content.Contains(new Point(mouse.X, mouse.Y)))
            {
                int row = mouse.Y - content.Y - 2;
                if (row >= 0 && _Top + row < _Filtered.Count)
                    Choose(_Top + row);
            }

            return true;
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            int width = 30;
            for (int i = 0; i < _Commands.Count; i++)
                width = Math.Max(width, TextFit.Width(RowLabel(_Commands[i])) + 2 + TextFit.Width(ChordLabel(_Commands[i])));

            return Math.Min(availableWidth, width);
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return 2 + _VisibleRows;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            int height = content.Size.Height;
            content.DrawText(0, 0, "> ", HighlightStyle.Over(BackgroundStyle).WithAttribute(CellAttributes.Reverse, false));
            _Query.Render(new SurfaceView(content, new Rect(2, 0, Math.Max(1, width - 2), 1)));
            int rows = Math.Max(0, height - 2);
            if (_Filtered.Count == 0)
            {
                content.DrawText(0, 2, TextFit.Ellipsize("No matching commands.", width), MutedStyle);
                return;
            }

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + rows)
                _Top = _Selected - rows + 1;

            for (int r = 0; r < rows && _Top + r < _Filtered.Count; r++)
            {
                Command command = _Filtered[_Top + r];
                bool selected = _Top + r == _Selected;
                CellStyle style = selected ? HighlightStyle : BackgroundStyle;
                if (!command.IsEnabled)
                    style = style.WithAttribute(CellAttributes.Dim, true);
                int y = r + 2;
                content.Fill(new Rect(0, y, width, 1), Cell.Blank(style));
                string chord = ChordLabel(command);
                int chordWidth = TextFit.Width(chord);
                content.DrawText(0, y, TextFit.Ellipsize(command.Title, Math.Max(1, width - chordWidth - 2)), style);
                int titleEnd = TextFit.Width(TextFit.Ellipsize(command.Title, Math.Max(1, width - chordWidth - 2)));
                string category = "  " + command.Category;
                if (titleEnd + TextFit.Width(category) < width - chordWidth - 1)
                    content.DrawText(titleEnd, y, category, selected ? style : MutedStyle.Over(BackgroundStyle));
                if (chordWidth > 0 && chordWidth < width)
                    content.DrawText(width - chordWidth, y, chord, selected ? style : MutedStyle.Over(BackgroundStyle));
            }
        }

        private string RowLabel(Command command)
        {
            return command.Title + "  " + command.Category;
        }

        private string ChordLabel(Command command)
        {
            return command.Chord.HasValue ? command.Chord.Value.ToLabel(ChordStyle) : string.Empty;
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= _Filtered.Count)
                return;

            Command command = _Filtered[index];
            if (!command.IsEnabled)
                return;

            Close(command);
            if (RunOnSelect)
                command.Handler();
        }

        private void Refilter()
        {
            _Filtered.Clear();
            List<int> scores = new List<int>();
            string query = _Query.Value;
            for (int i = 0; i < _Commands.Count; i++)
            {
                Command command = _Commands[i];
                if (!command.IsEnabled && !_ShowDisabled)
                    continue;

                int score = Score(command, query);
                if (score <= 0)
                    continue;

                int at = 0;
                while (at < scores.Count && scores[at] >= score)
                    at++;
                scores.Insert(at, score);
                _Filtered.Insert(at, command);
            }

            _Selected = 0;
            _Top = 0;
        }

        private static int ScoreText(string text, string query)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 300;

            int index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                bool wordStart = index > 0 && !char.IsLetterOrDigit(text[index - 1]);
                return wordStart ? 250 : 200;
            }

            int q = 0;
            for (int i = 0; i < text.Length && q < query.Length; i++)
            {
                if (char.ToLowerInvariant(text[i]) == char.ToLowerInvariant(query[q]))
                    q++;
            }

            return q == query.Length ? 100 : 0;
        }
    }
}
