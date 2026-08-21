namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for the page and jump navigation keys — PageUp, PageDown, Home, and End —
    /// across the selection and scroll widgets and the modals built on them. Paging moves by the
    /// widget's last-rendered viewport height, so each positive case renders once to establish that
    /// height before feeding keys. Negative cases assert the keys are safe (and clamp) on empty or
    /// unmatched lists.
    /// </summary>
    public static class NavigationKeysSuite
    {
        /// <summary>
        /// Builds the navigation-keys suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "NavigationKeys",
                displayName: "Page / Home / End Navigation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("NavigationKeys", "ListViewPaging", "ListView pages and jumps with PageUp/PageDown/Home/End",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.SetItems(MakeItems(20));
                            Render(list, 12, 5); // viewport height 5

                            list.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(5, list.SelectedIndex, "PageDown advances by one page");
                            list.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(10, list.SelectedIndex, "PageDown again advances another page");
                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(19, list.SelectedIndex, "End jumps to the last item");
                            list.HandleKey(KeyEvent.Special(KeyCode.PageUp));
                            Check.Equal(14, list.SelectedIndex, "PageUp retreats by one page");
                            list.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, list.SelectedIndex, "Home jumps to the first item");
                            list.HandleKey(KeyEvent.Special(KeyCode.PageUp));
                            Check.Equal(0, list.SelectedIndex, "PageUp at the top clamps");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "ListViewEmptySafe", "ListView paging is a safe no-op when empty",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            Check.Equal(-1, list.SelectedIndex, "empty list has no selection");
                            Check.True(list.HandleKey(KeyEvent.Special(KeyCode.PageDown)), "PageDown consumed");
                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            list.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(-1, list.SelectedIndex, "still empty, nothing thrown");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "SelectModalHomeEnd", "SelectModal (SelectAsync) honors Home and End",
                        async ct =>
                        {
                            SelectModal end = new SelectModal("Pick", MakeItems(10));
                            end.HandleKey(KeyEvent.Special(KeyCode.End));
                            end.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(9, (int)(await end.Completion.ConfigureAwait(false))!, "End then Enter chooses the last option");

                            SelectModal home = new SelectModal("Pick", MakeItems(10));
                            home.HandleKey(KeyEvent.Special(KeyCode.Down));
                            home.HandleKey(KeyEvent.Special(KeyCode.Down));
                            home.HandleKey(KeyEvent.Special(KeyCode.Home));
                            home.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(0, (int)(await home.Completion.ConfigureAwait(false))!, "Home then Enter chooses the first option");
                        }),

                    new TestCaseDescriptor("NavigationKeys", "CheckListPaging", "CheckList pages with PageUp/PageDown and jumps with Home/End",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(MakeItems(10));
                            Render(list, 20, 3); // viewport height 3

                            list.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(3, list.SelectedIndex, "PageDown advances by one page");
                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(9, list.SelectedIndex, "End jumps to the last item");
                            list.HandleKey(KeyEvent.Special(KeyCode.PageUp));
                            Check.Equal(6, list.SelectedIndex, "PageUp retreats by one page");
                            list.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, list.SelectedIndex, "Home jumps to the first item");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "CheckListEmptyIgnores", "CheckList ignores paging keys when empty",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(Array.Empty<string>());
                            Check.False(list.HandleKey(KeyEvent.Special(KeyCode.PageDown)), "PageDown not consumed when empty");
                            Check.False(list.HandleKey(KeyEvent.Special(KeyCode.End)), "End not consumed when empty");
                            Check.Equal(-1, list.SelectedIndex, "no selection");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "MultiSelectModalEnd", "MultiSelectModal routes End to the cursor",
                        async ct =>
                        {
                            MultiSelectModal<string> modal = new MultiSelectModal<string>("Pick", MakeItems(10));
                            modal.HandleKey(KeyEvent.Special(KeyCode.End));
                            modal.HandleKey(KeyEvent.Char(' '));   // toggle the last item
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));

                            IReadOnlyList<int> indices = (IReadOnlyList<int>)(await modal.Completion.ConfigureAwait(false))!;
                            Check.Equal(1, indices.Count, "one item checked");
                            Check.Equal(9, indices[0], "End moved the cursor to the last item");
                        }),

                    new TestCaseDescriptor("NavigationKeys", "TreeHomeEndPaging", "Tree pages and jumps through visible nodes",
                        _ =>
                        {
                            Tree<int> tree = new Tree<int>(
                                0,
                                n => n == 0 ? new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 } : Array.Empty<int>(),
                                n => n.ToString());
                            Render(tree, 20, 4); // viewport height 4; visible nodes 0..9

                            tree.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(9, tree.SelectedNode, "End selects the last visible node");
                            tree.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, tree.SelectedNode, "Home selects the first visible node");
                            tree.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(4, tree.SelectedNode, "PageDown advances by one page");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "DataTablePaging", "DataTable pages and jumps through rows",
                        _ =>
                        {
                            DataTable<int> table = new DataTable<int>().Column("n", i => i.ToString());
                            List<int> rows = new List<int>();
                            for (int i = 0; i < 20; i++)
                                rows.Add(i);
                            table.Bind(rows);
                            Render(table, 20, 6); // header + 5 visible rows

                            table.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(5, table.SelectedIndex, "PageDown advances by one page of rows");
                            table.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(19, table.SelectedIndex, "End jumps to the last row");
                            table.HandleKey(KeyEvent.Special(KeyCode.PageUp));
                            Check.Equal(14, table.SelectedIndex, "PageUp retreats by one page");
                            table.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, table.SelectedIndex, "Home jumps to the first row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "DataTableEmptySafe", "DataTable paging is a safe no-op when empty",
                        _ =>
                        {
                            DataTable<int> table = new DataTable<int>().Column("n", i => i.ToString());
                            Check.Equal(-1, table.SelectedIndex, "empty table has no selection");
                            table.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            table.HandleKey(KeyEvent.Special(KeyCode.End));
                            table.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(-1, table.SelectedIndex, "still empty, nothing thrown");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "FuzzyListPaging", "FuzzyList pages and jumps through matches",
                        _ =>
                        {
                            List<string> items = new List<string>();
                            for (int i = 0; i < 20; i++)
                                items.Add("item" + i.ToString("00"));
                            FuzzyList<string> list = new FuzzyList<string>(items);
                            Render(list, 20, 6); // prompt + 5 visible rows

                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal("item19", list.SelectedItem, "End selects the last match");
                            list.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal("item00", list.SelectedItem, "Home selects the first match");
                            list.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal("item05", list.SelectedItem, "PageDown advances by one page");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "FuzzyListNoMatchSafe", "FuzzyList paging is safe when nothing matches",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "apple", "banana" });
                            list.Query = "zzz";
                            Check.Equal(0, list.MatchCount, "query matches nothing");
                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            list.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.True(list.SelectedItem == null, "no selection, nothing thrown");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "ReorderableListDelegates", "ReorderableList inherits End through its inner list",
                        _ =>
                        {
                            ReorderableList<string> list = new ReorderableList<string>(MakeItems(10));
                            Render(list, 12, 4);
                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(9, list.SelectedIndex, "End reaches the inner ListView");
                            list.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, list.SelectedIndex, "Home reaches the inner ListView");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "KeyBindingEditorPaging", "KeyBindingEditor pages and jumps through bindings",
                        _ =>
                        {
                            KeyBindingSet set = new KeyBindingSet();
                            for (int i = 0; i < 12; i++)
                                set.Add("cmd" + i, ((char)('a' + i)).ToString());
                            KeyBindingEditor editor = new KeyBindingEditor(set);
                            Render(editor, 24, 6); // header + 5 visible rows

                            editor.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(11, editor.SelectedIndex, "End selects the last binding");
                            editor.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, editor.SelectedIndex, "Home selects the first binding");
                            editor.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(5, editor.SelectedIndex, "PageDown advances by one page");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "ScrollViewHomeEnd", "ScrollView jumps to top and bottom with Home/End",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            for (int i = 0; i < 50; i++)
                                pane.WriteLine("L" + i);
                            ScrollView view = new ScrollView(pane, 8, 100);
                            Render(view, 10, 10); // establishes the viewport (inner height 10)

                            view.HandleKey(KeyEvent.Special(KeyCode.End));
                            Render(view, 10, 10); // render clamps the offset to the last full viewport
                            Check.Equal(90, view.ScrollY, "End scrolls to the bottom (content 100 - viewport 10)");
                            view.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, view.ScrollY, "Home scrolls to the top");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "DiffViewHomeEnd", "DiffView jumps to the first and last line with Home/End",
                        _ =>
                        {
                            StringBuilder builder = new StringBuilder();
                            for (int i = 0; i < 30; i++)
                            {
                                if (i > 0)
                                    builder.Append('\n');
                                builder.Append("l").Append(i);
                            }

                            DiffView diff = new DiffView("l0", builder.ToString());

                            diff.HandleKey(KeyEvent.Special(KeyCode.End));
                            CellBuffer bottom = Render(diff, 40, 5);
                            Check.True(Row(bottom, 0).Contains("l29"), "End scrolls the last line into view");

                            diff.HandleKey(KeyEvent.Special(KeyCode.Home));
                            CellBuffer top = Render(diff, 40, 5);
                            Check.True(Row(top, 0).Contains("l0"), "Home scrolls back to the first line");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NavigationKeys", "FileBrowserHomeEnd", "FileBrowser pages and jumps through entries",
                        _ =>
                        {
                            string dir = Path.Combine(Path.GetTempPath(), "tuikit_nav_" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(dir);
                            try
                            {
                                for (int i = 0; i < 12; i++)
                                    File.WriteAllText(Path.Combine(dir, "file" + i.ToString("00") + ".txt"), string.Empty);

                                FileBrowser browser = new FileBrowser(dir);
                                Render(browser, 30, 6); // header + 5 visible rows; entries are ".." then file00..file11

                                browser.HandleKey(KeyEvent.Special(KeyCode.End));
                                Check.Equal("file11.txt", browser.SelectedName, "End selects the last file");
                                browser.HandleKey(KeyEvent.Special(KeyCode.Home));
                                Check.Equal("..", browser.SelectedName, "Home selects the parent link");
                                browser.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                                Check.Equal("file04.txt", browser.SelectedName, "PageDown advances by one page of entries");
                            }
                            finally
                            {
                                Directory.Delete(dir, true);
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static List<string> MakeItems(int count)
        {
            List<string> items = new List<string>(count);
            for (int i = 0; i < count; i++)
                items.Add("item" + i.ToString("00"));

            return items;
        }

        private static CellBuffer Render(IWidget widget, int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            widget.Render(new BufferSurface(buffer));
            return buffer;
        }

        private static string Row(CellBuffer buffer, int y)
        {
            StringBuilder builder = new StringBuilder(buffer.Width);
            for (int x = 0; x < buffer.Width; x++)
                builder.Append(buffer.Get(x, y).Grapheme);

            return builder.ToString();
        }
    }
}
