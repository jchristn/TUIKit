namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;
    using TUIKit.Unicode;

    // Lays a parsed MarkdownDocument out at a fixed width. Every row returned is at most that width.
    internal sealed class MarkdownLayout
    {
        private readonly MarkdownRenderOptions _Options;

        internal MarkdownLayout(MarkdownRenderOptions options)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        internal List<StyledText> Layout(MarkdownDocument document, int width, CancellationToken token)
        {
            List<StyledText> rows = new List<StyledText>();
            CellStyle text = _Options.Styles.Text;
            IReadOnlyList<MarkdownBlock> blocks = document.Blocks;
            for (int i = 0; i < blocks.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (i > 0)
                    AddBlank(rows, _Options.BlockSpacing);

                rows.AddRange(LayoutBlock(blocks[i], width, text, 0));
            }

            if (rows.Count == 0)
                rows.Add(StyledText.Empty);

            for (int i = 0; i < rows.Count; i++)
                rows[i] = MarkdownTextFit.Clip(rows[i], width);

            return rows;
        }

        private List<StyledText> LayoutBlocks(IReadOnlyList<MarkdownBlock> blocks, int width, CellStyle text, int listDepth, int spacing)
        {
            List<StyledText> rows = new List<StyledText>();
            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                    AddBlank(rows, spacing);

                rows.AddRange(LayoutBlock(blocks[i], width, text, listDepth));
            }

            return rows;
        }

        private List<StyledText> LayoutBlock(MarkdownBlock block, int width, CellStyle text, int listDepth)
        {
            if (block is MarkdownParagraphBlock paragraph)
                return LayoutParagraph(paragraph, width, text);
            if (block is MarkdownHeadingBlock heading)
                return LayoutHeading(heading, width, text);
            if (block is MarkdownCodeBlock code)
                return LayoutCode(code, width, text);
            if (block is MarkdownQuoteBlock quote)
                return LayoutQuote(quote, width, text, listDepth);
            if (block is MarkdownListBlock list)
                return LayoutList(list, width, text, listDepth);
            if (block is MarkdownTableBlock table)
                return MarkdownTableLayout.Layout(table, width, text, _Options);

            return LayoutRule(width, text);
        }

        private List<StyledText> LayoutParagraph(MarkdownParagraphBlock paragraph, int width, CellStyle text)
        {
            List<StyledText> rows = new List<StyledText>();
            for (int i = 0; i < paragraph.Segments.Count; i++)
            {
                StyledText inline = MarkdownInlineParser.Parse(paragraph.Segments[i], text, _Options);
                rows.AddRange(MarkdownTextFit.Fit(inline, width, _Options.Overflow, _Options.Ellipsis));
            }

            return rows;
        }

        private List<StyledText> LayoutHeading(MarkdownHeadingBlock heading, int width, CellStyle text)
        {
            MarkdownStyles styles = _Options.Styles;
            CellStyle style = MarkdownStyles.Overlay(text, heading.Level <= 1 ? styles.Heading1 : styles.Heading);
            string source = _Options.ShowHeadingMarkers ? new string('#', heading.Level) + " " + heading.Text : heading.Text;
            StyledText inline = MarkdownInlineParser.Parse(source, style, _Options);
            return MarkdownTextFit.Fit(inline, width, _Options.Overflow, _Options.Ellipsis);
        }

        private List<StyledText> LayoutRule(int width, CellStyle text)
        {
            int length = _Options.RuleWidth > 0 ? Math.Min(_Options.RuleWidth, width) : width;
            string rule = MarkdownTextFit.Repeat(_Options.RuleGlyph, length).TrimEnd(' ');
            CellStyle style = MarkdownStyles.Overlay(text, _Options.Styles.Rule);
            return new List<StyledText> { Text.From(rule, style) };
        }

        private List<StyledText> LayoutCode(MarkdownCodeBlock code, int width, CellStyle text)
        {
            MarkdownStyles styles = _Options.Styles;
            CellStyle codeStyle = MarkdownStyles.Overlay(text, styles.CodeBlock);
            List<StyledText> rows = new List<StyledText>();
            string indent = new string(' ', _Options.CodeIndent);
            bool highlight = _Options.SyntaxHighlighting && code.Language.Length > 0;

            if (_Options.ShowCodeLanguage && code.Language.Length > 0)
            {
                StyledText label = Text.From(indent, text).Append(Text.From(code.Language, MarkdownStyles.Overlay(text, styles.CodeLanguage)));
                rows.Add(MarkdownTextFit.Clip(label, width));
            }

            int contentWidth = Math.Max(1, width - _Options.CodeIndent);
            StyledText prefix = Text.From(indent, codeStyle);
            for (int i = 0; i < code.Lines.Count; i++)
            {
                string line = code.Lines[i].Replace("\t", "    ");
                StyledText styled = highlight ? Highlight(line, code.Language, codeStyle) : Text.From(line, codeStyle);
                // A wrapped code line hangs at its own leading whitespace, capped at half the width.
                int lead = 0;
                while (lead < line.Length && line[lead] == ' ')
                    lead++;

                lead = Math.Min(lead, contentWidth / 2);
                List<StyledText> fitted = _Options.CodeOverflow == MarkdownOverflow.Wrap
                    ? MarkdownTextFit.Wrap(styled, contentWidth, contentWidth - lead)
                    : MarkdownTextFit.Fit(styled, contentWidth, _Options.CodeOverflow, _Options.Ellipsis);
                StyledText hanging = Text.From(new string(' ', lead), codeStyle);
                for (int f = 0; f < fitted.Count; f++)
                {
                    StyledText content = f > 0 && lead > 0 ? hanging.Append(fitted[f]) : fitted[f];
                    StyledText row = indent.Length > 0 ? prefix.Append(content) : content;
                    if (_Options.FillCodeBackground)
                        row = MarkdownTextFit.Pad(row, width, MarkdownColumnAlignment.Left, codeStyle);

                    rows.Add(row);
                }
            }

            if (code.Lines.Count == 0 && _Options.FillCodeBackground)
                rows.Add(Text.From(new string(' ', width), codeStyle));

            return rows;
        }

        private static StyledText Highlight(string line, string language, CellStyle codeStyle)
        {
            StyledText highlighted = SyntaxHighlighter.HighlightLine(line, language);
            List<StyledSpan> spans = new List<StyledSpan>(highlighted.Spans.Count);
            for (int i = 0; i < highlighted.Spans.Count; i++)
            {
                StyledSpan span = highlighted.Spans[i];
                spans.Add(new StyledSpan(span.Text, MarkdownStyles.Overlay(codeStyle, span.Style)));
            }

            return spans.Count == 0 ? StyledText.Empty : new StyledText(spans);
        }

        private List<StyledText> LayoutQuote(MarkdownQuoteBlock quote, int width, CellStyle text, int listDepth)
        {
            CellStyle quoteStyle = MarkdownStyles.Overlay(text, _Options.Styles.Quote);
            int prefixWidth = TextFit.Width(_Options.QuotePrefix);
            List<StyledText> inner = LayoutBlocks(quote.Children, Math.Max(1, width - prefixWidth), quoteStyle, listDepth, _Options.BlockSpacing);
            if (inner.Count == 0)
                inner.Add(StyledText.Empty);

            StyledText prefix = Text.From(_Options.QuotePrefix, quoteStyle);
            List<StyledText> rows = new List<StyledText>(inner.Count);
            for (int i = 0; i < inner.Count; i++)
                rows.Add(MarkdownTextFit.Clip(prefix.Append(inner[i]), width));

            return rows;
        }

        private List<StyledText> LayoutList(MarkdownListBlock list, int width, CellStyle text, int listDepth)
        {
            MarkdownStyles styles = _Options.Styles;
            List<StyledText> rows = new List<StyledText>();
            string indent = new string(' ', _Options.ListIndent);
            int innerSpacing = list.Loose ? _Options.BlockSpacing : 0;
            int numberWidth = 0;
            if (list.Ordered)
            {
                long last = (long)list.Start + list.Items.Count - 1;
                numberWidth = Math.Max(list.Start.ToString().Length, last.ToString().Length);
            }

            for (int k = 0; k < list.Items.Count; k++)
            {
                MarkdownListItem item = list.Items[k];
                if (k > 0)
                    AddBlank(rows, innerSpacing);

                StyledText marker;
                if (list.Ordered)
                {
                    long number = (long)list.Start + k;
                    marker = Text.From(number.ToString().PadLeft(numberWidth) + list.Delimiter, MarkdownStyles.Overlay(text, styles.Bullet));
                    if (item.IsTask)
                        marker = marker.Append(Text.From(" ", text)).Append(TaskMarker(item, text));
                }
                else if (item.IsTask)
                {
                    marker = TaskMarker(item, text);
                }
                else
                {
                    IReadOnlyList<string> bullets = _Options.BulletGlyphs;
                    marker = Text.From(bullets[listDepth % bullets.Count], MarkdownStyles.Overlay(text, styles.Bullet));
                }

                StyledText first = Text.From(indent, text).Append(marker).Append(Text.From(" ", text));
                int prefixWidth = first.Width;
                StyledText continuation = Text.From(new string(' ', prefixWidth), text);

                List<StyledText> content = LayoutBlocks(item.Children, Math.Max(1, width - prefixWidth), text, listDepth + 1, innerSpacing);
                if (content.Count == 0)
                    content.Add(StyledText.Empty);

                for (int r = 0; r < content.Count; r++)
                {
                    StyledText row = (r == 0 ? first : continuation).Append(content[r]);
                    rows.Add(MarkdownTextFit.Clip(row, width));
                }
            }

            return rows;
        }

        private StyledText TaskMarker(MarkdownListItem item, CellStyle text)
        {
            MarkdownStyles styles = _Options.Styles;
            return item.IsDone
                ? Text.From(_Options.TaskDoneGlyph, MarkdownStyles.Overlay(text, styles.TaskDone))
                : Text.From(_Options.TaskOpenGlyph, MarkdownStyles.Overlay(text, styles.TaskOpen));
        }

        private static void AddBlank(List<StyledText> rows, int count)
        {
            for (int i = 0; i < count; i++)
                rows.Add(StyledText.Empty);
        }
    }
}
