namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A columnar table with a styled header row. By default columns split the available width evenly
    /// and there is no border (back-compatible). Opt into a <see cref="TableBorder"/> for box-drawing
    /// borders, <see cref="ColumnSizing.FitContent"/> to size columns to their content, per-column
    /// <see cref="CellAlignment"/>, and styled cells via <see cref="AddRow(StyledText[])"/> /
    /// <see cref="AddMarkupRow"/>. Cells that overflow their column are clipped with an ellipsis.
    /// </summary>
    public sealed class Table : IWidget
    {
        private readonly string[] _Headers;
        private readonly List<StyledText[]> _Rows = new List<StyledText[]>();
        private readonly CellAlignment[] _Alignments;

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class with no border.
        /// </summary>
        /// <param name="headers">The column headers. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="headers"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="headers"/> is empty.</exception>
        public Table(string[] headers)
            : this(headers, TableBorder.None)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class with the supplied border.
        /// </summary>
        /// <param name="headers">The column headers. Must not be null or empty.</param>
        /// <param name="border">The border style.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="headers"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="headers"/> is empty.</exception>
        public Table(string[] headers, TableBorder border)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));
            if (headers.Length == 0)
                throw new ArgumentException("At least one column is required.", nameof(headers));

            _Headers = headers;
            _Alignments = new CellAlignment[headers.Length];
            Border = border;
        }

        /// <summary>Gets or sets the border style. Defaults to <see cref="TableBorder.None"/>.</summary>
        public TableBorder Border { get; set; }

        /// <summary>Gets or sets how columns are sized. Defaults to <see cref="ColumnSizing.Even"/>.</summary>
        public ColumnSizing Sizing { get; set; }

        /// <summary>Gets the number of data rows.</summary>
        public int RowCount
        {
            get { return _Rows.Count; }
        }

        /// <summary>
        /// Sets the horizontal alignment for a column.
        /// </summary>
        /// <param name="columnIndex">The zero-based column index.</param>
        /// <param name="alignment">The alignment to apply.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the column index is out of range.</exception>
        public void SetAlignment(int columnIndex, CellAlignment alignment)
        {
            if (columnIndex < 0 || columnIndex >= _Alignments.Length)
                throw new ArgumentOutOfRangeException(nameof(columnIndex));

            _Alignments[columnIndex] = alignment;
        }

        /// <summary>
        /// Adds a data row of plain-text cells.
        /// </summary>
        /// <param name="cells">The cell values, one per column. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        public void AddRow(string[] cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            StyledText[] styled = new StyledText[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                styled[i] = Text.From(cells[i] ?? string.Empty);

            _Rows.Add(styled);
        }

        /// <summary>
        /// Adds a data row of pre-styled cells.
        /// </summary>
        /// <param name="cells">The styled cell values, one per column. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        public void AddRow(params StyledText[] cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            _Rows.Add(cells);
        }

        /// <summary>
        /// Adds a data row whose cells are parsed as inline markup (see <see cref="Markup.Parse(string)"/>).
        /// </summary>
        /// <param name="cells">The markup cell values, one per column. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        public void AddMarkupRow(params string[] cells)
        {
            if (cells == null)
                throw new ArgumentNullException(nameof(cells));

            StyledText[] styled = new StyledText[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                styled[i] = Markup.Parse(cells[i] ?? string.Empty);

            _Rows.Add(styled);
        }

        /// <summary>
        /// Removes all data rows.
        /// </summary>
        public void ClearRows()
        {
            _Rows.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int rows = Border == TableBorder.None ? _Rows.Count + 1 : _Rows.Count + 4;
            return new Size(available.Width, Math.Min(available.Height, rows));
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

            if (Border == TableBorder.None)
                RenderPlain(surface, width, height);
            else
                RenderBordered(surface, width, height);
        }

        private void RenderPlain(ISurface surface, int width, int height)
        {
            int count = _Headers.Length;
            int[] columnX = new int[count];
            int[] innerWidth = new int[count];

            if (Sizing == ColumnSizing.FitContent)
            {
                int[] natural = NaturalWidths();
                int x = 0;
                for (int c = 0; c < count; c++)
                {
                    columnX[c] = x;
                    innerWidth[c] = Math.Min(natural[c], Math.Max(1, width - x));
                    x += innerWidth[c] + 1;
                }
            }
            else
            {
                int columnWidth = Math.Max(1, width / count);
                for (int c = 0; c < count; c++)
                {
                    columnX[c] = c * columnWidth;
                    innerWidth[c] = Math.Max(1, columnWidth - 1);
                }
            }

            CellStyle headerStyle = CellStyle.Default.WithAttribute(CellAttributes.Bold, true).WithForeground(Color.FromPalette(6));
            for (int c = 0; c < count; c++)
                DrawCell(surface, columnX[c], 0, Text.From(_Headers[c] ?? string.Empty), innerWidth[c], _Alignments[c], headerStyle);

            for (int r = 0; r < _Rows.Count && r + 1 < height; r++)
            {
                for (int c = 0; c < count; c++)
                    DrawCell(surface, columnX[c], r + 1, CellAt(r, c), innerWidth[c], _Alignments[c], CellStyle.Default);
            }
        }

        private void RenderBordered(ISurface surface, int width, int height)
        {
            int count = _Headers.Length;
            int[] widths = FitWidths(width);

            string topLeft = Border == TableBorder.Rounded ? "╭" : "┌";
            string topRight = Border == TableBorder.Rounded ? "╮" : "┐";
            string bottomLeft = Border == TableBorder.Rounded ? "╰" : "└";
            string bottomRight = Border == TableBorder.Rounded ? "╯" : "┘";
            CellStyle line = CellStyle.Default.WithForeground(Color.FromPalette(8));
            CellStyle headerStyle = CellStyle.Default.WithAttribute(CellAttributes.Bold, true).WithForeground(Color.FromPalette(6));

            int rowsToDraw = _Rows.Count;
            int neededHeight = rowsToDraw + 4;
            if (neededHeight > height)
                rowsToDraw = Math.Max(0, height - 4);

            DrawBorderRow(surface, 0, widths, topLeft, "┬", topRight, line);
            DrawContentRow(surface, 1, widths, RowCells(_Headers), headerStyle, line);
            DrawBorderRow(surface, 2, widths, "├", "┼", "┤", line);

            for (int r = 0; r < rowsToDraw; r++)
                DrawContentRow(surface, 3 + r, widths, DataCells(r), CellStyle.Default, line);

            DrawBorderRow(surface, 3 + rowsToDraw, widths, bottomLeft, "┴", bottomRight, line);
        }

        private StyledText[] RowCells(string[] headers)
        {
            StyledText[] cells = new StyledText[headers.Length];
            for (int c = 0; c < headers.Length; c++)
                cells[c] = Text.From(headers[c] ?? string.Empty);

            return cells;
        }

        private StyledText[] DataCells(int rowIndex)
        {
            StyledText[] cells = new StyledText[_Headers.Length];
            for (int c = 0; c < _Headers.Length; c++)
                cells[c] = CellAt(rowIndex, c);

            return cells;
        }

        private void DrawBorderRow(ISurface surface, int y, int[] widths, string left, string junction, string right, CellStyle style)
        {
            int x = 0;
            surface.DrawText(x, y, left, style);
            x += 1;
            for (int c = 0; c < widths.Length; c++)
            {
                surface.DrawText(x, y, new string('─', widths[c] + 2), style);
                x += widths[c] + 2;
                surface.DrawText(x, y, c < widths.Length - 1 ? junction : right, style);
                x += 1;
            }
        }

        private void DrawContentRow(ISurface surface, int y, int[] widths, StyledText[] cells, CellStyle baseStyle, CellStyle lineStyle)
        {
            int x = 0;
            surface.DrawText(x, y, "│", lineStyle);
            x += 1;
            for (int c = 0; c < widths.Length; c++)
            {
                StyledText cell = c < cells.Length ? cells[c] : Text.From(string.Empty);
                DrawCell(surface, x + 1, y, cell, widths[c], _Alignments[c], baseStyle);
                x += widths[c] + 2;
                surface.DrawText(x, y, "│", lineStyle);
                x += 1;
            }
        }

        private StyledText CellAt(int rowIndex, int column)
        {
            StyledText[] row = _Rows[rowIndex];
            return column < row.Length && row[column] != null ? row[column] : Text.From(string.Empty);
        }

        private int[] NaturalWidths()
        {
            int count = _Headers.Length;
            int[] widths = new int[count];
            for (int c = 0; c < count; c++)
                widths[c] = (_Headers[c] ?? string.Empty).Length;

            for (int r = 0; r < _Rows.Count; r++)
            {
                StyledText[] row = _Rows[r];
                for (int c = 0; c < count && c < row.Length; c++)
                {
                    int len = row[c] == null ? 0 : row[c].ToPlainString().Length;
                    if (len > widths[c])
                        widths[c] = len;
                }
            }

            for (int c = 0; c < count; c++)
            {
                if (widths[c] < 1)
                    widths[c] = 1;
            }

            return widths;
        }

        private int[] FitWidths(int available)
        {
            int count = _Headers.Length;
            int[] widths = NaturalWidths();
            int overhead = 2 + (count - 1) + (2 * count); // outer borders + inner separators + per-cell padding
            int budget = available - overhead;
            if (budget < count)
                budget = count;

            int total = 0;
            for (int c = 0; c < count; c++)
                total += widths[c];

            // Shrink the widest column repeatedly until the content fits the budget.
            while (total > budget)
            {
                int widest = 0;
                for (int c = 1; c < count; c++)
                {
                    if (widths[c] > widths[widest])
                        widest = c;
                }

                if (widths[widest] <= 1)
                    break;

                widths[widest]--;
                total--;
            }

            return widths;
        }

        private static void DrawCell(ISurface surface, int x, int y, StyledText text, int innerWidth, CellAlignment alignment, CellStyle baseStyle)
        {
            if (innerWidth <= 0)
                return;

            StyledText clipped = Clip(text, innerWidth);
            int length = clipped.ToPlainString().Length;
            int offset = 0;
            if (alignment == CellAlignment.Right)
                offset = innerWidth - length;
            else if (alignment == CellAlignment.Center)
                offset = (innerWidth - length) / 2;

            if (offset < 0)
                offset = 0;

            surface.DrawStyledText(x + offset, y, clipped, baseStyle);
        }

        private static StyledText Clip(StyledText text, int maxWidth)
        {
            if (maxWidth <= 0)
                return StyledText.Empty;

            string plain = text.ToPlainString();
            if (plain.Length <= maxWidth)
                return text;

            List<StyledSpan> spans = new List<StyledSpan>();
            int remaining = maxWidth - 1;
            IReadOnlyList<StyledSpan> source = text.Spans;
            for (int i = 0; i < source.Count && remaining > 0; i++)
            {
                StyledSpan span = source[i];
                if (span.Text.Length <= remaining)
                {
                    spans.Add(span);
                    remaining -= span.Text.Length;
                }
                else
                {
                    spans.Add(new StyledSpan(span.Text.Substring(0, remaining), span.Style));
                    remaining = 0;
                }
            }

            CellStyle ellipsisStyle = spans.Count > 0 ? spans[spans.Count - 1].Style : CellStyle.Default;
            spans.Add(new StyledSpan("…", ellipsisStyle));
            return new StyledText(spans);
        }
    }
}
