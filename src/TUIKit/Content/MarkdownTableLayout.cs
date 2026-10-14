namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    // Renders a GFM table at a fixed width: content-sized columns, borders from MarkdownTableGlyphs, alignment,
    // cell overflow, and the narrow-width fallbacks. Every row returned is at most the width.
    internal static class MarkdownTableLayout
    {
        internal static List<StyledText> Layout(MarkdownTableBlock table, int width, CellStyle text, MarkdownRenderOptions options)
        {
            MarkdownStyles styles = options.Styles;
            MarkdownTableOptions tableOptions = options.Table;
            int columns = table.Header.Count;

            CellStyle cellStyle = MarkdownStyles.Overlay(text, styles.TableCell);
            CellStyle headerStyle = MarkdownStyles.Overlay(cellStyle, styles.TableHeader);
            CellStyle firstStyle = MarkdownStyles.Overlay(cellStyle, styles.TableFirstColumn);

            StyledText[] header = new StyledText[columns];
            for (int c = 0; c < columns; c++)
                header[c] = MarkdownInlineParser.Parse(table.Header[c], headerStyle, options);

            List<StyledText[]> body = new List<StyledText[]>(table.Rows.Count);
            for (int r = 0; r < table.Rows.Count; r++)
            {
                StyledText[] cells = new StyledText[columns];
                for (int c = 0; c < columns; c++)
                    cells[c] = MarkdownInlineParser.Parse(table.Rows[r][c], c == 0 ? firstStyle : cellStyle, options);

                body.Add(cells);
            }

            int cap = tableOptions.MaxColumnWidth > 0 ? Math.Max(tableOptions.MaxColumnWidth, tableOptions.MinColumnWidth) : int.MaxValue;
            bool wrapCells = tableOptions.CellOverflow == MarkdownOverflow.Wrap;
            int[] natural = new int[columns];
            int[] minimum = new int[columns];
            for (int c = 0; c < columns; c++)
            {
                int widest = header[c].Width;
                int longestWord = MarkdownTextFit.LongestWord(header[c]);
                for (int r = 0; r < body.Count; r++)
                {
                    widest = Math.Max(widest, body[r][c].Width);
                    longestWord = Math.Max(longestWord, MarkdownTextFit.LongestWord(body[r][c]));
                }

                natural[c] = Math.Min(cap, Math.Max(tableOptions.MinColumnWidth, widest));
                minimum[c] = wrapCells ? Math.Max(tableOptions.MinColumnWidth, longestWord) : tableOptions.MinColumnWidth;
                minimum[c] = Math.Min(minimum[c], natural[c]);
            }

            int padding = tableOptions.CellPadding;
            int chrome = (tableOptions.ShowOuterBorder ? 2 : 0) + (columns - 1) + (2 * padding * columns);
            int budget = width - chrome;

            int[]? widths = MarkdownColumnSolver.Solve(natural, minimum, budget, tableOptions.Expand);
            if (widths == null)
            {
                switch (tableOptions.Fallback)
                {
                    case MarkdownTableFallback.Shrink:
                        widths = MarkdownColumnSolver.Even(columns, budget);
                        break;
                    case MarkdownTableFallback.Clip:
                        widths = minimum;
                        break;
                    default:
                        return Stack(header, body, width, text, headerStyle, options);
                }
            }
            else if (tableOptions.Fallback == MarkdownTableFallback.Stack && wrapCells && tableOptions.MaxRowLines > 0
                && TallestRow(body, widths) > tableOptions.MaxRowLines)
            {
                return Stack(header, body, width, text, headerStyle, options);
            }

            List<StyledText> rows = Grid(table, header, body, widths, text, headerStyle, cellStyle, firstStyle, options);
            for (int i = 0; i < rows.Count; i++)
                rows[i] = MarkdownTextFit.Clip(rows[i], width);

            return rows;
        }

        private static int TallestRow(List<StyledText[]> body, int[] widths)
        {
            int tallest = 0;
            for (int r = 0; r < body.Count; r++)
            {
                for (int c = 0; c < widths.Length; c++)
                    tallest = Math.Max(tallest, MarkdownTextFit.Wrap(body[r][c], widths[c]).Count);
            }

            return tallest;
        }

        private static List<StyledText> Grid(
            MarkdownTableBlock table,
            StyledText[] header,
            List<StyledText[]> body,
            int[] widths,
            CellStyle text,
            CellStyle headerStyle,
            CellStyle cellStyle,
            CellStyle firstStyle,
            MarkdownRenderOptions options)
        {
            MarkdownTableOptions tableOptions = options.Table;
            MarkdownTableGlyphs glyphs = tableOptions.Glyphs;
            CellStyle borderStyle = MarkdownStyles.Overlay(text, options.Styles.TableBorder);
            bool outer = tableOptions.ShowOuterBorder;
            bool lines = glyphs.Horizontal != " ";
            List<StyledText> rows = new List<StyledText>();

            if (outer && lines)
                rows.Add(Line(widths, glyphs.TopLeft, glyphs.TopTee, glyphs.TopRight, glyphs, outer, tableOptions.CellPadding, borderStyle));

            CellStyle[] headerStyles = new CellStyle[widths.Length];
            for (int c = 0; c < widths.Length; c++)
                headerStyles[c] = headerStyle;

            rows.AddRange(Row(header, widths, table.Alignments, headerStyles, glyphs, outer, borderStyle, options));

            if (tableOptions.ShowHeaderSeparator && lines)
                rows.Add(Line(widths, glyphs.LeftTee, glyphs.Cross, glyphs.RightTee, glyphs, outer, tableOptions.CellPadding, borderStyle));

            CellStyle[] bodyStyles = new CellStyle[widths.Length];
            for (int c = 0; c < widths.Length; c++)
                bodyStyles[c] = c == 0 ? firstStyle : cellStyle;

            for (int r = 0; r < body.Count; r++)
            {
                if (r > 0 && tableOptions.ShowRowSeparators && lines)
                    rows.Add(Line(widths, glyphs.LeftTee, glyphs.Cross, glyphs.RightTee, glyphs, outer, tableOptions.CellPadding, borderStyle));

                rows.AddRange(Row(body[r], widths, table.Alignments, bodyStyles, glyphs, outer, borderStyle, options));
            }

            if (outer && lines)
                rows.Add(Line(widths, glyphs.BottomLeft, glyphs.BottomTee, glyphs.BottomRight, glyphs, outer, tableOptions.CellPadding, borderStyle));

            return rows;
        }

        private static StyledText Line(int[] widths, string left, string middle, string right, MarkdownTableGlyphs glyphs, bool outer, int padding, CellStyle borderStyle)
        {
            StringBuilder line = new StringBuilder();
            if (outer)
                line.Append(left);

            for (int c = 0; c < widths.Length; c++)
            {
                line.Append(MarkdownTextFit.Repeat(glyphs.Horizontal, widths[c] + (2 * padding)));
                if (c < widths.Length - 1)
                    line.Append(middle);
            }

            if (outer)
                line.Append(right);

            return Text.From(line.ToString(), borderStyle);
        }

        private static List<StyledText> Row(
            StyledText[] cells,
            int[] widths,
            IReadOnlyList<MarkdownColumnAlignment> alignments,
            CellStyle[] cellStyles,
            MarkdownTableGlyphs glyphs,
            bool outer,
            CellStyle borderStyle,
            MarkdownRenderOptions options)
        {
            MarkdownTableOptions tableOptions = options.Table;
            int columns = widths.Length;
            List<StyledText>[] fitted = new List<StyledText>[columns];
            int height = 1;
            for (int c = 0; c < columns; c++)
            {
                fitted[c] = MarkdownTextFit.Fit(cells[c], widths[c], tableOptions.CellOverflow, options.Ellipsis);
                height = Math.Max(height, fitted[c].Count);
            }

            StyledText vertical = Text.From(glyphs.Vertical, borderStyle);
            string pad = new string(' ', tableOptions.CellPadding);
            List<StyledText> rows = new List<StyledText>(height);
            for (int k = 0; k < height; k++)
            {
                StyledText row = outer ? vertical : StyledText.Empty;
                for (int c = 0; c < columns; c++)
                {
                    MarkdownColumnAlignment alignment = c < alignments.Count ? alignments[c] : MarkdownColumnAlignment.Left;
                    StyledText content = k < fitted[c].Count ? fitted[c][k] : StyledText.Empty;
                    if (pad.Length > 0)
                        row = row.Append(Text.From(pad, cellStyles[c]));

                    row = row.Append(MarkdownTextFit.Pad(content, widths[c], alignment, cellStyles[c]));
                    if (pad.Length > 0)
                        row = row.Append(Text.From(pad, cellStyles[c]));

                    if (c < columns - 1 || outer)
                        row = row.Append(vertical);
                }

                rows.Add(row);
            }

            return rows;
        }

        // Each body row becomes a record of "Header: value" rows; records are separated by a rule.
        private static List<StyledText> Stack(StyledText[] header, List<StyledText[]> body, int width, CellStyle text, CellStyle headerStyle, MarkdownRenderOptions options)
        {
            MarkdownTableOptions tableOptions = options.Table;
            MarkdownTableGlyphs glyphs = tableOptions.Glyphs;
            CellStyle borderStyle = MarkdownStyles.Overlay(text, options.Styles.TableBorder);
            List<StyledText> rows = new List<StyledText>();
            int hanging = Math.Min(2, Math.Max(0, width - 1));
            StyledText indent = Text.From(new string(' ', hanging), text);

            if (body.Count == 0)
            {
                for (int c = 0; c < header.Length; c++)
                    rows.AddRange(MarkdownTextFit.Fit(header[c], width, tableOptions.CellOverflow, options.Ellipsis));

                return rows;
            }

            for (int r = 0; r < body.Count; r++)
            {
                if (r > 0)
                {
                    string rule = glyphs.Horizontal != " " ? MarkdownTextFit.Repeat(glyphs.Horizontal, width) : string.Empty;
                    rows.Add(rule.Length > 0 ? Text.From(rule, borderStyle) : StyledText.Empty);
                }

                for (int c = 0; c < header.Length; c++)
                {
                    StyledText record = header[c].Append(Text.From(":", headerStyle)).Append(Text.From(" ", text)).Append(body[r][c]);
                    List<StyledText> fitted = tableOptions.CellOverflow == MarkdownOverflow.Wrap
                        ? MarkdownTextFit.Wrap(record, width, width - hanging)
                        : MarkdownTextFit.Fit(record, width, tableOptions.CellOverflow, options.Ellipsis);
                    for (int f = 0; f < fitted.Count; f++)
                        rows.Add(MarkdownTextFit.Clip(f == 0 ? fitted[f] : indent.Append(fitted[f]), width));
                }
            }

            return rows;
        }
    }
}
