namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    // The "Heat map palettes" tour page (1.13): the hourly week from the spacing page drawn through each HeatMapPalette
    // preset and one custom palette built from explicit colors. 'p' cycles the palettes, 'b' switches between colored
    // glyphs and solid tiles, 'l' switches between five levels and continuous shading, and 'a' switches to ASCII glyphs.
    // The legend follows the palette.
    internal sealed class HeatMapPaletteDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource, ITooltipProvider
    {
        private static readonly string[] _ShadeGlyphs = { "·", "░", "▒", "▓", "█" };
        private static readonly string[] _AsciiGlyphs = { ".", "-", "=", "+", "#" };

        private readonly HeatMap _Map;
        private readonly IReadOnlyList<HeatMapPalette> _Palettes;
        private int _Index;

        internal HeatMapPaletteDemoWidget()
        {
            _Map = HeatMapSpacingDemoWidget.Build(HeatMapSpacingDemoWidget.HourlyActivity());
            _Map.IsSelectable = true;
            _Map.ShowSelectionDescription = true;
            _Map.CellWidth = 2;
            _Map.CellGap = 1;
            _Map.Select(2, 10);
            _Palettes = AllPalettes();
            _Map.Palette = _Palettes[0];
        }

        internal HeatMap Map
        {
            get { return _Map; }
        }

        // A palette built from explicit colors: teal through to magenta, with its own empty color.
        internal static HeatMapPalette Custom { get; } = new HeatMapPalette(
            "Custom (teal to magenta)",
            Color.FromRgb(0x8C8C8C),
            new[] { Color.FromRgb(0x5CC8C8), Color.FromRgb(0x5A8FD8), Color.FromRgb(0xA05AD0), Color.FromRgb(0xD0408C) });

        // The presets in order, then the custom palette.
        internal static IReadOnlyList<HeatMapPalette> AllPalettes()
        {
            return HeatMapPalette.Presets.Concat(new[] { Custom }).ToList();
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Map.HandleKey(key))
                return true;

            if (key.Code != KeyCode.Character || key.Modifiers != KeyModifiers.None)
                return false;

            switch (key.Rune)
            {
                case 'p':
                    _Index = (_Index + 1) % _Palettes.Count;
                    _Map.Palette = _Palettes[_Index];
                    return true;
                case 'b':
                    _Map.PaletteMode = _Map.PaletteMode == HeatMapPaletteMode.Foreground ? HeatMapPaletteMode.Background : HeatMapPaletteMode.Foreground;
                    return true;
                case 'l':
                    bool ascii = _Map.LevelGlyphs != null && _Map.LevelGlyphs[0] == _AsciiGlyphs[0];
                    _Map.LevelCount = _Map.LevelCount == 0 ? 5 : 0;
                    if (_Map.LevelCount > 0)
                        _Map.LevelGlyphs = ascii ? _AsciiGlyphs : _ShadeGlyphs;
                    return true;
                case 'a':
                    if (_Map.LevelCount == 0)
                        _Map.LevelCount = 5;
                    _Map.LevelGlyphs = _Map.LevelGlyphs != null && _Map.LevelGlyphs[0] == _AsciiGlyphs[0] ? _ShadeGlyphs : _AsciiGlyphs;
                    return true;
                default:
                    return false;
            }
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = _Map.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("P", "Palette"));
            hints.Add(new KeyHint("B", "Tiles"));
            hints.Add(new KeyHint("L", "Levels"));
            hints.Add(new KeyHint("A", "ASCII"));
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _Map.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _Map.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount));
        }

        public string? GetTooltip(int x, int y)
        {
            return _Map.GetTooltip(x, y - 1);
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

            string mode = _Map.PaletteMode == HeatMapPaletteMode.Background ? "tiles" : "glyphs";
            string levels = _Map.LevelCount == 0 ? "continuous" : _Map.LevelCount + " levels";
            string status = "p " + _Map.Palette + " " + (_Index + 1) + "/" + _Palettes.Count + "  b " + mode + "  l " + levels + "  a ascii";
            surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(status, width), CellStyle.Default.WithForeground(Color.FromPalette(2)));
            _Map.Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }
    }
}
