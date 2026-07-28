namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// Wraps <see cref="StyledText"/> to a column width, breaking at spaces where possible and hard
    /// breaking overlong words. Styling is preserved across wrap points and grapheme clusters are
    /// never split. All members are thread-safe.
    /// </summary>
    public static class TextWrapper
    {
        /// <summary>
        /// Wraps styled text into visual rows that each fit within the supplied width.
        /// </summary>
        /// <param name="text">The text to wrap. Must not be null.</param>
        /// <param name="width">The maximum row width in columns. Values of one or less return the text unwrapped.</param>
        /// <returns>The visual rows, at least one. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static IReadOnlyList<StyledText> Wrap(StyledText text, int width)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<StyledText> rows = new List<StyledText>();

            List<WrapCell> cells = Flatten(text);
            if (cells.Count == 0)
            {
                rows.Add(StyledText.Empty);
                return rows;
            }

            if (width <= 1)
            {
                rows.Add(text);
                return rows;
            }

            int lineStart = 0;
            int lastBreak = -1;
            int lineWidth = 0;

            int i = 0;
            while (i < cells.Count)
            {
                WrapCell cell = cells[i];

                if (cell.IsSpace)
                {
                    if (lineWidth >= width)
                    {
                        // A space at the wrap boundary is consumed rather than carried to the next line.
                        rows.Add(BuildRow(cells, lineStart, i));
                        lineStart = i + 1;
                        lastBreak = -1;
                        lineWidth = 0;
                        i++;
                        continue;
                    }

                    lineWidth += cell.Width;
                    lastBreak = i + 1;
                    i++;
                    continue;
                }

                if (lineWidth + cell.Width > width && i > lineStart)
                {
                    if (lastBreak > lineStart)
                    {
                        rows.Add(BuildRow(cells, lineStart, lastBreak));
                        lineStart = lastBreak;
                        lineWidth = Sum(cells, lineStart, i);
                    }
                    else
                    {
                        rows.Add(BuildRow(cells, lineStart, i));
                        lineStart = i;
                        lineWidth = 0;
                    }

                    lastBreak = -1;
                }

                lineWidth += cell.Width;
                i++;
            }

            rows.Add(BuildRow(cells, lineStart, cells.Count));
            return rows;
        }

        private static int Sum(List<WrapCell> cells, int start, int end)
        {
            int total = 0;
            for (int i = start; i < end; i++)
                total += cells[i].Width;

            return total;
        }

        private static List<WrapCell> Flatten(StyledText text)
        {
            List<WrapCell> cells = new List<WrapCell>();
            IReadOnlyList<StyledSpan> spans = text.Spans;
            for (int s = 0; s < spans.Count; s++)
            {
                StyledSpan span = spans[s];
                IReadOnlyList<Grapheme> clusters = Graphemes.Split(span.Text);
                for (int c = 0; c < clusters.Count; c++)
                {
                    Grapheme cluster = clusters[c];
                    bool isSpace = cluster.Text == " " || cluster.Text == "\t";
                    cells.Add(new WrapCell(cluster.Text, cluster.Width, span.Style, isSpace));
                }
            }

            return cells;
        }

        private static StyledText BuildRow(List<WrapCell> cells, int start, int end)
        {
            List<StyledSpan> spans = new List<StyledSpan>();
            StringBuilder run = new StringBuilder();
            bool hasRun = false;
            CellStyle runStyle = CellStyle.Default;

            for (int i = start; i < end; i++)
            {
                WrapCell cell = cells[i];
                if (!hasRun)
                {
                    runStyle = cell.Style;
                    hasRun = true;
                }
                else if (cell.Style != runStyle)
                {
                    spans.Add(new StyledSpan(run.ToString(), runStyle));
                    run.Clear();
                    runStyle = cell.Style;
                }

                run.Append(cell.Grapheme);
            }

            if (hasRun)
                spans.Add(new StyledSpan(run.ToString(), runStyle));

            if (spans.Count == 0)
                return StyledText.Empty;

            return new StyledText(spans);
        }
    }
}
