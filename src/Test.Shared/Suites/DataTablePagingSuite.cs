namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C2: <see cref="DataTable{T}.PagingMode"/>, page state and navigation, the paging bar,
    /// <see cref="DataTable{T}.BindPage"/>, <see cref="DataTable{T}.PageRequested"/>, <see cref="DataTablePageRequest"/>,
    /// page keys, <see cref="DataTable{T}.PageAtEdges"/>, marks across pages, and <see cref="DataTable{T}.EndReached"/>.
    /// </summary>
    public static class DataTablePagingSuite
    {
        private const string _Id = "DataTablePaging";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "DataTable Paging",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "NoneUnchanged", "With PagingMode None a populated and an empty table render as in 1.7",
                        _ =>
                        {
                            DataTable<int> table = Table(5);
                            Check.Equal(DataTablePagingMode.None, table.PagingMode, "default None");
                            string screen = Snapshot.RenderWidget(table, 20, 7);
                            string expected = string.Join("\n", new[] { "N".PadRight(10) + "Sq" }.Concat(Enumerable.Range(1, 5).Select(i => i.ToString().PadRight(10) + (i * i))));
                            Check.Equal(expected, screen.TrimEnd(), "populated");
                            Check.Equal(1, table.PageCount, "one page");
                            Check.False(table.NextPage(), "no paging");
                            DataTable<int> empty = Table(0);
                            empty.EmptyText = "Nothing";
                            Check.Equal("N".PadRight(10) + "Sq\nNothing", Snapshot.RenderWidget(empty, 20, 4).TrimEnd(), "empty");
                            Check.Equal(new Size(20, 6), table.Measure(new Size(20, 30)), "measure unchanged");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LocalPages", "Local mode: 60 rows at 25 per page give three pages with the right bar text",
                        _ =>
                        {
                            DataTable<int> table = Table(60);
                            table.PagingMode = DataTablePagingMode.Local;
                            Check.Equal(3, table.PageCount, "three pages");
                            Check.True(LastLine(table, 40, 30).StartsWith("Showing 1-25 of 60. Page 1 of 3.", StringComparison.Ordinal), "page 1 bar");
                            Check.True(table.NextPage(), "next");
                            Check.True(LastLine(table, 40, 30).StartsWith("Showing 26-50 of 60. Page 2 of 3.", StringComparison.Ordinal), "page 2 bar");
                            Check.Equal(25, table.SelectedIndex, "absolute index of the page's first row");
                            Check.True(table.GoToPage(99), "clamped");
                            Check.Equal(3, table.PageNumber, "last page");
                            Check.True(LastLine(table, 40, 30).StartsWith("Showing 51-60 of 60. Page 3 of 3.", StringComparison.Ordinal), "page 3 bar");
                            Check.False(table.NextPage(), "past the end returns false");
                            Check.True(table.PreviousPage(), "previous");
                            Check.False(table.GoToPage(2), "same page returns false");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LocalIndexes", "Local mode keeps absolute indexes for selection, activation, and navigation within a page",
                        _ =>
                        {
                            DataTable<int> table = Table(60);
                            table.PagingMode = DataTablePagingMode.Local;
                            int activated = -1;
                            table.RowActivated += i => activated = i;
                            table.NextPage();
                            table.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(26, table.SelectedIndex, "absolute");
                            table.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(26, activated, "RowActivated absolute");
                            table.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(49, table.SelectedIndex, "End stays on the page");
                            table.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(49, table.SelectedIndex, "Down at the page edge stays without PageAtEdges");
                            table.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(25, table.SelectedIndex, "Home to the page's first row");
                            table.Select(55);
                            Check.Equal(3, table.PageNumber, "Select moves to the row's page");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LocalSortAndSize", "Local sort orders all rows before slicing; SetPageSize keeps the first visible row",
                        _ =>
                        {
                            DataTable<int> table = Table(60);
                            table.PagingMode = DataTablePagingMode.Local;
                            table.SortByColumn(0, false);
                            Check.True(Snapshot.RenderWidget(table, 20, 5).Split('\n')[1].StartsWith("60", StringComparison.Ordinal), "page 1 starts at 60 after a descending sort");
                            table.SortByColumn(0, true);
                            table.GoToPage(2);
                            Snapshot.RenderWidget(table, 20, 30);
                            table.SetPageSize(10);
                            Check.Equal(10, table.PageSize, "size");
                            Check.Equal(3, table.PageNumber, "row 26 is on page 3 at size 10");
                            table.CyclePageSize();
                            Check.Equal(25, table.PageSize, "next size in the list");
                            table.PageSize = 100;
                            table.CyclePageSize();
                            Check.Equal(10, table.PageSize, "wraps");
                            Check.Throws<ArgumentOutOfRangeException>(() => table.PageSize = 0, "size 0 rejected");
                            Check.Throws<ArgumentOutOfRangeException>(() => table.SetPageSize(0), "SetPageSize 0 rejected");
                            Check.Throws<ArgumentNullException>(() => table.PageSizes = null!, "null sizes");
                            Check.Throws<ArgumentException>(() => table.PageSizes = new int[0], "empty sizes");
                            Check.Throws<ArgumentException>(() => table.PageSizes = new[] { 5, 0 }, "size below 1");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RemoteRequests", "Remote mode: moves, size changes, and sorts raise PageRequested; nothing changes until BindPage",
                        _ =>
                        {
                            DataTable<int> table = Table(0);
                            table.PagingMode = DataTablePagingMode.Remote;
                            List<DataTablePageRequest> requests = new List<DataTablePageRequest>();
                            table.PageRequested += (s, r) => requests.Add(r);
                            table.BindPage(Enumerable.Range(1, 25).ToList(), 1, 100);
                            Check.Equal(4, table.PageCount, "four pages of 100");
                            Check.True(table.GoToPage(3), "requested");
                            Check.Equal(1, table.PageNumber, "still page 1 until BindPage");
                            Check.Equal(3, requests[0].PageNumber, "page 3 requested");
                            Check.Equal(50L, requests[0].Offset, "offset");
                            table.SetPageSize(50);
                            Check.Equal(50, requests[1].PageSize, "size requested");
                            Check.Equal(1, requests[1].PageNumber, "first row stays on page 1");
                            table.SortByColumn(1, false);
                            Check.Equal(1, requests[2].PageNumber, "sort requests page 1");
                            Check.Equal("sq", requests[2].SortColumnKey, "sort key");
                            Check.False(requests[2].SortAscending, "descending");
                            Check.Equal(1, table.CurrentRequest().PageNumber, "current request");
                            table.PageSize = 25;
                            table.BindPage(Enumerable.Range(51, 25).ToList(), 3, 100);
                            Check.Equal(3, table.PageNumber, "bound");
                            Check.Equal(0, table.SelectedIndex, "page-relative index");
                            Check.True(LastLine(table, 40, 30).StartsWith("Showing 51-75 of 100. Page 3 of 4.", StringComparison.Ordinal), "bar");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RemoteGuards", "BindPage in another mode throws; invalid arguments throw; Remote ignores SortLocally",
                        _ =>
                        {
                            DataTable<int> table = Table(3);
                            Check.Throws<InvalidOperationException>(() => table.BindPage(new List<int>(), 1, 0), "None mode");
                            table.PagingMode = DataTablePagingMode.Local;
                            Check.Throws<InvalidOperationException>(() => table.BindPage(new List<int>(), 1, 0), "Local mode");
                            table.PagingMode = DataTablePagingMode.Remote;
                            Check.Throws<ArgumentNullException>(() => table.BindPage(null!, 1, 0), "null rows");
                            Check.Throws<ArgumentOutOfRangeException>(() => table.BindPage(new List<int>(), 0, 0), "page 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => table.BindPage(new List<int>(), 1, -2), "total -2");
                            table.BindPage(new List<int> { 3, 1, 2 }, 1, 3);
                            Check.True(table.SortLocally, "SortLocally left at true");
                            int requests = 0;
                            table.PageRequested += (s, r) => requests++;
                            Snapshot.RenderWidget(table, 20, 6);
                            table.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 0, KeyModifiers.None, 1));
                            Check.Equal(1, requests, "header click requested a page");
                            Check.Equal("3,1,2", string.Join(",", table.Rows), "page not reordered locally");
                            Check.Throws<ArgumentOutOfRangeException>(() => new DataTablePageRequest(0, 1, -1, null, true), "request page 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => new DataTablePageRequest(1, 0, -1, null, true), "request size 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => new DataTablePageRequest(1, 1, -2, null, true), "request index -2");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "UnknownTotal", "An unknown total uses UnknownTotalFormat and allows NextPage while the page is full",
                        _ =>
                        {
                            DataTable<int> table = Table(0);
                            table.PagingMode = DataTablePagingMode.Remote;
                            table.PageSize = 10;
                            int requested = 0;
                            table.PageRequested += (s, r) => requested = r.PageNumber;
                            table.BindPage(Enumerable.Range(1, 10).ToList(), 1, -1);
                            Check.Equal(-1L, table.TotalRows, "unknown");
                            Check.True(LastLine(table, 40, 20).StartsWith("Showing 1-10. Page 1.", StringComparison.Ordinal), "unknown format");
                            Check.True(table.NextPage(), "full page allows next");
                            Check.Equal(2, requested, "page 2 requested");
                            table.BindPage(Enumerable.Range(11, 4).ToList(), 2, -1);
                            Check.False(table.NextPage(), "a short page is the last");
                            Check.True(table.PreviousPage(), "previous");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MarksAcrossPages", "Remote marks by key survive paging; ClearMarks clears other pages; MarkAll marks the page; no key drops marks",
                        _ =>
                        {
                            DataTable<int> table = Table(0);
                            table.PagingMode = DataTablePagingMode.Remote;
                            table.MultiSelect = true;
                            table.KeySelector = i => i.ToString();
                            table.BindPage(new List<int> { 1, 2, 3 }, 1, 6);
                            table.HandleKey(KeyEvent.Char(' '));
                            table.BindPage(new List<int> { 4, 5, 6 }, 2, 6);
                            Check.Equal(0, table.MarkedCount, "nothing marked on page 2");
                            Check.Equal("1", string.Join(",", table.MarkedKeys), "page 1 key kept");
                            table.MarkAll();
                            Check.Equal(3, table.MarkedCount, "MarkAll marks the page");
                            table.BindPage(new List<int> { 1, 2, 3 }, 1, 6);
                            Check.Equal("1", string.Join(",", table.MarkedRows), "mark restored on return");
                            Check.Equal(4, table.MarkedKeys.Count, "keys from both pages");
                            table.ClearMarks();
                            Check.Equal(0, table.MarkedKeys.Count, "ClearMarks clears every page");
                            DataTable<int> noKey = Table(0);
                            noKey.PagingMode = DataTablePagingMode.Remote;
                            noKey.MultiSelect = true;
                            noKey.BindPage(new List<int> { 1, 2 }, 1, 4);
                            noKey.HandleKey(KeyEvent.Char(' '));
                            noKey.BindPage(new List<int> { 1, 2 }, 1, 4);
                            Check.Equal(0, noKey.MarkedCount, "without a key BindPage drops marks");
                            Check.Equal(0, noKey.MarkedKeys.Count, "and MarkedKeys is empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LocalMarks", "Local marks span pages without a key and MarkAll marks every bound row",
                        _ =>
                        {
                            DataTable<int> table = Table(30);
                            table.PagingMode = DataTablePagingMode.Local;
                            table.MultiSelect = true;
                            table.HandleKey(KeyEvent.Char(' '));
                            table.NextPage();
                            table.PreviousPage();
                            Check.True(table.IsMarked(0), "mark survived a page visit");
                            table.MarkAll();
                            Check.Equal(30, table.MarkedCount, "every bound row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "BarAndKeys", "The bar's < and > respond to clicks; the viewport excludes the bar; page keys are unbound by default",
                        _ =>
                        {
                            DataTable<int> table = Table(60);
                            table.PagingMode = DataTablePagingMode.Local;
                            Snapshot.RenderWidget(table, 40, 8);
                            Check.True(table.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 39, 7, KeyModifiers.None, 1)), "> clicked");
                            Check.Equal(2, table.PageNumber, "next page");
                            Snapshot.RenderWidget(table, 40, 8);
                            table.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 37, 7, KeyModifiers.None, 1));
                            Check.Equal(1, table.PageNumber, "< clicked");
                            table.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(6, table.SelectedIndex, "PageDown moves by the six visible rows, not seven");
                            Check.False(table.HandleKey(KeyEvent.Char('>')), "> not bound by default");
                            table.NextPageKey = KeyChord.Parse(">");
                            table.PreviousPageKey = KeyChord.Parse("<");
                            Check.True(table.HandleKey(KeyEvent.Char('>')), "bound");
                            Check.Equal(2, table.PageNumber, "next");
                            table.HandleKey(KeyEvent.Char('<'));
                            Check.Equal(1, table.PageNumber, "previous");
                            Check.True(table.GetKeyHints()!.Any(h => h.Description == "Next page"), "hint");
                            table.ShowPagingBar = false;
                            Check.False(Snapshot.RenderWidget(table, 40, 8).Contains("Showing"), "bar hidden");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PageAtEdges", "PageAtEdges moves Down past the last row to the next page and Up to the previous page's last row",
                        _ =>
                        {
                            DataTable<int> table = Table(30);
                            table.PagingMode = DataTablePagingMode.Local;
                            table.PageSize = 10;
                            table.HandleKey(KeyEvent.Special(KeyCode.End));
                            table.PageAtEdges = true;
                            table.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(2, table.PageNumber, "next page");
                            Check.Equal(10, table.SelectedIndex, "first row of page 2");
                            table.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(1, table.PageNumber, "previous page");
                            Check.Equal(9, table.SelectedIndex, "last row of page 1");
                            DataTable<int> remote = Table(0);
                            remote.PagingMode = DataTablePagingMode.Remote;
                            remote.PageAtEdges = true;
                            remote.PageSize = 3;
                            remote.BindPage(new List<int> { 4, 5, 6 }, 2, 9);
                            remote.HandleKey(KeyEvent.Special(KeyCode.Up));
                            remote.BindPage(new List<int> { 1, 2, 3 }, 1, 9);
                            Check.Equal(2, remote.SelectedIndex, "remote Up lands on the previous page's last row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "EndReachedSilent", "EndReached is not raised in a paging mode",
                        _ =>
                        {
                            DataTable<int> table = Table(5);
                            int reached = 0;
                            table.EndReached += () => reached++;
                            table.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(1, reached, "raised without paging");
                            table.PagingMode = DataTablePagingMode.Local;
                            table.HandleKey(KeyEvent.Special(KeyCode.Home));
                            table.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(1, reached, "not raised while paging");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Formats", "Paging formats are validated and localizable",
                        _ =>
                        {
                            DataTable<int> table = Table(3);
                            table.PagingMode = DataTablePagingMode.Local;
                            Check.Throws<ArgumentException>(() => table.PagingFormat = "Seite {3", "bad syntax");
                            Check.Throws<ArgumentException>(() => table.PagingFormat = "{5}", "out of range");
                            Check.Throws<ArgumentNullException>(() => table.UnknownTotalFormat = null!, "null");
                            table.PagingFormat = "Seite {3} von {4}";
                            Check.True(LastLine(table, 30, 6).StartsWith("Seite 1 von 1", StringComparison.Ordinal), "localized");
                            return Task.CompletedTask;
                        }),
                });
        }

        internal static DataTable<int> Table(int rows)
        {
            DataTable<int> table = new DataTable<int>()
                .Column("n", "N", i => i.ToString(), true)
                .Column("sq", "Sq", i => (i * i).ToString(), true);
            table.Columns[0].SortKey = i => i;
            table.Bind(Enumerable.Range(1, rows).ToList());
            return table;
        }

        internal static string LastLine(IWidget widget, int width, int height)
        {
            string[] lines = Snapshot.RenderWidget(widget, width, height).Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Trim().Length > 0)
                    return lines[i];
            }

            return string.Empty;
        }
    }
}
