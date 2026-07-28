namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable single position in a <see cref="CellBuffer"/>: the grapheme cluster painted at
    /// that position, its style, and its column width. A wide (two-column) glyph occupies a leading
    /// cell of <see cref="Width"/> 2 followed by a continuation cell. Instances are value types and
    /// are safe to share across threads.
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        /// <summary>
        /// Gets the grapheme cluster text painted at this position. A single space represents a
        /// blank cell. Empty for a continuation cell. Never null.
        /// </summary>
        public string Grapheme { get; }

        /// <summary>
        /// Gets the style applied to this cell.
        /// </summary>
        public CellStyle Style { get; }

        /// <summary>
        /// Gets the column width of the glyph: 1 for a narrow glyph, 2 for a wide glyph, or 0 for a
        /// continuation cell.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets a value indicating whether this cell is the second (right) half of a wide glyph and
        /// carries no glyph of its own.
        /// </summary>
        public bool IsContinuation { get; }

        /// <summary>
        /// Gets a blank cell: a single space with the default style.
        /// </summary>
        public static Cell Empty
        {
            get { return new Cell(" ", CellStyle.Default, 1, false); }
        }

        private Cell(string grapheme, CellStyle style, int width, bool isContinuation)
        {
            Grapheme = grapheme;
            Style = style;
            Width = width;
            IsContinuation = isContinuation;
        }

        /// <summary>
        /// Creates a blank cell (a space) with the supplied style.
        /// </summary>
        /// <param name="style">The style to apply.</param>
        /// <returns>The blank cell.</returns>
        public static Cell Blank(CellStyle style)
        {
            return new Cell(" ", style, 1, false);
        }

        /// <summary>
        /// Creates a glyph cell.
        /// </summary>
        /// <param name="grapheme">The grapheme cluster text. Must not be null or empty.</param>
        /// <param name="style">The style to apply.</param>
        /// <param name="width">The column width (1 or 2).</param>
        /// <returns>The glyph cell.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="grapheme"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is not 1 or 2.</exception>
        public static Cell Glyph(string grapheme, CellStyle style, int width)
        {
            if (string.IsNullOrEmpty(grapheme))
                throw new ArgumentException("Grapheme must not be null or empty.", nameof(grapheme));
            if (width != 1 && width != 2)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Glyph width must be 1 or 2.");

            return new Cell(grapheme, style, width, false);
        }

        /// <summary>
        /// Creates a continuation cell representing the right half of a wide glyph.
        /// </summary>
        /// <param name="style">The style to apply (matching the leading cell).</param>
        /// <returns>The continuation cell.</returns>
        public static Cell Continuation(CellStyle style)
        {
            return new Cell(string.Empty, style, 0, true);
        }

        /// <inheritdoc/>
        public bool Equals(Cell other)
        {
            return Width == other.Width
                && IsContinuation == other.IsContinuation
                && string.Equals(Grapheme, other.Grapheme, StringComparison.Ordinal)
                && Style.Equals(other.Style);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Cell other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Grapheme != null ? StringComparer.Ordinal.GetHashCode(Grapheme) : 0;
                hash = (hash * 31) ^ Style.GetHashCode();
                hash = (hash * 31) ^ Width;
                hash = (hash * 31) ^ (IsContinuation ? 1 : 0);
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two cells are equal.
        /// </summary>
        /// <param name="left">The first cell.</param>
        /// <param name="right">The second cell.</param>
        /// <returns><c>true</c> if the cells are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(Cell left, Cell right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two cells are not equal.
        /// </summary>
        /// <param name="left">The first cell.</param>
        /// <param name="right">The second cell.</param>
        /// <returns><c>true</c> if the cells differ; otherwise <c>false</c>.</returns>
        public static bool operator !=(Cell left, Cell right)
        {
            return !left.Equals(right);
        }
    }
}
