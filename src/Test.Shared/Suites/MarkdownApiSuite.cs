namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Coverage for the public width-aware render API (plan item M6): overload equivalence, the async variant and
    /// cancellation, natural-width measurement, argument guards, and unchanged legacy output.
    /// </summary>
    public static class MarkdownApiSuite
    {
        private const string _Id = "MarkdownApi";

        private const string _Doc = "# Title\n\nSome **text** with a [link](https://x.dev).\n\n| Name | Value |\n|---|--:|\n| alpha | 1 |\n| beta gamma | 22 |\n\n- one\n- two";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Render API",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "DefaultOverload", "Render(markdown, width) equals Render with new default options",
                        _ =>
                        {
                            Check.Equal(
                                MarkdownProbe.Join(MarkdownProbe.Plain(_Doc, 50)),
                                string.Join("\n", Plain(MarkdownRenderer.Render(_Doc, 50))),
                                "same rows");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RenderAsync", "RenderAsync returns the same rows as Render",
                        async ct =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> rows = await MarkdownRenderer.RenderAsync(_Doc, 50, o, ct).ConfigureAwait(false);
                            Check.Equal(MarkdownProbe.Join(MarkdownProbe.Plain(_Doc, 50, o)), string.Join("\n", Plain(rows)), "same rows");
                        }),

                    new TestCaseDescriptor(_Id, "RenderAsyncCancelled", "A cancelled token throws OperationCanceledException",
                        async _ =>
                        {
                            using (CancellationTokenSource cts = new CancellationTokenSource())
                            {
                                cts.Cancel();
                                await Check.ThrowsAsync<OperationCanceledException>(
                                    () => MarkdownRenderer.RenderAsync(_Doc, 50, new MarkdownRenderOptions(), cts.Token),
                                    "cancelled").ConfigureAwait(false);
                            }
                        }),

                    new TestCaseDescriptor(_Id, "RenderAsyncGuards", "RenderAsync validates its arguments",
                        async _ =>
                        {
                            await Check.ThrowsAsync<ArgumentNullException>(() => MarkdownRenderer.RenderAsync(null!, 50, new MarkdownRenderOptions(), CancellationToken.None), "null markdown").ConfigureAwait(false);
                            await Check.ThrowsAsync<ArgumentNullException>(() => MarkdownRenderer.RenderAsync("x", 50, null!, CancellationToken.None), "null options").ConfigureAwait(false);
                            await Check.ThrowsAsync<ArgumentOutOfRangeException>(() => MarkdownRenderer.RenderAsync("x", 0, new MarkdownRenderOptions(), CancellationToken.None), "zero width").ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(_Id, "MeasureWidth", "MeasureWidth returns the natural width, and rendering at it wraps nothing",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            Check.Equal(11, MarkdownRenderer.MeasureWidth("hello world", o), "paragraph");
                            Check.Equal(0, MarkdownRenderer.MeasureWidth(string.Empty, o), "empty");
                            Check.Equal(13, MarkdownRenderer.MeasureWidth("| a | bb |\n|---|---|\n| ccc | d |", o), "table");
                            Check.True(MarkdownRenderer.MeasureWidth("---", o) <= 1, "rules do not count");

                            int natural = MarkdownRenderer.MeasureWidth(_Doc, o);
                            List<string> atNatural = MarkdownProbe.Plain(_Doc, natural, o);
                            Check.True(MarkdownProbe.Join(atNatural).Contains("Some text with a link (https://x.dev)."), "paragraph on one row");
                            Check.True(MarkdownProbe.Join(atNatural).Contains("│ beta gamma │"), "table at natural widths");

                            o.MaxWidth = 20;
                            Check.True(MarkdownRenderer.MeasureWidth(_Doc, o) <= 20, "capped by MaxWidth");
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.MeasureWidth(null!, o), "null markdown");
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.MeasureWidth("x", null!), "null options");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "OptionsNotModified", "Rendering and measuring never change the options passed in",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            MarkdownRenderer.MeasureWidth(_Doc, o);
                            MarkdownRenderer.Render(_Doc, 30, o);
                            Check.Equal(MarkdownOverflow.Wrap, o.Overflow, "overflow");
                            Check.Equal(MarkdownOverflow.Wrap, o.Table.CellOverflow, "cell overflow");
                            Check.Equal(0, o.RuleWidth, "rule width");
                            Check.Equal(10, o.MinWidth, "min width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LegacyUnchanged", "The line-oriented engine keeps its 1.5.0 output",
                        _ =>
                        {
                            IReadOnlyList<StyledText> legacy = MarkdownRenderer.Render("| a | b |\n|---|---|\n| 1 | 2 |");
                            Check.Equal("a │ b", legacy[0].ToPlainString(), "legacy header row");
                            Check.Equal(new string('─', 9), legacy[1].ToPlainString(), "legacy separator");
                            Check.Equal("1 │ 2", legacy[2].ToPlainString(), "legacy body row");
                            Check.Equal("a **b", MarkdownRenderer.RenderInline("a **b", CellStyle.Default).ToPlainString(), "legacy inline keeps an unclosed delimiter");
                            return Task.CompletedTask;
                        })
                });
        }

        private static List<string> Plain(IReadOnlyList<StyledText> rows)
        {
            List<string> plain = new List<string>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
                plain.Add(rows[i].ToPlainString());

            return plain;
        }
    }
}
