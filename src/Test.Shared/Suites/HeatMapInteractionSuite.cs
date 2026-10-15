namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.10 item C8: <see cref="HeatMap"/> selection, levels, legend, wide cells, spanning labels,
    /// descriptions, scrolling, theme, the selectable-cell and navigation hooks, and the 1.9 defaults.
    /// </summary>
    public static class HeatMapInteractionSuite
    {
        private const string _Id = "HeatMapInteraction";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "HeatMap Interaction",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Defaults", "With defaults the map renders as in 1.9, takes no keys or clicks, and is not focusable",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.SetColumnLabels(new[] { "Mon", "Tue", "Wed", "Thu" });
                            map.SetRowLabels(new[] { "a", "b" });
                            Check.Equal("  MTWT\na ░▒▓█\nb █▓▒░", Snapshot.RenderWidget(map, 10, 3).TrimEnd(), "1.9 rendering");
                            Check.False(map.HandleKey(KeyEvent.Special(KeyCode.Right)), "no keys");
                            Check.False(map.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 3, 1, KeyModifiers.None, 1)), "no clicks");
                            Check.False(FocusScope.IsFocusable(map), "not focusable");
                            Check.False(FocusScope.IsTabStop(map), "not a Tab stop");
                            Check.True(map.GetKeyHints() == null, "no hints");
                            Check.Equal(new Size(6, 3), map.Measure(new Size(20, 10)), "measure");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "HostClick", "A bound map that is not selectable does not take focus from a click; a selectable one does",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                HeatMap map = Grid();
                                TextField field = new TextField();
                                app.Layout = TUIKit.Layout.Layout.Create()
                                    .Add("field", r => r.FillWidth().TopAnchored(0, 1).WithPadding(0))
                                    .Add("map", r => r.FillWidth().TopAnchored(1, 4).WithPadding(0))
                                    .Build();
                                app.Bind("field", field);
                                app.Bind("map", map);
                                app.Start();
                                app.RenderOnce();
                                backend.FeedClick(2, 2);
                                app.PumpInputOnce();
                                Check.Equal("field", app.CurrentFocusPath.Region, "focus stayed in the field");
                                map.IsSelectable = true;
                                app.RenderOnce();
                                backend.FeedClick(2, 2);
                                app.PumpInputOnce();
                                Check.Equal("map", app.CurrentFocusPath.Region, "a selectable map takes focus");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Selection", "Arrow keys, Home, End, and clicks move the selection; events fire; Enter is consumed only with a handler",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.IsSelectable = true;
                            Check.True(FocusScope.IsFocusable(map), "focusable when selectable");
                            Check.False(FocusScope.IsTabStop(map), "IsFocusStop defaults to false");
                            map.IsFocusStop = true;
                            Check.True(FocusScope.IsTabStop(map), "Tab stop on request");
                            int changes = 0;
                            map.SelectionChanged += (s, e) => changes++;
                            map.HandleKey(KeyEvent.Special(KeyCode.Right));
                            map.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(1, map.SelectedRow, "row");
                            Check.Equal(1, map.SelectedColumn, "column");
                            Check.Equal(2, changes, "two changes");
                            map.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(3, map.SelectedColumn, "End");
                            map.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, map.SelectedColumn, "Home");
                            map.HandleKey(KeyEvent.Special(KeyCode.Up));
                            map.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(0, map.SelectedRow, "stops at the edge");
                            Check.False(map.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter without a handler");
                            int activated = 0;
                            map.CellActivated += (s, e) => activated++;
                            Check.True(map.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter with a handler");
                            Snapshot.RenderWidget(map, 10, 3);
                            Check.True(map.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 2, 1, KeyModifiers.None, 2)), "double click");
                            Check.Equal(1, map.SelectedRow, "clicked row");
                            Check.Equal(2, map.SelectedColumn, "clicked column");
                            Check.Equal(2, activated, "Enter and double click");
                            Check.True((MarkdownThemeSuite.Draw(map, 10, 3).Get(2, 1).Style.Attributes & CellAttributes.Reverse) != 0, "selection drawn reversed");
                            map.OnFocusChanged(true);
                            Check.True(map.GetKeyHints()!.Any(h => h.Description == "Move"), "hints while focused");
                            map.Select(9, 9);
                            Check.Equal(1, map.SelectedRow, "Select clamps the row");
                            Check.Equal(3, map.SelectedColumn, "Select clamps the column");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Levels", "LevelOf boundaries; glyph and style lists validated; distinct glyphs render per level",
                        _ =>
                        {
                            HeatMap map = new HeatMap();
                            map.SetCells(new double[,] { { 0, 1, 2, 3, 4 } });
                            Check.Throws<ArgumentOutOfRangeException>(() => map.LevelCount = 1, "1 rejected");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.LevelCount = 17, "17 rejected");
                            map.LevelCount = 5;
                            Check.Equal("0,1,2,3,4", string.Join(",", Enumerable.Range(0, 5).Select(c => map.LevelOf(0, c))), "five levels");
                            Check.Throws<ArgumentException>(() => map.LevelGlyphs = new[] { "a" }, "wrong glyph count");
                            Check.Throws<ArgumentException>(() => map.LevelStyles = new[] { CellStyle.Default }, "wrong style count");
                            map.LevelGlyphs = new[] { " ", ".", ":", "+", "#" };
                            Check.Equal(" .:+#", Snapshot.RenderWidget(map, 10, 1).TrimEnd(' ').PadRight(5).Substring(0, 5), "distinct glyphs");
                            CellStyle red = CellStyle.Default.WithForeground(Color.FromPalette(1));
                            map.LevelStyles = new[] { CellStyle.Default, CellStyle.Default, CellStyle.Default, CellStyle.Default, red };
                            Check.Equal(red, MarkdownThemeSuite.Draw(map, 10, 1).Get(4, 0).Style, "level style");
                            map.LevelCount = 3;
                            Check.True(map.LevelGlyphs == null && map.LevelStyles == null, "mismatched lists cleared");
                            Check.Equal("0,1,1,2,2", string.Join(",", Enumerable.Range(0, 5).Select(c => map.LevelOf(0, c))), "three levels");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.LevelOf(5, 0), "outside");
                            map.LevelCount = 0;
                            Check.Equal(3, map.LevelOf(0, 4), "continuous shade index");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LegendWidthLabels", "The legend, CellWidth, and spanning labels draw and truncate correctly",
                        _ =>
                        {
                            HeatMap map = new HeatMap();
                            map.SetCells(new double[,] { { 0, 1, 2, 3, 0, 1 } });
                            map.ShowLegend = true;
                            string legend = Snapshot.RenderWidget(map, 30, 2).Split('\n')[1];
                            Check.Equal("Less ░ ▒ ▓ █ More", legend, "continuous legend");
                            Check.Throws<ArgumentNullException>(() => map.LegendLowText = null!, "null legend text");
                            map.CellWidth = 2;
                            Check.Equal(12, Snapshot.RenderWidget(map, 30, 2).Split('\n')[0].Length, "CellWidth 2 doubles the width");
                            Check.Throws<ArgumentOutOfRangeException>(() => map.CellWidth = 5, "width 5");
                            map.SetColumnLabels(new[] { "January", "", "", "Feb", "", "" });
                            map.SpanColumnLabels = true;
                            string header = Snapshot.RenderWidget(map, 30, 3).Split('\n')[0];
                            Check.Equal("Januar", header.Substring(0, 6), "spans three empty columns then truncates");
                            Check.Equal("Feb", header.Substring(6, 3), "next label at its column");
                            map.SpanColumnLabels = false;
                            Check.Equal("Ja    Fe", Snapshot.RenderWidget(map, 30, 3).Split('\n')[0].TrimEnd(), "without spanning");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Description", "CellDescription serves the tooltip and the selection footer",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.IsSelectable = true;
                            map.CellDescription = (r, c) => "cell " + r + "," + c;
                            map.ShowSelectionDescription = true;
                            Check.Equal("cell 0,0", Snapshot.RenderWidget(map, 20, 4).Split('\n')[2], "footer");
                            Check.Equal("cell 1,2", map.GetTooltip(2, 1), "tooltip");
                            Check.True(map.GetTooltip(19, 3) == null, "outside the grid");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Scrolling", "A wide selectable grid scrolls to keep the selection visible",
                        _ =>
                        {
                            HeatMap map = new HeatMap { IsSelectable = true };
                            map.SetCells(new double[1, 30]);
                            Snapshot.RenderWidget(map, 10, 1);
                            for (int i = 0; i < 15; i++)
                                map.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Snapshot.RenderWidget(map, 10, 1);
                            Check.Equal(6, map.FirstVisibleColumn, "column 15 is the last visible");
                            map.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Snapshot.RenderWidget(map, 10, 1);
                            Check.Equal(0, map.FirstVisibleColumn, "back to the start");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SelectableCells", "IsCellSelectable skips cells, refuses clicks, and moves Select; NavigateOverride redirects movement",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.IsSelectable = true;
                            map.IsCellSelectable = (r, c) => c != 1;
                            map.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(2, map.SelectedColumn, "skipped column 1");
                            Snapshot.RenderWidget(map, 10, 2);
                            Check.False(map.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 0, KeyModifiers.None, 1)), "click refused");
                            map.Select(0, 1);
                            Check.Equal(2, map.SelectedColumn, "Select moved to the nearest selectable cell");
                            map.IsCellSelectable = (r, c) => false;
                            Check.False(map.HasSelection, "nothing selectable");
                            map.IsCellSelectable = null;
                            map.NavigateOverride = (r, c, key) => key.Code == KeyCode.Right ? new Point(0, 1) : (Point?)null;
                            map.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(1, map.SelectedRow, "override moved down");
                            Check.Equal(0, map.SelectedColumn, "and to column 0");
                            map.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.Equal(0, map.SelectedColumn, "null falls back (already at the edge)");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Theme", "UseThemeStyles false ignores the theme; true styles labels, cells, and the selection",
                        _ =>
                        {
                            HeatMap map = Grid();
                            map.SetRowLabels(new[] { "a", "b" });
                            CellBuffer before = MarkdownThemeSuite.Draw(map, 10, 2);
                            Theme theme = Theme.Light;
                            map.ApplyTheme(theme);
                            Check.True(MarkdownThemeSuite.SameCells(before, MarkdownThemeSuite.Draw(map, 10, 2)), "unchanged while off");
                            map.UseThemeStyles = true;
                            CellBuffer themed = MarkdownThemeSuite.Draw(map, 10, 2);
                            Check.Equal(theme.Muted, themed.Get(0, 0).Style, "label from Muted");
                            Check.Equal(theme.Success, themed.Get(3, 0).Style, "cell from Success");
                            Check.Throws<ArgumentNullException>(() => map.ApplyTheme(null!), "null theme");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(_Id, "DisabledClick", "A click no longer moves focus to a bound widget that cannot take focus, such as a disabled field",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 4);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                TextField enabled = new TextField();
                                TextField disabled = new TextField { IsEnabled = false };
                                app.Layout = TUIKit.Layout.Layout.Create()
                                    .Add("a", r => r.FillWidth().TopAnchored(0, 1).WithPadding(0))
                                    .Add("b", r => r.FillWidth().TopAnchored(1, 1).WithPadding(0))
                                    .Build();
                                app.Bind("a", enabled);
                                app.Bind("b", disabled);
                                app.Start();
                                app.RenderOnce();
                                backend.FeedClick(2, 1);
                                app.PumpInputOnce();
                                Check.Equal("a", app.CurrentFocusPath.Region, "the disabled field did not take focus");
                                disabled.IsEnabled = true;
                                app.RenderOnce();
                                backend.FeedClick(2, 1);
                                app.PumpInputOnce();
                                Check.Equal("b", app.CurrentFocusPath.Region, "once enabled it does");
                                app.Stop();
                            }

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
    }
}
