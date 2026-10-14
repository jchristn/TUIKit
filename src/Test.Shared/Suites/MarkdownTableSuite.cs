namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Coverage for Markdown table layout (plan item M5): content-sized columns, proportional squeezing, borders and
    /// glyph presets, alignment, padding, column caps, cell overflow, styles, and the three narrow-width fallbacks.
    /// </summary>
    public static class MarkdownTableSuite
    {
        private const string _Id = "MarkdownTable";

        private const string _Small = "| a | bb |\n|---|---|\n| ccc | d |";

        private const string _Wide = "| Key | Description |\n|---|---|\n| k1 | alpha beta gamma delta epsilon zeta eta theta |";

        private const string _Hard = "| Name | Description | Status |\n|---|---|---|\n| server-alpha | responsible for everything | operational |";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Tables",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "NaturalFit", "A table that fits uses its natural column widths exactly",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain(_Small, 80);
                            Check.Equal(5, rows.Count, "row count");
                            Check.Equal("┌─────┬─────┐", rows[0], "top");
                            Check.Equal("│ a   │ bb  │", rows[1], "header");
                            Check.Equal("├─────┼─────┤", rows[2], "separator");
                            Check.Equal("│ ccc │ d   │", rows[3], "body");
                            Check.Equal("└─────┴─────┘", rows[4], "bottom");
                            Check.Equal(MarkdownProbe.Join(rows), MarkdownProbe.Join(MarkdownProbe.Plain(_Small, 13)), "exactly the natural width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Alignment", "Delimiter colons align columns left, center, and right",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain("| a | bb |\n|:-:|--:|\n| ccc | d |", 80);
                            Check.Equal("│  a  │  bb │", rows[1], "centered and right-aligned header");
                            Check.Equal("│ ccc │   d │", rows[3], "right-aligned body");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Squeeze", "A squeezed table keeps short columns whole and wraps the wide one at word boundaries",
                        _ =>
                        {
                            IReadOnlyList<StyledText> styled = MarkdownRenderer.Render(_Wide, 30);
                            List<string> rows = MarkdownProbe.Plain(_Wide, 30);
                            MarkdownProbe.AssertFits(styled, 30, "squeezed");
                            Check.Equal("│ Key │ Description          │", rows[1], "key column keeps its natural width; description takes the rest");
                            Check.True(rows.Count > 5, "description wrapped");
                            string all = MarkdownProbe.Join(rows);
                            foreach (string word in new[] { "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta" })
                                Check.True(all.Contains(word), "word '" + word + "' unbroken");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ProportionalShare", "Spare space goes to columns in proportion to how much they still want",
                        _ =>
                        {
                            string md = "| A | B |\n|---|---|\n| aaaa aaaa aaaa aaaa aaaa aaaa | bb bb |";
                            List<string> rows = MarkdownProbe.Plain(md, 30);
                            Check.Equal(30, rows[0].Length, "uses the full width");
                            Check.True(rows[0].IndexOf('┬') > 15, "the wide column keeps most of the space: " + rows[0]);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ShortColumnsStayWhole", "Squeezing satisfies short columns first, so short headers never wrap",
                        _ =>
                        {
                            string md = "| Option | p99 (ms) | Cost | Notes |\n|:--|--:|:-:|---|\n| Redis cache | 12 | $$ | Fastest, but another service to run and page on |\n| In-process LRU | 18 | $ | No network hop; memory bound per instance |";
                            List<string> rows = MarkdownProbe.Plain(md, 60);
                            Check.Equal("│ Option         │ p99 (ms) │ Cost │ Notes                 │", rows[1], "short columns at natural width");
                            Check.Equal(60, rows[0].Length, "uses the width");
                            MarkdownRenderOptions grid = new MarkdownRenderOptions();
                            grid.Table.MaxRowLines = 0;
                            List<string> tight = MarkdownProbe.Plain(md, 46, grid);
                            Check.Equal("│ Option        │ p99 (ms) │ Cost │ Notes    │", tight[1], "the narrower of two equal shortfalls is satisfied first");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Expand", "Expand stretches a narrow table to the layout width",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.Expand = true;
                            foreach (string row in MarkdownProbe.Plain(_Small, 40, o))
                                Check.Equal(40, row.Length, "row '" + row + "'");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "BorderFlags", "Outer border off, row separators on, and padding 0 and 2",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.ShowOuterBorder = false;
                            List<string> inner = MarkdownProbe.Plain(_Small, 80, o);
                            Check.Equal(3, inner.Count, "header, separator, body");
                            Check.Equal(" a   │ bb  ", inner[0], "no outer edges");
                            Check.Equal("─────┼─────", inner[1], "separator without edges");

                            o = new MarkdownRenderOptions();
                            o.Table.ShowRowSeparators = true;
                            Check.Equal(9, MarkdownProbe.Plain("| a |\n|---|\n| 1 |\n| 2 |\n| 3 |", 80, o).Count, "two separators between three rows");
                            o.Table.ShowHeaderSeparator = false;
                            Check.Equal(8, MarkdownProbe.Plain("| a |\n|---|\n| 1 |\n| 2 |\n| 3 |", 80, o).Count, "header separator off");

                            o = new MarkdownRenderOptions();
                            o.Table.CellPadding = 0;
                            Check.Equal("│a  │bb │", MarkdownProbe.Plain(_Small, 80, o)[1], "no padding");
                            o.Table.CellPadding = 2;
                            Check.Equal("│  a    │  bb   │", MarkdownProbe.Plain(_Small, 80, o)[1], "two spaces of padding");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "GlyphPresets", "Each glyph preset draws its own corners, and None omits horizontal lines",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.Glyphs = MarkdownTableGlyphs.Ascii;
                            List<string> ascii = MarkdownProbe.Plain(_Small, 80, o);
                            Check.Equal("+-----+-----+", ascii[0], "ascii top");
                            Check.Equal("| a   | bb  |", ascii[1], "ascii row");
                            o.Table.Glyphs = MarkdownTableGlyphs.Rounded;
                            Check.Equal("╰", MarkdownProbe.Plain(_Small, 80, o)[4].Substring(0, 1), "rounded");
                            o.Table.Glyphs = MarkdownTableGlyphs.Heavy;
                            Check.Equal("┣", MarkdownProbe.Plain(_Small, 80, o)[2].Substring(0, 1), "heavy");
                            o.Table.Glyphs = MarkdownTableGlyphs.Double;
                            Check.Equal("╬", MarkdownProbe.Plain(_Small, 80, o)[2].Substring(6, 1), "double");
                            o.Table.Glyphs = MarkdownTableGlyphs.None;
                            List<string> none = MarkdownProbe.Plain(_Small, 80, o);
                            Check.Equal(2, none.Count, "no horizontal lines");
                            Check.Equal(none[0].IndexOf("bb", StringComparison.Ordinal), none[1].IndexOf("d", StringComparison.Ordinal), "columns still align");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MaxColumnWidth", "MaxColumnWidth caps a long column and wraps it",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.MaxColumnWidth = 5;
                            List<string> rows = MarkdownProbe.Plain("| v |\n|---|\n| alpha beta gamma |", 80, o);
                            Check.Equal("│ alpha │", rows[3], "first wrapped row");
                            Check.Equal("│ beta  │", rows[4], "second wrapped row");
                            Check.Equal("│ gamma │", rows[5], "third wrapped row");
                            o.Table.MaxColumnWidth = 1;
                            Check.Equal("│ alp │", MarkdownProbe.Plain("| v |\n|---|\n| alpha |", 80, o)[3], "a cap below MinColumnWidth acts as MinColumnWidth");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Styles", "Header, first column, cell, and border styles land on the right spans",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Styles.TableFirstColumn = CellStyle.Default.WithForeground(Color.FromPalette(3));
                            o.Styles.TableCell = CellStyle.Default.WithBackground(Color.FromPalette(4));
                            o.Styles.TableBorder = CellStyle.Default.WithForeground(Color.FromPalette(1));
                            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("| Name | Value |\n|---|---|\n| key | val |", 80, o);
                            CellStyle header = MarkdownProbe.StyleOf(rows, "Name");
                            Check.Equal(Color.FromPalette(6), header.Foreground, "header color");
                            Check.True(header.HasAttribute(CellAttributes.Bold), "header bold");
                            Check.Equal(Color.FromPalette(4), header.Background, "header sits on the cell background");
                            CellStyle key = MarkdownProbe.StyleOf(rows, "key");
                            Check.Equal(Color.FromPalette(3), key.Foreground, "first column color");
                            Check.Equal(Color.FromPalette(4), key.Background, "first column on the cell background");
                            CellStyle value = MarkdownProbe.StyleOf(rows, "val");
                            Check.Equal(Color.Default, value.Foreground, "other cells keep the text color");
                            Check.Equal(Color.FromPalette(4), value.Background, "cell background");
                            Check.Equal(Color.FromPalette(1), MarkdownProbe.StyleOf(rows, "┌").Foreground, "border color");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FallbackStack", "When minimums do not fit, rows stack as Header: value records",
                        _ =>
                        {
                            List<string> rows = MarkdownProbe.Plain(_Hard, 20);
                            string all = MarkdownProbe.Join(rows);
                            Check.Equal("Name: server-alpha", rows[0], "first record row");
                            Check.True(all.Contains("Status: operational"), "every column present");
                            Check.False(all.Contains("┌"), "no grid");
                            List<string> two = MarkdownProbe.Plain(_Hard + "\n| b | c | d |", 20);
                            Check.True(MarkdownProbe.Join(two).Contains(new string('─', 20)), "a rule separates records");
                            List<string> boundary = MarkdownProbe.Plain(_Small, 12);
                            Check.Equal("a: ccc", boundary[0], "one column narrower than natural stacks when the minimums equal the naturals");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MaxRowLines", "A grid that would wrap a row past MaxRowLines stacks instead",
                        _ =>
                        {
                            string md = "| Option | Notes |\n|---|---|\n| redis | fastest but another service to run and page on at night |";
                            Check.Equal(4, MarkdownProbe.Plain(md, 28).Count - 4, "4 wrapped lines at 28 keep the grid");
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain(md, 24)).Contains("Notes: fastest"), "default limit of 4 stacks a 6-line row at 24");
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.MaxRowLines = 0;
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain(md, 24, o)).Contains("┌"), "0 keeps the grid");
                            o.Table.MaxRowLines = 10;
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain(md, 24, o)).Contains("┌"), "a higher limit keeps the grid");
                            o.Table.MaxRowLines = 1;
                            o.Table.CellOverflow = MarkdownOverflow.Truncate;
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain(md, 24, o)).Contains("┌"), "ignored when cells truncate");
                            o.Table.CellOverflow = MarkdownOverflow.Wrap;
                            o.Table.Fallback = MarkdownTableFallback.Shrink;
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain(md, 24, o)).Contains("┌"), "only the Stack fallback stacks");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.MaxRowLines = -1, "negative");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.MaxRowLines = 1001, "too large");
                            o.Table.MaxRowLines = 7;
                            Check.Equal(7, o.Clone().Table.MaxRowLines, "cloned");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FallbackShrinkAndClip", "Shrink keeps a grid inside the width; Clip keeps minimums and cuts",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.Fallback = MarkdownTableFallback.Shrink;
                            IReadOnlyList<StyledText> shrunk = MarkdownRenderer.Render(_Hard, 20, o);
                            MarkdownProbe.AssertFits(shrunk, 20, "shrink");
                            Check.True(shrunk[1].ToPlainString().Contains("│"), "grid kept");
                            Check.Equal(20, shrunk[0].Width, "grid fills the width");

                            o.Table.Fallback = MarkdownTableFallback.Clip;
                            IReadOnlyList<StyledText> clipped = MarkdownRenderer.Render(_Hard, 20, o);
                            MarkdownProbe.AssertFits(clipped, 20, "clip");
                            Check.True(MarkdownProbe.Join(MarkdownProbe.Plain(_Hard, 20, o)).Contains("│ server-alpha │"), "first column whole");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CellOverflow", "Truncate and Clip keep each row one line high",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.CellOverflow = MarkdownOverflow.Truncate;
                            List<string> truncated = MarkdownProbe.Plain(_Wide, 30, o);
                            Check.Equal(5, truncated.Count, "single-line rows");
                            Check.True(truncated[3].Contains("…"), "ellipsis: " + truncated[3]);
                            o.Table.CellOverflow = MarkdownOverflow.Clip;
                            List<string> clipped = MarkdownProbe.Plain(_Wide, 30, o);
                            Check.Equal(5, clipped.Count, "single-line rows");
                            Check.False(clipped[3].Contains("…"), "no ellipsis");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Shapes", "One column, header only, empty cells, and code-only cells",
                        _ =>
                        {
                            Check.Equal("│ only │", MarkdownProbe.Plain("| only |\n|---|\n| v |", 80)[1], "one column");
                            List<string> headerOnly = MarkdownProbe.Plain("| a | b |\n|---|---|", 80);
                            Check.Equal(4, headerOnly.Count, "top, header, separator, bottom");
                            Check.Equal("│     │ x   │", MarkdownProbe.Plain("| a | b |\n|---|---|\n|  | x |", 80)[3], "empty cell");
                            Check.Equal("│ a*b │", MarkdownProbe.Plain("| c |\n|---|\n| `a*b` |", 80)[3], "code-only cell");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TinyWidths", "Every fallback survives width 1",
                        _ =>
                        {
                            foreach (MarkdownTableFallback fallback in new[] { MarkdownTableFallback.Stack, MarkdownTableFallback.Shrink, MarkdownTableFallback.Clip })
                            {
                                MarkdownRenderOptions o = new MarkdownRenderOptions();
                                o.MinWidth = 1;
                                o.Table.Fallback = fallback;
                                MarkdownProbe.AssertFits(MarkdownRenderer.Render(_Hard, 1, o), 1, fallback.ToString());
                                MarkdownProbe.AssertFits(MarkdownRenderer.Render(_Hard, 3, o), 3, fallback.ToString());
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
