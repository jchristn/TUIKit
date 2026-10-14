namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Coverage for the width-aware Markdown layout (plan item M4): hanging indents, quote bars, ordered markers,
    /// code blocks, spacing, width limits, overflow modes, wide characters, and argument validation.
    /// </summary>
    public static class MarkdownLayoutSuite
    {
        private const string _Id = "MarkdownLayout";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Layout",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "HangingIndent", "A wrapped bullet continues at its content column",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain("- alpha beta gamma delta epsilon zeta", 16);
                            Check.True(rows.Count > 1, "wrapped");
                            Check.True(rows[0].StartsWith("• ", StringComparison.Ordinal), "first row has the bullet");
                            for (int i = 1; i < rows.Count; i++)
                                Check.True(rows[i].StartsWith("  ", StringComparison.Ordinal) && rows[i][2] != ' ', "row " + i + " starts at column 2: '" + rows[i] + "'");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "QuoteBars", "Every wrapped quote row carries its bar, nested quotes two",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain("> > alpha beta gamma delta epsilon zeta eta", 16);
                            Check.True(rows.Count > 2, "wrapped");
                            foreach (string row in rows)
                                Check.True(row.StartsWith("│ │ ", StringComparison.Ordinal), "two bars on '" + row + "'");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "OrderedAlignment", "Ordered markers right-align to the widest number",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain("9. nine\n10. ten", 20);
                            Check.Equal(" 9. nine", rows[0], "padded");
                            Check.Equal("10. ten", rows[1], "widest");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CodeLanguageRow", "The language row shows by default, hides on request, and is absent without a tag",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            Check.Equal("  js", MarkdownProbe.Plain("```js\nx\n```", 20, o)[0], "label shown");
                            o.ShowCodeLanguage = false;
                            Check.Equal("  x", MarkdownProbe.Plain("```js\nx\n```", 20, o)[0], "label hidden");
                            Check.Equal(1, MarkdownProbe.Plain("```\nx\n```", 20).Count, "no tag, no label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Highlighting", "Named fences are highlighted; plain fences and disabled highlighting use the code style",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            Color code = o.Styles.CodeBlock.Foreground;
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("```csharp\npublic int x;\n```", 40, o);
                            Check.True(MarkdownProbe.StyleOf(rows, "public").Foreground != code, "keyword colored");
                            Check.Equal(code, MarkdownProbe.StyleOf(rows, " x;").Foreground, "plain code keeps the code color");
                            o.SyntaxHighlighting = false;
                            rows = MarkdownRenderer.Render("```csharp\npublic int x;\n```", 40, o);
                            Check.Equal(code, MarkdownProbe.StyleOf(rows, "public").Foreground, "highlighting off");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CodeFillAndTabs", "Code background fill pads rows to the width; tabs expand",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.FillCodeBackground = true;
                            o.ShowCodeLanguage = false;
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("```\na\n\tb\n```", 20, o);
                            Check.Equal(20, rows[0].Width, "filled row");
                            Check.Equal(20, rows[1].Width, "filled tab row");
                            Check.True(rows[1].ToPlainString().StartsWith("      b", StringComparison.Ordinal), "tab expanded to four spaces after the indent");
                            o.CodeOverflow = MarkdownOverflow.Clip;
                            Check.Equal(1, MarkdownRenderer.Render("```\n" + new string('x', 50) + "\n```", 20, o).Count, "clipped code keeps one row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CodeHangingIndent", "A wrapped code line continues at its own indentation",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.ShowCodeLanguage = false;
                            List<string> rows = MarkdownProbe.Plain("```\n    var total = first + second + third;\n```", 24, o);
                            Check.Equal("      var total = first", rows[0], "first row keeps the source indent");
                            Check.Equal("      + second + third;", rows[1], "continuation hangs at the same indent");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "BlockSpacing", "BlockSpacing sets the blank rows between blocks",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.BlockSpacing = 0;
                            Check.Equal(2, MarkdownProbe.Plain("a\n\nb", 20, o).Count, "none");
                            o.BlockSpacing = 4;
                            Check.Equal(6, MarkdownProbe.Plain("a\n\nb", 20, o).Count, "four");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "WidthLimits", "MaxWidth caps a wide layout; MinWidth sets a floor the host clips",
                        _ =>
                        {
                            string md = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu";
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.MaxWidth = 30;
                            IReadOnlyList<StyledText> capped = MarkdownRenderer.Render(md, 80, o);
                            MarkdownProbe.AssertFits(capped, 30, "MaxWidth");
                            Check.True(capped.Count > 1, "wrapped at the cap");
                            o.MaxWidth = 0;
                            IReadOnlyList<StyledText> floored = MarkdownRenderer.Render(md, 4, o);
                            MarkdownProbe.AssertFits(floored, 10, "MinWidth");
                            Check.Equal("alpha beta", floored[0].ToPlainString(), "laid out at the floor, not the available width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Overflow", "Truncate ends with the ellipsis, Clip does not, and both keep one row",
                        _ =>
                        {
                            string md = "alpha beta gamma delta epsilon";
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Overflow = MarkdownOverflow.Truncate;
                            List<string> truncated = MarkdownProbe.Plain(md, 12, o);
                            Check.Equal(1, truncated.Count, "one row");
                            Check.Equal("alpha beta …", truncated[0], "ellipsis");
                            o.Ellipsis = "...";
                            Check.Equal("alpha bet...", MarkdownProbe.Plain(md, 12, o)[0], "custom ellipsis");
                            o.Ellipsis = string.Empty;
                            Check.Equal("alpha beta g", MarkdownProbe.Plain(md, 12, o)[0], "empty ellipsis");
                            o.Overflow = MarkdownOverflow.Clip;
                            Check.Equal("alpha beta g", MarkdownProbe.Plain(md, 12, o)[0], "clip");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "WideCharacters", "CJK and emoji never split and never exceed the width",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.MinWidth = 1;
                            for (int width = 1; width <= 8; width++)
                            {
                                IReadOnlyList<StyledText> cjk = MarkdownRenderer.Render("日本語のテキスト", width, o);
                                MarkdownProbe.AssertFits(cjk, width, "cjk");
                                IReadOnlyList<StyledText> emoji = MarkdownRenderer.Render("👍🏽 ok 👍🏽", width, o);
                                MarkdownProbe.AssertFits(emoji, width, "emoji");
                                if (width >= 2)
                                    Check.Equal("日本語のテキスト", string.Concat(MarkdownProbe.Plain("日本語のテキスト", width, o)), "nothing lost at width " + width);
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "HeadingsAndRules", "Heading markers, rule width, and rule glyph",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.ShowHeadingMarkers = true;
                            Check.Equal("## Two", MarkdownProbe.Plain("## Two", 20, o)[0], "markers kept");
                            o.RuleWidth = 6;
                            o.RuleGlyph = "=";
                            Check.Equal("======", MarkdownProbe.Plain("---", 20, o)[0], "rule width and glyph");
                            o.RuleWidth = 50;
                            Check.Equal(20, MarkdownProbe.Plain("---", 20, o)[0].Length, "rule never exceeds the layout");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ListOptions", "ListIndent, bullet cycling, and task styles",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.ListIndent = 2;
                            o.BulletGlyphs = new[] { "*", "-" };
                            List<string> rows = MarkdownProbe.Plain("- a\n  - b\n    - c", 30, o);
                            Check.Equal("  * a", rows[0], "indent and first glyph");
                            Check.Equal("      - b", rows[1], "second glyph");
                            Check.Equal("          * c", rows[2], "cycled");
                            IReadOnlyList<StyledText> tasks = MarkdownRenderer.Render("- [x] done\n- [ ] open", 30);
                            MarkdownStyles s = MarkdownStyles.Default;
                            Check.Equal(s.TaskDone.Foreground, MarkdownProbe.StyleOf(tasks, "☑").Foreground, "done style");
                            Check.Equal(s.TaskOpen.Foreground, MarkdownProbe.StyleOf(tasks, "☐").Foreground, "open style");
                            Check.Equal("1. ☐ step", MarkdownProbe.Plain("1. [ ] step", 30)[0], "ordered task keeps its number");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TinyWidths", "Width 1 lays out everything without throwing",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.MinWidth = 1;
                            string md = "# H\n\n- a b\n  > q r\n\n```cs\nint x;\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n---";
                            MarkdownProbe.AssertFits(MarkdownRenderer.Render(md, 1, o), 1, "width 1");
                            MarkdownProbe.AssertFits(MarkdownRenderer.Render(md, 2, o), 2, "width 2");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "WidthGuards", "Width below 1 throws ArgumentOutOfRangeException",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => MarkdownRenderer.Render("x", 0), "zero");
                            Check.Throws<ArgumentOutOfRangeException>(() => MarkdownRenderer.Render("x", -5, new MarkdownRenderOptions()), "negative");
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.Render("x", 10, null!), "null options");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
