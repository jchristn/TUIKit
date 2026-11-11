namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.13 cell palettes: <see cref="HeatMapPalette"/> construction, presets, level and continuous colors,
    /// and on <see cref="HeatMap"/> the <see cref="HeatMap.Palette"/> precedence, <see cref="HeatMap.PaletteMode"/>, the
    /// legend, and how palette colors degrade at 256 and 16 colors and with color off.
    /// </summary>
    public static class HeatMapPaletteSuite
    {
        private const string _Id = "HeatMapPalette";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "HeatMap Palette",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Construction", "A palette needs two or more colors, copies them, and validates its arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => new HeatMapPalette(Color.FromRgb(0x112233)), "one color");
                            Check.Throws<ArgumentException>(() => new HeatMapPalette(), "no colors");
                            Check.Throws<ArgumentNullException>(() => new HeatMapPalette((Color[])null!), "null colors");
                            Check.Throws<ArgumentNullException>(() => new HeatMapPalette(null!, Color.Default, new[] { Color.Default, Color.Default }), "null name");
                            Check.Throws<ArgumentNullException>(() => new HeatMapPalette("x", Color.Default, null!), "null sequence");
                            Color[] source = { Color.FromRgb(0x000000), Color.FromRgb(0xFFFFFF) };
                            HeatMapPalette palette = new HeatMapPalette(source);
                            source[0] = Color.FromRgb(0x123456);
                            Check.Equal(Color.FromRgb(0x000000), palette.Colors[0], "the colors are copied");
                            Check.Equal(2, palette.Colors.Count, "two colors");
                            Check.Equal("Custom", palette.Name, "default name");
                            Check.Equal("Custom", palette.ToString(), "ToString is the name");
                            Check.Equal(Color.FromRgb(0x8C8C8C), palette.EmptyColor, "default empty color");
                            HeatMapPalette named = new HeatMapPalette("Ocean", Color.FromPalette(8), new List<Color> { Color.FromPalette(4), Color.FromPalette(12), Color.FromPalette(14) });
                            Check.Equal("Ocean", named.Name, "name");
                            Check.Equal(Color.FromPalette(8), named.EmptyColor, "empty color");
                            Check.Equal(3, named.Colors.Count, "three colors");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Presets", "Each preset has four truecolor stops, distinct from each other and from its empty color at 256 colors",
                        _ =>
                        {
                            Check.Equal("Green,Blue,Purple,Heat,Grayscale", string.Join(",", HeatMapPalette.Presets.Select(p => p.Name)), "preset order");
                            Check.True(ReferenceEquals(HeatMapPalette.Green, HeatMapPalette.Presets[0]), "Presets holds the static instances");
                            foreach (HeatMapPalette preset in HeatMapPalette.Presets)
                            {
                                Check.Equal(4, preset.Colors.Count, preset.Name + " stops");
                                Check.True(preset.Colors.All(c => c.Kind == ColorKind.Rgb), preset.Name + " is truecolor");
                                List<byte> indexes = preset.Colors.Concat(new[] { preset.EmptyColor }).Select(c => ColorQuantizer.ToPalette256(c.R, c.G, c.B)).ToList();
                                Check.Equal(indexes.Count, indexes.Distinct().Count(), preset.Name + " distinct at 256 colors: " + string.Join(",", indexes));
                                Check.Equal(preset.EmptyColor, preset.ColorForLevel(0, 5), preset.Name + " level 0 is empty");
                                for (int level = 1; level < 5; level++)
                                    Check.Equal(preset.Colors[level - 1], preset.ColorForLevel(level, 5), preset.Name + " level " + level);
                            }

                            Check.True(HeatMapPalette.Heat.Colors[0].R > 0xE0 && HeatMapPalette.Heat.Colors[0].G > 0xC0, "heat starts yellow");
                            Check.True(HeatMapPalette.Heat.Colors[3].R > 0xA0 && HeatMapPalette.Heat.Colors[3].G < 0x40, "heat ends red");
                            Check.True(HeatMapPalette.Grayscale.Colors.All(c => c.R == c.G && c.G == c.B), "grayscale is gray");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ColorAt", "ColorAt interpolates in RGB, clamps, treats NaN as 0, and steps between non-RGB stops",
                        _ =>
                        {
                            HeatMapPalette ramp = new HeatMapPalette(Color.FromRgb(0x000000), Color.FromRgb(0xFFFFFF));
                            Check.Equal(Color.FromRgb(0x000000), ramp.ColorAt(0), "0 is the first stop");
                            Check.Equal(Color.FromRgb(0xFFFFFF), ramp.ColorAt(1), "1 is the last stop");
                            Check.Equal(Color.FromRgb(0x808080), ramp.ColorAt(0.5), "midpoint");
                            Check.Equal(Color.FromRgb(0x404040), ramp.ColorAt(0.25), "quarter");
                            Check.Equal(Color.FromRgb(0x000000), ramp.ColorAt(-3), "below 0 clamps");
                            Check.Equal(Color.FromRgb(0xFFFFFF), ramp.ColorAt(9), "above 1 clamps");
                            Check.Equal(Color.FromRgb(0x000000), ramp.ColorAt(double.NaN), "NaN is 0");
                            Check.Equal(Color.FromRgb(0xFFFFFF), ramp.ColorAt(double.PositiveInfinity), "infinity clamps");

                            HeatMapPalette three = new HeatMapPalette(Color.FromRgb(0xFF0000), Color.FromRgb(0x00FF00), Color.FromRgb(0x0000FF));
                            Check.Equal(Color.FromRgb(0x00FF00), three.ColorAt(0.5), "the middle stop");
                            Check.Equal(Color.FromRgb(0x008080), three.ColorAt(0.75), "between the second and third stops");

                            HeatMapPalette indexed = new HeatMapPalette(Color.FromPalette(1), Color.FromPalette(9));
                            Check.Equal(Color.FromPalette(1), indexed.ColorAt(0.4), "palette stops do not blend: nearer is the first");
                            Check.Equal(Color.FromPalette(9), indexed.ColorAt(0.6), "nearer is the second");

                            Check.Equal(ramp.Colors[1], ramp.ColorForLevel(1, 2), "two levels: the one non-zero level is the top color");
                            Check.Equal(ramp.Colors[1], ramp.ColorForLevel(2, 3), "three levels: level 2 is the top");
                            Check.Equal(Color.FromRgb(0x808080), ramp.ColorForLevel(2, 4), "four levels: level 2 is halfway");
                            Check.Throws<ArgumentOutOfRangeException>(() => ramp.ColorForLevel(0, 1), "levelCount 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => ramp.ColorForLevel(0, 17), "levelCount 17");
                            Check.Throws<ArgumentOutOfRangeException>(() => ramp.ColorForLevel(5, 5), "level past the top");
                            Check.Throws<ArgumentOutOfRangeException>(() => ramp.ColorForLevel(-1, 5), "negative level");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Precedence", "LevelStyles wins over Palette, Palette over the theme, and the theme over CellColor",
                        _ =>
                        {
                            HeatMap map = Levels();
                            Color red = Color.FromPalette(1);
                            map.CellColor = red;
                            Check.Equal(CellStyle.Default.WithForeground(red), StyleAt(map, 4), "CellColor alone");
                            Theme theme = Theme.Light;
                            map.ApplyTheme(theme);
                            map.UseThemeStyles = true;
                            Check.Equal(theme.Success, StyleAt(map, 4), "the theme over CellColor");
                            map.Palette = HeatMapPalette.Blue;
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Blue.Colors[3]), StyleAt(map, 4), "the palette over the theme");
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Blue.Colors[0]), StyleAt(map, 1), "level 1 is the first stop");
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Blue.EmptyColor), StyleAt(map, 0), "level 0 is the empty color");
                            map.UseThemeStyles = false;
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Blue.Colors[3]), StyleAt(map, 4), "the palette over CellColor");
                            CellStyle bold = CellStyle.Default.WithForeground(Color.FromPalette(5)).WithAttribute(CellAttributes.Bold, true);
                            map.LevelStyles = new[] { bold, bold, bold, bold, bold };
                            map.PaletteMode = HeatMapPaletteMode.Background;
                            Check.Equal(bold, StyleAt(map, 4), "LevelStyles over the palette, and PaletteMode does not apply");
                            map.LevelStyles = null;
                            map.Palette = null;
                            Check.Equal(CellStyle.Default.WithForeground(red), StyleAt(map, 4), "back to CellColor");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Background", "Background mode fills tiles, leaves level 0 and gaps unfilled, and lets the selection show",
                        _ =>
                        {
                            HeatMap map = Levels();
                            map.Palette = HeatMapPalette.Heat;
                            map.PaletteMode = HeatMapPaletteMode.Background;
                            map.CellGap = 1;
                            CellBuffer buffer = MarkdownThemeSuite.Draw(map, 20, 1);
                            Color top = HeatMapPalette.Heat.Colors[3];
                            Check.Equal(new CellStyle(top, top), buffer.Get(8, 0).Style, "top level is a solid tile");
                            Check.Equal(new CellStyle(HeatMapPalette.Heat.Colors[1], HeatMapPalette.Heat.Colors[1]), buffer.Get(4, 0).Style, "level 2 tile");
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Heat.EmptyColor), buffer.Get(0, 0).Style, "level 0 keeps only its foreground");
                            Check.Equal(CellStyle.Default, buffer.Get(7, 0).Style, "gaps are not filled");
                            Check.Equal("█", buffer.Get(8, 0).Grapheme, "the level glyph is still drawn under the tile");

                            map.IsSelectable = true;
                            map.Select(0, 4);
                            CellStyle selected = MarkdownThemeSuite.Draw(map, 20, 1).Get(8, 0).Style;
                            Check.Equal(CellAttributes.Reverse, selected.Attributes, "selection reversed");
                            Check.Equal(Color.Default, selected.Background, "the selected cell drops its tile so reverse video shows");
                            Check.Equal(top, selected.Foreground, "and keeps its color");

                            map.PaletteMode = HeatMapPaletteMode.Foreground;
                            Check.Equal(Color.Default, MarkdownThemeSuite.Draw(map, 20, 1).Get(6, 0).Style.Background, "foreground mode sets no background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Legend", "The legend draws each level in the same style as the cells, in both modes and with continuous shading",
                        _ =>
                        {
                            foreach (HeatMapPaletteMode mode in new[] { HeatMapPaletteMode.Foreground, HeatMapPaletteMode.Background })
                            {
                                HeatMap map = Levels();
                                map.ShowLegend = true;
                                map.Palette = HeatMapPalette.Purple;
                                map.PaletteMode = mode;
                                CellBuffer buffer = MarkdownThemeSuite.Draw(map, 30, 2);
                                for (int level = 0; level < 5; level++)
                                {
                                    Check.Equal(buffer.Get(level, 0).Style, buffer.Get(5 + level * 2, 1).Style, mode + " legend level " + level);
                                    Check.Equal(buffer.Get(level, 0).Grapheme, buffer.Get(5 + level * 2, 1).Grapheme, mode + " legend glyph " + level);
                                }
                            }

                            HeatMap continuous = new HeatMap { ShowLegend = true, Palette = HeatMapPalette.Green };
                            continuous.SetCells(new double[,] { { 0, 1, 2, 3 } });
                            CellBuffer shaded = MarkdownThemeSuite.Draw(continuous, 30, 2);
                            Check.Equal("Less ░ ▒ ▓ █ More", Snapshot.RenderWidget(continuous, 30, 2).Split('\n')[1], "continuous legend text");
                            for (int shade = 0; shade < 4; shade++)
                                Check.Equal(shaded.Get(shade, 0).Style, shaded.Get(5 + shade * 2, 1).Style, "continuous legend shade " + shade);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Continuous", "With continuous shading each cell takes ColorAt of its normalized value",
                        _ =>
                        {
                            HeatMapPalette ramp = new HeatMapPalette(Color.FromRgb(0x000000), Color.FromRgb(0xFF0000));
                            HeatMap map = new HeatMap { Palette = ramp };
                            map.SetCells(new double[,] { { 0, 5, 10, 2.5 } });
                            CellBuffer buffer = MarkdownThemeSuite.Draw(map, 10, 1);
                            Check.Equal(Color.FromRgb(0x000000), buffer.Get(0, 0).Style.Foreground, "minimum is the first stop");
                            Check.Equal(Color.FromRgb(0x800000), buffer.Get(1, 0).Style.Foreground, "the middle value is the midpoint color");
                            Check.Equal(Color.FromRgb(0xFF0000), buffer.Get(2, 0).Style.Foreground, "maximum is the last stop");
                            Check.Equal(ramp.ColorAt(0.25), buffer.Get(3, 0).Style.Foreground, "a quarter of the way");
                            map.SetRange(0, 20);
                            Check.Equal(Color.FromRgb(0x800000), MarkdownThemeSuite.Draw(map, 10, 1).Get(2, 0).Style.Foreground, "SetRange rescales the colors");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Degrade", "Palette colors quantize to 256 and 16 colors, vanish with color off, and glyphs still tell levels apart",
                        _ =>
                        {
                            HeatMap map = Levels();
                            map.Palette = HeatMapPalette.Green;
                            CellStyle top = MarkdownThemeSuite.Draw(map, 10, 1).Get(4, 0).Style;
                            Check.True(Ansi.Sgr(top, TerminalColorDepth.TrueColor).Contains(";38;2;30;110;49"), "truecolor emits the stop");
                            Check.True(Ansi.Sgr(top, TerminalColorDepth.Palette256).Contains(";38;5;" + ColorQuantizer.ToPalette256(0x1E, 0x6E, 0x31)), "256 colors uses the renderer's quantizer");
                            Check.True(Ansi.Sgr(top, TerminalColorDepth.Ansi16).Contains(";3") || Ansi.Sgr(top, TerminalColorDepth.Ansi16).Contains(";9"), "16 colors emits an ANSI code");
                            map.PaletteMode = HeatMapPaletteMode.Background;
                            CellStyle tile = MarkdownThemeSuite.Draw(map, 10, 1).Get(4, 0).Style;
                            Check.Equal("\u001b[0m", Ansi.Sgr(tile, TerminalColorDepth.None), "color off drops foreground and background");
                            Check.Equal("·░▒▓█", Snapshot.RenderWidget(map, 10, 1).TrimEnd('\n', ' '), "the level glyphs remain distinct");
                            map.LevelGlyphs = new[] { ".", "-", "=", "+", "#" };
                            map.ShowLegend = true;
                            Check.Equal(".-=+#\nLess . - = + # More", Snapshot.RenderWidget(map, 30, 2).TrimEnd('\n', ' '), "ASCII glyphs and legend with a palette");
                            Check.Equal(HeatMapPalette.Green.Colors[3], MarkdownThemeSuite.Draw(map, 30, 2).Get(4, 0).Style.Background, "ASCII glyphs keep the palette tile");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static HeatMap Levels()
        {
            HeatMap map = new HeatMap();
            map.SetCells(new double[,] { { 0, 1, 2, 3, 4 } });
            map.LevelCount = 5;
            map.LevelGlyphs = new[] { "·", "░", "▒", "▓", "█" };
            return map;
        }

        private static CellStyle StyleAt(HeatMap map, int column)
        {
            return MarkdownThemeSuite.Draw(map, 10, 1).Get(column, 0).Style;
        }
    }
}
