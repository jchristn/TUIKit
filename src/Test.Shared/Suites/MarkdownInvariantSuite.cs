namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Property-style coverage for the width-aware Markdown engine: no row is ever wider than the layout width,
    /// for every option set and every width from 1 to 100, over a fixed corpus and over seeded random input, and
    /// no input throws. Also checks that rendering at the measured natural width wraps nothing.
    /// </summary>
    public static class MarkdownInvariantSuite
    {
        private const string _Id = "MarkdownInvariant";

        private static readonly string[] _Corpus = new[]
        {
            "# Heading with **bold** and `code`\n\nA paragraph that is long enough to wrap at most widths, with a [link](https://example.com/a/long/path).",
            "| Option | Latency (ms) | Notes |\n|:--|--:|:-:|\n| Redis cache | 1.2 | needs another service to run |\n| LRU | 0.3 | in process |\n| 日本語 | 45 | 👍🏽 wide |",
            "- one\n  - two with enough words to wrap\n    1. three\n    2. four\n- [ ] task\n- [x] done\n\n> quote\n> > nested quote with more words",
            "```csharp\npublic static int Add(int a, int b) { return a + b; } // a comment\n\tindented\n```\n\n~~~\nplain\n~~~",
            "---\n\nSetext\n======\n\n***x*** and ~~gone~~ and snake_case and <https://x.dev>",
            "| a |\n|---|\n\n| x | y | z | w | v |\n|---|---|---|---|---|\n| 1 | 2 | 3 | 4 | 5 |",
            "> | quoted | table |\n> |---|---|\n> | in | quote |\n\n- | listed | table |\n  |---|---|\n  | in | list |",
            "averyveryverylongwordwithoutanyspacesthatmustbehardbrokenatanywidth and more"
        };

        private static readonly string[] _Tokens = new[]
        {
            "*", "**", "_", "`", "```", "~~", "[", "]", "(", ")", "|", "-", "- ", "1. ", "#", "# ", ">", "> ", " ", " ", " ",
            "\n", "\n", "\n\n", "a", "bb", "word", "\\", "!", "<", ">", "日", "👍🏽", "---", "|---|", "\t", ":", "~~~", "[x] "
        };

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Width Invariant",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "CorpusFits", "Every corpus document fits every width from 1 to 100 under every option set",
                        _ =>
                        {
                            foreach (MarkdownRenderOptions options in OptionSets())
                            {
                                for (int d = 0; d < _Corpus.Length; d++)
                                {
                                    for (int width = 1; width <= 100; width++)
                                    {
                                        IReadOnlyList<StyledText> rows = MarkdownRenderer.Render(_Corpus[d], width, options);
                                        MarkdownProbe.AssertFits(rows, Expected(options, width), "doc " + d);
                                    }
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RandomInputFits", "Seeded random Markdown never throws and always fits",
                        _ =>
                        {
                            Random random = new Random(20261014);
                            List<MarkdownRenderOptions> sets = OptionSets();
                            for (int n = 0; n < 3000; n++)
                            {
                                StringBuilder source = new StringBuilder();
                                int tokens = random.Next(0, 160);
                                for (int t = 0; t < tokens; t++)
                                    source.Append(_Tokens[random.Next(_Tokens.Length)]);

                                MarkdownRenderOptions options = sets[n % sets.Count];
                                int width = random.Next(1, 90);
                                string markdown = source.ToString();
                                IReadOnlyList<StyledText> rows;
                                try
                                {
                                    rows = MarkdownRenderer.Render(markdown, width, options);
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException("Render threw " + ex.GetType().Name + " for input #" + n + ": " + Escape(markdown), ex);
                                }

                                MarkdownProbe.AssertFits(rows, Expected(options, width), "random #" + n + " " + Escape(markdown));
                                MarkdownRenderer.MeasureWidth(markdown, options);
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NaturalWidthNeverWraps", "Rendering at MeasureWidth produces the same rows as rendering very wide",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            options.MinWidth = 1;
                            for (int d = 0; d < _Corpus.Length; d++)
                            {
                                int natural = Math.Max(1, MarkdownRenderer.MeasureWidth(_Corpus[d], options));
                                List<string> atNatural = MarkdownProbe.Plain(_Corpus[d], natural, options);
                                List<string> wide = MarkdownProbe.Plain(_Corpus[d], 400, options);
                                Check.Equal(wide.Count, atNatural.Count, "doc " + d + " row count at natural width " + natural);
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static int Expected(MarkdownRenderOptions options, int width)
        {
            int expected = width;
            if (options.MaxWidth > 0 && options.MaxWidth < expected)
                expected = options.MaxWidth;

            return Math.Max(expected, options.MinWidth);
        }

        private static List<MarkdownRenderOptions> OptionSets()
        {
            List<MarkdownRenderOptions> sets = new List<MarkdownRenderOptions>();

            MarkdownRenderOptions plain = new MarkdownRenderOptions();
            plain.MinWidth = 1;
            sets.Add(plain);

            sets.Add(new MarkdownRenderOptions());

            MarkdownRenderOptions ascii = MarkdownRenderOptions.Ascii();
            ascii.MinWidth = 1;
            sets.Add(ascii);

            MarkdownRenderOptions truncate = new MarkdownRenderOptions();
            truncate.MinWidth = 1;
            truncate.Overflow = MarkdownOverflow.Truncate;
            truncate.CodeOverflow = MarkdownOverflow.Truncate;
            truncate.Table.CellOverflow = MarkdownOverflow.Truncate;
            sets.Add(truncate);

            MarkdownRenderOptions clip = new MarkdownRenderOptions();
            clip.MinWidth = 1;
            clip.Overflow = MarkdownOverflow.Clip;
            clip.CodeOverflow = MarkdownOverflow.Clip;
            clip.Table.CellOverflow = MarkdownOverflow.Clip;
            clip.Table.Fallback = MarkdownTableFallback.Clip;
            sets.Add(clip);

            MarkdownRenderOptions shrink = new MarkdownRenderOptions();
            shrink.MinWidth = 1;
            shrink.Table.Fallback = MarkdownTableFallback.Shrink;
            shrink.Table.ShowRowSeparators = true;
            shrink.Table.CellPadding = 0;
            sets.Add(shrink);

            MarkdownRenderOptions roomy = new MarkdownRenderOptions();
            roomy.MinWidth = 1;
            roomy.MaxWidth = 60;
            roomy.Table.Expand = true;
            roomy.Table.ShowOuterBorder = false;
            roomy.Table.CellPadding = 4;
            roomy.Table.MaxColumnWidth = 4;
            roomy.Table.Glyphs = MarkdownTableGlyphs.None;
            roomy.FillCodeBackground = true;
            roomy.CodeIndent = 16;
            roomy.ListIndent = 8;
            roomy.BlockSpacing = 0;
            roomy.ShowHeadingMarkers = true;
            roomy.QuotePrefix = ">>>> ";
            roomy.BulletGlyphs = new[] { "-->" };
            sets.Add(roomy);

            return sets;
        }

        private static string Escape(string value)
        {
            return value.Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
