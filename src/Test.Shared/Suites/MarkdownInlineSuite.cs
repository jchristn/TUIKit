namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Coverage for the width-aware inline parser (plan item M2): nested emphasis, code spans, links, images,
    /// autolinks, escapes, intraword underscores, and unmatched delimiters.
    /// </summary>
    public static class MarkdownInlineSuite
    {
        private const string _Id = "MarkdownInline";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Inline Parsing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "NestedEmphasis", "Emphasis nests inside strong and strong inside emphasis",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("**bold *both* bold** and *it **st** it*", 80);
                            Check.Equal("bold both bold and it st it", rows[0].ToPlainString(), "delimiters removed");
                            CellStyle both = MarkdownProbe.StyleOf(rows, "both");
                            Check.True(both.HasAttribute(CellAttributes.Bold) && both.HasAttribute(CellAttributes.Italic), "inner span is bold and italic");
                            CellStyle bold = MarkdownProbe.StyleOf(rows, "bold ");
                            Check.True(bold.HasAttribute(CellAttributes.Bold) && !bold.HasAttribute(CellAttributes.Italic), "outer span is bold only");
                            CellStyle st = MarkdownProbe.StyleOf(rows, "st");
                            Check.True(st.HasAttribute(CellAttributes.Bold) && st.HasAttribute(CellAttributes.Italic), "strong inside emphasis");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TripleDelimiters", "*** makes bold italic, and a shared closing run closes both",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("***x*** **bold *and italic***", 80);
                            Check.Equal("x bold and italic", rows[0].ToPlainString(), "text");
                            CellStyle x = MarkdownProbe.StyleOf(rows, "x");
                            Check.True(x.HasAttribute(CellAttributes.Bold) && x.HasAttribute(CellAttributes.Italic), "triple");
                            CellStyle inner = MarkdownProbe.StyleOf(rows, "and italic");
                            Check.True(inner.HasAttribute(CellAttributes.Bold) && inner.HasAttribute(CellAttributes.Italic), "shared closer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CodeSpans", "Code spans are literal and take any backtick run",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("`a*b*c` and `` x`y ``", 80, options);
                            Check.Equal("a*b*c and x`y", rows[0].ToPlainString(), "contents literal, padding trimmed");
                            Check.Equal(options.Styles.InlineCode.Foreground, MarkdownProbe.StyleOf(rows, "a*b*c").Foreground, "inline code style");
                            Check.False(MarkdownProbe.StyleOf(rows, "a*b*c").HasAttribute(CellAttributes.Italic), "no emphasis inside code");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Links", "Links show their URL by default, in link and URL styles",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("see [the *docs*](https://x.dev \"Docs\") now", 80, options);
                            Check.Equal("see the docs (https://x.dev) now", rows[0].ToPlainString(), "text and url, title dropped");
                            Check.True(MarkdownProbe.StyleOf(rows, "the ").HasAttribute(CellAttributes.Underline), "link text underlined");
                            Check.True(MarkdownProbe.StyleOf(rows, "docs").HasAttribute(CellAttributes.Italic), "emphasis inside link");
                            Check.Equal(options.Styles.LinkUrl.Foreground, MarkdownProbe.StyleOf(rows, "https").Foreground, "url style");
                            Check.Equal("a [b] c (u)", MarkdownProbe.Plain("[a [b] c](u)", 80)[0], "nested brackets in the label");
                            Check.Equal("https://x.dev", MarkdownProbe.Plain("[https://x.dev](https://x.dev)", 80)[0], "url not repeated");
                            options.ShowLinkUrls = false;
                            Check.Equal("docs", MarkdownProbe.Plain("[docs](https://x.dev)", 80, options)[0], "urls hidden");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ImagesAndAutolinks", "Images render as a label; autolinks render in link style",
                        _ =>
                        {
                            Check.Equal("[image: logo]", MarkdownProbe.Plain("![logo](x.png)", 80)[0], "image");
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("go <https://x.dev> or <a@b.io>", 80);
                            Check.Equal("go https://x.dev or a@b.io", rows[0].ToPlainString(), "brackets removed");
                            Check.True(MarkdownProbe.StyleOf(rows, "https://x.dev").HasAttribute(CellAttributes.Underline), "autolink styled");
                            Check.Equal("a <not a link> b", MarkdownProbe.Plain("a <not a link> b", 80)[0], "spaces mean not an autolink");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Escapes", "Backslash escapes make delimiters literal",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("\\*not em\\* \\`x\\` \\[y\\](z)", 80);
                            Check.Equal("*not em* `x` [y](z)", rows[0].ToPlainString(), "escaped text");
                            Check.False(MarkdownProbe.StyleOf(rows, "not em").HasAttribute(CellAttributes.Italic), "not italic");
                            Check.Equal("a\\b", MarkdownProbe.Plain("a\\b", 80)[0], "backslash before a letter is literal");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Underscores", "Intraword underscores are literal; _em_ and __strong__ work",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("snake_case_name _em_ __st__", 80);
                            Check.Equal("snake_case_name em st", rows[0].ToPlainString(), "text");
                            Check.False(MarkdownProbe.StyleOf(rows, "case").HasAttribute(CellAttributes.Italic), "intraword literal");
                            Check.True(MarkdownProbe.StyleOf(rows, "em").HasAttribute(CellAttributes.Italic), "underscore emphasis");
                            Check.True(MarkdownProbe.StyleOf(rows, "st").HasAttribute(CellAttributes.Bold), "underscore strong");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Strikethrough", "~~text~~ strikes; a single tilde is literal",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("~~gone~~ ~kept~", 80);
                            Check.Equal("gone ~kept~", rows[0].ToPlainString(), "text");
                            Check.True(MarkdownProbe.StyleOf(rows, "gone").HasAttribute(CellAttributes.Strikethrough), "struck");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Unmatched", "Unmatched or malformed delimiters render literally and never throw",
                        _ =>
                        {
                            Check.Equal("**open", MarkdownProbe.Plain("**open", 80)[0], "unclosed strong");
                            Check.Equal("[text](", MarkdownProbe.Plain("[text](", 80)[0], "unclosed link");
                            Check.Equal("a ` b", MarkdownProbe.Plain("a ` b", 80)[0], "lone backtick");
                            Check.Equal("end\\", MarkdownProbe.Plain("end\\", 80)[0], "trailing backslash");
                            Check.Equal("a * b * c", MarkdownProbe.Plain("a * b * c", 80)[0], "spaced stars are not emphasis");
                            Check.Equal(1, MarkdownProbe.Plain("[]()", 80).Count, "empty link renders without throwing");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
