namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C3: <see cref="DataColumn{T}.Key"/>, visibility, the column chooser, the column cursor,
    /// keyboard sorting, and <see cref="DataTableSortChangedEventArgs.ColumnKey"/>.
    /// </summary>
    public static class DataTableColumnsSuite
    {
        private const string _Id = "DataTableColumns";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "DataTable Columns",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Keys", "Default keys equal names; explicit keys must be unique; duplicate names without keys still work",
                        _ =>
                        {
                            DataColumn<int> plain = new DataColumn<int>("Name", i => "x");
                            Check.Equal("Name", plain.Key, "default key");
                            Check.False(plain.HasExplicitKey, "defaulted");
                            DataColumn<int> keyed = new DataColumn<int>("name", "Name", i => "x");
                            Check.True(keyed.HasExplicitKey, "explicit");
                            Check.Throws<ArgumentException>(() => new DataColumn<int>("", "Name", i => "x"), "empty key");
                            Check.Throws<ArgumentException>(() => new DataColumn<int>(null!, "Name", i => "x"), "null key");
                            DataTable<int> table = new DataTable<int>().Column("id", "Id", i => "1");
                            Check.Throws<ArgumentException>(() => table.Column("id", "Other", i => "2"), "explicit vs explicit");
                            Check.Throws<ArgumentException>(() => table.Column("id", i => "2"), "defaulted vs explicit");
                            DataTable<int> dup = new DataTable<int>().Column("Same", i => "a").Column("Same", i => "b");
                            Check.Equal(2, dup.Columns.Count, "1.7 duplicate headers still allowed");
                            Check.Throws<ArgumentException>(() => dup.Column("Same", "Third", i => "c"), "explicit vs defaulted");
                            dup.SetColumnVisible("Same", false);
                            Check.False(dup.Columns[0].Visible, "key lookups act on the first");
                            Check.True(dup.Columns[1].Visible, "second untouched");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SortByKey", "SortByColumn(key) sorts; SortChanged carries ColumnKey; the old args constructor copies the name",
                        _ =>
                        {
                            DataTable<int> table = DataTablePagingSuite.Table(3);
                            DataTableSortChangedEventArgs? args = null;
                            table.SortChanged += (s, e) => args = e;
                            table.SortByColumn("sq", false);
                            Check.Equal(1, table.SortColumn, "sorted by Sq");
                            Check.Equal("sq", args!.ColumnKey, "ColumnKey");
                            Check.Equal("Sq", args.ColumnName, "ColumnName");
                            Check.Equal(3, table.Rows[0], "descending");
                            Check.Throws<ArgumentException>(() => table.SortByColumn("missing", true), "unknown key");
                            Check.Throws<ArgumentNullException>(() => table.SortByColumn((string)null!, true), "null key");
                            DataTableSortChangedEventArgs old = new DataTableSortChangedEventArgs(2, "Header", true);
                            Check.Equal("Header", old.ColumnKey, "old constructor");
                            table.PagingMode = DataTablePagingMode.Remote;
                            DataTablePageRequest? request = null;
                            table.PageRequested += (s, r) => request = r;
                            table.SortByColumn("n", true);
                            Check.Equal("n", request!.SortColumnKey, "request carries the key");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Visibility", "Hiding redistributes width; the last visible and CanHide false columns cannot be hidden; ColumnsChanged fires once per change",
                        _ =>
                        {
                            DataTable<int> table = Three();
                            int changes = 0;
                            table.ColumnsChanged += (s, e) => changes++;
                            string before = Snapshot.RenderWidget(table, 30, 2);
                            Check.True(table.SetColumnVisible("b", false), "hidden");
                            Check.Equal(1, changes, "one change");
                            Check.False(table.SetColumnVisible("b", false), "already hidden");
                            Check.Equal(1, changes, "no event for a no-op");
                            string after = Snapshot.RenderWidget(table, 30, 2);
                            Check.False(after.Contains("B"), "B gone");
                            Check.True(after.IndexOf("C", StringComparison.Ordinal) < before.IndexOf("C", StringComparison.Ordinal), "C moved left");
                            Check.Equal("A,C", string.Join(",", table.VisibleColumns.Select(c => c.Name)), "VisibleColumns");
                            table.Columns[0].CanHide = false;
                            Check.False(table.SetColumnVisible("a", false), "CanHide false refused");
                            table.Columns[0].CanHide = true;
                            Check.True(table.SetColumnVisible("c", false), "c hidden");
                            Check.False(table.SetColumnVisible("a", false), "last visible refused");
                            Check.Equal(1, table.VisibleColumns.Count, "one left");
                            Check.Throws<ArgumentException>(() => table.SetColumnVisible("zz", true), "unknown key");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SetVisibleColumns", "SetVisibleColumns shows exactly the keys plus CanHide false columns and ignores unknown keys",
                        _ =>
                        {
                            DataTable<int> table = Three();
                            table.Columns[2].CanHide = false;
                            int changes = 0;
                            table.ColumnsChanged += (s, e) => changes++;
                            table.SetVisibleColumns(new[] { "a", "removed-in-v2" });
                            Check.Equal("A,C", string.Join(",", table.VisibleColumns.Select(c => c.Name)), "a plus fixed c");
                            Check.Equal(1, changes, "one event");
                            table.Columns[2].CanHide = true;
                            table.SetVisibleColumns(new string[0]);
                            Check.Equal(1, table.VisibleColumns.Count, "never zero visible");
                            Check.Throws<ArgumentNullException>(() => table.SetVisibleColumns(null!), "null keys");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Chooser", "The chooser lists only hideable columns, pre-checked by visibility; applying a completed chooser updates visibility",
                        _ =>
                        {
                            DataTable<int> table = Three();
                            table.Columns[0].CanHide = false;
                            table.SetColumnVisible("c", false);
                            MultiSelectModal<DataColumn<int>> chooser = table.CreateColumnChooser("Columns");
                            Check.Equal("B,C", string.Join(",", chooser.List.Items.Select(c => c.Name)), "hideable only");
                            Check.True(chooser.List.IsChecked(0) && !chooser.List.IsChecked(1), "pre-checked by visibility");
                            Check.False(table.ApplyColumnChooser(chooser), "not completed yet");
                            chooser.List.SetChecked(0, false);
                            chooser.List.SetChecked(1, true);
                            chooser.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.True(table.ApplyColumnChooser(chooser), "applied");
                            Check.Equal("A,C", string.Join(",", table.VisibleColumns.Select(c => c.Name)), "visibility from the chooser");
                            MultiSelectModal<DataColumn<int>> cancelled = table.CreateColumnChooser("Columns");
                            cancelled.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.False(table.ApplyColumnChooser(cancelled), "cancelled chooser ignored");
                            Check.Throws<ArgumentNullException>(() => table.CreateColumnChooser(null!), "null title");
                            Check.Throws<ArgumentNullException>(() => table.ApplyColumnChooser(null!), "null chooser");
                            DataTable<int> fixedOnly = new DataTable<int>();
                            fixedOnly.Column(new DataColumn<int>("x", "X", i => "x") { CanHide = false });
                            Check.Throws<InvalidOperationException>(() => fixedOnly.CreateColumnChooser("Columns"), "nothing to choose");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CursorOff", "With ShowColumnCursor false, Left and Right are not consumed and FocusedColumn is -1",
                        _ =>
                        {
                            DataTable<int> table = Three();
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.Left)), "Left");
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.Right)), "Right");
                            Check.Equal(-1, table.FocusedColumn, "no cursor");
                            table.SortKey = KeyChord.Parse("s");
                            Check.False(table.HandleKey(KeyEvent.Char('s')), "sort key inactive without the cursor");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Cursor", "The cursor moves across visible columns, marks the header, and the sort keys sort and reverse",
                        _ =>
                        {
                            DataTable<int> table = Three();
                            table.Columns[1].Sortable = false;
                            table.ShowColumnCursor = true;
                            table.SortKey = KeyChord.Parse("s");
                            table.ReverseSortKey = KeyChord.Parse("shift+s");
                            Check.Equal(0, table.FocusedColumn, "first column");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(table, 30, 3);
                            Check.True((buffer.Get(0, 0).Style.Attributes & CellAttributes.Underline) != 0, "focused header underlined");
                            Check.True((buffer.Get(10, 0).Style.Attributes & CellAttributes.Underline) == 0, "others not");
                            Check.True(table.HandleKey(KeyEvent.Char('s')), "sort");
                            Check.Equal(0, table.SortColumn, "sorted by A");
                            Check.True(table.SortAscending, "ascending");
                            Check.True(table.HandleKey(KeyEvent.Char('s')), "again");
                            Check.False(table.SortAscending, "toggled");
                            table.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(1, table.FocusedColumn, "B");
                            Check.False(table.HandleKey(KeyEvent.Char('s')), "non-sortable ignores the sort key");
                            table.SetColumnVisible("b", false);
                            Check.Equal(1, table.FocusedColumn, "hidden column skipped: cursor moved to C (index 1 of visible)");
                            table.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(1, table.FocusedColumn, "stays at the last visible");
                            table.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.Equal(0, table.FocusedColumn, "back to A, skipping hidden B");
                            table.HandleKey(KeyEvent.Special(KeyCode.Right));
                            table.HandleKey(KeyEvent.Char('S'));
                            Check.Equal(2, table.SortColumn, "reverse sorts C");
                            Check.False(table.SortAscending, "descending");
                            Check.True(table.GetKeyHints()!.Any(h => h.Description == "Column"), "column hint");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static DataTable<int> Three()
        {
            DataTable<int> table = new DataTable<int>()
                .Column("a", "A", i => i.ToString(), true)
                .Column("b", "B", i => (i * 2).ToString(), true)
                .Column("c", "C", i => (i * 3).ToString(), true);
            table.Bind(new List<int> { 2, 1, 3 });
            return table;
        }
    }
}
