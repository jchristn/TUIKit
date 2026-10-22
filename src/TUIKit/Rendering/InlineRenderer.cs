namespace TUIKit.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;
    using TUIKit.Terminal;

    /// <summary>
    /// Converts a <see cref="CellBuffer"/> into flowing, ANSI-styled lines suitable for writing to a
    /// <see cref="System.IO.TextWriter"/> at the current cursor position — the inline counterpart to
    /// the full-screen <see cref="TerminalRenderer"/>. Adjacent cells that share a
    /// <see cref="CellStyle"/> are emitted as a single SGR run; each line ends with a reset; trailing
    /// blank cells are trimmed and wide-glyph continuation cells are skipped, matching
    /// <see cref="TUIKit.Testing.Snapshot.ToText"/>. No cursor-movement sequences are produced. When
    /// the depth is <see cref="TerminalColorDepth.None"/>, the lines are plain text identical to
    /// <c>Snapshot.ToText</c>. All members are thread-safe.
    /// </summary>
    public static class InlineRenderer
    {
        /// <summary>
        /// Renders each row of a cell buffer to an ANSI-styled string, top to bottom.
        /// </summary>
        /// <param name="buffer">The buffer to render. Must not be null.</param>
        /// <param name="depth">The color depth to quantize to.</param>
        /// <returns>One string per row; plain text when <paramref name="depth"/> is <see cref="TerminalColorDepth.None"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        public static IReadOnlyList<string> ToAnsiLines(CellBuffer buffer, TerminalColorDepth depth)
        {
            return ToAnsiLines(buffer, depth, false);
        }

        /// <summary>Renders rows with independently selected color and OSC 8 output.</summary>
        /// <param name="buffer">The source buffer. Must not be null.</param>
        /// <param name="depth">The color depth; None suppresses SGR only.</param>
        /// <param name="hyperlinks">Whether to emit targets stored in cell styles.</param>
        /// <returns>Rows with balanced hyperlink boundaries, including wrapped rows.</returns>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        public static IReadOnlyList<string> ToAnsiLines(CellBuffer buffer, TerminalColorDepth depth, bool hyperlinks)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            List<string> lines = new List<string>(buffer.Height);
            List<Cell> row = new List<Cell>(buffer.Width);
            for (int y = 0; y < buffer.Height; y++)
            {
                row.Clear();
                for (int x = 0; x < buffer.Width; x++)
                {
                    Cell cell = buffer.Get(x, y);
                    if (!cell.IsContinuation)
                        row.Add(cell);
                }

                int end = row.Count;
                while (end > 0 && IsBlank(row[end - 1]))
                    end--;

                lines.Add(depth == TerminalColorDepth.None && !hyperlinks ? PlainRow(row, end) : StyledRow(row, end, depth, hyperlinks));
            }

            return lines;
        }

        private static string PlainRow(List<Cell> row, int end)
        {
            StringBuilder builder = new StringBuilder(end);
            for (int i = 0; i < end; i++)
                builder.Append(Glyph(row[i]));

            return builder.ToString();
        }

        private static string StyledRow(List<Cell> row, int end, TerminalColorDepth depth, bool hyperlinks)
        {
            if (end == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            CellStyle current = CellStyle.Default;
            bool first = true;
            string? currentLink = null;
            for (int i = 0; i < end; i++)
            {
                CellStyle style = row[i].Style;
                if (depth != TerminalColorDepth.None && (first || !style.Equals(current)))
                {
                    builder.Append(Ansi.Sgr(style, depth));
                    current = style;
                    first = false;
                }

                HyperlinkOutput.Transition(builder, ref currentLink, hyperlinks ? style.Hyperlink : null);
                builder.Append(Glyph(row[i]));
            }

            HyperlinkOutput.Transition(builder, ref currentLink, null);
            if (depth != TerminalColorDepth.None)
                builder.Append(Ansi.ResetAttributes);
            return builder.ToString();
        }

        private static string Glyph(Cell cell)
        {
            return string.IsNullOrEmpty(cell.Grapheme) ? " " : cell.Grapheme;
        }

        private static bool IsBlank(Cell cell)
        {
            return string.IsNullOrEmpty(cell.Grapheme) || cell.Grapheme == " ";
        }
    }
}
