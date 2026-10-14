namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    // Parses the CommonMark and GFM block subset that model output uses into a MarkdownDocument. Never throws on
    // malformed input: anything that is not a recognized block becomes paragraph text.
    internal static class MarkdownParser
    {
        private const int _TabWidth = 4;

        internal static MarkdownDocument Parse(string markdown)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));

            string[] raw = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            List<string> lines = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
                lines.Add(ExpandLeadingTabs(raw[i]));

            return new MarkdownDocument(ParseBlocks(lines));
        }

        internal static List<MarkdownBlock> ParseBlocks(List<string> lines)
        {
            List<MarkdownBlock> blocks = new List<MarkdownBlock>();
            int i = 0;
            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                {
                    i++;
                    continue;
                }

                string trimmed = line.TrimStart(' ');
                int indent = line.Length - trimmed.Length;

                if (TryFence(trimmed, out char fenceChar, out int fenceLength, out string language))
                {
                    i = ParseFence(lines, i + 1, indent, fenceChar, fenceLength, language, blocks);
                    continue;
                }

                if (TryAtxHeading(trimmed, out int level, out string headingText))
                {
                    blocks.Add(new MarkdownHeadingBlock(level, headingText));
                    i++;
                    continue;
                }

                if (IsRule(trimmed))
                {
                    blocks.Add(new MarkdownRuleBlock());
                    i++;
                    continue;
                }

                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    i = ParseQuote(lines, i, blocks);
                    continue;
                }

                if (TryMarkdownListMarker(line, out MarkdownListMarker? marker) && marker != null)
                {
                    i = ParseList(lines, i, marker, blocks);
                    continue;
                }

                if (IsTableStart(lines, i))
                {
                    i = ParseTable(lines, i, blocks);
                    continue;
                }

                i = ParseParagraph(lines, i, blocks);
            }

            return blocks;
        }

        private static int ParseFence(List<string> lines, int start, int indent, char fenceChar, int fenceLength, string language, List<MarkdownBlock> blocks)
        {
            List<string> code = new List<string>();
            int i = start;
            while (i < lines.Count)
            {
                string line = lines[i];
                string trimmed = line.TrimStart(' ');
                if (IsClosingFence(trimmed, fenceChar, fenceLength))
                {
                    i++;
                    break;
                }

                int strip = 0;
                while (strip < indent && strip < line.Length && line[strip] == ' ')
                    strip++;

                code.Add(line.Substring(strip));
                i++;
            }

            blocks.Add(new MarkdownCodeBlock(language, code));
            return i;
        }

        private static int ParseQuote(List<string> lines, int start, List<MarkdownBlock> blocks)
        {
            List<string> inner = new List<string>();
            int i = start;
            bool lastWasText = false;
            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                    break;

                string trimmed = line.TrimStart(' ');
                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    string content = trimmed.Substring(1);
                    if (content.StartsWith(" ", StringComparison.Ordinal))
                        content = content.Substring(1);

                    inner.Add(content);
                    lastWasText = !IsBlank(content);
                    i++;
                    continue;
                }

                // Lazy continuation: a plain line right after quoted text continues the quoted paragraph.
                if (lastWasText && !StartsBlock(lines, i))
                {
                    inner.Add(trimmed);
                    i++;
                    continue;
                }

                break;
            }

            blocks.Add(new MarkdownQuoteBlock(ParseBlocks(inner)));
            return i;
        }

        private static int ParseList(List<string> lines, int start, MarkdownListMarker first, List<MarkdownBlock> blocks)
        {
            List<MarkdownListItem> items = new List<MarkdownListItem>();
            bool loose = false;
            int i = start;
            MarkdownListMarker current = first;

            while (true)
            {
                List<string> itemLines = new List<string>();
                itemLines.Add(current.Content);
                i++;

                bool sawBlank = false;
                bool blankInsideItem = false;
                MarkdownListMarker? next = null;

                while (i < lines.Count)
                {
                    string line = lines[i];
                    if (IsBlank(line))
                    {
                        sawBlank = true;
                        itemLines.Add(string.Empty);
                        i++;
                        continue;
                    }

                    int indent = line.Length - line.TrimStart(' ').Length;
                    if (indent >= current.ContentColumn)
                    {
                        if (sawBlank)
                            blankInsideItem = true;

                        itemLines.Add(line.Substring(current.ContentColumn));
                        sawBlank = false;
                        i++;
                        continue;
                    }

                    if (TryMarkdownListMarker(line, out MarkdownListMarker? sibling) && sibling != null && sibling.IsSameType(first))
                    {
                        next = sibling;
                        if (sawBlank)
                            loose = true;

                        break;
                    }

                    if (!sawBlank && !StartsBlock(lines, i))
                    {
                        // Lazy continuation of the item's paragraph.
                        itemLines.Add(line.TrimStart(' '));
                        i++;
                        continue;
                    }

                    break;
                }

                while (itemLines.Count > 0 && IsBlank(itemLines[itemLines.Count - 1]))
                    itemLines.RemoveAt(itemLines.Count - 1);

                if (blankInsideItem)
                    loose = true;

                items.Add(BuildItem(itemLines));

                if (next == null)
                    break;

                current = next;
            }

            blocks.Add(new MarkdownListBlock(first.Ordered, first.Number, first.Delimiter, loose, items));
            return i;
        }

        private static MarkdownListItem BuildItem(List<string> itemLines)
        {
            bool isTask = false;
            bool isDone = false;
            if (itemLines.Count > 0)
            {
                string head = itemLines[0];
                if (head.Length >= 3 && head[0] == '[' && head[2] == ']' && (head.Length == 3 || head[3] == ' ')
                    && (head[1] == ' ' || head[1] == 'x' || head[1] == 'X'))
                {
                    isTask = true;
                    isDone = head[1] != ' ';
                    itemLines[0] = head.Length > 4 ? head.Substring(4) : string.Empty;
                }
            }

            return new MarkdownListItem(isTask, isDone, ParseBlocks(itemLines));
        }

        private static int ParseTable(List<string> lines, int start, List<MarkdownBlock> blocks)
        {
            List<string> header = SplitRow(lines[start]);
            List<MarkdownColumnAlignment> alignments = new List<MarkdownColumnAlignment>();
            List<string> delimiter = SplitRow(lines[start + 1]);
            for (int c = 0; c < delimiter.Count; c++)
                alignments.Add(AlignmentOf(delimiter[c].Trim()));

            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>();
            int i = start + 2;
            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line) || line.IndexOf('|') < 0)
                    break;

                string trimmed = line.TrimStart(' ');
                if (TryFence(trimmed, out _, out _, out _) || trimmed.StartsWith(">", StringComparison.Ordinal) || TryAtxHeading(trimmed, out _, out _))
                    break;

                List<string> cells = SplitRow(line);
                while (cells.Count < header.Count)
                    cells.Add(string.Empty);
                if (cells.Count > header.Count)
                    cells.RemoveRange(header.Count, cells.Count - header.Count);

                rows.Add(cells);
                i++;
            }

            blocks.Add(new MarkdownTableBlock(header, alignments, rows));
            return i;
        }

        private static int ParseParagraph(List<string> lines, int start, List<MarkdownBlock> blocks)
        {
            List<string> segments = new List<string>();
            StringBuilder segment = new StringBuilder();
            int i = start;
            bool first = true;

            while (i < lines.Count)
            {
                string line = lines[i];
                if (IsBlank(line))
                    break;

                string trimmed = line.TrimStart(' ');
                if (!first)
                {
                    int setext = SetextLevel(trimmed);
                    if (setext > 0)
                    {
                        AppendSegment(segments, segment);
                        blocks.Add(new MarkdownHeadingBlock(setext, string.Join(" ", segments)));
                        return i + 1;
                    }

                    if (StartsBlock(lines, i))
                        break;
                }

                // A hard break needs a following line in the same paragraph; at the end it is literal.
                bool hasNext = i + 1 < lines.Count && !IsBlank(lines[i + 1]) && !StartsBlock(lines, i + 1);
                bool hardBreak = hasNext && (line.EndsWith("  ", StringComparison.Ordinal)
                    || (line.EndsWith("\\", StringComparison.Ordinal) && !line.EndsWith("\\\\", StringComparison.Ordinal)));
                string content = trimmed.TrimEnd(' ');
                if (hardBreak && content.EndsWith("\\", StringComparison.Ordinal))
                    content = content.Substring(0, content.Length - 1);

                if (segment.Length > 0)
                    segment.Append(' ');
                segment.Append(content);

                if (hardBreak)
                    AppendSegment(segments, segment);

                first = false;
                i++;
            }

            AppendSegment(segments, segment);
            if (segments.Count > 0)
                blocks.Add(new MarkdownParagraphBlock(segments));

            return i;
        }

        private static void AppendSegment(List<string> segments, StringBuilder segment)
        {
            if (segment.Length == 0)
                return;

            segments.Add(segment.ToString());
            segment.Clear();
        }

        // True when the line at index starts a block that can interrupt a paragraph.
        private static bool StartsBlock(List<string> lines, int index)
        {
            string line = lines[index];
            string trimmed = line.TrimStart(' ');
            if (TryFence(trimmed, out _, out _, out _))
                return true;
            if (TryAtxHeading(trimmed, out _, out _))
                return true;
            if (IsRule(trimmed))
                return true;
            if (trimmed.StartsWith(">", StringComparison.Ordinal))
                return true;
            if (TryMarkdownListMarker(line, out MarkdownListMarker? marker) && marker != null && marker.Content.Trim().Length > 0
                && (!marker.Ordered || marker.Number == 1))
                return true;

            return IsTableStart(lines, index);
        }

        private static bool TryFence(string trimmed, out char fenceChar, out int fenceLength, out string language)
        {
            fenceChar = '\0';
            fenceLength = 0;
            language = string.Empty;
            if (trimmed.Length < 3 || (trimmed[0] != '`' && trimmed[0] != '~'))
                return false;

            char c = trimmed[0];
            int run = 0;
            while (run < trimmed.Length && trimmed[run] == c)
                run++;

            if (run < 3)
                return false;

            string info = trimmed.Substring(run).Trim();
            if (c == '`' && info.IndexOf('`') >= 0)
                return false;

            fenceChar = c;
            fenceLength = run;
            int space = info.IndexOf(' ');
            language = space >= 0 ? info.Substring(0, space) : info;
            return true;
        }

        private static bool IsClosingFence(string trimmed, char fenceChar, int fenceLength)
        {
            int run = 0;
            while (run < trimmed.Length && trimmed[run] == fenceChar)
                run++;

            return run >= fenceLength && trimmed.Substring(run).Trim().Length == 0;
        }

        private static bool TryAtxHeading(string trimmed, out int level, out string text)
        {
            level = 0;
            text = string.Empty;
            while (level < trimmed.Length && trimmed[level] == '#')
                level++;

            if (level == 0 || level > 6)
                return false;

            if (level < trimmed.Length && trimmed[level] != ' ')
                return false;

            string content = trimmed.Substring(level).Trim();

            // A closing run of '#' is removed when it is the whole content or follows a space.
            int end = content.Length;
            while (end > 0 && content[end - 1] == '#')
                end--;

            if (end == 0)
                content = string.Empty;
            else if (end < content.Length && content[end - 1] == ' ')
                content = content.Substring(0, end).TrimEnd();

            text = content;
            return true;
        }

        private static bool IsRule(string trimmed)
        {
            if (trimmed.Length < 3)
                return false;

            char c = trimmed[0];
            if (c != '-' && c != '*' && c != '_')
                return false;

            int count = 0;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                if (ch == c)
                    count++;
                else if (ch != ' ')
                    return false;
            }

            return count >= 3;
        }

        private static int SetextLevel(string trimmed)
        {
            string content = trimmed.TrimEnd(' ');
            if (content.Length == 0)
                return 0;

            char c = content[0];
            if (c != '=' && c != '-')
                return 0;

            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] != c)
                    return 0;
            }

            return c == '=' ? 1 : 2;
        }

        private static bool TryMarkdownListMarker(string line, out MarkdownListMarker? marker)
        {
            marker = null;
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
                indent++;

            if (indent >= line.Length)
                return false;

            char c = line[indent];
            int markerEnd;
            bool ordered = false;
            int number = 0;
            char delimiter = '\0';

            if (c == '-' || c == '*' || c == '+')
            {
                markerEnd = indent + 1;
                delimiter = c;
            }
            else if (c >= '0' && c <= '9')
            {
                int digits = indent;
                while (digits < line.Length && line[digits] >= '0' && line[digits] <= '9' && digits - indent < 9)
                    digits++;

                if (digits >= line.Length || (line[digits] != '.' && line[digits] != ')'))
                    return false;

                if (!int.TryParse(line.Substring(indent, digits - indent), out number))
                    return false;

                ordered = true;
                delimiter = line[digits];
                markerEnd = digits + 1;
            }
            else
            {
                return false;
            }

            if (markerEnd < line.Length && line[markerEnd] != ' ')
                return false;

            int spaces = 0;
            while (markerEnd + spaces < line.Length && line[markerEnd + spaces] == ' ')
                spaces++;

            string content;
            int contentColumn;
            if (markerEnd + spaces >= line.Length)
            {
                content = string.Empty;
                contentColumn = markerEnd + 1;
            }
            else if (spaces > 4)
            {
                // Content indented five or more counts as one space of padding plus indented text.
                contentColumn = markerEnd + 1;
                content = line.Substring(contentColumn);
            }
            else
            {
                contentColumn = markerEnd + spaces;
                content = line.Substring(contentColumn);
            }

            marker = new MarkdownListMarker(ordered, number, delimiter, contentColumn, content);
            return true;
        }

        private static bool IsTableStart(List<string> lines, int index)
        {
            if (index + 1 >= lines.Count)
                return false;

            string line = lines[index];
            if (line.IndexOf('|') < 0)
                return false;

            string delimiterLine = lines[index + 1];
            if (!IsDelimiterRow(delimiterLine))
                return false;

            return SplitRow(line).Count == SplitRow(delimiterLine).Count;
        }

        private static bool IsDelimiterRow(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.IndexOf('-') < 0)
                return false;

            List<string> cells = SplitRow(trimmed);
            if (cells.Count == 0)
                return false;

            // A single-column delimiter row needs a pipe, or "---" alone would read as a table.
            if (cells.Count == 1 && trimmed.IndexOf('|') < 0)
                return false;

            for (int c = 0; c < cells.Count; c++)
            {
                string cell = cells[c].Trim();
                if (cell.Length == 0)
                    return false;

                int start = cell[0] == ':' ? 1 : 0;
                int end = cell[cell.Length - 1] == ':' ? cell.Length - 1 : cell.Length;
                if (end - start < 1)
                    return false;

                for (int k = start; k < end; k++)
                {
                    if (cell[k] != '-')
                        return false;
                }
            }

            return true;
        }

        private static MarkdownColumnAlignment AlignmentOf(string delimiterCell)
        {
            bool left = delimiterCell.StartsWith(":", StringComparison.Ordinal);
            bool right = delimiterCell.EndsWith(":", StringComparison.Ordinal);
            if (left && right)
                return MarkdownColumnAlignment.Center;
            if (right)
                return MarkdownColumnAlignment.Right;

            return MarkdownColumnAlignment.Left;
        }

        // Splits a table row on unescaped pipes outside code spans, dropping one leading and one trailing pipe.
        private static List<string> SplitRow(string line)
        {
            string trimmed = line.Trim();
            List<string> cells = new List<string>();
            StringBuilder cell = new StringBuilder();
            int i = 0;
            if (trimmed.StartsWith("|", StringComparison.Ordinal))
                i = 1;

            int end = trimmed.Length;
            if (end > i && trimmed[end - 1] == '|' && !(end >= 2 && trimmed[end - 2] == '\\'))
                end--;

            while (i < end)
            {
                char c = trimmed[i];
                if (c == '\\' && i + 1 < end && trimmed[i + 1] == '|')
                {
                    cell.Append('|');
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int run = 0;
                    while (i + run < end && trimmed[i + run] == '`')
                        run++;

                    int close = FindBacktickRun(trimmed, i + run, end, run);
                    if (close >= 0)
                    {
                        cell.Append(trimmed, i, close + run - i);
                        i = close + run;
                        continue;
                    }

                    cell.Append(trimmed, i, run);
                    i += run;
                    continue;
                }

                if (c == '|')
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Clear();
                    i++;
                    continue;
                }

                cell.Append(c);
                i++;
            }

            cells.Add(cell.ToString().Trim());
            return cells;
        }

        private static int FindBacktickRun(string text, int from, int end, int length)
        {
            int i = from;
            while (i < end)
            {
                if (text[i] != '`')
                {
                    i++;
                    continue;
                }

                int run = 0;
                while (i + run < end && text[i + run] == '`')
                    run++;

                if (run == length)
                    return i;

                i += run;
            }

            return -1;
        }

        private static bool IsBlank(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != ' ' && line[i] != '\t')
                    return false;
            }

            return true;
        }

        private static string ExpandLeadingTabs(string line)
        {
            if (line.IndexOf('\t') < 0)
                return line;

            StringBuilder result = new StringBuilder();
            int column = 0;
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                if (line[i] == '\t')
                {
                    int spaces = _TabWidth - (column % _TabWidth);
                    result.Append(' ', spaces);
                    column += spaces;
                }
                else
                {
                    result.Append(' ');
                    column++;
                }

                i++;
            }

            result.Append(line, i, line.Length - i);
            return result.ToString();
        }
    }
}
