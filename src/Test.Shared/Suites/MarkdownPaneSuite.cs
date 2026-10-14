namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Testing;

    /// <summary>
    /// Coverage for Markdown blocks in a <see cref="Pane"/> (plan item M7): reflow on resize, in-place updates,
    /// option snapshots, search, capacity, plain snapshots, partial lines, line length limits, and guards.
    /// </summary>
    public static class MarkdownPaneSuite
    {
        private const string _Id = "MarkdownPane";

        private const string _Table = "| Name | Description |\n|---|---|\n| server-alpha | responsible for everything in production |";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown in Panes",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Reflow", "A committed table reflows when the pane renders at a new width",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            pane.WriteMarkdown(_Table);
                            string wide = RenderText(pane, 80, 10);
                            Check.True(wide.Contains("┌"), "grid at 80");
                            Check.True(wide.Contains("│ server-alpha │ responsible for everything in production │"), "natural widths at 80");
                            string narrow = RenderText(pane, 24, 10);
                            Check.False(narrow.Contains("┌"), "no grid at 24");
                            Check.True(narrow.Contains("Name: server-alpha"), "stacked at 24");
                            Check.True(RenderText(pane, 80, 10).Contains("┌"), "grid again at 80");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "UpdateMarkdown", "UpdateMarkdown replaces a block and keeps its options",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            o.Table.Glyphs = MarkdownTableGlyphs.Ascii;
                            PaneLineHandle handle = pane.WriteMarkdown("first", o);
                            Check.True(handle.UpdateMarkdown("| a |\n|---|\n| b |"), "updated");
                            string text = RenderText(pane, 40, 8);
                            Check.False(text.Contains("first"), "old content gone");
                            Check.True(text.Contains("+-----+"), "ascii glyphs kept");
                            Check.True(handle.UpdateMarkdown("| a |\n|---|", new MarkdownRenderOptions()), "updated with new options");
                            Check.True(RenderText(pane, 40, 8).Contains("┌"), "new options applied");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "OptionsSnapshot", "Options changed after the write do not affect the block",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            pane.WriteMarkdown("| a |\n|---|\n| b |", o);
                            o.Table.Glyphs = MarkdownTableGlyphs.Ascii;
                            o.Table = new MarkdownTableOptions { ShowOuterBorder = false };
                            Check.True(RenderText(pane, 40, 8).Contains("┌"), "block kept its own options");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PlainAndMarkdownConvert", "Update turns a block into plain text; UpdateMarkdown turns a line into a block",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            PaneLineHandle block = pane.WriteMarkdown("**bold**");
                            block.Update("plain **text**");
                            Check.True(RenderText(pane, 40, 4).Contains("plain **text**"), "plain now");
                            PaneLineHandle line = pane.WriteLine("x");
                            line.UpdateMarkdown("- item");
                            Check.True(RenderText(pane, 40, 4).Contains("• item"), "markdown now");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SearchAndSnapshot", "Search finds text inside a block; snapshots expand blocks into rows",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            pane.WriteLine("before");
                            pane.WriteMarkdown(_Table);
                            pane.WriteLine("after");
                            RenderText(pane, 80, 3);
                            pane.SetSearch("responsible");
                            Check.True(pane.FindNext(), "found inside the block");
                            IReadOnlyList<string> lines = pane.SnapshotPlainLines();
                            Check.Equal("before", lines[0], "first plain line");
                            Check.Equal("after", lines[lines.Count - 1], "last plain line");
                            Check.Equal(7, lines.Count, "five table rows between");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Capacity", "A block counts as one line for scrollback capacity",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            pane.ScrollbackCapacityLines = 2;
                            pane.WriteMarkdown("- a\n- b\n- c");
                            Check.Equal(1, pane.LineCount, "one committed line");
                            pane.WriteLine("x");
                            pane.WriteLine("y");
                            Check.False(string.Join("\n", pane.SnapshotPlainLines()).Contains("• a"), "evicted whole");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PartialLineFirst", "Uncommitted text is committed as its own line before the block",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            pane.Write("partial");
                            pane.WriteMarkdown("# Head");
                            IReadOnlyList<string> lines = pane.SnapshotPlainLines();
                            Check.Equal("partial", lines[0], "partial committed");
                            Check.Equal("Head", lines[1], "block after it");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MaxLineLength", "MaxLineLength applies to each source line, not the whole block",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            pane.MaxLineLength = 10;
                            pane.WriteMarkdown("short\n\n" + new string('x', 30));
                            IReadOnlyList<string> lines = pane.SnapshotPlainLines();
                            Check.Equal("short", lines[0], "short line intact");
                            Check.Equal(new string('x', 10) + "…", lines[2], "long line cut");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Guards", "Null arguments throw; updating a removed block returns false",
                        _ =>
                        {
                            Pane pane = new Pane("md");
                            Check.Throws<ArgumentNullException>(() => pane.WriteMarkdown(null!), "null markdown");
                            Check.Throws<ArgumentNullException>(() => pane.WriteMarkdown("x", null!), "null options");
                            PaneLineHandle handle = pane.WriteMarkdown("x");
                            Check.Throws<ArgumentNullException>(() => handle.UpdateMarkdown(null!), "null update");
                            Check.Throws<ArgumentNullException>(() => handle.UpdateMarkdown("x", null!), "null update options");
                            Check.True(handle.Remove(), "removed");
                            Check.False(handle.UpdateMarkdown("y"), "update after remove");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string RenderText(Pane pane, int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            pane.Render(new BufferSurface(buffer));
            return Snapshot.ToText(buffer);
        }
    }
}
