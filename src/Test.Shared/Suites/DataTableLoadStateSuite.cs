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
    /// Coverage for 1.8 item C2 load states: <see cref="DataTable{T}.State"/>, <see cref="DataTable{T}.SetLoading"/>,
    /// <see cref="DataTable{T}.SetError"/>, the texts and formats, <see cref="DataTable{T}.RetryKey"/>, and
    /// <see cref="DataTable{T}.RetryRequested"/>.
    /// </summary>
    public static class DataTableLoadStateSuite
    {
        private const string _Id = "DataTableLoadState";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "DataTable Load States",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "LoadingEmpty", "Loading with no rows shows LoadingText instead of EmptyText",
                        _ =>
                        {
                            DataTable<int> table = DataTablePagingSuite.Table(0);
                            table.EmptyText = "No rows";
                            Check.Equal(DataTableState.Ready, table.State, "ready by default");
                            table.SetLoading();
                            Check.Equal(DataTableState.Loading, table.State, "loading");
                            string screen = Snapshot.RenderWidget(table, 30, 4);
                            Check.True(screen.Contains("Loading...") && !screen.Contains("No rows"), "loading text: " + screen);
                            table.Bind(new List<int>());
                            Check.Equal(DataTableState.Ready, table.State, "Bind returns to Ready");
                            Check.True(Snapshot.RenderWidget(table, 30, 4).Contains("No rows"), "empty text again");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Refreshing", "Loading with rows keeps them and shows RefreshingText on the bar, or the last row without one",
                        _ =>
                        {
                            DataTable<int> table = DataTablePagingSuite.Table(3);
                            table.SetLoading();
                            string screen = Snapshot.RenderWidget(table, 30, 6);
                            Check.True(screen.Contains("Refreshing...") && screen.Contains("9"), "rows kept, refreshing shown: " + screen);
                            table.PagingMode = DataTablePagingMode.Local;
                            Check.True(DataTablePagingSuite.LastLine(table, 50, 8).Contains("Page 1 of 1. Refreshing..."), "appended to the bar");
                            table.RefreshingText = "Updating";
                            Check.True(DataTablePagingSuite.LastLine(table, 50, 8).Contains("Updating"), "custom text");
                            Check.Throws<ArgumentNullException>(() => table.RefreshingText = null!, "null refreshing text");
                            Check.Throws<ArgumentNullException>(() => table.LoadingText = null!, "null loading text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Error", "Error shows the formatted message in ErrorStyle and hides the rows; BindPage returns to Ready",
                        _ =>
                        {
                            DataTable<int> table = DataTablePagingSuite.Table(3);
                            table.PagingMode = DataTablePagingMode.Remote;
                            table.SetError("timeout");
                            Check.Equal("timeout", table.ErrorMessage, "message");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(table, 40, 5);
                            string screen = Snapshot.ToText(buffer);
                            Check.True(screen.Contains("Failed to load: timeout"), "formatted: " + screen);
                            Check.False(screen.Split('\n')[2].Contains("4"), "rows hidden");
                            Check.Equal(Color.FromPalette(1), buffer.Get(0, 1).Style.Foreground, "error style");
                            table.ErrorFormat = "Error ({0})";
                            Check.True(Snapshot.RenderWidget(table, 40, 5).Contains("Error (timeout)"), "custom format");
                            table.BindPage(new List<int> { 1 }, 1, 1);
                            Check.Equal(DataTableState.Ready, table.State, "BindPage returns to Ready");
                            Check.True(table.ErrorMessage == null, "message cleared");
                            Check.Throws<ArgumentNullException>(() => table.SetError(null!), "null message");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ErrorFormatGuards", "ErrorFormat must contain {0} and be a valid composite format",
                        _ =>
                        {
                            DataTable<int> table = DataTablePagingSuite.Table(0);
                            Check.Throws<ArgumentException>(() => table.ErrorFormat = "Failed", "no {0}");
                            Check.Throws<ArgumentException>(() => table.ErrorFormat = "Failed {0", "bad syntax");
                            Check.Throws<ArgumentException>(() => table.ErrorFormat = "{0} {1}", "too many arguments");
                            Check.Throws<ArgumentNullException>(() => table.ErrorFormat = null!, "null");
                            table.ErrorFormat = "{0:}";
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RetryKey", "RetryKey is unbound by default and raises RetryRequested only in Error",
                        _ =>
                        {
                            DataTable<int> table = DataTablePagingSuite.Table(2);
                            int retries = 0;
                            table.RetryRequested += (s, e) => retries++;
                            table.SetError("x");
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.F5)), "F5 not consumed without a key");
                            table.RetryKey = KeyChord.Parse("f5");
                            Check.True(Snapshot.RenderWidget(table, 40, 4).Contains("F5 Retry"), "retry key shown with the error");
                            Check.True(table.GetKeyHints()!.Any(h => h.Description == "Retry"), "retry hint");
                            Check.True(table.HandleKey(KeyEvent.Special(KeyCode.F5)), "consumed in Error");
                            Check.Equal(1, retries, "raised");
                            table.Bind(new List<int> { 1 });
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.F5)), "not consumed when Ready");
                            table.SetLoading();
                            Check.False(table.HandleKey(KeyEvent.Special(KeyCode.F5)), "not consumed when Loading");
                            Check.Equal(1, retries, "not raised again");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}
