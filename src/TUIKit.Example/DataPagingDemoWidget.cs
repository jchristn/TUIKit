namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // The "Server-backed tables" tour page (1.8, C2 and C3): a DataTable over a simulated server of 1,000 rows
    // with a 300 ms delay, paged in Remote mode through DataTableLoader, with load and error states, retry, marks
    // that survive paging, a column chooser, and a column cursor for keyboard sorting. Every feature has a key.
    internal sealed class DataPagingDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource, IDisposable
    {
        private static readonly string[] _Statuses = { "active", "paused", "failed", "done" };

        private readonly TuiApplication? _App;
        private readonly List<ServerRow> _Server;
        private readonly DataTable<ServerRow> _Table = new DataTable<ServerRow>();
        private DataTableLoader<ServerRow>? _Loader;
        private Theme? _AppliedTheme;
        private bool _FailNext;
        private string _LastRequest = "-";
        private string _Status = "Loading the first page...";
        private bool _Started;

        internal DataPagingDemoWidget(TuiApplication? app)
        {
            _App = app;
            _Server = Enumerable.Range(1, 1000)
                .Select(i => new ServerRow(i, "job-" + i.ToString("D4"), _Statuses[(i * 7) % _Statuses.Length], (i * 37) % 900 + 10))
                .ToList();

            _Table.Column("id", "Id", r => r.Id.ToString(), true);
            _Table.Column("name", "Name", r => r.Name, true);
            _Table.Column("status", "Status", r => r.Status, true);
            _Table.Column(new DataColumn<ServerRow>("size", "Size KB", r => r.SizeKb.ToString(), true) { Alignment = CellAlignment.Right });
            _Table.Columns[0].CanHide = false;
            _Table.Columns[0].SortKey = r => r.Id;
            _Table.Columns[3].SortKey = r => r.SizeKb;
            _Table.MultiSelect = true;
            _Table.KeySelector = r => r.Id.ToString();
            _Table.PageSize = 10;
            _Table.PageSizes = new[] { 5, 10, 25, 50 };
            _Table.NextPageKey = KeyChord.Parse(">");
            _Table.PreviousPageKey = KeyChord.Parse("<");
            _Table.RetryKey = KeyChord.Parse("f5");
            _Table.SortKey = KeyChord.Parse("s");
            _Table.ReverseSortKey = KeyChord.Parse("shift+s");
            _Table.EmptyText = "No rows";
            _Table.PageRequested += (s, r) => _LastRequest = Describe(r);
            _Table.RetryRequested += (s, e) => _Status = "Retrying...";
            _Table.ColumnsChanged += (s, e) => _Status = "Columns: " + string.Join(", ", _Table.VisibleColumns.Select(c => c.Key));
            UseRemote();
        }

        public void Dispose()
        {
            _Loader?.Dispose();
            _Loader = null;
        }

        public bool HandleKey(KeyEvent key)
        {
            EnsureStarted();
            if (key.Code == KeyCode.Character && key.Modifiers == KeyModifiers.None)
            {
                switch (key.Rune)
                {
                    case 'f':
                        _FailNext = !_FailNext;
                        _Status = _FailNext ? "The next load will fail. Press > or r." : "Loads succeed again.";
                        return true;
                    case 'r':
                        if (_Loader != null)
                            _Loader.Reload();
                        _Status = "Reloading the current page.";
                        return true;
                    case 'm':
                        if (_Table.PagingMode == DataTablePagingMode.Remote)
                            UseLocal();
                        else
                            UseRemote();
                        return true;
                    case 'z':
                        _Table.CyclePageSize();
                        _Status = "Page size " + _Table.PageSize + ".";
                        return true;
                    case 'e':
                        _Table.PageAtEdges = !_Table.PageAtEdges;
                        _Status = "PageAtEdges " + (_Table.PageAtEdges ? "on: Down on the last row pages" : "off") + ".";
                        return true;
                    case 'b':
                        _Table.ShowPagingBar = !_Table.ShowPagingBar;
                        return true;
                    case 'k':
                        _Table.ShowColumnCursor = !_Table.ShowColumnCursor;
                        _Status = _Table.ShowColumnCursor ? "Column cursor on: Left/Right, s sorts, S sorts descending." : "Column cursor off.";
                        return true;
                    case 'c':
                        OpenChooser();
                        return true;
                }
            }

            return _Table.HandleKey(key);
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = _Table.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("c", "Columns"));
            hints.Add(new KeyHint("m", "Mode"));
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _Table.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            EnsureStarted();
            return _Table.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 2, mouse.Modifiers, mouse.ClickCount));
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 3)
                return;

            EnsureStarted();
            Theme theme = _App?.Theme ?? Theme.Dark;
            if (!ReferenceEquals(theme, _AppliedTheme))
            {
                _AppliedTheme = theme;
                _Table.ApplyTheme(theme);
            }

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            string line = _Table.PagingMode + " | " + _Table.State + " | size " + _Table.PageSize + " | marked " + _Table.MarkedKeys.Count
                + " | f fail:" + (_FailNext ? "on" : "off") + " | last " + _LastRequest;
            surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(line, width), muted);
            surface.DrawText(0, 1, TUIKit.Unicode.TextFit.Ellipsize(_Status + "   < > page  z size  e edges  k cursor  b bar  r reload", width), CellStyle.Default.WithForeground(Color.FromPalette(2)));
            _Table.Render(new SurfaceView(surface, new Rect(0, 2, width, height - 2)));
        }

        private void EnsureStarted()
        {
            if (_Started)
                return;

            _Started = true;
            _Loader?.Reload();
        }

        private void UseRemote()
        {
            _Loader?.Dispose();
            _Table.PagingMode = DataTablePagingMode.Remote;
            _Loader = new DataTableLoader<ServerRow>(_Table, LoadAsync, Post);
            _Loader.LoadFailed += (s, e) => _Status = "Load failed. Press F5 to retry.";
            if (_Started)
                _Loader.Reload();
            _Status = "Remote: each page comes from the simulated server (300 ms).";
        }

        private void UseLocal()
        {
            _Loader?.Dispose();
            _Loader = null;
            _Table.PagingMode = DataTablePagingMode.Local;
            _Table.Bind(_Server);
            _Status = "Local: all 1,000 rows bound, paged by the table.";
        }

        private void Post(Action action)
        {
            if (_App != null)
                _App.Post(action);
            else
                action();
        }

        private async Task<DataTablePage<ServerRow>> LoadAsync(DataTablePageRequest request, CancellationToken token)
        {
            await Task.Delay(300, token).ConfigureAwait(false);
            if (_FailNext)
            {
                _FailNext = false;
                throw new InvalidOperationException("the server returned 503");
            }

            IEnumerable<ServerRow> rows = _Server;
            switch (request.SortColumnKey)
            {
                case "id":
                    rows = request.SortAscending ? rows.OrderBy(r => r.Id) : rows.OrderByDescending(r => r.Id);
                    break;
                case "name":
                    rows = request.SortAscending ? rows.OrderBy(r => r.Name, StringComparer.Ordinal) : rows.OrderByDescending(r => r.Name, StringComparer.Ordinal);
                    break;
                case "status":
                    rows = request.SortAscending ? rows.OrderBy(r => r.Status, StringComparer.Ordinal) : rows.OrderByDescending(r => r.Status, StringComparer.Ordinal);
                    break;
                case "size":
                    rows = request.SortAscending ? rows.OrderBy(r => r.SizeKb) : rows.OrderByDescending(r => r.SizeKb);
                    break;
            }

            List<ServerRow> page = rows.Skip((int)request.Offset).Take(request.PageSize).ToList();
            Post(() => _Status = "Loaded page " + request.PageNumber + ".");
            return new DataTablePage<ServerRow>(page, _Server.Count);
        }

        private void OpenChooser()
        {
            if (_App == null)
                return;

            MultiSelectModal<DataColumn<ServerRow>> chooser = _Table.CreateColumnChooser("Columns");
            _App.ShowAsync(chooser).ContinueWith(
                t => Post(() =>
                {
                    if (!_Table.ApplyColumnChooser(chooser))
                        _Status = "Column choice cancelled.";
                }),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        private static string Describe(DataTablePageRequest request)
        {
            return "p" + request.PageNumber + "x" + request.PageSize + (request.SortColumnKey != null ? " " + request.SortColumnKey + (request.SortAscending ? "+" : "-") : string.Empty);
        }
    }
}
