namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.13 cell spacing: <see cref="HeatMap.CellGap"/> and <see cref="HeatMap.RowGap"/>, their effect on
    /// cell placement, labels, the legend and footer, <see cref="HeatMap.Measure"/>, scrolling, hit testing, tooltips, and
    /// the selection highlight, plus a regression guard that the defaults draw exactly what 1.12.0 drew.
    /// </summary>
    public static class HeatMapSpacingSuite
    {
        private const string _Id = "HeatMapSpacing";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "HeatMap Spacing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Regression112", "With CellGap 0 and RowGap 0 a levelled, labelled, selected map draws the 1.12.0 buffer",
                        _ =>
                        {
                            // Expected text and styles were captured from the 1.12.0 library for this exact configuration.
                            HeatMap map = Rich();
                            string expected = "    JanuarFeb\nMon ··░░▒▒▓▓████\n    ████▓▓▒▒░░··\nWed ▒▒▒▒··██░░▓▓\n    Less · ░ ▒ ▓ █ More\nr1c2";
                            Check.Equal(expected, Snapshot.RenderWidget(map, 24, 7).TrimEnd('\n', ' '), "1.12.0 text");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(map, 24, 7);
                            Check.Equal(CellStyle.Default.WithForeground(Color.FromPalette(8)), buffer.Get(0, 1).Style, "row label style");
                            Check.Equal(CellStyle.Default.WithForeground(Color.FromPalette(2)), buffer.Get(4, 1).Style, "cell style");
                            Check.Equal(CellAttributes.Reverse, buffer.Get(8, 2).Style.Attributes, "selected cell reversed");
                            Check.Equal(CellAttributes.Reverse, buffer.Get(9, 2).Style.Attributes, "across its width");
                            Check.Equal(CellAttributes.None, buffer.Get(10, 2).Style.Attributes, "and no further");
                            Check.Equal(CellStyle.Default, buffer.Get(8, 4).Style, "legend spacer in the default style");
                            Check.Equal(new Size(16, 6), map.Measure(new Size(80, 20)), "1.12.0 measure");

                            Check.Equal(0, map.CellGap, "CellGap defaults to 0");
                            Check.Equal(0, map.RowGap, "RowGap defaults to 0");
                            Check.True(map.Palette == null, "Palette defaults to null");
                            Check.Equal(HeatMapPaletteMode.Foreground, map.PaletteMode, "PaletteMode defaults to Foreground");
                            HeatMap explicitDefaults = Rich();
                            explicitDefaults.CellGap = 0;
                            explicitDefaults.RowGap = 0;
                            explicitDefaults.Palette = null;
                            explicitDefaults.PaletteMode = HeatMapPaletteMode.Foreground;
                            Check.True(MarkdownThemeSuite.SameCells(buffer, MarkdownThemeSuite.Draw(explicitDefaults, 24, 7)), "explicit defaults draw the same cells");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "GapPositions", "CellGap 1 and 2 place cells one stride apart and leave the gap columns blank in the default style",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.CellGap = 1;
                            Check.Equal("░ ▒ ▓ █\n█ ▓ ▒ ░", Snapshot.RenderWidget(map, 10, 2).TrimEnd('\n', ' '), "gap 1");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(map, 10, 2);
                            for (int x = 1; x < 7; x += 2)
                            {
                                Check.Equal(" ", buffer.Get(x, 0).Grapheme, "gap column " + x + " blank");
                                Check.Equal(CellStyle.Default, buffer.Get(x, 0).Style, "gap column " + x + " default style");
                            }

                            Check.Equal(new Size(7, 2), map.Measure(new Size(40, 10)), "4 cells + 3 gaps");
                            map.CellGap = 2;
                            map.CellWidth = 2;
                            Check.Equal("░░  ▒▒  ▓▓  ██", Snapshot.RenderWidget(map, 20, 2).Split('\n')[0], "width 2, gap 2");
                            Check.Equal(new Size(14, 2), map.Measure(new Size(40, 10)), "4 x 2 + 3 x 2");
                            map.SetRowLabels(new[] { "a", "bb" });
                            Check.Equal(new Size(17, 2), map.Measure(new Size(40, 10)), "plus the row label gutter");
                            Check.Equal(new Size(10, 2), map.Measure(new Size(10, 10)), "clamped to the available width");
                            Check.Equal("bb ██  ▓▓  ▒▒  ░░", Snapshot.RenderWidget(map, 20, 2).Split('\n')[1], "the gutter offsets the first cell only");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "GapLabels", "Column labels align with their cells and stay one blank apart; spanning labels still span",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.SetColumnLabels(new[] { "Mon", "Tue", "Wed", "Thu" });
                            map.CellGap = 1;
                            Check.Equal("M T W T", Snapshot.RenderWidget(map, 20, 3).Split('\n')[0], "width 1, gap 1: one letter per cell");
                            map.CellGap = 2;
                            Check.Equal("Mo Tu We Th", Snapshot.RenderWidget(map, 20, 3).Split('\n')[0].TrimEnd(), "width 1, gap 2: the label uses part of the gap");
                            map.CellWidth = 2;
                            map.CellGap = 1;
                            string[] lines = Snapshot.RenderWidget(map, 20, 3).Split('\n');
                            Check.Equal("Mo Tu We Th", lines[0].TrimEnd(), "width 2, gap 1");
                            Check.Equal(lines[0].IndexOf("We", StringComparison.Ordinal), lines[1].IndexOf("▓▓", StringComparison.Ordinal), "label over its cell");

                            HeatMap span = new HeatMap();
                            span.SetCells(new double[,] { { 0, 1, 2, 3, 0, 1 } });
                            span.SetColumnLabels(new[] { "January", "", "", "Feb", "", "" });
                            span.SpanColumnLabels = true;
                            span.CellGap = 1;
                            string header = Snapshot.RenderWidget(span, 30, 2).Split('\n')[0];
                            Check.Equal("Janua Feb", header.TrimEnd(), "spans three cells and their gaps, one blank before the next label");
                            span.CellGap = 2;
                            Check.Equal("January  Feb", Snapshot.RenderWidget(span, 30, 2).Split('\n')[0].TrimEnd(), "room for the whole name");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "GapHitTesting", "Clicks and tooltips find cells, not gaps; the highlight stays in its cell",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.IsSelectable = true;
                            map.CellGap = 1;
                            map.CellDescription = (r, c) => r + "," + c;
                            map.SetRowLabels(new[] { "a", "b" });
                            Snapshot.RenderWidget(map, 20, 2);
                            Check.False(map.HandleMouse(Click(3, 0)), "a click in the first gap selects nothing");
                            Check.Equal(0, map.SelectedColumn, "selection unchanged");
                            Check.True(map.HandleMouse(Click(4, 1)), "a click on the second cell");
                            Check.Equal(1, map.SelectedRow, "row");
                            Check.Equal(1, map.SelectedColumn, "column");
                            Check.True(map.HandleMouse(Click(8, 0)), "the last cell");
                            Check.Equal(3, map.SelectedColumn, "column 3");
                            Check.False(map.HandleMouse(Click(9, 0)), "past the grid");
                            Check.Equal("0,2", map.GetTooltip(6, 0), "tooltip on a cell");
                            Check.True(map.GetTooltip(5, 0) == null, "no tooltip in a gap");
                            Check.True(map.GetTooltip(1, 0) == null, "no tooltip in the gutter");

                            map.CellWidth = 2;
                            map.CellGap = 2;
                            map.Select(0, 1);
                            CellBuffer buffer = MarkdownThemeSuite.Draw(map, 20, 2);
                            Check.Equal(CellAttributes.Reverse, buffer.Get(6, 0).Style.Attributes, "selected cell first column");
                            Check.Equal(CellAttributes.Reverse, buffer.Get(7, 0).Style.Attributes, "selected cell second column");
                            Check.Equal(CellStyle.Default, buffer.Get(8, 0).Style, "gap after the selection is not highlighted");
                            Check.Equal(CellStyle.Default, buffer.Get(5, 0).Style, "gap before the selection is not highlighted");
                            Check.False(map.HandleMouse(Click(8, 0)), "first gap column");
                            Check.False(map.HandleMouse(Click(9, 0)), "second gap column");
                            Check.True(map.HandleMouse(Click(11, 0)), "second column of the third cell");
                            Check.Equal(2, map.SelectedColumn, "the third cell");

                            map.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(3, map.SelectedColumn, "keys still move one cell");
                            map.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(1, map.SelectedRow, "and one row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "GapScrolling", "A grid wider than the widget counts visible cells with their gaps and scrolls to the selection",
                        _ =>
                        {
                            HeatMap map = new HeatMap { IsSelectable = true, CellGap = 1 };
                            map.SetCells(new double[1, 30]);
                            map.CellDescription = (r, c) => "c" + c;
                            Snapshot.RenderWidget(map, 10, 1);
                            Check.Equal("· · · · ·".Replace('·', '░'), Snapshot.RenderWidget(map, 10, 1).TrimEnd('\n', ' '), "five cells fit in ten columns with gap 1");
                            for (int i = 0; i < 15; i++)
                                map.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Snapshot.RenderWidget(map, 10, 1);
                            Check.Equal(11, map.FirstVisibleColumn, "column 15 is the fifth visible");
                            Check.Equal("c15", map.GetTooltip(8, 0), "hit testing follows the scroll");
                            Check.True(map.HandleMouse(Click(0, 0)), "click the first visible cell");
                            Check.Equal(11, map.SelectedColumn, "selects column 11");
                            map.CellWidth = 2;
                            map.CellGap = 2;
                            map.HandleKey(KeyEvent.Special(KeyCode.End));
                            Snapshot.RenderWidget(map, 10, 1);
                            Check.Equal(27, map.FirstVisibleColumn, "three cells of width 2 and gap 2 fit in ten columns");
                            map.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Snapshot.RenderWidget(map, 10, 1);
                            Check.Equal(0, map.FirstVisibleColumn, "back to the start");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RowGap", "RowGap 1 spaces the rows; labels, legend, footer, measure, and hit testing follow",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.SetRowLabels(new[] { "a", "b" });
                            map.SetColumnLabels(new[] { "w", "x", "y", "z" });
                            map.RowGap = 1;
                            map.ShowLegend = true;
                            map.IsSelectable = true;
                            map.ShowSelectionDescription = true;
                            map.CellDescription = (r, c) => "cell " + r + "," + c;
                            string[] lines = Snapshot.RenderWidget(map, 30, 8).Split('\n');
                            Check.Equal("  wxyz", lines[0], "header");
                            Check.Equal("a ░▒▓█", lines[1], "row 0");
                            Check.Equal(string.Empty, lines[2].Trim(), "gap row blank");
                            Check.Equal("b █▓▒░", lines[3], "row 1 with its label");
                            Check.True(lines[4].StartsWith("  Less", StringComparison.Ordinal), "legend below the spaced grid: " + lines[4]);
                            Check.Equal("cell 0,0", lines[5], "footer below the legend");
                            Check.Equal(new Size(6, 6), map.Measure(new Size(30, 20)), "header + 3 grid rows + legend + footer");
                            Check.False(map.HandleMouse(Click(3, 2)), "a click on the gap row selects nothing");
                            Check.True(map.GetTooltip(3, 2) == null, "no tooltip on the gap row");
                            Check.True(map.HandleMouse(Click(3, 3)), "a click on row 1");
                            Check.Equal(1, map.SelectedRow, "row 1");
                            Check.Equal(1, map.SelectedColumn, "column 1");
                            Check.Equal("cell 1,1", map.GetTooltip(3, 3), "tooltip on row 1");

                            HeatMap tall = new HeatMap { RowGap = 1 };
                            tall.SetCells(new double[3, 2]);
                            Check.Equal(new Size(2, 5), tall.Measure(new Size(10, 10)), "three rows and two gaps");
                            Check.Equal(new Size(2, 4), tall.Measure(new Size(10, 4)), "clamped to the available height");
                            string[] clipped = Snapshot.RenderWidget(tall, 2, 4).Split('\n');
                            Check.Equal("░░|  |░░|  ", clipped[0] + "|" + clipped[1].PadRight(2) + "|" + clipped[2] + "|" + clipped[3].PadRight(2), "a short surface clips the last row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Validation", "CellGap, RowGap, and PaletteMode reject values outside their ranges",
                        _ =>
                        {
                            HeatMap map = new HeatMap();
                            Check.Throws<ArgumentOutOfRangeException>(() => map.CellGap = -1, "CellGap -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.CellGap = 3, "CellGap 3");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.RowGap = -1, "RowGap -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.RowGap = 2, "RowGap 2");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.PaletteMode = (HeatMapPaletteMode)7, "undefined PaletteMode");
                            map.CellGap = 2;
                            map.RowGap = 1;
                            Check.Equal(2, map.CellGap, "CellGap 2 accepted");
                            Check.Equal(1, map.RowGap, "RowGap 1 accepted");
                            Check.Equal(new Size(0, 0), map.Measure(new Size(10, 10)), "an empty grid still measures zero");
                            Check.Equal(string.Empty, Snapshot.RenderWidget(map, 5, 2).Trim(), "and draws nothing");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static HeatMap Grid()
        {
            HeatMap map = new HeatMap();
            map.SetCells(new double[,] { { 0, 1, 2, 3 }, { 3, 2, 1, 0 } });
            return map;
        }

        private static HeatMap Rich()
        {
            HeatMap map = new HeatMap();
            map.SetCells(new double[,] { { 0, 1, 2, 3, 4, 5 }, { 5, 4, 3, 2, 1, 0 }, { 2, 2, 0, 5, 1, 3 } });
            map.SetRowLabels(new[] { "Mon", string.Empty, "Wed" });
            map.SetColumnLabels(new[] { "January", string.Empty, string.Empty, "Feb", string.Empty, string.Empty });
            map.SpanColumnLabels = true;
            map.LevelCount = 5;
            map.LevelGlyphs = new[] { "·", "░", "▒", "▓", "█" };
            map.ShowLegend = true;
            map.CellWidth = 2;
            map.IsSelectable = true;
            map.ShowSelectionDescription = true;
            map.CellDescription = (r, c) => "r" + r + "c" + c;
            map.Select(1, 2);
            return map;
        }

        private static MouseEvent Click(int x, int y)
        {
            return new MouseEvent(MouseEventKind.Press, MouseButton.Left, x, y, KeyModifiers.None, 1);
        }
    }
}
