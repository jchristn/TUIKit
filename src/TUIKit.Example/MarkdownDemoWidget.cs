namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // The "Markdown that fits" tour page: a MarkdownView over a model-style reply, with keys that change the
    // render options live so the reader can watch columns squeeze, cells truncate, and tables stack. The view
    // itself scrolls with Up/Down and Home/End, follows the app theme (t), searches with a "/" prompt and F3,
    // and can grow live with tail-follow on (l).
    internal sealed class MarkdownDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource, IDisposable
    {
        internal const string Sample =
            "## Cache options\n" +
            "\n" +
            "Ways to cut **p99 latency** on `/orders`, from *staging* ([dashboard](https://grafana.example/d)):\n" +
            "\n" +
            "| Option | p99 (ms) | Cost | Notes |\n" +
            "|:-------|---------:|:----:|-------|\n" +
            "| Redis cache | 12 | $$ | Fastest, but another service to run and page on |\n" +
            "| In-process LRU | 18 | $ | No network hop; memory bound per instance |\n" +
            "| None | 240 | - | Simplest |\n" +
            "\n" +
            "- Start with the **LRU**; it ships today\n" +
            "  - Size it at 10k entries, then watch the hit rate for a week\n" +
            "- [x] Benchmarks recorded\n" +
            "- [ ] Load test at 3x traffic\n" +
            "\n" +
            "> Redis is worth it once a second region needs a shared cache.\n" +
            "\n" +
            "```csharp\n" +
            "public sealed class OrderCache\n" +
            "{\n" +
            "    private readonly LruCache<int, Order> _Cache = new LruCache<int, Order>(10000);\n" +
            "}\n" +
            "```\n";

        private static readonly MarkdownTableGlyphs[] _Glyphs =
        {
            MarkdownTableGlyphs.Light,
            MarkdownTableGlyphs.Rounded,
            MarkdownTableGlyphs.Heavy,
            MarkdownTableGlyphs.Double,
            MarkdownTableGlyphs.Ascii,
            MarkdownTableGlyphs.None
        };

        private static readonly string[] _GlyphNames = { "Light", "Rounded", "Heavy", "Double", "Ascii", "None" };

        private static readonly int[] _MaxWidths = { 0, 60, 44, 30 };

        private readonly TuiApplication? _App;
        private readonly MarkdownView _View = new MarkdownView(Sample);
        private readonly TextField _Prompt = new TextField { Placeholder = "search" };
        private Theme? _AppliedTheme;
        private Timer? _Timer;
        private bool _Searching;
        private bool _Live;
        private int _Updates;
        private string _Document = Sample;
        private int _GlyphIndex;
        private int _WidthIndex;
        private MarkdownOverflow _Overflow = MarkdownOverflow.Wrap;
        private MarkdownTableFallback _Fallback = MarkdownTableFallback.Stack;
        private bool _KeyColumn;

        internal MarkdownDemoWidget(TuiApplication? app)
        {
            _App = app;
            _View.IsFocusStop = true;
            _View.UseThemeStyles = true;
            Apply();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Searching)
                return HandlePrompt(key);

            // The view's own keys first: Up/Down, Home/End, and F3/Shift+F3 while a search is set.
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
                case 't':
                    _View.UseThemeStyles = !_View.UseThemeStyles;
                    return true;
                case 'l':
                    ToggleLive();
                    return true;
                case 'b':
                    _GlyphIndex = (_GlyphIndex + 1) % _Glyphs.Length;
                    break;
                case 'o':
                    _Overflow = (MarkdownOverflow)(((int)_Overflow + 1) % 3);
                    break;
                case 'f':
                    _Fallback = (MarkdownTableFallback)(((int)_Fallback + 1) % 3);
                    break;
                case 'w':
                    _WidthIndex = (_WidthIndex + 1) % _MaxWidths.Length;
                    break;
                case 'k':
                    _KeyColumn = !_KeyColumn;
                    break;
                default:
                    return false;
            }

            Apply();
            return true;
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (_Searching)
                return new[] { new KeyHint("Enter", "Find"), new KeyHint("Esc", "Cancel") };

            List<KeyHint> hints = new List<KeyHint>
            {
                new KeyHint("/", "Search", 6),
                new KeyHint("T", "Theme", 5),
                new KeyHint("L", "Live", 5),
                new KeyHint("B", "Border", 4),
                new KeyHint("W", "Width", 4),
                new KeyHint("O", "Overflow", 3),
                new KeyHint("F", "Fallback", 3),
                new KeyHint("K", "Key column", 2),
            };

            IReadOnlyList<KeyHint>? viewHints = _View.GetKeyHints();
            if (viewHints != null)
                hints.AddRange(viewHints);

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

            // The view sits one row below the status line.
            MouseEvent local = new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount);
            return _View.HandleMouse(local);
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
            if (width <= 0 || height <= 0)
                return;

            // The tour draws its own frame instead of binding regions, so the theme reaches the view here.
            Theme theme = _App?.Theme ?? Theme.Dark;
            if (!ReferenceEquals(theme, _AppliedTheme))
            {
                _AppliedTheme = theme;
                _View.ApplyTheme(theme);
            }

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            if (_Searching)
            {
                surface.DrawText(0, 0, "/", muted);
                _Prompt.Render(new SurfaceView(surface, new Rect(1, 0, Math.Max(0, width - 1), 1)));
            }
            else
            {
                surface.DrawText(0, 0, Status(), muted);
            }

            // Themed styles carry no backgrounds, so the view's own Background shows through every element.
            _View.Background = _View.UseThemeStyles ? CellStyle.Default.WithBackground(theme.Text.Background) : CellStyle.Default;
            if (height > 1)
                _View.Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }

        internal static MarkdownRenderOptions BuildOptions(MarkdownTableGlyphs glyphs, int maxWidth, MarkdownOverflow overflow, MarkdownTableFallback fallback, bool keyColumn)
        {
            MarkdownRenderOptions options = new MarkdownRenderOptions();
            options.Table.Glyphs = glyphs;
            options.Table.CellOverflow = overflow;
            options.Table.Fallback = fallback;
            options.MaxWidth = maxWidth;
            if (keyColumn)
                options.Styles.TableFirstColumn = CellStyle.Default.WithForeground(Color.FromPalette(3)).WithAttribute(CellAttributes.Bold, true);

            return options;
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _Timer?.Dispose();
            _Timer = null;
        }

        private string Status()
        {
            string live = _Live ? "[l]" + (_View.TailFollow.IsFollowing ? "following " : "held ") : string.Empty;
            if (_View.SearchText != null)
                return live + "/" + _View.SearchText + "  F3 next, Shift+F3 previous, Esc clears";

            string width0 = _MaxWidths[_WidthIndex] == 0 ? "full" : _MaxWidths[_WidthIndex].ToString(CultureInfo.InvariantCulture);
            string status = "[b]" + _GlyphNames[_GlyphIndex] + " [w]" + width0 + " [o]" + _Overflow + " [f]" + _Fallback
                + (_KeyColumn ? " [k]key" : string.Empty)
                + " [t]" + (_View.UseThemeStyles ? "theme" : "plain");
            return live + status;
        }

        private bool HandlePrompt(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape)
            {
                _Searching = false;
                _View.SetSearch(null);
                return true;
            }

            if (key.Code == KeyCode.Enter)
            {
                _Searching = false;
                _View.SetSearch(_Prompt.Value);
                _View.FindNext();
                return true;
            }

            _Prompt.HandleKey(key);
            _View.SetSearch(_Prompt.Value);
            return true;
        }

        private void ToggleLive()
        {
            _Live = !_Live;
            if (!_Live)
            {
                _Timer?.Dispose();
                _Timer = null;
                _View.TailFollow.Mode = TailFollowMode.Never;
                return;
            }

            _View.TailFollow.Mode = TailFollowMode.FollowAtBottom;
            _View.ScrollToEnd();
            if (_App != null)
                _Timer = new Timer(_ => _App.Post(AppendUpdate), null, 1000, 1000);
        }

        private void AppendUpdate()
        {
            _Updates++;
            string n = _Updates.ToString(CultureInfo.InvariantCulture);
            _Document += "\n### Update " + n + "\n\nLoad test step " + n + ": p99 **" + (17 + (_Updates % 4)) + " ms** with the LRU.\n";
            _View.Markdown = _Document;
        }

        private void Apply()
        {
            _View.Options = BuildOptions(_Glyphs[_GlyphIndex], _MaxWidths[_WidthIndex], _Overflow, _Fallback, _KeyColumn);
        }
    }
}
