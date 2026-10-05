namespace TUIKit.Example
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// The tour's "Focus you can see" demo: a <see cref="SplitView"/> with <see cref="SplitView.ShowPaneFrames"/>
    /// holds a list on the left and a <see cref="TabView"/> with a strip focus stop on the right; the two
    /// pane frames share one line (<see cref="FocusFrameOptions.JoinBorders"/>). Tab moves
    /// between the list, the tab strip, and the tab content; the focused pane gets the heavy frame, the
    /// selected tab gets the marker only while the strip holds focus, and the bottom line prints the live
    /// <see cref="FocusPath"/>.
    /// </summary>
    internal sealed class FocusDemoWidget : IWidget, IFocusContainer, IFocusAware, IMouseAware
    {
        private readonly SplitView _Split;

        internal FocusDemoWidget()
        {
            ListView<string> files = new ListView<string>();
            files.SetItems(new[] { "README.md", "CHANGELOG.md", "src/", "docs/" });

            ListView<string> details = new ListView<string> { IsFocused = false };
            details.SetItems(new[] { "Size: 4 KB", "Modified: today", "Owner: you" });
            TextField notes = new TextField { Placeholder = "Type a note" };

            TabView tabs = new TabView { ForwardKeys = true, StripFocusStop = true };
            tabs.Add("Details", details);
            tabs.Add("Notes", notes);

            _Split = new SplitView(SplitOrientation.Horizontal, files, tabs, 0.45) { ForwardKeys = true, ShowPaneFrames = true, ShowDivider = false };
            _Split.FrameOptions.JoinBorders = true;
            _Split.ApplyTheme(Theme.Dark);
            tabs.ApplyTheme(Theme.Dark);
            files.ApplyTheme(Theme.Dark);
            details.ApplyTheme(Theme.Dark);
            _Split.OnFocusChanged(true);
            _Split.FocusEdge(true);
        }

        public IFocusable? FocusedLeaf
        {
            get { return _Split.FocusedLeaf; }
        }

        public bool HandleKey(KeyEvent key)
        {
            return _Split.HandleKey(key);
        }

        public bool MoveFocus(bool forward)
        {
            return _Split.MoveFocus(forward);
        }

        public void FocusEdge(bool first)
        {
            _Split.FocusEdge(first);
        }

        public void OnFocusChanged(bool focused)
        {
            _Split.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _Split.HandleMouse(mouse);
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

            _Split.Render(new SurfaceView(surface, new Rect(0, 0, width, height - 1)));
            FocusPath path = FocusPath.Build("demo", _Split);
            surface.DrawText(0, height - 1, "Focus: " + path, CellStyle.Default.WithForeground(Color.FromPalette(8)));
        }
    }
}
