namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // The "Diffs from git" tour page (1.10, C5): DiffView.FromUnifiedDiff over a bundled three-file patch with ']'
    // and '[' between files, a line-number gutter, search with a '/' prompt and F3, viewport paging, and theme
    // styles. Each feature has a key.
    internal sealed class DiffDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource
    {
        private readonly TuiApplication? _App;
        private readonly DiffView _View = DiffView.FromUnifiedDiff(SamplePatch.Text, "csharp");
        private readonly TextField _Prompt = new TextField { Placeholder = "search" };
        private Theme? _AppliedTheme;
        private bool _Searching;

        internal DiffDemoWidget(TuiApplication? app)
        {
            _App = app;
            _View.UseFileKeys = true;
            _View.ShowLineNumbers = true;
            _View.PageByViewport = true;
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Searching)
            {
                if (key.Code == KeyCode.Enter)
                {
                    _Searching = false;
                    _View.SetSearch(_Prompt.Value);
                    _View.FindNext();
                    return true;
                }

                if (key.Code == KeyCode.Escape)
                {
                    _Searching = false;
                    return true;
                }

                return _Prompt.HandleKey(key);
            }

            if (_View.HandleKey(key))
                return true;

            if (key.Code == KeyCode.Escape && _View.SearchText != null)
            {
                _View.SetSearch(null);
                return true;
            }

            if (key.Code != KeyCode.Character || key.Modifiers != KeyModifiers.None)
                return false;

            switch (key.Rune)
            {
                case '/':
                    _Searching = true;
                    _Prompt.Value = _View.SearchText ?? string.Empty;
                    _Prompt.OnFocusChanged(true);
                    return true;
                case 'n':
                    _View.ShowLineNumbers = !_View.ShowLineNumbers;
                    return true;
                case 't':
                    _View.UseThemeStyles = !_View.UseThemeStyles;
                    return true;
                case 'p':
                    _View.PageByViewport = !_View.PageByViewport;
                    return true;
                default:
                    return false;
            }
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (_Searching)
                return new[] { new KeyHint("Enter", "Find"), new KeyHint("Esc", "Cancel") };

            List<KeyHint> hints = new List<KeyHint> { new KeyHint("/", "Search"), new KeyHint("N", "Numbers"), new KeyHint("T", "Theme") };
            IReadOnlyList<KeyHint>? own = _View.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _View.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _View.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount));
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

            Theme theme = _App?.Theme ?? Theme.Dark;
            if (!ReferenceEquals(theme, _AppliedTheme))
            {
                _AppliedTheme = theme;
                _View.ApplyTheme(theme);
            }

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            if (_Searching)
            {
                surface.DrawText(0, 0, "/", CellStyle.Default.WithForeground(Color.FromPalette(6)));
                _Prompt.Render(new SurfaceView(surface, new Rect(1, 0, width - 1, 1)));
            }
            else
            {
                int file = _View.CurrentFileIndex;
                string status = "File " + (file + 1) + "/" + _View.Files.Count + ": " + (file >= 0 ? _View.Files[file] : "-")
                    + "  +" + _View.AddedCount + " -" + _View.RemovedCount + (_View.SearchText != null ? "  /" + _View.SearchText : string.Empty);
                surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(status, width), muted);
            }

            _View.Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }
    }
}
