namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;
    using TUIKit.Unicode;

    // Grapheme-safe fitting of styled text to a column width for the Markdown layout. Every row returned is at
    // most the requested width; a grapheme wider than the whole width is dropped rather than overflowing.
    internal static class MarkdownTextFit
    {
        internal static List<StyledText> Fit(StyledText text, int width, MarkdownOverflow overflow, string ellipsis)
        {
            switch (overflow)
            {
                case MarkdownOverflow.Truncate:
                    return new List<StyledText> { Truncate(text, width, ellipsis) };
                case MarkdownOverflow.Clip:
                    return new List<StyledText> { Clip(text, width) };
                default:
                    return Wrap(text, width);
            }
        }

        internal static List<StyledText> Wrap(StyledText text, int width)
        {
            return Wrap(text, width, width);
        }

        // Wraps with a different width for the first row than for the rest, for hanging indents.
        internal static List<StyledText> Wrap(StyledText text, int firstWidth, int restWidth)
        {
            List<StyledText> rows = new List<StyledText>();
            List<WrapCell> cells = Flatten(text);
            firstWidth = Math.Max(1, firstWidth);
            restWidth = Math.Max(1, restWidth);
            int width = firstWidth;

            if (cells.Count == 0)
            {
                rows.Add(StyledText.Empty);
                return rows;
            }

            int lineStart = 0;
            int lineWidth = 0;
            int lastBreak = -1;
            int i = 0;
            while (i < cells.Count)
            {
                WrapCell cell = cells[i];
                if (cell.Width > width)
                {
                    // Cannot fit even alone; drop it.
                    cells.RemoveAt(i);
                    continue;
                }

                if (cell.IsSpace)
                {
                    if (lineWidth + cell.Width > width)
                    {
                        rows.Add(BuildRow(cells, lineStart, i));
                        width = restWidth;
                        i = SkipSpaces(cells, i + 1);
                        lineStart = i;
                        lineWidth = 0;
                        lastBreak = -1;
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
                        // Re-scan from the break, since the carried word may not fit a narrower next row.
                        rows.Add(BuildRow(cells, lineStart, lastBreak));
                        width = restWidth;
                        lineStart = SkipSpaces(cells, lastBreak);
                        i = lineStart;
                        lineWidth = 0;
                    }
                    else
                    {
                        rows.Add(BuildRow(cells, lineStart, i));
                        width = restWidth;
                        lineStart = i;
                        lineWidth = 0;
                    }

                    lastBreak = -1;
                    continue;
                }

                lineWidth += cell.Width;
                i++;
            }

            if (lineStart < cells.Count || rows.Count == 0)
                rows.Add(BuildRow(cells, lineStart, cells.Count));

            return rows;
        }

        internal static StyledText Truncate(StyledText text, int width, string ellipsis)
        {
            if (text.Width <= width)
                return text;

            int ellipsisWidth = TextFit.Width(ellipsis);
            if (ellipsisWidth >= width)
                return Clip(Text.From(ellipsis, LastStyle(text)), width);

            StyledText head = Clip(text, width - ellipsisWidth);
            return head.Append(Text.From(ellipsis, LastStyle(head.Spans.Count > 0 ? head : text)));
        }

        internal static StyledText Clip(StyledText text, int width)
        {
            if (text.Width <= width)
                return text;

            List<WrapCell> cells = Flatten(text);
            int used = 0;
            int end = 0;
            while (end < cells.Count && used + cells[end].Width <= width)
            {
                used += cells[end].Width;
                end++;
            }

            return BuildRow(cells, 0, end, false);
        }

        // Pads text to exactly 'width' columns with spaces in 'style', placing the text by alignment.
        internal static StyledText Pad(StyledText text, int width, MarkdownColumnAlignment alignment, CellStyle style)
        {
            int gap = width - text.Width;
            if (gap <= 0)
                return text;

            int left = 0;
            if (alignment == MarkdownColumnAlignment.Right)
                left = gap;
            else if (alignment == MarkdownColumnAlignment.Center)
                left = gap / 2;

            int right = gap - left;
            StyledText result = left > 0 ? Text.From(new string(' ', left), style).Append(text) : text;
            if (right > 0)
                result = result.Append(Text.From(new string(' ', right), style));

            return result;
        }

        // Repeats a glyph to fill 'width' columns; a partial glyph at the end is filled with spaces.
        internal static string Repeat(string glyph, int width)
        {
            int glyphWidth = TextFit.Width(glyph);
            if (width <= 0 || glyphWidth <= 0)
                return string.Empty;

            StringBuilder result = new StringBuilder();
            int used = 0;
            while (used + glyphWidth <= width)
            {
                result.Append(glyph);
                used += glyphWidth;
            }

            if (used < width)
                result.Append(' ', width - used);

            return result.ToString();
        }

        internal static int LongestWord(StyledText text)
        {
            int longest = 0;
            int current = 0;
            List<WrapCell> cells = Flatten(text);
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].IsSpace)
                {
                    current = 0;
                    continue;
                }

                current += cells[i].Width;
                if (current > longest)
                    longest = current;
            }

            return longest;
        }

        private static CellStyle LastStyle(StyledText text)
        {
            IReadOnlyList<StyledSpan> spans = text.Spans;
            return spans.Count > 0 ? spans[spans.Count - 1].Style : CellStyle.Default;
        }

        private static int SkipSpaces(List<WrapCell> cells, int index)
        {
            while (index < cells.Count && cells[index].IsSpace)
                index++;

            return index;
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
                    if (cluster.Text == "\t")
                    {
                        for (int t = 0; t < 4; t++)
                            cells.Add(new WrapCell(" ", 1, span.Style, true));

                        continue;
                    }

                    cells.Add(new WrapCell(cluster.Text, cluster.Width, span.Style, cluster.Text == " "));
                }
            }

            return cells;
        }

        private static StyledText BuildRow(List<WrapCell> cells, int start, int end)
        {
            return BuildRow(cells, start, end, true);
        }

        private static StyledText BuildRow(List<WrapCell> cells, int start, int end, bool trimTrailingSpaces)
        {
            if (trimTrailingSpaces)
            {
                while (end > start && cells[end - 1].IsSpace)
                    end--;
            }

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

            if (hasRun && run.Length > 0)
                spans.Add(new StyledSpan(run.ToString(), runStyle));

            if (spans.Count == 0)
                return StyledText.Empty;

            return new StyledText(spans);
        }
    }
}
