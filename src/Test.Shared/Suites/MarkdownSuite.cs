namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Touchstone suite covering ANSI stripping and the Markdown renderer.
    /// </summary>
    public static class MarkdownSuite
    {
        /// <summary>
        /// Builds the markdown suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Markdown",
                displayName: "Markdown and ANSI Strip",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Markdown", "StripCsi", "CSI sequences are removed",
                        _ =>
                        {
                            string esc = ((char)27).ToString();
                            string input = esc + "[31mred" + esc + "[0m done";
                            Check.Equal("red done", AnsiStripper.Strip(input), "Stripped");
                            Check.Equal("plain", AnsiStripper.Strip("plain"), "No-op on plain text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Markdown", "StripOsc", "OSC sequences are removed",
                        _ =>
                        {
                            string esc = ((char)27).ToString();
                            string input = "before" + esc + "]8;;http://example.com" + esc + "\\link"
                                + esc + "]8;;" + esc + "\\after";
                            string stripped = AnsiStripper.Strip(input);
                            Check.False(stripped.Contains("]8"), "OSC removed");
                            Check.True(stripped.Contains("before") && stripped.Contains("after"), "Text preserved");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Markdown", "InlineBold", "Inline bold and code parse into spans",
                        _ =>
                        {
                            StyledText result = MarkdownRenderer.RenderInline("a **b** `c`", CellStyle.Default);
                            Check.Equal("a b c", result.ToPlainString(), "Plain text");
                            bool sawBold = false;
                            bool sawCode = false;
                            foreach (StyledSpan span in result.Spans)
                            {
                                if (span.Text == "b" && span.Style.HasAttribute(CellAttributes.Bold))
                                    sawBold = true;
                                if (span.Text == "c" && span.Style.Foreground == Color.FromPalette(3))
                                    sawCode = true;
                            }

                            Check.True(sawBold, "Bold span present");
                            Check.True(sawCode, "Code span present");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Markdown", "Blocks", "Headings, lists, and code fences render",
                        _ =>
                        {
                            string md = "# Title\n\n- item one\n- item two\n\n```\ncode line\n```\n";
                            IReadOnlyList<StyledText> lines = MarkdownRenderer.Render(md);

                            bool sawHeading = false;
                            bool sawBullet = false;
                            bool sawCode = false;
                            foreach (StyledText line in lines)
                            {
                                string plain = line.ToPlainString();
                                if (plain.Contains("Title") && line.Spans.Count > 0 && line.Spans[0].Style.HasAttribute(CellAttributes.Bold))
                                    sawHeading = true;
                                if (plain.Contains("• ") && plain.Contains("item one"))
                                    sawBullet = true;
                                if (plain.Contains("code line"))
                                    sawCode = true;
                            }

                            Check.True(sawHeading, "Heading rendered bold");
                            Check.True(sawBullet, "Bullet rendered");
                            Check.True(sawCode, "Code block content rendered");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
