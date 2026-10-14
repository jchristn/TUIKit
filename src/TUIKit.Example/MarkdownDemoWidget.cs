namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    // The "Markdown that fits" tour page: a MarkdownView over a model-style reply, with keys that change the
    // render options live so the reader can watch columns squeeze, cells truncate, and tables stack.
    internal sealed class MarkdownDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource
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

        private readonly MarkdownView _View = new MarkdownView(Sample);
        private int _GlyphIndex;
        private int _WidthIndex;
        private MarkdownOverflow _Overflow = MarkdownOverflow.Wrap;
        private MarkdownTableFallback _Fallback = MarkdownTableFallback.Stack;
        private bool _KeyColumn;

        internal MarkdownDemoWidget()
        {
            Apply();
        }

        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Up)
            {
                _View.ScrollOffset = Math.Max(0, _View.ScrollOffset - 1);
                return true;
            }

            if (key.Code == KeyCode.Down)
            {
                _View.ScrollOffset = _View.ScrollOffset + 1;
                return true;
            }

            if (key.Code != KeyCode.Character || key.Modifiers != KeyModifiers.None)
                return false;

            switch (key.Rune)
            {
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
            return new[]
            {
                new KeyHint("B", "Border", 5),
                new KeyHint("W", "Width", 5),
                new KeyHint("O", "Overflow", 4),
                new KeyHint("F", "Fallback", 4),
                new KeyHint("K", "Key column", 3),
                new KeyHint("Up/Down", "Scroll")
            };
        }

        public void OnFocusChanged(bool focused)
        {
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            return _View.HandleMouse(mouse);
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

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            string width0 = _MaxWidths[_WidthIndex] == 0 ? "full" : _MaxWidths[_WidthIndex].ToString();
            string status = "[b]" + _GlyphNames[_GlyphIndex] + " [w]" + width0 + " [o]" + _Overflow + " [f]" + _Fallback + (_KeyColumn ? " [k]key" : string.Empty);
            surface.DrawText(0, 0, status, muted);
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

        private void Apply()
        {
            _View.Options = BuildOptions(_Glyphs[_GlyphIndex], _MaxWidths[_WidthIndex], _Overflow, _Fallback, _KeyColumn);
        }
    }
}
