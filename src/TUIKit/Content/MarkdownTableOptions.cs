namespace TUIKit.Content
{
    using System;

    /// <summary>
    /// How the width-aware Markdown layout draws GFM tables: border glyphs and lines, padding, column width
    /// limits, cell overflow, and what happens when the table cannot fit. Colors live in
    /// <see cref="MarkdownStyles"/> (<see cref="MarkdownStyles.TableHeader"/>,
    /// <see cref="MarkdownStyles.TableFirstColumn"/>, <see cref="MarkdownStyles.TableCell"/>,
    /// <see cref="MarkdownStyles.TableBorder"/>).
    /// </summary>
    /// <remarks>
    /// Column widths come from cell content. A column's natural width is its widest cell; its minimum is its
    /// longest word when cells wrap, or <see cref="MinColumnWidth"/> when they truncate or clip. When the natural
    /// widths fit they are used as they are. Otherwise every column starts at its minimum and the remaining space
    /// is shared in proportion to how much each column still wants, so wide columns give up space first. When even
    /// the minimums do not fit, or when the squeezed grid would make a row taller than <see cref="MaxRowLines"/>,
    /// <see cref="Fallback"/> decides. Instances are mutable and not thread-safe; do not
    /// mutate one while a render is using it. <see cref="Pane.WriteMarkdown(string, MarkdownRenderOptions)"/>
    /// takes a copy, so later edits never change committed content.
    /// </remarks>
    public sealed class MarkdownTableOptions
    {
        private MarkdownTableGlyphs _Glyphs = MarkdownTableGlyphs.Light;
        private int _CellPadding = 1;
        private int _MinColumnWidth = 3;
        private int _MaxColumnWidth = 0;
        private int _MaxRowLines = 4;

        /// <summary>
        /// Gets or sets the border glyphs. Defaults to <see cref="MarkdownTableGlyphs.Light"/>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownTableGlyphs Glyphs
        {
            get { return _Glyphs; }
            set { _Glyphs = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets whether the top, bottom, left, and right edges are drawn. Defaults to true. When false the
        /// table is drawn with column separators and the header line only, two columns narrower.
        /// </summary>
        public bool ShowOuterBorder { get; set; } = true;

        /// <summary>
        /// Gets or sets whether a line is drawn under the header row. Defaults to true.
        /// </summary>
        public bool ShowHeaderSeparator { get; set; } = true;

        /// <summary>
        /// Gets or sets whether a line is drawn between body rows. Defaults to false. Useful when cells wrap onto
        /// several rows and records would otherwise run together.
        /// </summary>
        public bool ShowRowSeparators { get; set; }

        /// <summary>
        /// Gets or sets the number of spaces on each side of a cell's content. Defaults to 1. Minimum 0, maximum 4.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to 4.</exception>
        public int CellPadding
        {
            get { return _CellPadding; }
            set
            {
                if (value < 0 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "CellPadding must be between 0 and 4.");

                _CellPadding = value;
            }
        }

        /// <summary>
        /// Gets or sets the narrowest a column may become before <see cref="Fallback"/> applies. Defaults to 3.
        /// Minimum 1, maximum 1000. With <see cref="MarkdownTableFallback.Shrink"/>, columns may go narrower.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 1000.</exception>
        public int MinColumnWidth
        {
            get { return _MinColumnWidth; }
            set
            {
                if (value < 1 || value > 1000)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MinColumnWidth must be between 1 and 1000.");

                _MinColumnWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the widest a column may become, even when there is room. Defaults to 0, meaning no cap.
        /// Content wider than the cap wraps, truncates, or clips by <see cref="CellOverflow"/>. A value below
        /// <see cref="MinColumnWidth"/> acts as <see cref="MinColumnWidth"/>. Must be 0 or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int MaxColumnWidth
        {
            get { return _MaxColumnWidth; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxColumnWidth must be 0 (no cap) or greater.");

                _MaxColumnWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets what a cell does when its content is wider than its column. Defaults to
        /// <see cref="MarkdownOverflow.Wrap"/>, which grows the row's height; <see cref="MarkdownOverflow.Truncate"/>
        /// and <see cref="MarkdownOverflow.Clip"/> keep every row one line high.
        /// </summary>
        public MarkdownOverflow CellOverflow { get; set; } = MarkdownOverflow.Wrap;

        /// <summary>
        /// Gets or sets what happens when even the minimum column widths do not fit. Defaults to
        /// <see cref="MarkdownTableFallback.Stack"/>.
        /// </summary>
        public MarkdownTableFallback Fallback { get; set; } = MarkdownTableFallback.Stack;

        /// <summary>
        /// Gets or sets the tallest a wrapped body row may be before the table is shown stacked instead, when
        /// <see cref="Fallback"/> is <see cref="MarkdownTableFallback.Stack"/>. A grid squeezed until a cell wraps
        /// to one word per line is harder to read than <c>Header: value</c> records. Defaults to 4. 0 means no
        /// limit (the grid is kept whenever the minimum widths fit). Minimum 0, maximum 1000. Ignored when cells
        /// truncate or clip, since their rows are always one line.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to 1000.</exception>
        public int MaxRowLines
        {
            get { return _MaxRowLines; }
            set
            {
                if (value < 0 || value > 1000)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxRowLines must be between 0 (no limit) and 1000.");

                _MaxRowLines = value;
            }
        }

        /// <summary>
        /// Gets or sets whether a table narrower than the layout width is stretched to fill it, sharing the extra
        /// columns in proportion to each column's natural width. Defaults to false (tables take only the width
        /// their content needs).
        /// </summary>
        public bool Expand { get; set; }

        /// <summary>
        /// Returns a copy of these options. <see cref="MarkdownTableGlyphs"/> is immutable, so the copy shares it.
        /// </summary>
        /// <returns>A new instance with the same values. Never null.</returns>
        public MarkdownTableOptions Clone()
        {
            MarkdownTableOptions copy = new MarkdownTableOptions();
            copy._Glyphs = _Glyphs;
            copy.ShowOuterBorder = ShowOuterBorder;
            copy.ShowHeaderSeparator = ShowHeaderSeparator;
            copy.ShowRowSeparators = ShowRowSeparators;
            copy._CellPadding = _CellPadding;
            copy._MinColumnWidth = _MinColumnWidth;
            copy._MaxColumnWidth = _MaxColumnWidth;
            copy._MaxRowLines = _MaxRowLines;
            copy.CellOverflow = CellOverflow;
            copy.Fallback = Fallback;
            copy.Expand = Expand;
            return copy;
        }
    }
}
