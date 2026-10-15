namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// Renders a unified line diff, with added lines in green, removed lines in red, and unchanged context lines
    /// optionally syntax-highlighted. The constructor computes the diff between two texts with a
    /// longest-common-subsequence match; <see cref="FromUnifiedDiff"/> shows a diff produced elsewhere (git, a code
    /// review API, a patch file, or a server), with file and hunk headers, metadata, and the files it covers.
    /// Up/Down scroll when the diff is taller than the region.
    /// </summary>
    /// <remarks>
    /// The <c>+</c>, <c>-</c>, and space markers are always drawn, so a change is never conveyed by color alone.
    /// Opt-in features: file navigation (<see cref="NextFile"/>, <see cref="UseFileKeys"/>), a line-number gutter
    /// (<see cref="ShowLineNumbers"/>), search (<see cref="SetSearch"/>, F3), viewport paging
    /// (<see cref="PageByViewport"/>), and theme styles (<see cref="UseThemeStyles"/>). Not thread-safe: use it from
    /// the UI thread.
    /// </remarks>
    public sealed class DiffView : IWidget, IFocusable, IFocusAware, IMouseAware, IScrollExtent, IKeyHintSource, IThemeable
    {
        #region Private-Members

        private readonly List<DiffLine> _Lines = new List<DiffLine>();
        private readonly List<DiffFile> _Files = new List<DiffFile>();
        private readonly string _PlainText;
        private int _Top;
        private int _ViewportHeight;
        private bool _IsFocused;
        private string? _Search;
        private int _MatchLine = -1;
        private CellStyle _SearchStyle = SearchHighlighter.DefaultStyle;
        private Theme? _Theme;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffView"/> class, computing the diff between
        /// two texts.
        /// </summary>
        /// <param name="oldText">The old/left text. Must not be null.</param>
        /// <param name="newText">The new/right text. Must not be null.</param>
        /// <param name="language">The syntax language for context lines, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when a text argument is null.</exception>
        public DiffView(string oldText, string newText, string? language = null)
        {
            if (oldText == null)
                throw new ArgumentNullException(nameof(oldText));
            if (newText == null)
                throw new ArgumentNullException(nameof(newText));

            SyntaxLanguage = language;
            Compute(Split(oldText), Split(newText));
            _PlainText = string.Join("\n", _Lines.Select(l => l.Drawn));
        }

        private DiffView(string unifiedDiff, string? language, bool unified)
        {
            SyntaxLanguage = language;
            UnifiedDiffParser.Parse(unifiedDiff, _Lines, _Files);
            _PlainText = unifiedDiff;
        }

        /// <summary>
        /// Creates a view of a unified diff produced elsewhere. File headers (<c>diff --git</c>, <c>---</c>,
        /// <c>+++</c>), hunk headers (<c>@@ -a,b +c,d @@</c>), and metadata (<c>index</c>, file modes, renames,
        /// similarity, binary notices, "no newline at end of file") get their own kinds and styles. Each hunk's extent
        /// comes from its header counts, so a body line starting with <c>---</c> or <c>+++</c> stays a body line.
        /// Paths come from <c>+++</c> (or <c>---</c> for a deleted file), with <c>a/</c> and <c>b/</c> stripped and
        /// git's quoted paths unquoted. Plain text, a truncated hunk, and combined diffs (<c>diff --cc</c>) render
        /// literally as context; this never throws on content. CRLF input is accepted.
        /// </summary>
        /// <param name="diff">The unified diff text. Must not be null.</param>
        /// <param name="language">The syntax language for context lines, or null.</param>
        /// <returns>The view. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="diff"/> is null.</exception>
        public static DiffView FromUnifiedDiff(string diff, string? language = null)
        {
            if (diff == null)
                throw new ArgumentNullException(nameof(diff));

            return new DiffView(diff, language, true);
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets or sets the language used to syntax-highlight context lines, or null for plain text.
        /// </summary>
        public string? SyntaxLanguage { get; set; }

        /// <summary>
        /// Gets or sets the style applied to added lines and their <c>+</c> gutter. Defaults to a green
        /// (palette 2) foreground.
        /// </summary>
        public CellStyle AddedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(2));

        /// <summary>
        /// Gets or sets the style applied to removed lines and their <c>-</c> gutter. Defaults to a red
        /// (palette 1) foreground.
        /// </summary>
        public CellStyle RemovedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(1));

        /// <summary>
        /// Gets or sets the style applied to unchanged context lines (and the base the syntax highlighter
        /// composes over). Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle ContextStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of file header lines. Defaults to bold.
        /// </summary>
        public CellStyle FileHeaderStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of hunk header lines. Defaults to palette color 6 (cyan).
        /// </summary>
        public CellStyle HunkHeaderStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of metadata lines. Defaults to dim.
        /// </summary>
        public CellStyle MetaStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style of the line-number gutter. Defaults to palette color 8 (grey).
        /// </summary>
        public CellStyle LineNumberStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets the number of rendered lines.
        /// </summary>
        public int LineCount
        {
            get { return _Lines.Count; }
        }

        /// <summary>
        /// Gets the number of added lines.
        /// </summary>
        public int AddedCount
        {
            get { return CountKind(DiffLineKind.Added); }
        }

        /// <summary>
        /// Gets the number of removed lines.
        /// </summary>
        public int RemovedCount
        {
            get { return CountKind(DiffLineKind.Removed); }
        }

        /// <summary>
        /// Gets the kinds of the rendered lines in order. Never null.
        /// </summary>
        public IReadOnlyList<DiffLineKind> LineKinds
        {
            get { return _Lines.Select(l => l.Kind).ToList(); }
        }

        /// <summary>
        /// Gets the files in a unified diff, in order: the <c>b/</c> path, or the <c>a/</c> path for a deleted file.
        /// Empty for a computed diff. Never null.
        /// </summary>
        public IReadOnlyList<string> Files
        {
            get { return _Files.Select(f => f.Path).ToList(); }
        }

        /// <summary>
        /// Gets the index into <see cref="Files"/> of the file whose header is at or above the top row, or -1 when there
        /// are no files or the top row is above the first header.
        /// </summary>
        public int CurrentFileIndex
        {
            get
            {
                int result = -1;
                for (int i = 0; i < _Files.Count; i++)
                {
                    if (_Files[i].LineIndex <= _Top)
                        result = i;
                }

                return result;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether <c>]</c> and <c>[</c> call <see cref="NextFile"/> and
        /// <see cref="PreviousFile"/>. Defaults to false: letter keys are opt-in.
        /// </summary>
        public bool UseFileKeys { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a gutter shows the old and new line numbers (parsed from hunk
        /// headers, or computed for the two-text constructor), blank for header and metadata lines. Defaults to false.
        /// </summary>
        public bool ShowLineNumbers { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether PageUp and PageDown move by the viewport height minus one row.
        /// Defaults to false: they move ten lines, as in 1.7.
        /// </summary>
        public bool PageByViewport { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the view follows the theme passed to <see cref="ApplyTheme"/>.
        /// Defaults to false: <see cref="ApplyTheme"/> only remembers the theme (and its search role). While true,
        /// lines draw with the theme's roles (added from <see cref="Theme.Success"/>, removed from
        /// <see cref="Theme.Error"/>, context from <see cref="Theme.Text"/>, file headers from <see cref="Theme.Accent"/>
        /// bold, hunk headers from <see cref="Theme.Info"/>, metadata from <see cref="Theme.Muted"/>) instead of the style
        /// properties, which keep their values.
        /// </summary>
        public bool UseThemeStyles { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="GetKeyHints"/> lists the view's keys. Defaults to true.
        /// </summary>
        public bool ShowKeyHints { get; set; } = true;

        /// <summary>
        /// Gets the text to copy or save: the unified input as given, or for a computed diff the marked lines joined
        /// with newlines. Never null.
        /// </summary>
        public string PlainText
        {
            get { return _PlainText; }
        }

        /// <summary>
        /// Gets the active search term set by <see cref="SetSearch"/>, or null.
        /// </summary>
        public string? SearchText
        {
            get { return _Search; }
        }

        /// <summary>
        /// Gets the number of rows the view needs to show every line. Part of <see cref="IScrollExtent"/>.
        /// </summary>
        public int ContentHeight
        {
            get { return _Lines.Count; }
        }

        /// <summary>
        /// Gets the viewport height as of the last render, or 0 before the first.
        /// </summary>
        public int ViewportHeight
        {
            get { return _ViewportHeight; }
        }

        /// <summary>
        /// Gets or sets the index of the top visible line, clamped to [0, LineCount - 1].
        /// </summary>
        public int ScrollOffset
        {
            get { return _Top; }
            set { _Top = Math.Max(0, Math.Min(Math.Max(0, _Lines.Count - 1), value)); }
        }

        /// <summary>
        /// Gets a value indicating whether the view has keyboard focus, as last reported through <see cref="OnFocusChanged"/>.
        /// </summary>
        public bool IsFocused
        {
            get { return _IsFocused; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Scrolls by a number of lines (negative scrolls up), clamped.
        /// </summary>
        /// <param name="lines">The number of lines.</param>
        public void ScrollBy(int lines)
        {
            ScrollOffset = _Top + lines;
        }

        /// <summary>
        /// Scrolls to the first line.
        /// </summary>
        public void ScrollToTop()
        {
            _Top = 0;
        }

        /// <summary>
        /// Scrolls so the last line is at the bottom of the viewport (or at the top before the first render).
        /// </summary>
        public void ScrollToEnd()
        {
            _Top = Math.Max(0, _Lines.Count - Math.Max(1, _ViewportHeight));
        }

        /// <summary>
        /// Scrolls the next file's header to the top.
        /// </summary>
        /// <returns><c>true</c> when it moved; <c>false</c> when there is no later file.</returns>
        public bool NextFile()
        {
            DiffFile? next = _Files.FirstOrDefault(f => f.LineIndex > _Top);
            if (next == null)
                return false;

            _Top = next.LineIndex;
            return true;
        }

        /// <summary>
        /// Scrolls the previous file's header to the top.
        /// </summary>
        /// <returns><c>true</c> when it moved; <c>false</c> when there is no earlier file.</returns>
        public bool PreviousFile()
        {
            DiffFile? previous = _Files.LastOrDefault(f => f.LineIndex < _Top);
            if (previous == null)
                return false;

            _Top = previous.LineIndex;
            return true;
        }

        /// <summary>
        /// Scrolls a file's header to the top.
        /// </summary>
        /// <param name="index">The index into <see cref="Files"/>.</param>
        /// <returns><c>true</c> when it moved; <c>false</c> when the header is already at the top.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public bool GoToFile(int index)
        {
            if (index < 0 || index >= _Files.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "The file index must be within [0, Files.Count).");

            if (_Top == _Files[index].LineIndex)
                return false;

            _Top = _Files[index].LineIndex;
            return true;
        }

        /// <summary>
        /// Sets the search term, or null or empty to clear it. Matching is case-insensitive over the drawn text,
        /// markers included. Matches are highlighted with <see cref="Theme.SearchMatchRole"/> once a theme is applied,
        /// and with the <see cref="Pane"/> default otherwise.
        /// </summary>
        /// <param name="query">The term, or null or empty to clear.</param>
        public void SetSearch(string? query)
        {
            _Search = string.IsNullOrEmpty(query) ? null : query;
            _MatchLine = -1;
        }

        /// <summary>
        /// Scrolls to the next line matching the search term, wrapping around. The search starts after the current
        /// match when it is visible, otherwise at the top visible line.
        /// </summary>
        /// <returns><c>true</c> when a match was found; otherwise <c>false</c>.</returns>
        public bool FindNext()
        {
            return Find(true);
        }

        /// <summary>
        /// Scrolls to the previous line matching the search term, wrapping around.
        /// </summary>
        /// <returns><c>true</c> when a match was found; otherwise <c>false</c>.</returns>
        public bool FindPrevious()
        {
            return Find(false);
        }

        /// <summary>
        /// Scrolls with Up/Down (one line), PageUp/PageDown (ten lines, or the viewport with
        /// <see cref="PageByViewport"/>), and Home/End; F3 and Shift+F3 move between matches while a search is set; and
        /// with <see cref="UseFileKeys"/> <c>]</c> and <c>[</c> move between files.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            int page = PageByViewport ? Math.Max(1, _ViewportHeight - 1) : 10;
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Top = Math.Max(0, _Top - 1);
                    return true;
                case KeyCode.Down:
                    _Top = Math.Min(Math.Max(0, _Lines.Count - 1), _Top + 1);
                    return true;
                case KeyCode.PageUp:
                    _Top = Math.Max(0, _Top - page);
                    return true;
                case KeyCode.PageDown:
                    _Top = Math.Min(Math.Max(0, _Lines.Count - 1), _Top + page);
                    return true;
                case KeyCode.Home:
                    _Top = 0;
                    return true;
                case KeyCode.End:
                    _Top = Math.Max(0, _Lines.Count - 1);
                    return true;
                case KeyCode.F3:
                    if (_Search == null)
                        return false;
                    Find((key.Modifiers & KeyModifiers.Shift) == 0);
                    return true;
                case KeyCode.Character:
                    if (!UseFileKeys || key.Modifiers != KeyModifiers.None)
                        return false;
                    if (key.Rune == ']')
                    {
                        NextFile();
                        return true;
                    }

                    if (key.Rune == '[')
                    {
                        PreviousFile();
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Scrolls the diff one line per wheel notch. Coordinates are widget-local. Other mouse events are not
        /// consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the wheel scrolled the view; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Kind == MouseEventKind.Wheel && mouse.Button == MouseButton.WheelUp)
            {
                _Top = Math.Max(0, _Top - 1);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel && mouse.Button == MouseButton.WheelDown)
            {
                _Top = Math.Min(Math.Max(0, _Lines.Count - 1), _Top + 1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Records the focused state for <see cref="GetKeyHints"/>. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the view gained focus.</param>
        public void OnFocusChanged(bool focused)
        {
            _IsFocused = focused;
        }

        /// <summary>
        /// Gets the view's keys for the status bar while focused and scrollable: Up/Down Scroll, "]/[ File" with
        /// <see cref="UseFileKeys"/> and more than one file, and F3 Next match while a search is set. Part of
        /// <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null when <see cref="ShowKeyHints"/> is false, the view is not focused, or nothing applies.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!ShowKeyHints || !_IsFocused)
                return null;

            List<KeyHint> hints = new List<KeyHint>();
            if (_Lines.Count > Math.Max(1, _ViewportHeight))
                hints.Add(new KeyHint("Up/Down", "Scroll"));
            if (UseFileKeys && _Files.Count > 1)
                hints.Add(new KeyHint("]/[", "File"));
            if (_Search != null)
                hints.Add(new KeyHint(new KeyChord(KeyCode.F3, 0, KeyModifiers.None), "Next match"));
            return hints.Count > 0 ? hints : null;
        }

        /// <summary>
        /// Remembers the theme and its <see cref="Theme.SearchMatchRole"/>; while <see cref="UseThemeStyles"/> is true
        /// the lines draw with its roles. Part of <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _SearchStyle = theme.Resolve(Theme.SearchMatchRole, SearchHighlighter.DefaultStyle);
        }

        /// <summary>
        /// Gets the rectangle of the current search match when it is visible. Part of <see cref="IScrollExtent"/>.
        /// </summary>
        /// <param name="rect">The match row, in view coordinates.</param>
        /// <returns><c>true</c> when a match is visible.</returns>
        public bool TryGetFocusRect(out Rect rect)
        {
            if (_MatchLine >= _Top && _MatchLine < _Top + Math.Max(1, _ViewportHeight))
            {
                rect = new Rect(0, _MatchLine - _Top, 1, 1);
                return true;
            }

            rect = default;
            return false;
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
            _ViewportHeight = Math.Max(0, height);
            if (width <= 0 || height <= 0)
                return;

            Theme? theme = UseThemeStyles ? _Theme : null;
            CellStyle added = theme != null ? theme.Success : AddedStyle;
            CellStyle removed = theme != null ? theme.Error : RemovedStyle;
            CellStyle context = theme != null ? theme.Text : ContextStyle;
            CellStyle file = theme != null ? theme.Accent.WithAttribute(CellAttributes.Bold, true) : FileHeaderStyle;
            CellStyle hunk = theme != null ? theme.Info : HunkHeaderStyle;
            CellStyle meta = theme != null ? theme.Muted : MetaStyle;
            CellStyle gutterStyle = theme != null ? theme.Muted : LineNumberStyle;

            int oldWidth = 0;
            int newWidth = 0;
            if (ShowLineNumbers && _Lines.Count > 0)
            {
                oldWidth = Math.Max(1, _Lines.Max(l => l.OldNumber).ToString(CultureInfo.InvariantCulture).Length);
                newWidth = Math.Max(1, _Lines.Max(l => l.NewNumber).ToString(CultureInfo.InvariantCulture).Length);
            }

            int gutter = ShowLineNumbers ? oldWidth + newWidth + 2 : 0;
            for (int row = 0; row < height && _Top + row < _Lines.Count; row++)
            {
                DiffLine line = _Lines[_Top + row];
                if (ShowLineNumbers)
                {
                    string oldText = line.OldNumber > 0 ? line.OldNumber.ToString(CultureInfo.InvariantCulture) : string.Empty;
                    string newText = line.NewNumber > 0 ? line.NewNumber.ToString(CultureInfo.InvariantCulture) : string.Empty;
                    surface.DrawText(0, row, oldText.PadLeft(oldWidth) + " " + newText.PadLeft(newWidth) + " ", gutterStyle);
                }

                StyledText content;
                CellStyle baseStyle = context;
                switch (line.Kind)
                {
                    case DiffLineKind.Added:
                        content = StyledText.From("+", added).Append(StyledText.From(line.Text, added));
                        break;
                    case DiffLineKind.Removed:
                        content = StyledText.From("-", removed).Append(StyledText.From(line.Text, removed));
                        break;
                    case DiffLineKind.FileHeader:
                        content = StyledText.From(line.Text, file);
                        break;
                    case DiffLineKind.HunkHeader:
                        content = StyledText.From(line.Text, hunk);
                        break;
                    case DiffLineKind.Meta:
                        content = StyledText.From(line.Text, meta);
                        break;
                    default:
                        content = StyledText.From(" ", context).Append(SyntaxLanguage != null
                            ? SyntaxHighlighter.HighlightLine(line.Text, SyntaxLanguage)
                            : StyledText.From(line.Text, context));
                        break;
                }

                if (_Search != null)
                    content = SearchHighlighter.Highlight(content, _Search, _SearchStyle);
                if (gutter < width)
                    surface.DrawStyledText(gutter, row, content, baseStyle);
            }
        }

        #endregion

        #region Private-Methods

        private bool Find(bool down)
        {
            if (_Search == null || _Lines.Count == 0)
                return false;

            int visible = Math.Max(1, _ViewportHeight);
            int start = _MatchLine >= _Top && _MatchLine < _Top + visible ? _MatchLine : (down ? _Top - 1 : _Top);
            int count = _Lines.Count;
            for (int step = 1; step <= count; step++)
            {
                int index = down ? (((start + step) % count) + count) % count : (((start - step) % count) + count) % count;
                if (_Lines[index].Drawn.IndexOf(_Search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                _MatchLine = index;
                if (index < _Top || index >= _Top + visible)
                    _Top = Math.Max(0, Math.Min(index, Math.Max(0, count - visible)));
                return true;
            }

            return false;
        }

        private int CountKind(DiffLineKind kind)
        {
            int count = 0;
            for (int i = 0; i < _Lines.Count; i++)
            {
                if (_Lines[i].Kind == kind)
                    count++;
            }

            return count;
        }

        private void Compute(string[] oldLines, string[] newLines)
        {
            int n = oldLines.Length;
            int m = newLines.Length;
            int[,] dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    if (oldLines[i] == newLines[j])
                        dp[i, j] = dp[i + 1, j + 1] + 1;
                    else
                        dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }

            int a = 0;
            int b = 0;
            while (a < n && b < m)
            {
                if (oldLines[a] == newLines[b])
                {
                    _Lines.Add(new DiffLine(DiffLineKind.Context, oldLines[a], a + 1, b + 1));
                    a++;
                    b++;
                }
                else if (dp[a + 1, b] >= dp[a, b + 1])
                {
                    _Lines.Add(new DiffLine(DiffLineKind.Removed, oldLines[a], a + 1, 0));
                    a++;
                }
                else
                {
                    _Lines.Add(new DiffLine(DiffLineKind.Added, newLines[b], 0, b + 1));
                    b++;
                }
            }

            while (a < n)
            {
                _Lines.Add(new DiffLine(DiffLineKind.Removed, oldLines[a], a + 1, 0));
                a++;
            }

            while (b < m)
            {
                _Lines.Add(new DiffLine(DiffLineKind.Added, newLines[b], 0, b + 1));
                b++;
            }
        }

        private static string[] Split(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }

        #endregion
    }
}
