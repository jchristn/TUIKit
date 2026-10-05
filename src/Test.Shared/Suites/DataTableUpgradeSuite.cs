namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the DataTable upgrades (U3): column sizing and alignment, typed sorting
    /// with indicators and header clicks, server-side sort, multi-select, per-cell styles, paging hooks,
    /// key-stable rebinds, and CJK-safe widths.
    /// </summary>
    public static class DataTableUpgradeSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DataTableUpgrade",
                displayName: "DataTable Sizing, Sorting, and Selection",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DataTableUpgrade", "TypedSort", "SortKey sorts numerically and the sort is stable",
                        _ =>
                        {
                            DataTable<int[]> table = new DataTable<int[]>();
                            table.Column(new DataColumn<int[]>("N", r => r[0].ToString(), true) { SortKey = r => r[0] });
                            table.Bind(new List<int[]> { new[] { 10, 0 }, new[] { 9, 1 }, new[] { 100, 2 }, new[] { 9, 3 } });
                            table.SortByColumn(0);
                            Check.Equal("9,9,10,100", Join(table), "numeric, not ordinal");
                            Check.Equal(1, table.Rows[0][1], "stable: equal keys keep their order");
                            Check.Equal(3, table.Rows[1][1], "stable second");
                            table.SortByColumn(0);
                            Check.Equal("100,10,9,9", Join(table), "descending");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "IndicatorAndHeaderClick", "The header shows a sort indicator and a header click sorts",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>()
                                .Column("Name", s => s, true)
                                .Column("Len", s => s.Length.ToString(), false);
                            table.Bind(new List<string> { "b", "a", "c" });
                            Snapshot.RenderWidget(table, 30, 5);
                            int sorts = 0;
                            table.SortChanged += (s, e) => sorts++;
                            Check.True(table.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 0, KeyModifiers.None, 1)), "header click consumed");
                            Check.Equal(0, table.SortColumn, "sorted by Name");
                            Check.Equal(1, sorts, "event raised");
                            string text = Snapshot.RenderWidget(table, 30, 5);
                            Check.True(text.Contains("\u25B2"), "ascending indicator drawn");
                            Check.False(table.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 20, 0, KeyModifiers.None, 1)), "unsortable header ignored");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "ServerSort", "With SortLocally off the rows keep their order and the sort is reported",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string> { SortLocally = false }.Column("V", s => s, true);
                            table.Bind(new List<string> { "b", "a" });
                            DataTableSortChangedEventArgs? args = null;
                            table.SortChanged += (s, e) => args = e;
                            table.SortByColumn(0, false);
                            Check.Equal("b", table.Rows[0], "not reordered");
                            Check.True(args != null && args.ColumnName == "V" && !args.Ascending, "sort reported");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "ColumnSizing", "Fixed and weighted columns with right alignment",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>();
                            table.Column(new DataColumn<string>("Id", s => s) { Width = 4 });
                            table.Column(new DataColumn<string>("Name", s => "name-" + s) { Weight = 3 });
                            table.Column(new DataColumn<string>("Qty", s => "7") { Weight = 1, Alignment = CellAlignment.Right });
                            table.Bind(new List<string> { "1" });
                            CellBuffer buffer = new CellBuffer(24, 2);
                            table.Render(new BufferSurface(buffer));
                            Check.Equal("I", buffer.Get(0, 0).Grapheme, "Id at 0");
                            Check.Equal("N", buffer.Get(4, 0).Grapheme, "Name after the fixed 4");
                            Check.Equal("Q", buffer.Get(20, 0).Grapheme, "Qty header right-aligned after 15 weighted columns");
                            Check.Equal("7", buffer.Get(22, 1).Grapheme, "right aligned within its cell");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "CjkWidths", "Wide glyphs never overflow into the next column",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>()
                                .Column("A", s => s)
                                .Column("B", s => "x");
                            table.Bind(new List<string> { "\u4E2D\u6587\u5B57\u7B26\u6D4B\u8BD5" });
                            CellBuffer buffer = new CellBuffer(12, 2);
                            table.Render(new BufferSurface(buffer));
                            Check.Equal("x", buffer.Get(6, 1).Grapheme, "second column starts at 6");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "MultiSelect", "Space, Shift+Down, Ctrl+A, and Escape manage marks",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string> { MultiSelect = true }.Column("V", s => s);
                            table.Bind(new List<string> { "a", "b", "c", "d" });
                            int changes = 0;
                            table.MarkedChanged += (s, e) => changes++;
                            table.HandleKey(KeyEvent.Char(' '));
                            Check.Equal(1, table.MarkedCount, "Space marked");
                            table.HandleKey(KeyEvent.Special(KeyCode.Down, KeyModifiers.Shift));
                            Check.Equal("a,b", string.Join(",", table.MarkedRows), "Shift+Down extended");
                            table.HandleKey(KeyEvent.Char('a', KeyModifiers.Ctrl));
                            Check.Equal(4, table.MarkedCount, "Ctrl+A marked all");
                            string text = Snapshot.RenderWidget(table, 20, 6);
                            Check.True(text.Contains("[x]"), "mark column drawn");
                            Check.True(table.HandleKey(KeyEvent.Special(KeyCode.Escape)), "Escape consumed while marks exist");
                            Check.Equal(0, table.MarkedCount, "cleared");
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.Escape)), "Escape falls through with no marks");
                            Check.True(changes >= 4, "events raised");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "KeyedRebind", "KeySelector keeps selection and marks on their rows across Bind",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string> { MultiSelect = true, KeySelector = s => s }.Column("V", s => s);
                            table.Bind(new List<string> { "a", "b", "c" });
                            table.Select(2);
                            table.SetMarked(1, true);
                            table.Bind(new List<string> { "z", "c", "b", "a" });
                            Check.Equal("c", table.SelectedRow, "selection followed the row");
                            Check.Equal("b", string.Join(",", table.MarkedRows), "mark followed the row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "CellStylesAndPaging", "Per-cell styles render and EndReached fires near the end",
                        _ =>
                        {
                            Color red = Color.FromPalette(1);
                            DataTable<string> table = new DataTable<string>();
                            table.Column(new DataColumn<string>("S", s => s) { CellStyle = s => s == "bad" ? CellStyle.Default.WithForeground(red) : (CellStyle?)null });
                            table.Bind(new List<string> { "ok", "bad", "ok" });
                            CellBuffer buffer = new CellBuffer(10, 4);
                            table.Render(new BufferSurface(buffer));
                            Check.True(buffer.Get(0, 2).Style.Foreground == red, "styled cell");
                            int ends = 0;
                            table.EndReached += () => ends++;
                            table.EndReachedThreshold = 1;
                            table.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(1, ends, "fired within the threshold");
                            int activated = -1;
                            table.RowActivated += i => activated = i;
                            table.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(1, activated, "RowActivated on Enter");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DataTableUpgrade", "SelectionEvent", "SelectionChanged fires for navigation and Select",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>().Column("V", s => s);
                            table.Bind(new List<string> { "a", "b" });
                            List<int> seen = new List<int>();
                            table.SelectionChanged += (s, e) => seen.Add(e.NewValue);
                            table.HandleKey(KeyEvent.Special(KeyCode.Down));
                            table.Select(0);
                            table.Select(0);
                            Check.Equal("1,0", string.Join(",", seen), "two changes");
                            table.IsEnabled = false;
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.Down)), "disabled");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string Join(DataTable<int[]> table)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < table.RowCount; i++)
                values.Add(table.Rows[i][0].ToString());
            return string.Join(",", values);
        }
    }
}
