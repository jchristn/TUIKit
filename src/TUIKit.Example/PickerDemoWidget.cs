namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // The "Pickers" tour page (1.8, C4): a FuzzyList of packages with detail text, a deprecated entry that cannot
    // be chosen, multi-select checkboxes, and a provider that offers "Create <query>". F5 to F9 switch each
    // feature; Enter chooses; Ctrl+U and Ctrl+W edit the query; a paste goes to the query.
    internal sealed class PickerDemoWidget : IWidget, IFocusable, IMouseAware, IKeyHintSource, ITextEntry, ITextEntryKeys, IPasteTarget
    {
        private static readonly PackageInfo[] _Packages =
        {
            new PackageInfo("serilog", "4.1.0", "structured logging", true),
            new PackageInfo("newtonsoft.json", "13.0.3", "JSON serializer", true),
            new PackageInfo("polly", "8.4.2", "resilience and retries", true),
            new PackageInfo("log4net", "2.0.17", "deprecated: use serilog", false),
            new PackageInfo("dapper", "2.1.35", "micro ORM", true),
            new PackageInfo("humanizer", "2.14.1", "strings, dates, and numbers for humans", true),
            new PackageInfo("moq", "4.20.72", "mocking for tests", true),
        };

        private readonly TuiApplication? _App;
        private readonly FuzzyList<PackageInfo> _List;
        private Theme? _AppliedTheme;
        private bool _Provider;
        private string _Status = "Type to filter; Enter chooses.";

        internal PickerDemoWidget(TuiApplication? app)
        {
            _App = app;
            _List = new FuzzyList<PackageInfo>(_Packages, p => p.Name, new NameComparer());
            _List.Detail = p => p.Version + "  " + p.Description;
            _List.IsEnabled = p => p.Available;
            _List.UseLineEditingKeys = true;
            _List.ItemActivated += (s, p) => _Status = _List.MultiSelect
                ? "Chose " + string.Join(", ", _List.CheckedItems.Select(c => c.Name).DefaultIfEmpty(p.Name))
                : "Chose " + p.Name + " " + p.Version;
            _List.CheckedChanged += (s, e) => _Status = _List.CheckedItems.Count + " checked";
            _List.ProviderFailed += (s, e) => _Status = "Provider failed: " + e.Message;
        }

        public bool AcceptsText
        {
            get { return true; }
        }

        public KeyHint? LeaveHint
        {
            get { return null; }
        }

        public bool ConsumesChord(KeyChord chord)
        {
            return _List.ConsumesChord(chord) || chord.Code == KeyCode.F5 || chord.Code == KeyCode.F6 || chord.Code == KeyCode.F7 || chord.Code == KeyCode.F8 || chord.Code == KeyCode.F9;
        }

        public bool HandlePaste(string text)
        {
            return _List.HandlePaste(text);
        }

        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.F5:
                    _List.Detail = _List.Detail == null ? (Func<PackageInfo, string>)(p => p.Version + "  " + p.Description) : null;
                    _Status = "Detail " + (_List.Detail != null ? "on" : "off") + ".";
                    return true;
                case KeyCode.F6:
                    _List.SearchDetail = !_List.SearchDetail;
                    _Status = "SearchDetail " + (_List.SearchDetail ? "on: try 'json' or 'retries'" : "off") + ".";
                    return true;
                case KeyCode.F7:
                    _List.MultiSelect = !_List.MultiSelect;
                    _Status = "MultiSelect " + (_List.MultiSelect ? "on: Space checks, Ctrl+A checks all" : "off") + ".";
                    return true;
                case KeyCode.F8:
                    _Provider = !_Provider;
                    _List.ItemsProvider = _Provider ? Provide : (Func<string, IEnumerable<PackageInfo>>?)null;
                    if (!_Provider)
                        _List.Query = _List.Query;
                    _Status = "Provider " + (_Provider ? "on: type a new name" : "off") + ".";
                    return true;
                case KeyCode.F9:
                    _List.UseThemeStyles = !_List.UseThemeStyles;
                    _Status = "UseThemeStyles " + (_List.UseThemeStyles ? "on (Ctrl+T cycles the theme)" : "off") + ".";
                    return true;
            }

            return _List.HandleKey(key);
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = _List.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("F5-F9", "Settings"));
            return hints;
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _List.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 2, mouse.Modifiers, mouse.ClickCount));
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

            Theme theme = _App?.Theme ?? Theme.Dark;
            if (!ReferenceEquals(theme, _AppliedTheme))
            {
                _AppliedTheme = theme;
                _List.ApplyTheme(theme);
            }

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            string flags = "F5 detail:" + Flag(_List.Detail != null) + " F6 search detail:" + Flag(_List.SearchDetail)
                + " F7 multi:" + Flag(_List.MultiSelect) + " F8 create:" + Flag(_Provider) + " F9 theme:" + Flag(_List.UseThemeStyles);
            surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(flags, width), muted);
            surface.DrawText(0, 1, TUIKit.Unicode.TextFit.Ellipsize(_Status, width), CellStyle.Default.WithForeground(Color.FromPalette(2)));
            _List.Render(new SurfaceView(surface, new Rect(0, 2, width, height - 2)));
        }

        private static IEnumerable<PackageInfo> Provide(string query)
        {
            List<PackageInfo> result = _Packages.ToList();
            string name = query.Trim();
            if (name.Length > 0 && !_Packages.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                result.Insert(0, new PackageInfo(name, "new", "Create a package named " + name, true));
            return result;
        }

        private static string Flag(bool value)
        {
            return value ? "on" : "off";
        }
    }
}
