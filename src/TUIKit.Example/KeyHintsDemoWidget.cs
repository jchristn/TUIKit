namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// The tour's "Keys follow focus" demo: a table and a search field in a split, with a status bar
    /// that lists the keys of whatever holds focus (<see cref="KeyHintResolver"/>). With the table
    /// focused the bar offers Enter, the marking keys, and the single-letter shortcuts; press Tab into
    /// the field and the bar leads with how to leave it and hides every key that would type a letter.
    /// </summary>
    internal sealed class KeyHintsDemoWidget : IWidget, IFocusContainer, IFocusAware
    {
        private readonly SplitView _Split;
        private readonly StatusBar _Bar = new StatusBar();
        private readonly KeyHintResolver _Resolver = new KeyHintResolver();

        internal KeyHintsDemoWidget()
        {
            DataTable<string> table = new DataTable<string> { MultiSelect = true };
            table.Column("File", name => name).Column("Length", name => name.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            table.Bind(new[] { "Program.cs", "README.md", "Layout.cs", "Theme.cs" });
            table.RowActivated += _ => { };
            table.ApplyTheme(Theme.Dark);

            TextField search = new TextField { Placeholder = "Search" };
            search.ApplyTheme(Theme.Dark);

            _Split = new SplitView(SplitOrientation.Horizontal, table, search, 0.6) { ForwardKeys = true, ShowPaneFrames = true, ShowDivider = false };
            _Split.ApplyTheme(Theme.Dark);
            _Split.OnFocusChanged(true);
            _Split.FocusEdge(true);

            _Resolver.AddAppHint("q", "Quit").AddAppHint(new KeyHint("?", "Help")).AddAppHint("ctrl+s", "Save");
            _Bar.HintSource = () => _Resolver.Resolve(FocusPath.Build("demo", _Split));
            _Bar.KeyStyle = Theme.Dark.Accent.WithAttribute(CellAttributes.Bold, true);
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
            _Bar.Render(new SurfaceView(surface, new Rect(0, height - 1, width, 1)));
        }
    }
}
