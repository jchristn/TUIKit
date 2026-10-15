namespace TUIKit.Widgets
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The async plumbing for a server-backed <see cref="DataTable{T}"/> in <see cref="DataTablePagingMode.Remote"/>
    /// mode: it answers <see cref="DataTable{T}.PageRequested"/> and <see cref="DataTable{T}.RetryRequested"/> by
    /// calling a load delegate, cancels the load a newer one supersedes, drops late results, marshals every table
    /// call to the UI thread through a post delegate, and turns failures into <see cref="DataTable{T}.SetError"/>.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <remarks>
    /// Thread safety: construct it, call <see cref="LoadAsync"/>, <see cref="Reload"/>, and <see cref="Dispose()"/>
    /// on the UI thread. The load delegate runs wherever its awaits take it; only the post delegate touches the
    /// table, so pass the host's UI marshaller (for example <c>app.Post</c>), or <c>a =&gt; a()</c> in tests.
    /// </remarks>
    public sealed class DataTableLoader<T> : IDisposable
    {
        #region Private-Members

        private readonly DataTable<T> _Table;
        private readonly Func<DataTablePageRequest, CancellationToken, Task<DataTablePage<T>>> _Load;
        private readonly Action<Action> _Post;
        private readonly object _Sync = new object();
        private CancellationTokenSource? _Current;
        private long _Generation;
        private int _Loading;
        private bool _Disposed;
        private Func<Exception, string> _ErrorText = ex => ex.Message;

        #endregion

        #region Public-Members

        /// <summary>
        /// Raised on the UI thread (through the post delegate) when a load fails with an exception other than a
        /// cancellation, after the table enters the error state. Not raised for a superseded load.
        /// </summary>
        public event EventHandler<Exception>? LoadFailed;

        /// <summary>
        /// Gets or sets the text shown for a failure, from its exception. Defaults to <see cref="Exception.Message"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public Func<Exception, string> ErrorText
        {
            get { return _ErrorText; }
            set { _ErrorText = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets a value indicating whether a load is in progress. Thread-safe.
        /// </summary>
        public bool IsLoading
        {
            get { return Interlocked.CompareExchange(ref _Loading, 0, 0) != 0; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTableLoader{T}"/> class and subscribes to the table's
        /// <see cref="DataTable{T}.PageRequested"/> and <see cref="DataTable{T}.RetryRequested"/>.
        /// </summary>
        /// <param name="table">The table. Must not be null, and must be in <see cref="DataTablePagingMode.Remote"/> mode.</param>
        /// <param name="load">Loads one page for a request, observing the token. Must not be null.</param>
        /// <param name="post">Runs an action on the UI thread. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is not in <see cref="DataTablePagingMode.Remote"/> mode.</exception>
        public DataTableLoader(DataTable<T> table, Func<DataTablePageRequest, CancellationToken, Task<DataTablePage<T>>> load, Action<Action> post)
        {
            _Table = table ?? throw new ArgumentNullException(nameof(table));
            _Load = load ?? throw new ArgumentNullException(nameof(load));
            _Post = post ?? throw new ArgumentNullException(nameof(post));
            if (table.PagingMode != DataTablePagingMode.Remote)
                throw new InvalidOperationException("DataTableLoader requires a table in PagingMode Remote; this table is in " + table.PagingMode + " mode.");

            _Table.PageRequested += OnPageRequested;
            _Table.RetryRequested += OnRetryRequested;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Loads the table's current request (<see cref="DataTable{T}.CurrentRequest"/>), superseding any load in
        /// progress. The returned task completes when this load has bound, failed, or been superseded; it does not
        /// throw for a load failure (the table shows it) and completes as cancelled only when
        /// <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="token">Cancels this load.</param>
        /// <returns>A task that completes when the load is finished.</returns>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/>.</exception>
        public Task LoadAsync(CancellationToken token = default)
        {
            if (_Disposed)
                throw new ObjectDisposedException(nameof(DataTableLoader<T>));

            return LoadRequestAsync(_Table.CurrentRequest(), token);
        }

        /// <summary>
        /// Starts <see cref="LoadAsync"/> without awaiting it, for event handlers such as a refresh key.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose()"/>.</exception>
        public void Reload()
        {
            Observe(LoadAsync());
        }

        /// <summary>
        /// Unsubscribes from the table and cancels the load in progress. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region Private-Methods

        private void Dispose(bool disposing)
        {
            if (_Disposed)
                return;

            _Disposed = true;
            if (disposing)
            {
                _Table.PageRequested -= OnPageRequested;
                _Table.RetryRequested -= OnRetryRequested;
                CancellationTokenSource? current;
                lock (_Sync)
                {
                    current = _Current;
                    _Current = null;
                    Interlocked.Increment(ref _Generation);
                }

                if (current != null)
                {
                    current.Cancel();
                    current.Dispose();
                }

                Interlocked.Exchange(ref _Loading, 0);
            }
        }

        private void OnPageRequested(object? sender, DataTablePageRequest request)
        {
            if (!_Disposed)
                Observe(LoadRequestAsync(request, CancellationToken.None));
        }

        private void OnRetryRequested(object? sender, EventArgs e)
        {
            if (!_Disposed)
                Reload();
        }

        private async Task LoadRequestAsync(DataTablePageRequest request, CancellationToken token)
        {
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(token);
            CancellationTokenSource? previous;
            long generation;
            lock (_Sync)
            {
                previous = _Current;
                _Current = source;
                generation = Interlocked.Increment(ref _Generation);
            }

            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            Interlocked.Exchange(ref _Loading, 1);
            _Post(() =>
            {
                if (IsCurrent(generation))
                    _Table.SetLoading();
            });

            try
            {
                Task<DataTablePage<T>> pending = _Load(request, source.Token);
                if (pending == null)
                    throw new InvalidOperationException("The load delegate returned a null task.");

                DataTablePage<T> page = await pending.ConfigureAwait(false);
                source.Token.ThrowIfCancellationRequested();
                if (page == null)
                    throw new InvalidOperationException("The load delegate returned a null page.");

                _Post(() =>
                {
                    if (!IsCurrent(generation))
                        return;

                    Finish(generation);
                    _Table.BindPage(page.Rows, request.PageNumber, page.TotalRows);
                });
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested)
            {
                if (IsCurrent(generation))
                {
                    Finish(generation);
                    _Post(() => _Table.ClearLoadState());
                }

                token.ThrowIfCancellationRequested();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && source.IsCancellationRequested))
            {
                _Post(() =>
                {
                    if (!IsCurrent(generation))
                        return;

                    Finish(generation);
                    _Table.SetError(_ErrorText(ex) ?? string.Empty);
                    LoadFailed?.Invoke(this, ex);
                });
            }
        }

        private bool IsCurrent(long generation)
        {
            return !_Disposed && Interlocked.Read(ref _Generation) == generation;
        }

        private void Finish(long generation)
        {
            lock (_Sync)
            {
                if (Interlocked.Read(ref _Generation) == generation)
                    Interlocked.Exchange(ref _Loading, 0);
            }
        }

        private static void Observe(Task task)
        {
            task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        #endregion
    }
}
