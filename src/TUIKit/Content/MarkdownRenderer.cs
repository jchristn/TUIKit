namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// Renders a practical subset of Markdown into styled lines suitable for a pane: ATX headings,
    /// unordered lists, block quotes, fenced code blocks, horizontal rules, and the inline spans
    /// <c>**bold**</c>, <c>*italic*</c>, <c>`code`</c>, and <c>~~strike~~</c>. Nesting of inline spans
    /// is not supported. All members are thread-safe.
    /// </summary>
    public static class MarkdownRenderer
    {
        /// <summary>
        /// Renders Markdown into one styled text per output line, using <see cref="MarkdownStyles.Default"/>.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <returns>The rendered lines. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
        public static IReadOnlyList<StyledText> Render(string markdown)
        {
            return Render(markdown, MarkdownStyles.Default);
        }

        /// <summary>
        /// Renders Markdown into one styled text per output line with the supplied element styles (for
        /// example <see cref="MarkdownStyles.FromTheme"/>).
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="styles">The element styles. Must not be null.</param>
        /// <returns>The rendered lines. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IReadOnlyList<StyledText> Render(string markdown, MarkdownStyles styles)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));
            if (styles == null)
                throw new ArgumentNullException(nameof(styles));

            List<StyledText> output = new List<StyledText>();
            string[] lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            bool inCodeBlock = false;

            for (int i = 0; i < lines.Length; i++)
            {
                StyledText? rendered = RenderLine(lines[i], styles, ref inCodeBlock);
                if (rendered != null)
                    output.Add(rendered);
            }

            if (output.Count == 0)
                output.Add(StyledText.Empty);

            return output;
        }

        /// <summary>
        /// Renders one source line of a Markdown block, carrying fenced-code state across calls, so a
        /// stream can be rendered incrementally line by line with output identical to
        /// <see cref="Render(string, MarkdownStyles)"/> over the whole block.
        /// </summary>
        /// <param name="line">One source line without its line terminator. Must not be null.</param>
        /// <param name="styles">The element styles. Must not be null.</param>
        /// <param name="inCodeBlock">The fenced-code state: false at the start of a block; updated by fence lines.</param>
        /// <returns>The rendered line, or null when the line produces no output (a code fence).</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static StyledText? RenderLine(string line, MarkdownStyles styles, ref bool inCodeBlock)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (styles == null)
                throw new ArgumentNullException(nameof(styles));

            string trimmed = line.TrimStart();
            CellStyle text = styles.Text;

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                return null;
            }

            if (inCodeBlock)
                return Text.From("  " + line, MarkdownStyles.Overlay(text, styles.CodeBlock));

            if (IsHorizontalRule(trimmed))
                return Text.From(new string('\u2500', 24), MarkdownStyles.Overlay(text, styles.Rule));

            if (trimmed.StartsWith("#", StringComparison.Ordinal))
                return RenderHeading(trimmed, styles);

            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                CellStyle quote = MarkdownStyles.Overlay(text, styles.Quote);
                return Text.From("\u2502 ", quote).Append(RenderInline(trimmed.Substring(2), quote, styles));
            }

            int indent = line.Length - trimmed.Length;
            string pad = indent > 0 ? new string(' ', Math.Min(indent, 8)) : string.Empty;

            if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal) || IsTaskItem(trimmed))
            {
                bool done = trimmed.Length > 3 && (trimmed[3] == 'x' || trimmed[3] == 'X');
                CellStyle mark = MarkdownStyles.Overlay(text, done ? styles.TaskDone : styles.TaskOpen);
                return Text.From(pad + (done ? "\u2611 " : "\u2610 "), mark)
                    .Append(RenderInline(trimmed.Substring(6), text, styles));
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                return Text.From(pad + "\u2022 ", MarkdownStyles.Overlay(text, styles.Bullet))
                    .Append(RenderInline(trimmed.Substring(2), text, styles));
            }

            int orderedLength = OrderedMarkerLength(trimmed);
            if (orderedLength > 0)
            {
                return Text.From(pad + trimmed.Substring(0, orderedLength), MarkdownStyles.Overlay(text, styles.Bullet))
                    .Append(RenderInline(trimmed.Substring(orderedLength), text, styles));
            }

            if (trimmed.IndexOf('|') >= 0)
                return RenderTableRow(trimmed, styles);

            return RenderInline(line, text, styles);
        }

        private static bool IsTaskItem(string trimmed)
        {
            return trimmed.StartsWith("- [x] ", StringComparison.Ordinal)
                || trimmed.StartsWith("- [X] ", StringComparison.Ordinal);
        }

        private static int OrderedMarkerLength(string trimmed)
        {
            int i = 0;
            while (i < trimmed.Length && trimmed[i] >= '0' && trimmed[i] <= '9')
                i++;

            if (i > 0 && i + 1 < trimmed.Length && trimmed[i] == '.' && trimmed[i + 1] == ' ')
                return i + 2;

            return 0;
        }

        private static StyledText RenderTableRow(string trimmed, MarkdownStyles styles)
        {
            CellStyle muted = MarkdownStyles.Overlay(styles.Text, styles.Rule);

            bool separator = trimmed.Length > 0;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c != '-' && c != '|' && c != ':' && c != ' ')
                {
                    separator = false;
                    break;
                }
            }

            if (separator)
                return Text.From(new string('\u2500', Math.Min(24, Math.Max(1, trimmed.Length))), muted);

            string body = trimmed.Trim('|');
            string[] cells = body.Split('|');
            StyledText row = StyledText.Empty;
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    row = row.Append(Text.From(" \u2502 ", muted));

                row = row.Append(RenderInline(cells[i].Trim(), styles.Text, styles));
            }

            return row;
        }

        /// <summary>
        /// Parses a single line of inline Markdown into styled text.
        /// </summary>
        /// <param name="text">The inline source. Must not be null.</param>
        /// <param name="baseStyle">The base style to apply to unmarked text.</param>
        /// <returns>The styled text. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static StyledText RenderInline(string text, CellStyle baseStyle)
        {
            return RenderInline(text, baseStyle, MarkdownStyles.Default);
        }

        /// <summary>
        /// Parses a single line of inline Markdown into styled text with the supplied element styles.
        /// </summary>
        /// <param name="text">The inline source. Must not be null.</param>
        /// <param name="baseStyle">The base style to apply to unmarked text.</param>
        /// <param name="styles">The element styles (inline code uses <see cref="MarkdownStyles.InlineCode"/>). Must not be null.</param>
        /// <returns>The styled text. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="styles"/> is null.</exception>
        public static StyledText RenderInline(string text, CellStyle baseStyle, MarkdownStyles styles)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (styles == null)
                throw new ArgumentNullException(nameof(styles));

            List<StyledSpan> spans = new List<StyledSpan>();
            System.Text.StringBuilder plain = new System.Text.StringBuilder();
            int i = 0;

            while (i < text.Length)
            {
                if (Match(text, i, "`"))
                {
                    FlushPlain(spans, plain, baseStyle);
                    i = EmitDelimited(text, i, "`", spans, MarkdownStyles.Overlay(baseStyle, styles.InlineCode));
                    continue;
                }

                if (Match(text, i, "**"))
                {
                    FlushPlain(spans, plain, baseStyle);
                    i = EmitDelimited(text, i, "**", spans, baseStyle.WithAttribute(CellAttributes.Bold, true));
                    continue;
                }

                if (Match(text, i, "~~"))
                {
                    FlushPlain(spans, plain, baseStyle);
                    i = EmitDelimited(text, i, "~~", spans, baseStyle.WithAttribute(CellAttributes.Strikethrough, true));
                    continue;
                }

                if (Match(text, i, "*"))
                {
                    FlushPlain(spans, plain, baseStyle);
                    i = EmitDelimited(text, i, "*", spans, baseStyle.WithAttribute(CellAttributes.Italic, true));
                    continue;
                }

                plain.Append(text[i]);
                i++;
            }

            FlushPlain(spans, plain, baseStyle);
            if (spans.Count == 0)
                return StyledText.Empty;

            return new StyledText(spans);
        }

        private static StyledText RenderHeading(string trimmed, MarkdownStyles styles)
        {
            int level = 0;
            while (level < trimmed.Length && trimmed[level] == '#')
                level++;

            string content = trimmed.Substring(level).TrimStart();
            CellStyle style = MarkdownStyles.Overlay(styles.Text, level <= 1 ? styles.Heading1 : styles.Heading);
            return RenderInline(content, style, styles);
        }

        private static bool IsHorizontalRule(string trimmed)
        {
            if (trimmed.Length < 3)
                return false;

            char first = trimmed[0];
            if (first != '-' && first != '*' && first != '_')
                return false;

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] != first)
                    return false;
            }

            return true;
        }

        private static int EmitDelimited(string text, int start, string delimiter, List<StyledSpan> spans, CellStyle style)
        {
            int contentStart = start + delimiter.Length;
            int close = text.IndexOf(delimiter, contentStart, StringComparison.Ordinal);
            if (close < 0)
            {
                spans.Add(new StyledSpan(delimiter, style));
                return contentStart;
            }

            string content = text.Substring(contentStart, close - contentStart);
            spans.Add(new StyledSpan(content, style));
            return close + delimiter.Length;
        }

        private static bool Match(string text, int index, string token)
        {
            if (index + token.Length > text.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (text[index + i] != token[i])
                    return false;
            }

            return true;
        }

        private static void FlushPlain(List<StyledSpan> spans, System.Text.StringBuilder plain, CellStyle style)
        {
            if (plain.Length == 0)
                return;

            spans.Add(new StyledSpan(plain.ToString(), style));
            plain.Clear();
        }
    }
}
