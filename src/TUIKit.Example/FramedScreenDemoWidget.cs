namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    internal sealed class FramedScreenDemoWidget : IWidget, IFocusContainer, IFocusAware, IFocusPathNode, IMouseAware
    {
        private readonly FramedStack _Stack;
        private readonly ListView<string> _Drawer = new ListView<string>();
        private readonly ListView<string> _Transcript = new ListView<string>();
        private readonly TextEditor _Composer = new TextEditor { ConsumeUnboundControlKeys = false };
        private readonly KeyHintResolver _Hints = new KeyHintResolver();
        private readonly StatusBar _Status = new StatusBar();

        internal FramedScreenDemoWidget()
        {
            ListView<string> channels = new ListView<string>();
            channels.SetItems(new[] { "#general", "#builds", "#random" });
            _Transcript.SetItems(new[] { "ana: deploy is green", "raj: shipping 1.5 today", "you: nice" });
            _Drawer.SetItems(new[] { "Pinned: release notes", "Pinned: on-call rota" });
            _Composer.LeaveHint = new KeyHint("Tab", "Leave composer");

            FramedStack right = new FramedStack(SplitOrientation.Vertical)
                .Add(_Transcript, StackSize.Weighted(1, 2), "Transcript")
                .Add(new ComposerHints(_Composer), StackSize.Fixed(2), "Composer");
            _Stack = new FramedStack(SplitOrientation.Horizontal)
                .Add(channels, StackSize.Weighted(1, 10), "Channels")
                .Add(right, StackSize.Weighted(3));
            _Stack.FrameOptions.TitleAlignment = TitleAlignment.Left;
            _Stack.OverlayTitle = "Pinned";
            _Stack.OverlayRect = area => new Rect(area.Right - Math.Max(10, area.Width / 2), area.Y + 1, Math.Max(10, area.Width / 2), Math.Max(3, area.Height - 2));
            right.FrameOptions.TitleAlignment = TitleAlignment.Left;
            _Stack.ApplyTheme(Theme.Dark);
            _Drawer.ApplyTheme(Theme.Dark);

            _Hints.AddAppHint(new KeyHint("F2", "Drawer"));
            _Status.ReservedHint = new KeyHint("F2", "Drawer");
            _Status.RightText = "framed";
            _Status.HintSource = () => _Hints.Resolve(FocusPath.Build("demo", this));
            _Stack.OnFocusChanged(true);
        }

        public IFocusable? FocusedChild
        {
            get { return _Stack; }
        }

        public IFocusable? FocusedLeaf
        {
            get { return _Stack.FocusedLeaf; }
        }

        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.F2)
            {
                _Stack.Overlay = _Stack.Overlay == null ? _Drawer : null;
                return true;
            }

            if (key.Code == KeyCode.Escape && _Stack.Overlay != null)
            {
                _Stack.Overlay = null;
                return true;
            }

            return _Stack.HandleKey(key);
        }

        public bool MoveFocus(bool forward)
        {
            return _Stack.MoveFocus(forward);
        }

        public void FocusEdge(bool first)
        {
            _Stack.FocusEdge(first);
        }

        public void OnFocusChanged(bool focused)
        {
            _Stack.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _Stack.HandleMouse(mouse);
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
            if (width <= 0 || height <= 2)
                return;

            _Stack.Render(new SurfaceView(surface, new Rect(0, 0, width, height - 1)));
            _Status.Render(new SurfaceView(surface, new Rect(0, height - 1, width, 1)));
        }
    }
}
