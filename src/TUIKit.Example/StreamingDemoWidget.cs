namespace TUIKit.Example
{
    using System;
    using System.Threading;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// The tour's "Streaming without losing your place" demo: a feed (a <see cref="ListView{T}"/> with
    /// <see cref="ListView{T}.TailFollow"/>) receives a new event every second. While the last row is
    /// in view the list follows; selecting a visible row with Up/Down does not stop it. Move the
    /// selection above the top and the view holds still while a "N new below" marker counts arrivals;
    /// End, or a click on the marker, returns to the newest event.
    /// </summary>
    internal sealed class StreamingDemoWidget : IWidget, IFocusable, IMouseAware, IDisposable
    {
        private readonly TuiApplication _App;
        private readonly ListView<string> _Feed = new ListView<string> { TailFollow = new TailFollow() };
        private Timer? _Timer;
        private int _Next;

        internal StreamingDemoWidget(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Feed.ApplyTheme(Theme.Dark);
            for (int i = 0; i < 30; i++)
                AppendEvent();
            _Feed.Select(_Next - 1);
        }

        internal void StartFeed()
        {
            if (_Timer != null)
                return;

            _Timer = new Timer(_ => _App.Post(AppendEvent), null, 1000, 1000);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool HandleKey(KeyEvent key)
        {
            return _Feed.HandleKey(key);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            return _Feed.HandleMouse(mouse);
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
            if (width <= 0 || height <= 1)
                return;

            _Feed.Render(new SurfaceView(surface, new Rect(0, 0, width, height - 1)));
            string state = _Feed.TailFollow!.IsFollowing ? "following the newest event" : "holding still (End returns)";
            surface.DrawText(0, height - 1, "Feed: " + state, CellStyle.Default.WithForeground(Color.FromPalette(8)));
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _Timer?.Dispose();
            _Timer = null;
        }

        private void AppendEvent()
        {
            _Feed.Append("event " + _Next.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + "  build step finished");
            _Next++;
        }
    }
}
