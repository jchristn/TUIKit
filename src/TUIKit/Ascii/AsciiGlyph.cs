namespace TUIKit.Ascii
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The immutable multi-row sub-character bitmap for a single character in an
    /// <see cref="IAsciiFont"/>. Every row has the same length (<see cref="Width"/>) and the row count
    /// equals the font's <see cref="AsciiFontMetrics.Height"/>. Rows may still contain the font's
    /// hardblank character; the render engine substitutes spaces for hardblanks only after
    /// composition. Instances are read-only and safe to share across threads.
    /// </summary>
    public sealed class AsciiGlyph
    {
        private readonly string[] _Rows;

        /// <summary>
        /// Gets the width of the glyph in columns. Always zero or greater.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the number of rows in the glyph, equal to the font height. Always zero or greater.
        /// </summary>
        public int Height
        {
            get { return _Rows.Length; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiGlyph"/> class.
        /// </summary>
        /// <param name="rows">
        /// The glyph rows, top to bottom. Must not be null and must contain no null row. Every row
        /// must have the same length; that length becomes <see cref="Width"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> or any row is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the rows are not all the same length.</exception>
        public AsciiGlyph(IReadOnlyList<string> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            string[] copy = new string[rows.Count];
            int width = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i];
                if (row == null)
                    throw new ArgumentNullException(nameof(rows), "Glyph rows must not contain a null row.");

                if (i == 0)
                    width = row.Length;
                else if (row.Length != width)
                    throw new ArgumentException("All glyph rows must have the same length.", nameof(rows));

                copy[i] = row;
            }

            _Rows = copy;
            Width = width;
        }

        /// <summary>
        /// Gets the row at the supplied index.
        /// </summary>
        /// <param name="index">The zero-based row index, from 0 to <see cref="Height"/> minus one.</param>
        /// <returns>The row text, never null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public string Row(int index)
        {
            if (index < 0 || index >= _Rows.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Row index is out of range.");

            return _Rows[index];
        }
    }
}
