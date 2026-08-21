namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;

    /// <summary>
    /// Renders a unified line diff between two texts, with added lines in green, removed lines in red,
    /// and unchanged context lines optionally syntax-highlighted. The line diff is computed with a
    /// longest-common-subsequence match. Up/Down scroll when the diff is taller than the region.
    /// </summary>
    public sealed class DiffView : IWidget, IFocusable
    {
        private readonly List<DiffLine> _Lines = new List<DiffLine>();
        private int _Top;

        /// <summary>
        /// Gets or sets the language used to syntax-highlight context lines, or null for plain text.
        /// </summary>
        public string? SyntaxLanguage { get; set; }

        /// <summary>
        /// Gets the number of diff lines.
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
        }

        /// <summary>
        /// Scrolls the diff with Up/Down (one line), PageUp/PageDown (ten lines), and Home/End (jump to
        /// the first or last line).
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Top = Math.Max(0, _Top - 1);
                    return true;
                case KeyCode.Down:
                    _Top = Math.Min(Math.Max(0, _Lines.Count - 1), _Top + 1);
                    return true;
                case KeyCode.PageUp:
                    _Top = Math.Max(0, _Top - 10);
                    return true;
                case KeyCode.PageDown:
                    _Top = Math.Min(Math.Max(0, _Lines.Count - 1), _Top + 10);
                    return true;
                case KeyCode.Home:
                    _Top = 0;
                    return true;
                case KeyCode.End:
                    _Top = Math.Max(0, _Lines.Count - 1);
                    return true;
                default:
                    return false;
            }
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
            if (width <= 0 || height <= 0)
                return;

            for (int row = 0; row < height && _Top + row < _Lines.Count; row++)
            {
                DiffLine line = _Lines[_Top + row];
                string prefix;
                CellStyle prefixStyle;
                StyledText content;

                switch (line.Kind)
                {
                    case DiffLineKind.Added:
                        prefix = "+";
                        prefixStyle = CellStyle.Default.WithForeground(Color.FromPalette(2));
                        content = Text.From(line.Text, CellStyle.Default.WithForeground(Color.FromPalette(2)));
                        break;
                    case DiffLineKind.Removed:
                        prefix = "-";
                        prefixStyle = CellStyle.Default.WithForeground(Color.FromPalette(1));
                        content = Text.From(line.Text, CellStyle.Default.WithForeground(Color.FromPalette(1)));
                        break;
                    default:
                        prefix = " ";
                        prefixStyle = CellStyle.Default;
                        content = SyntaxLanguage != null
                            ? SyntaxHighlighter.HighlightLine(line.Text, SyntaxLanguage)
                            : Text.From(line.Text);
                        break;
                }

                surface.DrawText(0, row, prefix, prefixStyle);
                surface.DrawStyledText(1, row, content);
            }
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
                    _Lines.Add(new DiffLine(DiffLineKind.Context, oldLines[a]));
                    a++;
                    b++;
                }
                else if (dp[a + 1, b] >= dp[a, b + 1])
                {
                    _Lines.Add(new DiffLine(DiffLineKind.Removed, oldLines[a]));
                    a++;
                }
                else
                {
                    _Lines.Add(new DiffLine(DiffLineKind.Added, newLines[b]));
                    b++;
                }
            }

            while (a < n)
            {
                _Lines.Add(new DiffLine(DiffLineKind.Removed, oldLines[a]));
                a++;
            }

            while (b < m)
            {
                _Lines.Add(new DiffLine(DiffLineKind.Added, newLines[b]));
                b++;
            }
        }

        private static string[] Split(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }
    }
}
