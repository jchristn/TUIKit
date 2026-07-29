namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for tranche 3: the diff view (10.12), the data table (10.17), syntax
    /// highlighting (10.18), and Markdown completeness — task lists, ordered lists, and tables (10.29).
    /// </summary>
    public static class Tranche3Suite
    {
        /// <summary>
        /// Builds the tranche 3 suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Tranche3",
                displayName: "Tranche 3 (Diff / Table / Markdown)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Tranche3", "DiffComputes", "Diff classifies added, removed, and context lines",
                        _ =>
                        {
                            DiffView diff = new DiffView("a\nb\nc", "a\nx\nc");
                            Check.Equal(4, diff.LineCount, "context+removed+added+context");
                            Check.Equal(1, diff.AddedCount, "one added");
                            Check.Equal(1, diff.RemovedCount, "one removed");

                            CellBuffer buffer = new CellBuffer(20, 6);
                            diff.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).StartsWith(" a"), "context keeps a leading space");
                            Check.True(ReadRow(buffer, 1).StartsWith("-b"), "removed line prefixed with minus");
                            Check.True(ReadRow(buffer, 2).StartsWith("+x"), "added line prefixed with plus");
                            Check.True(ReadRow(buffer, 3).StartsWith(" c"), "trailing context preserved");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "DiffIdentical", "Identical texts produce only context",
                        _ =>
                        {
                            DiffView diff = new DiffView("one\ntwo", "one\ntwo");
                            Check.Equal(2, diff.LineCount, "both lines kept");
                            Check.Equal(0, diff.AddedCount, "nothing added");
                            Check.Equal(0, diff.RemovedCount, "nothing removed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "DiffGuards", "Diff rejects null texts",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new DiffView(null!, "x"), "null old text");
                            Check.Throws<ArgumentNullException>(() => new DiffView("x", null!), "null new text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "DiffScroll", "Diff scrolls with the keyboard",
                        _ =>
                        {
                            StringBuilder oldText = new StringBuilder();
                            for (int i = 0; i < 30; i++)
                                oldText.Append("line" + i + "\n");

                            DiffView diff = new DiffView(oldText.ToString(), oldText.ToString());
                            Check.True(diff.HandleKey(KeyEvent.Special(KeyCode.Down)), "down consumed");
                            Check.True(diff.HandleKey(KeyEvent.Special(KeyCode.PageDown)), "page down consumed");
                            Check.False(diff.HandleKey(KeyEvent.Char('z')), "unrelated key ignored");

                            CellBuffer buffer = new CellBuffer(20, 4);
                            diff.Render(new BufferSurface(buffer));
                            Check.False(ReadRow(buffer, 0).Contains("line0"), "scrolled past the first line");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "TableRenders", "Data table renders headers and rows",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>()
                                .Column("Name", s => s.Split(',')[0])
                                .Column("Age", s => s.Split(',')[1], true);
                            table.Bind(new List<string> { "Alice,30", "Bob,25", "Cleo,40" });

                            Check.Equal(3, table.RowCount, "three rows bound");
                            Check.Equal(0, table.SelectedIndex, "first row selected");

                            CellBuffer buffer = new CellBuffer(30, 6);
                            table.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).Contains("Name") && ReadRow(buffer, 0).Contains("Age"), "header row");
                            Check.True(ReadRow(buffer, 1).Contains("Alice"), "first data row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "TableNavigate", "Data table moves selection with arrows",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>().Column("V", s => s);
                            table.Bind(new List<string> { "a", "b", "c" });

                            Check.True(table.HandleKey(KeyEvent.Special(KeyCode.Down)), "down consumed");
                            Check.Equal(1, table.SelectedIndex, "moved to second row");
                            Check.Equal("b", table.SelectedRow, "selected row value");
                            table.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(0, table.SelectedIndex, "moved back to first");
                            Check.False(table.HandleKey(KeyEvent.Char('q')), "unrelated key ignored");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "TableSort", "Data table sorts a sortable column and toggles order",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>()
                                .Column("Name", s => s.Split(',')[0])
                                .Column("Age", s => s.Split(',')[1], true);
                            table.Bind(new List<string> { "Alice,30", "Bob,25", "Cleo,40" });

                            table.SortByColumn(1);
                            Check.Equal("Bob,25", First(table), "ascending puts 25 first");
                            table.SortByColumn(1);
                            Check.Equal("Cleo,40", First(table), "second call flips to descending");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "TableGuards", "Data table validates inputs",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>().Column("V", s => s);
                            Check.Throws<ArgumentNullException>(() => table.Column(null!, s => s), "null header");
                            Check.Throws<ArgumentNullException>(() => table.Column("X", null!), "null selector");
                            Check.Throws<ArgumentNullException>(() => table.Bind(null!), "null rows");
                            Check.Throws<InvalidOperationException>(() => table.SortByColumn(0), "column not sortable");
                            Check.Throws<ArgumentOutOfRangeException>(() => table.SortByColumn(9), "column out of range");

                            DataTable<string> empty = new DataTable<string>().Column("V", s => s);
                            Check.Equal(-1, empty.SelectedIndex, "empty table has no selection");
                            Check.True(empty.SelectedRow == null, "empty table row is null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "SyntaxHighlight", "Syntax highlighter colors keywords, strings, and comments",
                        _ =>
                        {
                            StyledText line = SyntaxHighlighter.HighlightLine("int x = 5; // note", "csharp");
                            Check.Equal("int x = 5; // note", line.ToString(), "text preserved verbatim");

                            bool sawKeyword = false;
                            bool sawComment = false;
                            foreach (StyledSpan span in line.Spans)
                            {
                                if (span.Text.Contains("int") && span.Style.Foreground.Equals(Color.FromPalette(5)))
                                    sawKeyword = true;
                                if (span.Text.Contains("note") && span.Style.Foreground.Equals(Color.FromPalette(8)))
                                    sawComment = true;
                            }

                            Check.True(sawKeyword, "keyword highlighted");
                            Check.True(sawComment, "comment highlighted");

                            IReadOnlyList<StyledText> plain = SyntaxHighlighter.Highlight("hello world", "unknown-language");
                            Check.Equal(1, plain.Count, "unknown language still yields one line");
                            Check.Equal("hello world", plain[0].ToString(), "unknown language passes text through");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "MarkdownTaskList", "Markdown renders checked and unchecked task items",
                        _ =>
                        {
                            IReadOnlyList<StyledText> lines = MarkdownRenderer.Render("- [ ] todo\n- [x] done");
                            Check.True(lines[0].ToString().Contains("☐") && lines[0].ToString().Contains("todo"), "unchecked box");
                            Check.True(lines[1].ToString().Contains("☑") && lines[1].ToString().Contains("done"), "checked box");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "MarkdownOrdered", "Markdown preserves ordered list markers",
                        _ =>
                        {
                            IReadOnlyList<StyledText> lines = MarkdownRenderer.Render("1. first\n2. second");
                            Check.True(lines[0].ToString().Contains("1. first"), "first item marker kept");
                            Check.True(lines[1].ToString().Contains("2. second"), "second item marker kept");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "MarkdownNested", "Markdown indents nested bullets",
                        _ =>
                        {
                            IReadOnlyList<StyledText> lines = MarkdownRenderer.Render("- top\n    - nested");
                            Check.True(lines[0].ToString().StartsWith("•"), "top bullet flush left");
                            Check.True(lines[1].ToString().StartsWith(" ") && lines[1].ToString().Contains("• nested"), "nested bullet indented");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche3", "MarkdownTable", "Markdown renders table rows and rules",
                        _ =>
                        {
                            IReadOnlyList<StyledText> lines = MarkdownRenderer.Render("| A | B |\n| --- | --- |\n| 1 | 2 |");
                            Check.True(lines[0].ToString().Contains("A") && lines[0].ToString().Contains("│"), "header cells with divider");
                            Check.True(lines[1].ToString().Contains("─"), "separator rendered as a rule");
                            Check.True(lines[2].ToString().Contains("1") && lines[2].ToString().Contains("2"), "data cells");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string First(DataTable<string> table)
        {
            return table.SelectedRow ?? string.Empty;
        }

        private static string ReadRow(CellBuffer buffer, int row)
        {
            StringBuilder builder = new StringBuilder();
            for (int x = 0; x < buffer.Width; x++)
                builder.Append(buffer.Get(x, row).Grapheme);

            return builder.ToString().TrimEnd();
        }
    }
}
