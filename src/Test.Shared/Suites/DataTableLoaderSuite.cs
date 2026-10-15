namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C2: <see cref="DataTableLoader{T}"/> and <see cref="DataTablePage{T}"/>.
    /// </summary>
    public static class DataTableLoaderSuite
    {
        private const string _Id = "DataTableLoader";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "DataTable Loader",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Loads", "A successful load sets Loading, then binds the page and returns to Ready",
                        async _ =>
                        {
                            DataTable<int> table = Remote();
                            List<DataTableState> states = new List<DataTableState>();
                            using (DataTableLoader<int> loader = new DataTableLoader<int>(table, (r, ct) => Task.FromResult(Page(r, 100)), a => { a(); states.Add(table.State); }))
                            {
                                await loader.LoadAsync().ConfigureAwait(false);
                                Check.Equal(DataTableState.Loading, states[0], "loading first");
                                Check.Equal(DataTableState.Ready, table.State, "ready after");
                                Check.Equal(10, table.RowCount, "page bound");
                                Check.Equal(100L, table.TotalRows, "total");
                                Check.False(loader.IsLoading, "not loading");
                            }
                        }),

                    new TestCaseDescriptor(_Id, "PageRequests", "The loader answers PageRequested and binds the requested page",
                        async _ =>
                        {
                            DataTable<int> table = Remote();
                            TaskCompletionSource<bool> bound = new TaskCompletionSource<bool>();
                            using (DataTableLoader<int> loader = new DataTableLoader<int>(table, (r, ct) => Task.FromResult(Page(r, 100)), a => { a(); if (table.PageNumber == 3) bound.TrySetResult(true); }))
                            {
                                await loader.LoadAsync().ConfigureAwait(false);
                                table.GoToPage(3);
                                await Task.WhenAny(bound.Task, Task.Delay(2000)).ConfigureAwait(false);
                                Check.Equal(3, table.PageNumber, "page 3 bound");
                                Check.Equal(21, table.Rows[0], "page 3 rows");
                            }
                        }),

                    new TestCaseDescriptor(_Id, "Supersede", "A slow load superseded by a fast one never overwrites it",
                        async _ =>
                        {
                            DataTable<int> table = Remote();
                            TaskCompletionSource<DataTablePage<int>> slow = new TaskCompletionSource<DataTablePage<int>>();
                            int call = 0;
                            using (DataTableLoader<int> loader = new DataTableLoader<int>(table, (r, ct) =>
                            {
                                call++;
                                if (call == 1)
                                {
                                    ct.Register(() => slow.TrySetCanceled());
                                    return slow.Task;
                                }

                                return Task.FromResult(new DataTablePage<int>(new List<int> { 99 }, 1));
                            }, a => a()))
                            {
                                Task first = loader.LoadAsync();
                                await loader.LoadAsync().ConfigureAwait(false);
                                Check.Equal(99, table.Rows[0], "fast result bound");
                                slow.TrySetResult(new DataTablePage<int>(new List<int> { 1 }, 1));
                                await first.ConfigureAwait(false);
                                Check.Equal(99, table.Rows[0], "slow result dropped");
                                Check.Equal(DataTableState.Ready, table.State, "ready");
                            }
                        }),

                    new TestCaseDescriptor(_Id, "Cancelled", "A load cancelled by its token binds nothing and returns the table to Ready",
                        async _ =>
                        {
                            DataTable<int> table = Remote();
                            table.BindPage(new List<int> { 7 }, 1, 1);
                            using (CancellationTokenSource cts = new CancellationTokenSource())
                            using (DataTableLoader<int> loader = new DataTableLoader<int>(table, async (r, ct) =>
                            {
                                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                                return Page(r, 1);
                            }, a => a()))
                            {
                                Task load = loader.LoadAsync(cts.Token);
                                Check.True(loader.IsLoading, "loading");
                                cts.Cancel();
                                await Check.ThrowsAsync<OperationCanceledException>(() => load, "cancelled task").ConfigureAwait(false);
                                Check.Equal(7, table.Rows[0], "nothing bound");
                                Check.Equal(DataTableState.Ready, table.State, "ready again");
                                Check.False(loader.IsLoading, "not loading");
                            }
                        }),

                    new TestCaseDescriptor(_Id, "Failure", "An exception sets the error, raises LoadFailed, and RetryRequested reloads",
                        async _ =>
                        {
                            DataTable<int> table = Remote();
                            bool fail = true;
                            using (DataTableLoader<int> loader = new DataTableLoader<int>(table, (r, ct) =>
                            {
                                if (fail)
                                    throw new InvalidOperationException("boom");
                                return Task.FromResult(Page(r, 5));
                            }, a => a()))
                            {
                                Exception? seen = null;
                                loader.LoadFailed += (s, e) => seen = e;
                                loader.ErrorText = e => "custom " + e.Message;
                                await loader.LoadAsync().ConfigureAwait(false);
                                Check.Equal(DataTableState.Error, table.State, "error");
                                Check.Equal("custom boom", table.ErrorMessage, "ErrorText applied");
                                Check.True(seen is InvalidOperationException, "LoadFailed raised");
                                fail = false;
                                table.RetryKey = KeyChord.Parse("f5");
                                table.HandleKey(KeyEvent.Special(KeyCode.F5));
                                for (int i = 0; i < 50 && table.State != DataTableState.Ready; i++)
                                    await Task.Delay(10).ConfigureAwait(false);
                                Check.Equal(DataTableState.Ready, table.State, "retry loaded");
                                Check.Throws<ArgumentNullException>(() => loader.ErrorText = null!, "null ErrorText");
                            }
                        }),

                    new TestCaseDescriptor(_Id, "Dispose", "Dispose unsubscribes and cancels the load in progress",
                        async _ =>
                        {
                            DataTable<int> table = Remote();
                            int loads = 0;
                            CancellationToken seen = CancellationToken.None;
                            DataTableLoader<int> loader = new DataTableLoader<int>(table, (r, ct) =>
                            {
                                loads++;
                                seen = ct;
                                return new TaskCompletionSource<DataTablePage<int>>().Task;
                            }, a => a());
                            Task pending = loader.LoadAsync();
                            loader.Dispose();
                            Check.True(seen.IsCancellationRequested, "cancelled");
                            table.GoToPage(1);
                            table.BindPage(new List<int> { 1 }, 1, 50);
                            table.NextPage();
                            Check.Equal(1, loads, "no load after Dispose");
                            loader.Dispose();
                            Check.Throws<ObjectDisposedException>(() => loader.LoadAsync(), "LoadAsync after Dispose");
                            await Task.CompletedTask.ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(_Id, "Guards", "Constructor and page guards reject bad arguments and the wrong mode",
                        _ =>
                        {
                            DataTable<int> table = Remote();
                            Func<DataTablePageRequest, CancellationToken, Task<DataTablePage<int>>> load = (r, ct) => Task.FromResult(Page(r, 1));
                            Check.Throws<ArgumentNullException>(() => new DataTableLoader<int>(null!, load, a => a()), "null table");
                            Check.Throws<ArgumentNullException>(() => new DataTableLoader<int>(table, null!, a => a()), "null load");
                            Check.Throws<ArgumentNullException>(() => new DataTableLoader<int>(table, load, null!), "null post");
                            Check.Throws<InvalidOperationException>(() => new DataTableLoader<int>(DataTablePagingSuite.Table(1), load, a => a()), "not Remote");
                            Check.Throws<ArgumentNullException>(() => new DataTablePage<int>(null!, 0), "null rows");
                            Check.Throws<ArgumentOutOfRangeException>(() => new DataTablePage<int>(new List<int>(), -2), "total -2");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static DataTable<int> Remote()
        {
            DataTable<int> table = DataTablePagingSuite.Table(0);
            table.PagingMode = DataTablePagingMode.Remote;
            table.PageSize = 10;
            return table;
        }

        private static DataTablePage<int> Page(DataTablePageRequest request, long total)
        {
            int start = (int)request.Offset + 1;
            int count = (int)Math.Max(0, Math.Min(request.PageSize, total - request.Offset));
            return new DataTablePage<int>(Enumerable.Range(start, count).ToList(), total);
        }
    }
}
