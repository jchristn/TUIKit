namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit.Unicode;

    /// <summary>
    /// Formats rows of cells into aligned text columns, sizing each column to the widest cell in it and
    /// separating columns with a fixed gap. Useful for command menus, key-binding references, and any
    /// list where columns must line up. Column count is taken from the widest row; short rows are
    /// padded with empty trailing cells. All members are thread-safe.
    /// </summary>
    public static class ColumnFormatter
    {
        /// <summary>
        /// Formats rows into aligned, left-justified columns separated by two spaces.
        /// </summary>
        /// <param name="rows">The rows, each an array of cell strings. Must not be null, and no row or cell may be null.</param>
        /// <returns>The formatted lines, one per row, with trailing padding trimmed. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/>, a row, or a cell is null.</exception>
        public static IReadOnlyList<string> Format(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            return Format(rows, 2);
        }

        /// <summary>
        /// Formats rows into aligned, left-justified columns separated by <paramref name="gap"/> spaces.
        /// </summary>
        /// <param name="rows">The rows, each a list of cell strings. Must not be null, and no row or cell may be null.</param>
        /// <param name="gap">The number of spaces between columns. Must be zero or greater.</param>
        /// <returns>The formatted lines, one per row, with trailing padding trimmed. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/>, a row, or a cell is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gap"/> is negative.</exception>
        public static IReadOnlyList<string> Format(IReadOnlyList<IReadOnlyList<string>> rows, int gap)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            if (gap < 0)
                throw new ArgumentOutOfRangeException(nameof(gap), gap, "Gap must be zero or greater.");

            int columnCount = 0;
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<string> row = rows[r];
                if (row == null)
                    throw new ArgumentNullException(nameof(rows), "A row must not be null.");
                if (row.Count > columnCount)
                    columnCount = row.Count;
            }

            int[] widths = new int[columnCount];
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<string> row = rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    string cell = row[c];
                    if (cell == null)
                        throw new ArgumentNullException(nameof(rows), "A cell must not be null.");

                    int cellWidth = Graphemes.MeasureWidth(cell);
                    if (cellWidth > widths[c])
                        widths[c] = cellWidth;
                }
            }

            string gapText = gap > 0 ? new string(' ', gap) : string.Empty;
            List<string> lines = new List<string>(rows.Count);
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<string> row = rows[r];
                StringBuilder builder = new StringBuilder();
                for (int c = 0; c < columnCount; c++)
                {
                    string cell = c < row.Count ? row[c] : string.Empty;
                    builder.Append(cell);

                    bool lastPopulated = c >= columnCount - 1;
                    if (!lastPopulated)
                    {
                        int pad = widths[c] - Graphemes.MeasureWidth(cell);
                        if (pad > 0)
                            builder.Append(new string(' ', pad));
                        builder.Append(gapText);
                    }
                }

                lines.Add(TrimEnd(builder.ToString()));
            }

            return lines;
        }

        private static string TrimEnd(string value)
        {
            int end = value.Length;
            while (end > 0 && value[end - 1] == ' ')
                end--;

            return value.Substring(0, end);
        }
    }
}
