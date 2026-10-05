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
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the focus follow-ups found by applying one focus treatment to a whole application:
    /// joined box borders with tee and cross glyphs (<see cref="SurfaceExtensions.DrawJoinedBox(ISurface, Rect, CellStyle, BorderStyle, string?)"/>,
    /// <see cref="FocusFrameOptions.JoinBorders"/>, <see cref="TuiApplication.JoinRegionBorders"/>), dialogs
    /// with a settable border and public bounds, <see cref="Modal.IsTopmost"/>, region geometry for overlays,
    /// panes going plain behind a modal, and hidden or empty widgets dropping out of the focus order
    /// (<see cref="IHideable"/>, <see cref="FocusAuditProblemKind.InvisibleStop"/>).
    /// </summary>
    public static class FocusFollowUpSuite
    {
        /// <summary>
        /// Builds the focus follow-up suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FocusFollowUp",
                displayName: "Joined Frames, Dialogs, and Focus Stops",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FocusFollowUp", "JoinedLightBoxes", "Boxes sharing an edge or nested inside another meet in tee and cross glyphs",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(21, 7);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawJoinedBox(new Rect(0, 0, 21, 7), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(0, 0, 11, 7), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(10, 0, 11, 4), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(5, 2, 11, 3), CellStyle.Default, BorderStyle.Line);

                            Check.Equal("┬", buffer.Get(10, 0).Grapheme, "top tee where the split meets the outer edge");
                            Check.Equal("┴", buffer.Get(10, 6).Grapheme, "bottom tee");
                            Check.Equal("├", buffer.Get(10, 3).Grapheme, "left tee where the right box's bottom meets the split");
                            Check.Equal("┤", buffer.Get(20, 3).Grapheme, "right tee on the outer edge");
                            Check.Equal("┼", buffer.Get(10, 2).Grapheme, "cross where the nested box crosses the split");
                            Check.Equal("┌", buffer.Get(0, 0).Grapheme, "outer corner untouched");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "HeavyWinsSharedEdge", "A heavy box drawn last stays whole over a shared light line, with mixed-weight tees",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(21, 7);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawJoinedBox(new Rect(0, 0, 11, 7), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(10, 0, 11, 7), CellStyle.Default, BorderStyle.Thick);

                            Check.Equal("┲", buffer.Get(10, 0).Grapheme, "light left meets heavy down and right");
                            Check.Equal("┃", buffer.Get(10, 3).Grapheme, "shared edge drawn heavy");
                            Check.Equal("┺", buffer.Get(10, 6).Grapheme, "light left meets heavy up and right");
                            Check.Equal("┓", buffer.Get(20, 0).Grapheme, "heavy box keeps its own corner");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "DoubleAsciiRounded", "Double, ASCII, and rounded borders join sensibly",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(21, 5);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawJoinedBox(new Rect(0, 0, 21, 5), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(10, 0, 11, 5), CellStyle.Default, BorderStyle.Double);
                            Check.Equal("\u2566", buffer.Get(10, 0).Grapheme, "a single line meeting a double corner joins as a double tee");
                            Check.Equal("╗", buffer.Get(20, 0).Grapheme, "double corner over a single corner stays double");

                            CellBuffer ascii = new CellBuffer(21, 5);
                            BufferSurface asciiSurface = new BufferSurface(ascii);
                            asciiSurface.DrawJoinedBox(new Rect(0, 0, 21, 5), CellStyle.Default, BorderStyle.Ascii);
                            asciiSurface.DrawJoinedBox(new Rect(0, 2, 21, 3), CellStyle.Default, BorderStyle.Ascii);
                            Check.Equal("+", ascii.Get(0, 2).Grapheme, "ASCII tee is a plus");
                            Check.Equal("-", ascii.Get(5, 2).Grapheme, "ASCII line stays a dash");
                            asciiSurface.DrawJoinedBox(new Rect(0, 0, 11, 5), CellStyle.Default, BorderStyle.AsciiHeavy);
                            Check.Equal("#", ascii.Get(10, 2).Grapheme, "heavy ASCII crossing is a hash");

                            CellBuffer rounded = new CellBuffer(20, 4);
                            BufferSurface roundedSurface = new BufferSurface(rounded);
                            roundedSurface.DrawJoinedBox(new Rect(0, 0, 20, 4), CellStyle.Default, BorderStyle.Rounded, "T");
                            Check.Equal("╭", rounded.Get(0, 0).Grapheme, "a lone rounded box keeps rounded corners");
                            Check.True(Snapshot.ToText(rounded).Contains(" T "), "title drawn");
                            roundedSurface.DrawJoinedBox(new Rect(0, 0, 5, 4), CellStyle.Default, BorderStyle.Rounded);
                            Check.Equal("┬", rounded.Get(4, 0).Grapheme, "a rounded box joining another makes a tee");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "JoinedBoxGuards", "Joined boxes reject a null surface, skip tiny rects, and fall back on unreadable surfaces",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => SurfaceExtensions.DrawJoinedBox(null!, new Rect(0, 0, 4, 4), CellStyle.Default, BorderStyle.Line), "null surface");

                            CellBuffer buffer = new CellBuffer(6, 4);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawJoinedBox(new Rect(0, 0, 1, 4), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(0, 0, 6, 4), CellStyle.Default, BorderStyle.None);
                            Check.Equal(string.Empty, Snapshot.ToText(buffer).Trim(), "nothing drawn");

                            CellBuffer plain = new CellBuffer(11, 4);
                            WriteOnlySurface writeOnly = new WriteOnlySurface(plain);
                            writeOnly.DrawJoinedBox(new Rect(0, 0, 11, 4), CellStyle.Default, BorderStyle.Line);
                            writeOnly.DrawJoinedBox(new Rect(5, 0, 6, 4), CellStyle.Default, BorderStyle.Line);
                            Check.Equal("┌", plain.Get(5, 0).Grapheme, "an unreadable surface draws a plain box over the line");

                            CellBuffer outer = new CellBuffer(6, 4);
                            SurfaceView view = new SurfaceView(new BufferSurface(outer), new Rect(1, 1, 4, 2));
                            outer.Set(2, 1, Cell.Glyph("x", CellStyle.Default, 1));
                            Check.Equal("x", view.Get(1, 0).Grapheme, "a view reads through to its parent");
                            Check.Equal(Cell.Empty, view.Get(9, 9), "outside the view reads empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "SplitViewJoinedPanes", "Joined pane frames share the divider line and keep clicks aligned",
                        _ =>
                        {
                            ListView<string> left = new ListView<string>();
                            left.SetItems(new[] { "l0", "l1", "l2" });
                            ListView<string> right = new ListView<string>();
                            right.SetItems(new[] { "r0", "r1", "r2" });
                            SplitView split = new SplitView(SplitOrientation.Horizontal, left, right) { ForwardKeys = true, ShowPaneFrames = true };
                            split.FrameOptions.JoinBorders = true;
                            split.OnFocusChanged(true);

                            WidgetTester tester = WidgetTester.For(split, 21, 6).Render();
                            Check.Equal("┱", tester.CellAt(10, 0).Grapheme, "focused left pane's heavy corner joins the right pane's light edge");
                            Check.Equal("┃", tester.CellAt(10, 2).Grapheme, "shared line drawn heavy for the focused pane");
                            Check.Equal("┹", tester.CellAt(10, 5).Grapheme, "bottom junction");
                            Check.Equal("l0", tester.Row(1).Substring(1, 2), "left content inside the frame");
                            Check.Equal("r0", tester.Row(1).Substring(11, 2), "right content inside the frame");

                            tester.Press(KeyEvent.Special(KeyCode.Tab)).Render();
                            Check.Equal("┲", tester.CellAt(10, 0).Grapheme, "focus moved right: the right pane's heavy corner wins");

                            tester.Click(11, 3);
                            Check.Equal(2, right.SelectedIndex, "click maps into the right pane's third row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "HostJoinedRegions", "Regions overlapping by one column join, with the focused frame whole",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.HighlightFocusedRegion = true;
                                app.JoinRegionBorders = true;
                                app.Layout = Layout.Create()
                                    .Add("a", r => r.LeftAnchored(0, 11).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                                    .Add("b", r => r.LeftAnchored(10, 11).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                                    .Build();
                                ListView<string> a = new ListView<string>();
                                a.SetItems(new[] { "aaa" });
                                ListView<string> b = new ListView<string>();
                                b.SetItems(new[] { "bbb" });
                                app.Bind("a", a);
                                app.Bind("b", b);
                                app.Start();

                                app.RenderOnce();
                                CellBuffer frame = app.CaptureFrame()!;
                                Check.Equal("┱", frame.Get(10, 0).Grapheme, "a focused: heavy corner joins b's light edge");
                                Check.Equal("aaa", Snapshot.ToText(frame).Split('\n')[1].Substring(1, 3), "a's content drawn");
                                Check.Equal("bbb", Snapshot.ToText(frame).Split('\n')[1].Substring(11, 3), "b's content drawn");

                                app.FocusNext();
                                app.RenderOnce();
                                Check.Equal("┲", app.CaptureFrame()!.Get(10, 0).Grapheme, "b focused: b's heavy corner wins");

                                app.JoinRegionBorders = false;
                                app.RenderOnce();
                                Check.Equal("┏", app.CaptureFrame()!.Get(10, 0).Grapheme, "without joining, b simply overdraws the shared column");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "ModalTopmost", "Modals know when they are topmost, and the topmost dialog can mark itself",
                        _ =>
                        {
                            ModalStack stack = new ModalStack();
                            ProbeDialogModal first = new ProbeDialogModal(20, 3);
                            ProbeDialogModal second = new ProbeDialogModal(20, 3) { FocusedBorder = BorderStyle.Double };
                            Check.False(first.IsTopmost, "not shown yet");
                            stack.Push(first);
                            Check.True(first.IsTopmost, "first alone is topmost");
                            stack.Push(second);
                            Check.False(first.IsTopmost, "first is beneath");
                            Check.True(second.IsTopmost, "second is on top");

                            CellBuffer buffer = new CellBuffer(60, 20);
                            stack.Render(new BufferSurface(buffer));
                            Check.Equal("╔", buffer.Get(second.FrameBounds.X, second.FrameBounds.Y).Grapheme, "topmost dialog draws its focused border");
                            Check.False(second.ContentBounds.IsEmpty, "content bounds are public and set");
                            Check.True(second.FrameBounds.Contains(new Point(second.ContentBounds.X, second.ContentBounds.Y)), "content lies inside the frame");

                            second.RequestClose(null);
                            stack.RemoveClosed();
                            Check.False(second.IsTopmost, "closed modal is not topmost");
                            Check.True(first.IsTopmost, "first is topmost again");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "DialogBorderSettings", "Dialog borders are settable and follow ASCII themes",
                        _ =>
                        {
                            ProbeDialogModal plain = new ProbeDialogModal(10, 2) { Border = BorderStyle.Thick };
                            CellBuffer buffer = new CellBuffer(40, 12);
                            plain.Render(new BufferSurface(buffer));
                            Check.Equal("┏", buffer.Get(plain.FrameBounds.X, plain.FrameBounds.Y).Grapheme, "thick border drawn");

                            NotificationHistoryModal history = new NotificationHistoryModal(new NotificationCenter());
                            Check.Equal(BorderStyle.Rounded, history.Border, "rounded by default");
                            history.ApplyTheme(Theme.HighContrast);
                            Check.Equal(BorderStyle.Ascii, history.Border, "ASCII theme gives an ASCII border");
                            history.ApplyTheme(Theme.Dark);
                            Check.Equal(BorderStyle.Rounded, history.Border, "back to rounded");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "PanesPlainBehindModal", "While a modal is open, the focused region draws plain",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 12);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.HighlightFocusedRegion = true;
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0).WithBorder(BorderStyle.Line)).Build();
                                app.Bind("main", new ListView<string>());
                                app.Start();
                                app.RenderOnce();
                                Check.Equal("┏", app.CaptureFrame()!.Get(0, 0).Grapheme, "focused before the modal");

                                app.ShowAsync(new MessageModal("Hi", "there", new[] { "OK" }));
                                app.RenderOnce();
                                Check.Equal("┌", app.CaptureFrame()!.Get(0, 0).Grapheme, "plain behind the modal");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "RegionBoundsForOverlays", "Region geometry is available to overlays after a frame",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().Add("side", r => r.LeftAnchored(0, 10).FillHeight().WithPadding(1).WithBorder(BorderStyle.Line)).Build();
                                Check.True(app.GetRegionBounds("side") == null, "no geometry before a frame");
                                Rect? seen = null;
                                app.RenderOverlay = surface =>
                                {
                                    seen = app.GetRegionBounds("side");
                                    if (seen.HasValue)
                                        surface.DrawText(seen.Value.X + 1, seen.Value.Y + 1, "ov", CellStyle.Default);
                                };
                                app.Start();
                                app.RenderOnce();

                                Check.Equal(new Rect(0, 0, 10, 8), app.GetRegionBounds("side")!.Value, "outer bounds");
                                Check.Equal(new Rect(2, 2, 6, 4), app.GetRegionContentBounds("side")!.Value, "content bounds inside border and padding");
                                Check.True(seen.HasValue, "the overlay saw the bounds during the frame");
                                Check.Equal("o", app.CaptureFrame()!.Get(1, 1).Grapheme, "overlay drew over the finished region");
                                Check.True(app.GetRegionBounds("missing") == null, "unknown region");
                                Check.Throws<ArgumentException>(() => app.GetRegionBounds(string.Empty), "empty id");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "HiddenStopsSkipped", "Empty button rows, hidden children, and regions off the layout are not focus stops",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(60, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                ButtonRow row = new ButtonRow();
                                Layout full = Layout.Create()
                                    .Add("a", r => r.ProportionalWidth(0, 0.3).FillHeight().WithPadding(0))
                                    .Add("b", r => r.ProportionalWidth(0.3, 0.4).FillHeight().WithPadding(0))
                                    .Add("c", r => r.ProportionalWidth(0.7, 0.3).FillHeight().WithPadding(0))
                                    .Build();
                                app.Layout = full;
                                app.Bind("a", new ListView<string>());
                                app.Bind("b", row);
                                app.Bind("c", new ListView<string>());
                                app.Start();
                                app.RenderOnce();

                                Check.False(row.IsVisible, "an empty row is hidden");
                                app.FocusNext();
                                Check.Equal("c", app.FocusedRegion, "Tab skipped the empty button row");

                                row.Add(new Button("Go"));
                                app.FocusNext();
                                app.FocusNext();
                                Check.Equal("b", app.FocusedRegion, "a row with a button is a stop");

                                app.Layout = Layout.Create()
                                    .Add("a", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0))
                                    .Add("b", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0))
                                    .Build();
                                app.RenderOnce();
                                app.FocusNext();
                                Check.Equal("a", app.FocusedRegion, "region c is gone from the layout, so Tab skips it");
                                app.Stop();
                            }

                            FocusScope scope = new FocusScope { Wrap = true };
                            ListView<string> visible = scope.Add(new ListView<string>());
                            ButtonRow hidden = scope.Add(new ButtonRow());
                            ListView<string> last = scope.Add(new ListView<string>());
                            Check.True(scope.MoveFocus(true), "moved");
                            Check.True(ReferenceEquals(last, scope.Focused), "the scope skipped the hidden child");
                            Check.False(FocusScope.IsFocusable(hidden), "hidden widgets are not focusable");
                            Check.True(FocusScope.IsFocusable(visible), "visible widgets are");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFollowUp", "AuditInvisibleStop", "The focus audit reports focus resting on a hidden widget",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.HighlightFocusedRegion = true;
                                app.Layout = Layout.Create()
                                    .Add("row", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                                    .Add("list", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                                    .Build();
                                app.Bind("row", new ButtonRow());
                                app.Bind("list", new ListView<string>());
                                app.Start();

                                FocusAuditResult result = FocusAudit.Run(app);
                                bool found = false;
                                foreach (FocusAuditProblem problem in result.Problems)
                                    found |= problem.Kind == FocusAuditProblemKind.InvisibleStop && problem.Stop == 0;
                                Check.True(found, "the starting stop on an empty row is reported");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
