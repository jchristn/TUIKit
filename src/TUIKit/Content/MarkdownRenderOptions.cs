namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// Everything the width-aware Markdown layout needs to know about how output should look: element colors
    /// (<see cref="Styles"/>), table layout (<see cref="Table"/>), width limits, overflow behavior, and the glyphs
    /// used for bullets, quotes, rules, and tasks. Pass it to
    /// <see cref="MarkdownRenderer.Render(string, int, MarkdownRenderOptions)"/>,
    /// <see cref="Pane.WriteMarkdown(string, MarkdownRenderOptions)"/>, <see cref="Widgets.MarkdownView"/>, or
    /// <see cref="StreamingTranscript.RenderOptions"/>.
    /// </summary>
    /// <remarks>
    /// A new instance uses Unicode glyphs and the colors of <see cref="MarkdownStyles.Default"/>;
    /// <see cref="Ascii"/> returns one that uses only ASCII. Instances are mutable and not thread-safe; do not
    /// mutate one while a render is using it. Panes, views, and transcripts that keep options take a copy
    /// (<see cref="Clone"/>), so later edits to your instance never change content already written.
    /// </remarks>
    public sealed class MarkdownRenderOptions
    {
        private MarkdownStyles _Styles = MarkdownStyles.Default;
        private MarkdownTableOptions _Table = new MarkdownTableOptions();
        private int _MaxWidth = 0;
        private int _MinWidth = 10;
        private string _Ellipsis = "…";
        private int _BlockSpacing = 1;
        private IReadOnlyList<string> _BulletGlyphs = new[] { "•", "◦", "▪" };
        private int _ListIndent = 0;
        private string _TaskOpenGlyph = "☐";
        private string _TaskDoneGlyph = "☑";
        private string _QuotePrefix = "│ ";
        private string _RuleGlyph = "─";
        private int _RuleWidth = 0;
        private int _CodeIndent = 2;

        /// <summary>
        /// Gets or sets the colors and attributes of every element. Defaults to <see cref="MarkdownStyles.Default"/>.
        /// Use <see cref="MarkdownStyles.FromTheme(Theme)"/> to follow the application theme. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownStyles Styles
        {
            get { return _Styles; }
            set { _Styles = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets how tables are laid out and drawn. Defaults to a new <see cref="MarkdownTableOptions"/>.
        /// Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownTableOptions Table
        {
            get { return _Table; }
            set { _Table = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the widest the layout may be, whatever the available width. Defaults to 0, meaning the full
        /// available width. Useful for keeping prose at a readable measure in a wide pane. Must be 0 or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int MaxWidth
        {
            get { return _MaxWidth; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxWidth must be 0 (no cap) or greater.");

                _MaxWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the narrowest the layout may be. When the available width is smaller, the layout happens
        /// at this width and the host clips the right edge, instead of wrapping every word onto its own row.
        /// Defaults to 10. Minimum 1, maximum 1000.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 1000.</exception>
        public int MinWidth
        {
            get { return _MinWidth; }
            set
            {
                if (value < 1 || value > 1000)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MinWidth must be between 1 and 1000.");

                _MinWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets what paragraphs, headings, list items, and quotes do when they are wider than the layout.
        /// Defaults to <see cref="MarkdownOverflow.Wrap"/>.
        /// </summary>
        public MarkdownOverflow Overflow { get; set; } = MarkdownOverflow.Wrap;

        /// <summary>
        /// Gets or sets what fenced code lines do when they are wider than the layout. Defaults to
        /// <see cref="MarkdownOverflow.Wrap"/>; <see cref="MarkdownOverflow.Clip"/> keeps one row per source line.
        /// </summary>
        public MarkdownOverflow CodeOverflow { get; set; } = MarkdownOverflow.Wrap;

        /// <summary>
        /// Gets or sets the marker <see cref="MarkdownOverflow.Truncate"/> appends. Defaults to <c>…</c>. May be
        /// empty (then truncation looks like clipping). Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Ellipsis
        {
            get { return _Ellipsis; }
            set { _Ellipsis = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the number of blank rows between blocks. Defaults to 1. Minimum 0, maximum 4. Items of a
        /// tight list (no blank lines between items in the source) are never separated.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to 4.</exception>
        public int BlockSpacing
        {
            get { return _BlockSpacing; }
            set
            {
                if (value < 0 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "BlockSpacing must be between 0 and 4.");

                _BlockSpacing = value;
            }
        }

        /// <summary>
        /// Gets or sets whether headings keep their <c>#</c> markers. Defaults to false (headings are shown by
        /// style alone).
        /// </summary>
        public bool ShowHeadingMarkers { get; set; }

        /// <summary>
        /// Gets or sets the bullet glyphs, one per nesting depth; deeper lists cycle back to the first. Defaults
        /// to <c>•</c>, <c>◦</c>, <c>▪</c>. Must not be null or empty, and no entry may be null or empty. The list
        /// is copied on set.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the list is empty or holds a null or empty entry.</exception>
        public IReadOnlyList<string> BulletGlyphs
        {
            get { return _BulletGlyphs; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.Count == 0)
                    throw new ArgumentException("BulletGlyphs must hold at least one glyph.", nameof(value));

                string[] copy = new string[value.Count];
                for (int i = 0; i < value.Count; i++)
                {
                    if (string.IsNullOrEmpty(value[i]))
                        throw new ArgumentException("BulletGlyphs entry " + i + " must not be null or empty.", nameof(value));

                    copy[i] = value[i];
                }

                _BulletGlyphs = copy;
            }
        }

        /// <summary>
        /// Gets or sets the number of spaces before every list marker, at every depth. Defaults to 0. Minimum 0,
        /// maximum 8. Nested lists are always indented to their parent item's content column, on top of this.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to 8.</exception>
        public int ListIndent
        {
            get { return _ListIndent; }
            set
            {
                if (value < 0 || value > 8)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ListIndent must be between 0 and 8.");

                _ListIndent = value;
            }
        }

        /// <summary>
        /// Gets or sets the marker of an open task item (<c>- [ ]</c>). Defaults to <c>☐</c>. Must not be null or
        /// empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string.</exception>
        public string TaskOpenGlyph
        {
            get { return _TaskOpenGlyph; }
            set { _TaskOpenGlyph = RequireText(value, "TaskOpenGlyph"); }
        }

        /// <summary>
        /// Gets or sets the marker of a completed task item (<c>- [x]</c>). Defaults to <c>☑</c>. Must not be null
        /// or empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string.</exception>
        public string TaskDoneGlyph
        {
            get { return _TaskDoneGlyph; }
            set { _TaskDoneGlyph = RequireText(value, "TaskDoneGlyph"); }
        }

        /// <summary>
        /// Gets or sets the prefix drawn before every row of a block quote, once per nesting level, in
        /// <see cref="MarkdownStyles.Quote"/> style. Defaults to <c>"│ "</c>. Must not be null or empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string.</exception>
        public string QuotePrefix
        {
            get { return _QuotePrefix; }
            set { _QuotePrefix = RequireText(value, "QuotePrefix"); }
        }

        /// <summary>
        /// Gets or sets the glyph repeated to draw a thematic break (<c>---</c>). Defaults to <c>─</c>. Must not be
        /// null or empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string.</exception>
        public string RuleGlyph
        {
            get { return _RuleGlyph; }
            set { _RuleGlyph = RequireText(value, "RuleGlyph"); }
        }

        /// <summary>
        /// Gets or sets the width of a thematic break. Defaults to 0, meaning the full layout width; a larger
        /// layout is never exceeded. Must be 0 or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int RuleWidth
        {
            get { return _RuleWidth; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "RuleWidth must be 0 (full width) or greater.");

                _RuleWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of spaces before every fenced code line. Defaults to 2. Minimum 0, maximum 16.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to 16.</exception>
        public int CodeIndent
        {
            get { return _CodeIndent; }
            set
            {
                if (value < 0 || value > 16)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "CodeIndent must be between 0 and 16.");

                _CodeIndent = value;
            }
        }

        /// <summary>
        /// Gets or sets whether a fence's language tag (<c>```csharp</c>) is shown on a row above the code, in
        /// <see cref="MarkdownStyles.CodeLanguage"/> style. Defaults to true. Fences without a tag show no row.
        /// </summary>
        public bool ShowCodeLanguage { get; set; } = true;

        /// <summary>
        /// Gets or sets whether code in a fence that names a language is colored by <see cref="SyntaxHighlighter"/>.
        /// Defaults to true. Fences without a language are never highlighted.
        /// </summary>
        public bool SyntaxHighlighting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether code rows are padded with spaces to the layout width, so a background color in
        /// <see cref="MarkdownStyles.CodeBlock"/> reads as a solid block. Defaults to false.
        /// </summary>
        public bool FillCodeBackground { get; set; }

        /// <summary>
        /// Gets or sets whether links show their target: <c>[docs](https://x.dev)</c> renders as
        /// <c>docs (https://x.dev)</c>. Defaults to true. When false, or when the text is the URL, only the text
        /// shows.
        /// </summary>
        public bool ShowLinkUrls { get; set; } = true;

        /// <summary>
        /// Returns a new instance that uses only ASCII: <c>-</c>, <c>*</c>, <c>+</c> bullets, <c>[ ]</c> and
        /// <c>[x]</c> tasks, a <c>"| "</c> quote prefix, <c>-</c> rules, a <c>...</c> ellipsis, and
        /// <see cref="MarkdownTableGlyphs.Ascii"/> tables. Colors are unchanged.
        /// </summary>
        /// <returns>A new options instance. Never null.</returns>
        public static MarkdownRenderOptions Ascii()
        {
            MarkdownRenderOptions options = new MarkdownRenderOptions();
            options._BulletGlyphs = new[] { "-", "*", "+" };
            options._TaskOpenGlyph = "[ ]";
            options._TaskDoneGlyph = "[x]";
            options._QuotePrefix = "| ";
            options._RuleGlyph = "-";
            options._Ellipsis = "...";
            options._Table.Glyphs = MarkdownTableGlyphs.Ascii;
            return options;
        }

        /// <summary>
        /// Returns a deep copy: the copy's <see cref="Styles"/> and <see cref="Table"/> are new instances, so
        /// changing them never affects this one.
        /// </summary>
        /// <returns>A new instance with the same values. Never null.</returns>
        public MarkdownRenderOptions Clone()
        {
            MarkdownRenderOptions copy = new MarkdownRenderOptions();
            copy._Styles = _Styles.Clone();
            copy._Table = _Table.Clone();
            copy._MaxWidth = _MaxWidth;
            copy._MinWidth = _MinWidth;
            copy.Overflow = Overflow;
            copy.CodeOverflow = CodeOverflow;
            copy._Ellipsis = _Ellipsis;
            copy._BlockSpacing = _BlockSpacing;
            copy.ShowHeadingMarkers = ShowHeadingMarkers;
            copy._BulletGlyphs = _BulletGlyphs;
            copy._ListIndent = _ListIndent;
            copy._TaskOpenGlyph = _TaskOpenGlyph;
            copy._TaskDoneGlyph = _TaskDoneGlyph;
            copy._QuotePrefix = _QuotePrefix;
            copy._RuleGlyph = _RuleGlyph;
            copy._RuleWidth = _RuleWidth;
            copy._CodeIndent = _CodeIndent;
            copy.ShowCodeLanguage = ShowCodeLanguage;
            copy.SyntaxHighlighting = SyntaxHighlighting;
            copy.FillCodeBackground = FillCodeBackground;
            copy.ShowLinkUrls = ShowLinkUrls;
            return copy;
        }

        /// <summary>
        /// Returns a copy of these options that follows a theme: the elements a theme defines (text, headings, code,
        /// bullets, quotes, rules, tasks, links, table header and border) take their styles from
        /// <see cref="MarkdownStyles.FromTheme(Theme, bool)"/> without backgrounds, so the surface underneath shows
        /// through, while the elements a theme does not define (<see cref="MarkdownStyles.Strong"/>,
        /// <see cref="MarkdownStyles.Emphasis"/>, <see cref="MarkdownStyles.Strikethrough"/>,
        /// <see cref="MarkdownStyles.TableFirstColumn"/>, and <see cref="MarkdownStyles.TableCell"/>) keep this
        /// instance's values. When <see cref="Theme.UseAsciiBorders"/> is true the glyphs <see cref="Ascii"/> sets are
        /// applied too (bullets, task glyphs, quote prefix, rule glyph, ellipsis, and table glyphs). Layout
        /// settings (widths, overflow, spacing, table sizing) are kept. This instance is never changed.
        /// </summary>
        /// <remarks>
        /// The result is not reversible: on a theme without ASCII borders the glyphs of this instance are kept as
        /// they are, so options that already carry the ASCII glyphs keep them. Call it on your own base options
        /// each time the theme changes, never on a previous result. Each call returns a new instance; keep and
        /// reuse it until the next theme change, because caches such as <see cref="MarkdownLayoutCache"/> compare
        /// options by reference.
        /// </remarks>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <returns>A new options instance. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public MarkdownRenderOptions WithTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            MarkdownRenderOptions copy = Clone();
            MarkdownStyles themed = MarkdownStyles.FromTheme(theme, false);
            themed.Strong = _Styles.Strong;
            themed.Emphasis = _Styles.Emphasis;
            themed.Strikethrough = _Styles.Strikethrough;
            themed.TableFirstColumn = _Styles.TableFirstColumn;
            themed.TableCell = _Styles.TableCell;
            copy._Styles = themed;
            if (theme.UseAsciiBorders)
            {
                MarkdownRenderOptions ascii = Ascii();
                copy._BulletGlyphs = ascii._BulletGlyphs;
                copy._TaskOpenGlyph = ascii._TaskOpenGlyph;
                copy._TaskDoneGlyph = ascii._TaskDoneGlyph;
                copy._QuotePrefix = ascii._QuotePrefix;
                copy._RuleGlyph = ascii._RuleGlyph;
                copy._Ellipsis = ascii._Ellipsis;
                copy._Table.Glyphs = ascii._Table.Glyphs;
            }

            return copy;
        }

        internal int ResolveWidth(int available)
        {
            int width = available;
            if (_MaxWidth > 0 && _MaxWidth < width)
                width = _MaxWidth;
            if (width < _MinWidth)
                width = _MinWidth;

            return width;
        }

        private static string RequireText(string value, string name)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), name + " must not be null.");
            if (value.Length == 0)
                throw new ArgumentException(name + " must not be empty.", nameof(value));

            if (TextFit.Width(value) < 1)
                throw new ArgumentException(name + " must be at least one column wide.", nameof(value));

            return value;
        }
    }
}
