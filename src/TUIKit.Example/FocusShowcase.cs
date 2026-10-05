namespace TUIKit.Example
{
    using System;
    using System.Threading;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// A small real application built from bound regions, showing the 1.4 usability features working
    /// together in the host: focused regions draw a heavy frame (<see cref="TuiApplication.HighlightFocusedRegion"/>),
    /// the tab view's strip is its own focus stop, the status bar lists the keys of whatever holds focus
    /// (<see cref="TuiApplication.BindKeyHints"/>), the log pane follows new output without fighting the
    /// reader, the activity list has clickable inline buttons, and repeated saves coalesce into one
    /// toast. Run it with <c>--focus</c>; <c>--focus-once</c> prints a frame and <c>--focus-audit-once</c>
    /// runs <see cref="FocusAudit"/> over it.
    /// </summary>
    internal sealed class FocusShowcase : IDisposable
    {
        private readonly TuiApplication _App;
        private readonly Pane _Log = new Pane("log");
        private Timer? _Timer;
        private int _Tick;

        internal FocusShowcase(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _App.HighlightFocusedRegion = true;
            _App.Layout = Layout.Create()
                .Add("files", r => r.ProportionalWidth(0.0, 0.25).Vertical(AxisConstraint.Stretch(0, 1)).WithPadding(0).WithBorder(BorderStyle.Rounded, "Files"))
                .Add("details", r => r.ProportionalWidth(0.25, 0.4).Vertical(AxisConstraint.Stretch(0, 1)).WithPadding(0).WithBorder(BorderStyle.Rounded, "Details"))
                .Add("activity", r => r.ProportionalWidth(0.65, 0.35).Vertical(AxisConstraint.Stretch(0, 1)).WithPadding(0).WithBorder(BorderStyle.Rounded, "Activity"))
                .Add("status", r => r.FillWidth().BottomAnchored(0, 1).WithPadding(0).BackgroundRole(Theming.Theme.StatusBarRole))
                .Build();

            ListView<string> files = new ListView<string>();
            files.SetItems(new[] { "README.md", "CHANGELOG.md", "Program.cs", "Layout.cs", "Theme.cs" });
            files.ItemActivated += index => _App.Notify("Opened " + files.Items[index], NotificationSeverity.Info, 2500);

            Form form = new Form { WrapFocus = false };
            form.Add("Name", new TextField { Value = "README.md" });
            form.Add("Owner", new TextField { Placeholder = "who owns this file" });

            for (int i = 0; i < 12; i++)
                _Log.WriteLine("build step " + i + " finished");

            TabView tabs = new TabView { ForwardKeys = true, StripFocusStop = true };
            tabs.Add("Properties", form);
            tabs.Add("Log", _Log);

            ClickableRowsWidget activity = new ClickableRowsWidget(_App, new[] { "deploy #41", "deploy #42", "deploy #43" });

            StatusBar status = new StatusBar().Add("Ctrl+Q", "Quit");
            _App.Bind("files", files);
            _App.Bind("details", tabs);
            _App.Bind("activity", activity);
            _App.Bind("status", status);

            KeyHintResolver hints = _App.BindKeyHints(status);
            hints.AddAppHint("ctrl+s", "Save");

            _App.Bind("ctrl+q", _App.Quit);
            _App.Bind("ctrl+s", () => _App.Notify("Saved", NotificationSeverity.Success, 3000));
            _App.ApplyThemeToWidgets = true;
        }

        internal void StartLog()
        {
            if (_Timer != null)
                return;

            _Timer = new Timer(_ => _Log.WriteLine("build step " + (12 + Interlocked.Increment(ref _Tick)) + " finished"), null, 1000, 1000);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _Timer?.Dispose();
            _Timer = null;
        }
    }
}
