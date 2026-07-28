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
        /// Renders Markdown into one styled text per output line.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <returns>The rendered lines. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
        public static IReadOnlyList<StyledText> Render(string markdown)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));

            List<StyledText> output = new List<StyledText>();
            string[] lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            bool inCodeBlock = false;
            CellStyle codeStyle = CellStyle.Default.WithForeground(Color.FromPalette(2));

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock)
                {
                    output.Add(Text.From("  " + line, codeStyle));
                    continue;
                }

                if (IsHorizontalRule(trimmed))
                {
                    output.Add(Text.From(new string('─', 24), CellStyle.Default.WithForeground(Color.FromPalette(8))));
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    output.Add(RenderHeading(trimmed));
                    continue;
                }

                if (trimmed.StartsWith("> ", StringComparison.Ordinal))
                {
                    CellStyle quote = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);
                    output.Add(Text.From("│ ", quote).Append(RenderInline(trimmed.Substring(2), quote)));
                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
                {
                    output.Add(Text.From("• ", CellStyle.Default.WithForeground(Color.FromPalette(6)))
                        .Append(RenderInline(trimmed.Substring(2), CellStyle.Default)));
                    continue;
                }

                output.Add(RenderInline(line, CellStyle.Default));
            }

            if (output.Count == 0)
                output.Add(StyledText.Empty);

            return output;
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
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<StyledSpan> spans = new List<StyledSpan>();
            System.Text.StringBuilder plain = new System.Text.StringBuilder();
            int i = 0;

            while (i < text.Length)
            {
                if (Match(text, i, "`"))
                {
                    FlushPlain(spans, plain, baseStyle);
                    i = EmitDelimited(text, i, "`", spans, baseStyle.WithForeground(Color.FromPalette(3)));
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

        private static StyledText RenderHeading(string trimmed)
        {
            int level = 0;
            while (level < trimmed.Length && trimmed[level] == '#')
                level++;

            string content = trimmed.Substring(level).TrimStart();
            CellStyle style = CellStyle.Default
                .WithForeground(Color.FromPalette((byte)(level <= 1 ? 6 : 4)))
                .WithAttribute(CellAttributes.Bold, true);
            return RenderInline(content, style);
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
