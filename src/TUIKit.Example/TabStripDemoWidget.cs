namespace TUIKit.Example
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class TabStripDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IFocusPathNode
    {
        private static readonly string[] _Routes = { "/inbox", "/drafts", "/archive" };
        private readonly TabStrip _Strip = new TabStrip { TabPrefix = "[", TabSuffix = "]", TabFocusPrefix = ">", TabFocusSuffix = "<" };

        internal TabStripDemoWidget()
        {
            _Strip.Add("Inbox").Add("Drafts").Add("Archive");
            _Strip.ApplyTheme(Theme.Dark);
            _Strip.OnFocusChanged(true);
        }

        public IFocusable? FocusedChild
        {
            get { return _Strip; }
        }

        public bool HandleKey(KeyEvent key)
        {
            return _Strip.HandleKey(key);
        }

        public void OnFocusChanged(bool focused)
        {
            _Strip.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return mouse.Y == 0 && _Strip.HandleMouse(mouse);
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            if (surface.Size.Height < 3)
                return;

            _Strip.Render(new SurfaceView(surface, new Rect(0, 0, surface.Size.Width, 1)));
            string route = _Routes[Math.Max(0, _Strip.ActiveIndex)];
            surface.DrawText(0, 2, "Routed content for " + route + " (your app renders this, not the strip).", CellStyle.Default);
        }
    }
}
