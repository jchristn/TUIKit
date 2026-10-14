namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Coverage for the width-aware Markdown block parser (plan item M1), observed through rendered output:
    /// every block kind, nesting, looseness, table detection, line endings, and malformed input.
    /// </summary>
    public static class MarkdownParsingSuite
    {
        private const string _Id = "MarkdownParsing";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Block Parsing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "AtxHeadings", "ATX headings drop their markers and closing run; #hashtag stays text",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain("# One\n\n### Three ###\n\n#hashtag", 40);
                            Check.Equal("One", rows[0], "level 1");
                            Check.Equal("Three", rows[2], "closing run removed");
                            Check.Equal("#hashtag", rows[4], "no space after # is not a heading");
                            IReadOnlyList<StyledText> styled = MarkdownRenderer.Render("# One", 40);
                            Check.True(MarkdownProbe.StyleOf(styled, "One").HasAttribute(CellAttributes.Bold), "heading bold");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SetextHeadings", "Setext underlines make headings and are not drawn",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> styled = MarkdownRenderer.Render("Title\n=====\n\nSub\n---", 40, options);
                            List<string> rows = MarkdownProbe.Plain("Title\n=====\n\nSub\n---", 40);
                            Check.Equal(3, rows.Count, "two headings and a blank row");
                            Check.Equal("Title", rows[0], "level 1 text");
                            Check.Equal("Sub", rows[2], "level 2 text");
                            Check.Equal(options.Styles.Heading1.Foreground, MarkdownProbe.StyleOf(styled, "Title").Foreground, "level 1 style");
                            Check.Equal(options.Styles.Heading.Foreground, MarkdownProbe.StyleOf(styled, "Sub").Foreground, "level 2 style");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ParagraphsAndBreaks", "Soft-wrapped lines join; two spaces or a backslash break the line",
                        _ =>
                        {
                            Check.Equal("one two", MarkdownProbe.Plain("one\ntwo", 40)[0], "soft wrap joins");
                            List<string> hard = MarkdownProbe.Plain("one  \ntwo", 40);
                            Check.Equal(2, hard.Count, "two-space break");
                            Check.Equal("two", hard[1], "second row");
                            List<string> slash = MarkdownProbe.Plain("one\\\ntwo", 40);
                            Check.Equal("one", slash[0], "backslash removed");
                            Check.Equal("two", slash[1], "backslash break");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Fences", "Tilde fences, longer fences, unclosed fences, and fences inside fences",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            Check.Equal("  code", MarkdownProbe.Plain("~~~\ncode\n~~~", 40, options)[0], "tilde fence");
                            Check.Equal("  open", MarkdownProbe.Plain("```\nopen", 40, options)[0], "unclosed fence runs to the end");
                            List<string> nested = MarkdownProbe.Plain("````\n```\ninner\n```\n````", 40, options);
                            Check.Equal(3, nested.Count, "inner fence is content");
                            Check.Equal("  ```", nested[0], "inner fence kept");
                            List<string> labelled = MarkdownProbe.Plain("```python title\nx = 1\n```", 40, options);
                            Check.Equal("  python", labelled[0], "first word of the info string is the language");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Quotes", "Quotes nest, hold other blocks, and take lazy continuation lines",
                        _ =>
                        {
                            Check.Equal("│ a lazy", MarkdownProbe.Plain("> a\nlazy", 40)[0], "lazy line joins the quoted paragraph");
                            List<string> nested = MarkdownProbe.Plain("> outer\n>\n> > inner", 40);
                            Check.Equal("│ │ inner", nested[nested.Count - 1], "nested quote has two bars");
                            Check.Equal("│ • item", MarkdownProbe.Plain("> - item", 40)[0], "list inside a quote");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Lists", "Nested, ordered, parenthesized, loose, and tight lists",
                        _ =>
                        {
                            List<string> nested = MarkdownProbe.Plain("- a\n  - b\n    - c\n      - d", 40);
                            Check.Equal("• a", nested[0], "depth 0");
                            Check.Equal("  ◦ b", nested[1], "depth 1");
                            Check.Equal("    ▪ c", nested[2], "depth 2");
                            Check.Equal("      • d", nested[3], "depth 3 cycles");
                            List<string> ordered = MarkdownProbe.Plain("3. x\n4. y", 40);
                            Check.Equal("3. x", ordered[0], "start number kept");
                            Check.Equal("4. y", ordered[1], "numbers count up");
                            Check.Equal("1) z", MarkdownProbe.Plain("1) z", 40)[0], "parenthesis delimiter");
                            Check.Equal(2, MarkdownProbe.Plain("- a\n- b", 40).Count, "tight list has no blank rows");
                            Check.Equal(3, MarkdownProbe.Plain("- a\n\n- b", 40).Count, "loose list separates items");
                            List<string> quoted = MarkdownProbe.Plain("- item\n  > quoted", 40);
                            Check.Equal("  │ quoted", quoted[1], "quote inside a list item");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Rules", "Thematic breaks from dashes, stars, and underscores, with spaces",
                        _ =>
                        {
                            foreach (string rule in new[] { "---", "***", "___", "- - -" })
                            {
                                List<string> rows = MarkdownProbe.Plain(rule, 12);
                                Check.Equal(new string('─', 12), rows[0], "rule " + rule);
                            }

                            Check.Equal("**", MarkdownProbe.Plain("**", 12)[0], "two stars are text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TableDetection", "A table needs a matching delimiter row",
                        _ =>
                        {
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain("| a | b |\n|---|---|", 40)).Contains("┌"), "valid table");
                            Check.False(MarkdownProbe.Join(MarkdownProbe.Plain("| a | b |\n|---|", 40)).Contains("┌"), "cell count mismatch is text");
                            Check.False(MarkdownProbe.Join(MarkdownProbe.Plain("a | b", 40)).Contains("┌"), "no delimiter row is text");
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain("Intro:\n| a | b |\n|:-:|--:|\n| 1 | 2 |", 40)).Contains("┌"), "a table can interrupt a paragraph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TableCells", "Escaped pipes, pipes in code, and ragged rows",
                        _ =>
                        {
                            string text = MarkdownProbe.Join(MarkdownProbe.Plain("| a | b |\n|---|---|\n| x \\| y | `p|q` |", 60));
                            Check.True(text.Contains("x | y"), "escaped pipe is literal");
                            Check.True(text.Contains("p|q"), "pipe inside code is literal");
                            List<string> ragged = MarkdownProbe.Plain("| a | b |\n|---|---|\n| only |\n| 1 | 2 | 3 |", 60);
                            Check.Equal(6, ragged.Count, "short row padded and long row trimmed");
                            Check.False(MarkdownProbe.Join(ragged).Contains("3"), "extra cell dropped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LineEndings", "CRLF and CR parse like LF",
                        _ =>
                        {
                            string lf = MarkdownProbe.Join(MarkdownProbe.Plain("# T\n\n- a\n- b\n\n| x | y |\n|---|---|\n| 1 | 2 |", 40));
                            Check.Equal(lf, MarkdownProbe.Join(MarkdownProbe.Plain("# T\r\n\r\n- a\r\n- b\r\n\r\n| x | y |\r\n|---|---|\r\n| 1 | 2 |", 40)), "CRLF");
                            Check.Equal(lf, MarkdownProbe.Join(MarkdownProbe.Plain("# T\r\r- a\r- b\r\r| x | y |\r|---|---|\r| 1 | 2 |", 40)), "CR");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Degenerate", "Empty, blank, a lone quote marker, and a lone list marker",
                        _ =>
                        {
                            List<string> empty = MarkdownProbe.Plain(string.Empty, 20);
                            Check.Equal(1, empty.Count, "empty gives one row");
                            Check.Equal(string.Empty, empty[0], "empty row");
                            Check.Equal(1, MarkdownProbe.Plain("   \n\t\n", 20).Count, "whitespace gives one row");
                            Check.Equal("│", MarkdownProbe.Plain(">", 20)[0].TrimEnd(), "lone quote marker");
                            Check.Equal("•", MarkdownProbe.Plain("-", 20)[0].TrimEnd(), "lone list marker");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NullSource", "A null source throws ArgumentNullException",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.Render(null!, 40), "null markdown");
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.Render(null!, 40, new MarkdownRenderOptions()), "null markdown with options");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
