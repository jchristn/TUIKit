namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for visible focus: <see cref="FocusFrame"/>, <see cref="FocusFrameOptions"/>, the host's
    /// focused-region treatment (<see cref="TuiApplication.HighlightFocusedRegion"/> and
    /// <see cref="RegionBuilder.WithFocusedBorder"/>), <see cref="FocusPath"/> and
    /// <see cref="TuiApplication.FocusPathChanged"/>, and <see cref="SplitView.ShowPaneFrames"/>.
    /// Assertions inspect glyphs and styles in the captured frame.
    /// </summary>
    public static class FocusFrameSuite
    {
        /// <summary>
        /// Builds the focus frame suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FocusFrame",
                displayName: "Focus Frames and Focus Path",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FocusFrame", "FocusedRegionHeavyBorder", "The focused region draws heavy glyphs in the focus style; focus moves swap them",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = TwoRegions(backend, true))
                            {
                                CellBuffer frame = Render(app);
                                CellStyle focus = FocusFrame.FocusedStyle(app.Theme);
                                Check.Equal("┏", frame.Get(0, 0).Grapheme, "left region corner is heavy");
                                Check.Equal(focus, frame.Get(0, 0).Style, "left region corner uses the focus style");
                                Check.Equal("┌", frame.Get(20, 0).Grapheme, "right region corner is light");
                                Check.Equal(app.Theme.Border, frame.Get(20, 0).Style, "right region corner uses the border style");

                                app.FocusNext();
                                frame = Render(app);
                                Check.Equal("┌", frame.Get(0, 0).Grapheme, "left region now light");
                                Check.Equal("┏", frame.Get(20, 0).Grapheme, "right region now heavy");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "ContentNeverShifts", "Moving focus leaves every content cell where it was",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = TwoRegions(backend, true))
                            {
                                CellBuffer before = Render(app);
                                app.FocusNext();
                                CellBuffer after = Render(app);
                                for (int y = 1; y < 7; y++)
                                {
                                    for (int x = 1; x < 19; x++)
                                        Check.Equal(before.Get(x, y).Grapheme, after.Get(x, y).Grapheme, "left content cell " + x + "," + y);
                                    for (int x = 21; x < 39; x++)
                                        Check.Equal(before.Get(x, y).Grapheme, after.Get(x, y).Grapheme, "right content cell " + x + "," + y);
                                }

                                Check.Equal("A", before.Get(1, 1).Grapheme, "left content starts inside the border");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "TitleMarkerOnlyOnFocused", "The title marker appears only on the focused region's title and can be disabled",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = TwoRegions(backend, true))
                            {
                                string top = Snapshot.ToText(Render(app)).Split('\n')[0];
                                Check.True(top.Contains("> Left"), "focused title marked");
                                Check.True(top.Contains(" Right ") && !top.Contains("> Right"), "unfocused title unmarked");

                                app.FocusFrameOptions.TitleMarker = string.Empty;
                                top = Snapshot.ToText(Render(app)).Split('\n')[0];
                                Check.False(top.Contains(">"), "marker disabled");
                                Check.Equal("┏", Render(app).Get(0, 0).Grapheme, "glyph cue remains without the marker");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "OffByDefault", "Without opting in, regions keep their plain borders",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = TwoRegions(backend, false))
                            {
                                CellBuffer frame = Render(app);
                                Check.Equal("┌", frame.Get(0, 0).Grapheme, "focused region still light");
                                Check.False(Snapshot.ToText(frame).Contains(">"), "no marker");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "PerRegionOptIn", "WithFocusedBorder opts one region in with its own style",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create()
                                    .Add("a", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "A").WithFocusedBorder(BorderStyle.Double))
                                    .Add("b", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "B"))
                                    .Build();
                                app.Bind("a", new ListView<string>());
                                app.Bind("b", new ListView<string>());
                                app.Start();

                                Check.Equal("╔", Render(app).Get(0, 0).Grapheme, "opted-in region uses its focused border");
                                app.FocusNext();
                                CellBuffer frame = Render(app);
                                Check.Equal("┌", frame.Get(0, 0).Grapheme, "opted-in region unfocused uses its border");
                                Check.Equal("┌", frame.Get(20, 0).Grapheme, "other region never highlighted");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "AsciiDiffersByGlyph", "With ASCII borders (HighContrast) focused and unfocused frames still differ by glyph",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = TwoRegions(backend, true))
                            {
                                app.Theme = Theme.HighContrast;
                                CellBuffer frame = Render(app);
                                Check.Equal("#", frame.Get(0, 0).Grapheme, "focused corner is heavy ASCII");
                                Check.Equal("=", frame.Get(10, 7).Grapheme, "focused bottom edge is heavy ASCII");
                                Check.Equal("+", frame.Get(20, 0).Grapheme, "unfocused corner is plain ASCII");
                                Check.Equal("-", frame.Get(30, 7).Grapheme, "unfocused bottom edge is plain ASCII");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "BorderlessRegionUnaffected", "A region with no border draws no frame when focused",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.HighlightFocusedRegion = true;
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                ListView<string> list = new ListView<string>();
                                list.SetItems(new[] { "row" });
                                app.Bind("main", list);
                                app.Start();
                                CellBuffer frame = Render(app);
                                Check.Equal("r", frame.Get(0, 0).Grapheme, "content at the origin, no border");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "DrawHelper", "FocusFrame.Draw renders focused, unfocused, gutter, and empty cases",
                        _ =>
                        {
                            Theme theme = Theme.Dark;
                            FocusFrameOptions options = new FocusFrameOptions();
                            CellBuffer buffer = new CellBuffer(10, 5);
                            BufferSurface surface = new BufferSurface(buffer);

                            FocusFrame.Draw(surface, new Rect(0, 0, 10, 5), true, theme, options, "T");
                            Check.Equal("┏", buffer.Get(0, 0).Grapheme, "focused corner");
                            Check.True(Snapshot.ToText(buffer).Contains("> T"), "focused title marked");

                            buffer = new CellBuffer(10, 5);
                            surface = new BufferSurface(buffer);
                            FocusFrame.Draw(surface, new Rect(0, 0, 10, 5), false, theme, options, "T");
                            Check.Equal("┌", buffer.Get(0, 0).Grapheme, "unfocused corner");

                            buffer = new CellBuffer(10, 5);
                            surface = new BufferSurface(buffer);
                            FocusFrame.Draw(surface, new Rect(0, 0, 2, 5), true, theme, options);
                            Check.Equal(options.GutterGlyph, buffer.Get(0, 3).Grapheme, "narrow focused area draws the gutter bar");
                            Check.Equal(" ", buffer.Get(1, 0).Grapheme, "gutter is one column");

                            buffer = new CellBuffer(10, 5);
                            surface = new BufferSurface(buffer);
                            FocusFrame.Draw(surface, new Rect(0, 0, 2, 5), false, theme, options);
                            Check.Equal(" ", buffer.Get(0, 0).Grapheme, "narrow unfocused area draws nothing");

                            FocusFrame.Draw(surface, new Rect(0, 0, 0, 0), true, theme, options);
                            FocusFrame.Draw(surface, new Rect(3, 3, 4, 0), true, theme, options);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "Guards", "Options, helpers, regions, and the host reject invalid input",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions();
                            Check.Throws<ArgumentOutOfRangeException>(() => options.MinimumBoxSize = 1, "minimum box below 2");
                            Check.Throws<ArgumentOutOfRangeException>(() => options.MinimumBoxSize = 65, "minimum box above 64");
                            Check.Throws<ArgumentException>(() => options.FocusedBorder = BorderStyle.None, "focused border none");
                            Check.Throws<ArgumentNullException>(() => options.TitleMarker = null!, "null marker");
                            Check.Throws<ArgumentException>(() => options.GutterGlyph = string.Empty, "empty gutter glyph");

                            BufferSurface surface = new BufferSurface(new CellBuffer(4, 4));
                            Check.Throws<ArgumentNullException>(() => FocusFrame.Draw(null!, new Rect(0, 0, 4, 4), true, Theme.Dark, options), "null surface");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.Draw(surface, new Rect(0, 0, 4, 4), true, (Theme)null!, options), "null theme");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.Draw(surface, new Rect(0, 0, 4, 4), true, Theme.Dark, null!), "null options");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.FocusedStyle(null!), "null theme for style");

                            Check.Throws<ArgumentException>(() => Region.Define("r").FillWidth().FillHeight().WithFocusedBorder(BorderStyle.None), "builder rejects none");
                            Check.Throws<ArgumentException>(() => new Region("r", AxisConstraint.Stretch(0, 0), AxisConstraint.Stretch(0, 0), Padding.Empty, BorderStyle.Line, null, null, null, BorderStyle.None), "constructor rejects none");

                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(10, 4)))
                                Check.Throws<ArgumentNullException>(() => app.FocusFrameOptions = null!, "host rejects null options");

                            Check.Throws<ArgumentNullException>(() => FocusPath.Empty.Contains(null!), "path contains null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "FocusPathFollowsContainers", "CurrentFocusPath walks into containers and FocusPathChanged fires on inner moves",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                ListView<string> list = new ListView<string>();
                                list.SetItems(new[] { "x" });
                                TextField field = new TextField();
                                SplitView split = new SplitView(SplitOrientation.Horizontal, list, field) { ForwardKeys = true };
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                List<FocusPath> changes = new List<FocusPath>();
                                app.FocusPathChanged += path => changes.Add(path);
                                app.Bind("main", split);
                                app.Start();
                                app.RenderOnce();

                                FocusPath path = app.CurrentFocusPath;
                                Check.Equal("main", path.Region, "region");
                                Check.Equal(2, path.Depth, "split and list");
                                Check.True(ReferenceEquals(list, path.Leaf), "list is the leaf");
                                Check.True(path.Contains(split) && path.Contains(list) && !path.Contains(field), "membership");

                                int before = changes.Count;
                                backend.FeedKey("tab");
                                app.PumpInputOnce();
                                path = app.CurrentFocusPath;
                                Check.True(ReferenceEquals(field, path.Leaf), "Tab moved inside the split to the field");
                                Check.True(changes.Count > before, "FocusPathChanged raised for an inner move");
                                Check.True(ReferenceEquals(path, changes[changes.Count - 1]), "event carries the new path");

                                int settled = changes.Count;
                                app.RenderOnce();
                                app.RenderOnce();
                                Check.Equal(settled, changes.Count, "no event when nothing moved");
                                Check.True(path.ToString().Contains("SplitView > TextField"), "readable description");
                                app.Stop();
                            }

                            Check.True(FocusPath.Build("r", null).IsEmpty, "null root builds the empty path");
                            Check.True(FocusPath.Empty.Leaf == null, "empty path has no leaf");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "SplitViewPaneFrames", "SplitView pane frames highlight the focused pane and keep clicks aligned",
                        _ =>
                        {
                            ListView<string> left = new ListView<string>();
                            left.SetItems(new[] { "l0", "l1", "l2" });
                            ListView<string> right = new ListView<string>();
                            right.SetItems(new[] { "r0", "r1", "r2" });
                            SplitView split = new SplitView(SplitOrientation.Horizontal, left, right) { ForwardKeys = true, ShowPaneFrames = true, ShowDivider = false };
                            split.ApplyTheme(Theme.Dark);
                            split.OnFocusChanged(true);

                            WidgetTester tester = WidgetTester.For(split, 20, 6).Render();
                            Check.Equal("┏", tester.CellAt(0, 0).Grapheme, "focused left pane is heavy");
                            Check.Equal("┌", tester.CellAt(10, 0).Grapheme, "right pane is light");
                            Check.Equal("l0", tester.Row(1).Substring(1, 2), "left content inset by the frame");

                            tester.Press(KeyEvent.Special(KeyCode.Tab)).Render();
                            Check.Equal("┌", tester.CellAt(0, 0).Grapheme, "left pane now light");
                            Check.Equal("┏", tester.CellAt(10, 0).Grapheme, "right pane now heavy");

                            tester.Click(11, 3).Render();
                            Check.Equal(2, right.SelectedIndex, "a click maps through the frame to the third row");

                            split.OnFocusChanged(false);
                            tester.Render();
                            Check.Equal("┌", tester.CellAt(10, 0).Grapheme, "no pane is heavy once the split loses focus");
                            Check.Throws<ArgumentNullException>(() => split.FrameOptions = null!, "null frame options");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFrame", "ThemesRegisterRoles", "All built-in themes register the focus roles",
                        _ =>
                        {
                            foreach (Theme theme in new[] { Theme.Dark, Theme.Light, Theme.HighContrast })
                            {
                                Check.True(theme.HasStyle(Theme.FocusBorderRole), theme.Name + " focus border");
                                Check.True(theme.HasStyle(Theme.FocusTitleRole), theme.Name + " focus title");
                                Check.True(theme.HasStyle(Theme.TabFocusedRole), theme.Name + " focused tab");
                                Check.True(theme.HasStyle(Theme.InlineButtonHoverRole), theme.Name + " inline hover");
                                Check.True((theme.GetStyle(Theme.FocusBorderRole).Attributes & CellAttributes.Bold) != 0, theme.Name + " focus border is bold");
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static TuiApplication TwoRegions(HeadlessBackend backend, bool highlight)
        {
            TuiApplication app = new TuiApplication(backend);
            app.HighlightFocusedRegion = highlight;
            app.Layout = Layout.Create()
                .Add("left", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "Left"))
                .Add("right", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "Right"))
                .Build();
            ListView<string> left = new ListView<string>();
            left.SetItems(new[] { "A1", "A2" });
            ListView<string> right = new ListView<string>();
            right.SetItems(new[] { "B1", "B2" });
            app.Bind("left", left);
            app.Bind("right", right);
            app.Start();
            return app;
        }

        private static CellBuffer Render(TuiApplication app)
        {
            app.RenderOnce();
            return app.CaptureFrame()!;
        }
    }
}
