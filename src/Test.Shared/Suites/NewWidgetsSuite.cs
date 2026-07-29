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
    /// Touchstone suite covering the new widgets: status bar (10.7), scroll view (10.2), collapsible
    /// (10.13), tab view (10.14), tree (10.11), and fuzzy list (10.3).
    /// </summary>
    public static class NewWidgetsSuite
    {
        /// <summary>
        /// Builds the new widgets suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "NewWidgets",
                displayName: "New Widgets",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("NewWidgets", "StatusBar", "Status bar renders key/label hints",
                        _ =>
                        {
                            StatusBar bar = new StatusBar().Add("^Q", "quit").Add("^P", "palette");
                            Check.Equal(2, bar.Count, "Two hints");
                            CellBuffer buffer = new CellBuffer(40, 1);
                            bar.Render(new BufferSurface(buffer));
                            string row = ReadRow(buffer, 0);
                            Check.True(row.Contains("^Q") && row.Contains("quit") && row.Contains("^P"), "Hints rendered");
                            bar.Clear();
                            Check.Equal(0, bar.Count, "Cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "ScrollViewWindow", "Scroll view blits a window and scrolls",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            for (int i = 0; i < 20; i++)
                                pane.WriteLine("L" + i);

                            ScrollView view = new ScrollView(pane, 8, 20);
                            CellBuffer buffer = new CellBuffer(10, 6);
                            view.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).StartsWith("L0"), "Shows first line at top");

                            view.ScrollBy(0, 3);
                            buffer = new CellBuffer(10, 6);
                            view.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).StartsWith("L3"), "Scrolled down three lines");

                            string bar = buffer.Get(9, 0).Grapheme;
                            Check.True(bar == "│" || bar == "█", "Vertical scrollbar drawn on the right");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "ScrollViewGuards", "Scroll view rejects bad arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new ScrollView(null!, 5, 5), "Null child");
                            Check.Throws<ArgumentOutOfRangeException>(() => new ScrollView(new Pane("p"), 0, 5), "Zero width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "Collapsible", "Collapsible shows/hides its child",
                        _ =>
                        {
                            Collapsible section = new Collapsible("Head", new Label(Text.From("BODY")));
                            CellBuffer buffer = new CellBuffer(12, 3);
                            section.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).Contains("▼"), "Expanded disclosure");
                            Check.True(ReadRow(buffer, 1).Contains("BODY"), "Child visible when expanded");

                            section.HandleKey(KeyEvent.Special(KeyCode.Enter)); // collapse
                            buffer = new CellBuffer(12, 3);
                            section.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).Contains("▶"), "Collapsed disclosure");
                            Check.False(ReadRow(buffer, 1).Contains("BODY"), "Child hidden when collapsed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "TabView", "Tab view switches active content",
                        _ =>
                        {
                            TabView tabs = new TabView()
                                .Add("A", new Label(Text.From("AAA")))
                                .Add("B", new Label(Text.From("BBB")));
                            Check.Equal(0, tabs.ActiveIndex, "First tab active");

                            CellBuffer buffer = new CellBuffer(14, 3);
                            tabs.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 1).Contains("AAA"), "First tab content");

                            tabs.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(1, tabs.ActiveIndex, "Second tab active");
                            buffer = new CellBuffer(14, 3);
                            tabs.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 1).Contains("BBB"), "Second tab content");

                            Check.Throws<ArgumentOutOfRangeException>(() => tabs.Activate(5), "Activate out of range");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "Tree", "Tree expands, collapses, and navigates",
                        _ =>
                        {
                            Dictionary<string, string[]> children = new Dictionary<string, string[]>
                            {
                                { "root", new[] { "a", "b" } },
                                { "a", new[] { "a1" } }
                            };
                            Tree<string> tree = new Tree<string>(
                                "root",
                                s => children.TryGetValue(s, out string[]? kids) ? kids : Array.Empty<string>(),
                                s => s);

                            CellBuffer buffer = new CellBuffer(20, 6);
                            tree.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).Contains("root"), "Root shown");
                            Check.True(ReadRow(buffer, 1).Contains("a"), "Child a shown");
                            Check.True(ReadRow(buffer, 2).Contains("b"), "Child b shown");

                            tree.Expand("a");
                            buffer = new CellBuffer(20, 6);
                            tree.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 2).Contains("a1"), "Grandchild shown after expand");

                            tree.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal("a", tree.SelectedNode, "Down moves selection to 'a'");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "TreeGuards", "Tree rejects null root",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(
                                () => new Tree<string>(null!, s => Array.Empty<string>(), s => s), "Null root");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "FuzzyList", "Fuzzy list filters and highlights",
                        _ =>
                        {
                            FuzzyList list = new FuzzyList(new[] { "apple", "banana", "grape" });
                            list.Query = "ap";
                            Check.Equal(2, list.MatchCount, "apple and grape match 'ap'");

                            list.Query = "xyz";
                            Check.Equal(0, list.MatchCount, "No matches");
                            Check.True(list.SelectedItem == null, "No selection when empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NewWidgets", "FuzzyTyping", "Typing edits the fuzzy query",
                        _ =>
                        {
                            FuzzyList list = new FuzzyList(new[] { "alpha", "beta" });
                            list.HandleKey(KeyEvent.Char('a'));
                            list.HandleKey(KeyEvent.Char('l'));
                            Check.Equal("al", list.Query, "Query built from typing");
                            list.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("a", list.Query, "Backspace edits the query");

                            Check.Throws<ArgumentNullException>(() => new FuzzyList(null!), "Null items");
                            return Task.CompletedTask;
                        })
                });
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
