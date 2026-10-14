namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;

    /// <summary>
    /// Renders Markdown into styled rows. Two engines are available.
    /// <para>
    /// The width-aware engine (<see cref="Render(string, int, MarkdownRenderOptions)"/>,
    /// <see cref="RenderAsync"/>, and <see cref="MeasureWidth"/>) parses the whole block and lays it out at a
    /// known width: GFM tables with columns sized from their content, nested lists and quotes that keep their
    /// indent when they wrap, setext and ATX headings, fenced code with syntax highlighting, and nested inline
    /// emphasis, links, and escapes. Every visual choice comes from <see cref="MarkdownRenderOptions"/>.
    /// </para>
    /// <para>
    /// The line-oriented engine (<see cref="Render(string)"/>, <see cref="Render(string, MarkdownStyles)"/>,
    /// <see cref="RenderLine"/>, and <see cref="RenderInline(string, CellStyle)"/>) styles one source line at a
    /// time without knowing the width, and is kept unchanged for compatibility: ATX headings, unordered lists,
    /// block quotes, fenced code blocks, horizontal rules, and the inline spans <c>**bold**</c>,
    /// <c>*italic*</c>, <c>`code`</c>, and <c>~~strike~~</c>, without nesting.
    /// </para>
    /// All members are stateless and thread-safe, provided the options passed in are not mutated during a call.
    /// </summary>
    public static class MarkdownRenderer
    {
        private const int _MeasureLimit = 100000;

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
        /// example <see cref="MarkdownStyles.FromTheme(Theming.Theme)"/>).
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
        /// Renders Markdown laid out at a known width with default <see cref="MarkdownRenderOptions"/>.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="width">The available width in columns. Must be 1 or greater.</param>
        /// <returns>The rendered rows, at least one, each at most the layout width. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        public static IReadOnlyList<StyledText> Render(string markdown, int width)
        {
            return Render(markdown, width, new MarkdownRenderOptions());
        }

        /// <summary>
        /// Renders Markdown laid out at a known width. The layout width is <paramref name="width"/>, capped by
        /// <see cref="MarkdownRenderOptions.MaxWidth"/> and raised to <see cref="MarkdownRenderOptions.MinWidth"/>;
        /// no returned row is wider than that. Malformed Markdown never throws: it renders as literal text.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="width">The available width in columns. Must be 1 or greater.</param>
        /// <param name="options">How the output should look. Must not be null. Not modified.</param>
        /// <returns>The rendered rows, at least one. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        public static IReadOnlyList<StyledText> Render(string markdown, int width, MarkdownRenderOptions options)
        {
            Validate(markdown, width, options);
            return RenderCore(markdown, width, options, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously renders Markdown laid out at a known width. Produces the same rows as
        /// <see cref="Render(string, int, MarkdownRenderOptions)"/> and checks the token between top-level blocks,
        /// so a long document can be abandoned. The work runs on the calling thread until it completes.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="width">The available width in columns. Must be 1 or greater.</param>
        /// <param name="options">How the output should look. Must not be null. Not modified.</param>
        /// <param name="token">Cancels the render.</param>
        /// <returns>The rendered rows, at least one. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="token"/> is cancelled.</exception>
        public static Task<IReadOnlyList<StyledText>> RenderAsync(string markdown, int width, MarkdownRenderOptions options, CancellationToken token)
        {
            Validate(markdown, width, options);
            token.ThrowIfCancellationRequested();
            return Task.FromResult(RenderCore(markdown, width, options, token));
        }

        /// <summary>
        /// Returns the natural width of Markdown: the width of its widest row when nothing wraps, capped by
        /// <see cref="MarkdownRenderOptions.MaxWidth"/>. Thematic breaks and code background fill do not count.
        /// Use it to size a modal, a column, or a <see cref="Widgets.MarkdownView"/> from its content; rendering at
        /// the returned width wraps nothing.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="options">How the output should look. Must not be null. Not modified.</param>
        /// <returns>The natural width in columns, 0 for empty input.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> or <paramref name="options"/> is null.</exception>
        public static int MeasureWidth(string markdown, MarkdownRenderOptions options)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            MarkdownRenderOptions measuring = options.Clone();
            measuring.Overflow = MarkdownOverflow.Clip;
            measuring.CodeOverflow = MarkdownOverflow.Clip;
            measuring.Table.CellOverflow = MarkdownOverflow.Clip;
            measuring.Table.Expand = false;
            measuring.FillCodeBackground = false;
            measuring.RuleWidth = 1;
            measuring.MinWidth = 1;

            int width = measuring.ResolveWidth(_MeasureLimit);
            MarkdownLayout layout = new MarkdownLayout(measuring);
            List<StyledText> rows = layout.Layout(MarkdownParser.Parse(markdown), width, CancellationToken.None);
            int widest = 0;
            for (int i = 0; i < rows.Count; i++)
                widest = Math.Max(widest, rows[i].Width);

            return widest;
        }

        /// <summary>
        /// Returns the longest prefix of Markdown still arriving (from a model, a process, or a socket) that is safe
        /// to lay out now, so a table never renders short partial cells or flips from text to a table while it
        /// streams. Re-render the result on each chunk; when the stream ends, render the full text, never the
        /// prefix, or the held-back tail is lost.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two things are held back. A trailing line with no newline yet whose first character after up to three
        /// spaces is <c>|</c> (a table row still arriving). And a trailing complete line that could be a table
        /// header (the same <c>|</c> test) that is not itself a delimiter row and does not follow another table
        /// line: it waits for the next line, so a header becomes a table in one step instead of showing as text
        /// first. While that next line is partial and could still be a delimiter row, both stay held; once it is
        /// clearly not one, or once its newline arrives and it is not a delimiter row, the header is released.
        /// </para>
        /// <para>
        /// Every other partial line (prose, list items, emphasis still open) is returned as it arrived, so text
        /// keeps streaming at the same pace. Indentation counts spaces only and at most three, as GFM does: a tab
        /// or four spaces start an indented code block, not a table, so such a <c>|</c> is not held back.
        /// </para>
        /// <para>
        /// Stateless and thread-safe. Allocation-free when nothing is held back: the input instance is returned.
        /// </para>
        /// </remarks>
        /// <param name="received">The Markdown received so far. Must not be null.</param>
        /// <returns>The safe prefix, without the newline before the held-back part; <see cref="string.Empty"/> when
        /// nothing is safe yet. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="received"/> is null.</exception>
        public static string StreamingPrefix(string received)
        {
            if (received == null)
                throw new ArgumentNullException(nameof(received));

            int lastNewline = received.LastIndexOf('\n');
            int partialStart = lastNewline + 1;
            bool hasPartial = partialStart < received.Length;
            bool partialIsRow = hasPartial && StartsWithPipe(received, partialStart, received.Length);
            if (lastNewline < 0)
                return partialIsRow ? string.Empty : received;

            // The last complete line runs from headerStart up to lastNewline.
            int headerStart = received.LastIndexOf('\n', Math.Max(0, lastNewline - 1)) + 1;
            if (lastNewline == 0)
                headerStart = 0;

            bool headerHeld = IsHeaderCandidate(received, headerStart, lastNewline)
                && (!hasPartial || partialIsRow || CouldBeDelimiter(received, partialStart, received.Length));

            if (headerHeld)
                return headerStart == 0 ? string.Empty : received.Substring(0, headerStart - 1);
            if (partialIsRow)
                return received.Substring(0, lastNewline);

            return received;
        }

        internal static IReadOnlyList<StyledText> RenderCore(string markdown, int width, MarkdownRenderOptions options, CancellationToken token)
        {
            MarkdownDocument document = MarkdownParser.Parse(markdown);
            MarkdownLayout layout = new MarkdownLayout(options);
            return layout.Layout(document, options.ResolveWidth(width), token);
        }

        private static void Validate(string markdown, int width, MarkdownRenderOptions options)
        {
            if (markdown == null)
                throw new ArgumentNullException(nameof(markdown));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be 1 or greater.");
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

        // True when the line [start, end) begins, after at most three spaces, with a pipe.
        private static bool StartsWithPipe(string text, int start, int end)
        {
            int i = start;
            int spaces = 0;
            while (i < end && text[i] == ' ' && spaces < 3)
            {
                i++;
                spaces++;
            }

            return i < end && text[i] == '|';
        }

        // True when the line [start, end) is a GFM table delimiter row: pipes, dashes, colons, and spaces, with at
        // least one dash.
        private static bool IsDelimiterRow(string text, int start, int end)
        {
            bool dash = false;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c == '-')
                    dash = true;
                else if (c != '|' && c != ':' && c != ' ' && c != '\t' && c != '\r')
                    return false;
            }

            return dash;
        }

        // True when a partial line [start, end) could still grow into a delimiter row.
        private static bool CouldBeDelimiter(string text, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c != '-' && c != '|' && c != ':' && c != ' ' && c != '\t')
                    return false;
            }

            return true;
        }

        // True when the complete line [start, end) could be a table header waiting for its delimiter row: it starts
        // with a pipe, is not a delimiter row, and does not follow another table line.
        private static bool IsHeaderCandidate(string text, int start, int end)
        {
            if (!StartsWithPipe(text, start, end) || IsDelimiterRow(text, start, end))
                return false;
            if (start == 0)
                return true;

            int previousEnd = start - 1;
            int previousStart = previousEnd == 0 ? 0 : text.LastIndexOf('\n', previousEnd - 1) + 1;
            return !StartsWithPipe(text, previousStart, previousEnd);
        }
    }
}
