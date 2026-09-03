namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite covering host-side hover routing: Enter/Leave synthesis from hit-test
    /// transitions, move coalescing, widget-local coordinates, tracking-mode escape rewriting,
    /// terminal focus handling, and link hover.
    /// </summary>
    public static class MouseHoverRoutingSuite
    {
        private const string Esc = "\u001b";

        /// <summary>
        /// Builds the hover routing suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseHoverRouting",
                displayName: "Mouse Hover Routing and Synthesis",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseHoverRouting", "EnterOnce", "Entering a widget fires Enter once, then Moves",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                BindTwoRegions(app, left, new RecordingMouseWidget());
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;3;2M");
                                app.PumpInputOnce();
                                backend.FeedInput(Esc + "[<35;5;2M");
                                app.PumpInputOnce();

                                Check.Equal(3, left.Events.Count, "Enter, Move, Move");
                                Check.Equal((int)MouseEventKind.Enter, (int)left.Events[0].Kind, "Enter first");
                                Check.Equal((int)MouseEventKind.Move, (int)left.Events[1].Kind, "Move second");
                                Check.Equal((int)MouseEventKind.Move, (int)left.Events[2].Kind, "No second Enter within the region");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "LeaveThenEnter", "Crossing regions delivers Leave to the old widget and Enter to the new",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                RecordingMouseWidget right = new RecordingMouseWidget();
                                BindTwoRegions(app, left, right);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;3;2M");
                                app.PumpInputOnce();
                                backend.FeedInput(Esc + "[<35;25;2M");
                                app.PumpInputOnce();

                                Check.Equal((int)MouseEventKind.Leave, (int)left.Events[left.Events.Count - 1].Kind, "Old widget got Leave");
                                Check.Equal((int)MouseEventKind.Enter, (int)right.Events[0].Kind, "New widget got Enter first");
                                Check.Equal((int)MouseEventKind.Move, (int)right.Events[1].Kind, "Then the triggering Move");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "LocalCoordinates", "Enter/Move/Leave coordinates are widget-local",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                RecordingMouseWidget right = new RecordingMouseWidget();
                                BindTwoRegions(app, left, right);
                                app.Start();
                                app.RenderOnce();

                                // The right region spans x 20-39 with a default border, so its content
                                // rect starts at (21, 1): absolute cell (25, 3) is local (4, 2).
                                backend.FeedInput(Esc + "[<35;26;4M");
                                app.PumpInputOnce();

                                Check.Equal(4, right.Events[0].X, "Enter X is local");
                                Check.Equal(2, right.Events[0].Y, "Enter Y is local");
                                Check.Equal(4, right.Events[1].X, "Move X is local");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "LeaveToUnboundArea", "Moving to unbound screen area delivers Leave and falls through raw",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                // Only the left half is bound; the right half is unbound screen area.
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                app.Layout = Layout.Create()
                                    .Add("left", r => r.LeftAnchored(0, 20).FillHeight())
                                    .Build();
                                app.Bind("left", left);

                                List<MouseEvent> raw = new List<MouseEvent>();
                                app.MouseReceived += raw.Add;

                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;3;2M");
                                app.PumpInputOnce();
                                backend.FeedInput(Esc + "[<35;30;2M");
                                app.PumpInputOnce();

                                Check.Equal((int)MouseEventKind.Leave, (int)left.Events[left.Events.Count - 1].Kind, "Leave delivered");

                                // Both moves reach MouseReceived: the widget observed the first but did
                                // not consume it, and the second hit no widget at all.
                                Check.Equal(2, raw.Count, "Unconsumed and unbound moves fell through");
                                Check.Equal(29, raw[1].X, "Raw event keeps absolute coordinates");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "MoveCoalescing", "A drained run of moves collapses to the last position",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                BindTwoRegions(app, left, new RecordingMouseWidget());
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;2;2M" + Esc + "[<35;3;2M" + Esc + "[<35;4;2M");
                                app.PumpInputOnce();

                                Check.Equal(2, left.Events.Count, "Enter plus exactly one Move");

                                // The bordered left region's content rect starts at (1, 1), so the last
                                // move (absolute x 3) arrives as local x 2.
                                Check.Equal(2, left.Events[1].X, "The surviving Move is the last position");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "PressIsCoalescingBarrier", "A press between moves prevents coalescing across it",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                BindTwoRegions(app, left, new RecordingMouseWidget());
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;2;2M" + Esc + "[<0;3;2M" + Esc + "[<35;4;2M");
                                app.PumpInputOnce();

                                Check.Equal(4, left.Events.Count, "Enter, Move, Press, Move");
                                Check.Equal((int)MouseEventKind.Press, (int)left.Events[2].Kind, "Press preserved in order");
                                Check.Equal((int)MouseEventKind.Move, (int)left.Events[3].Kind, "Move after the barrier survives");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "ConsumedEventsStopRawFallback", "A consumed event never reaches MouseReceived",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                left.ConsumeEvents = true;
                                BindTwoRegions(app, left, new RecordingMouseWidget());

                                List<MouseEvent> raw = new List<MouseEvent>();
                                app.MouseReceived += raw.Add;

                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<0;3;2M");
                                app.PumpInputOnce();

                                Check.Equal(0, raw.Count, "Consumed press suppressed the raw event");
                                Check.True(left.Events.Count >= 2, "Widget received Enter and Press");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "RoutingDisabled", "EnableMouseRouting=false yields raw events only, no synthesis",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                BindTwoRegions(app, left, new RecordingMouseWidget());
                                app.EnableMouseRouting = false;

                                List<MouseEvent> raw = new List<MouseEvent>();
                                app.MouseReceived += raw.Add;

                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;3;2M");
                                app.PumpInputOnce();

                                Check.Equal(0, left.Events.Count, "No routed events");
                                Check.Equal(1, raw.Count, "Raw event raised");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "FocusLossClearsHover", "CSI O delivers Leave and raises TerminalFocusChanged",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget left = new RecordingMouseWidget();
                                BindTwoRegions(app, left, new RecordingMouseWidget());

                                List<bool> focusChanges = new List<bool>();
                                app.TerminalFocusChanged += focusChanges.Add;

                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;3;2M");
                                app.PumpInputOnce();
                                backend.FeedInput(Esc + "[O" + Esc + "[I");
                                app.PumpInputOnce();

                                Check.Equal((int)MouseEventKind.Leave, (int)left.Events[left.Events.Count - 1].Kind, "Leave on focus loss");
                                Check.Equal(2, focusChanges.Count, "Both focus transitions raised");
                                Check.False(focusChanges[0], "Lost first");
                                Check.True(focusChanges[1], "Gained second");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "TrackingModeSequences", "Tracking mode and capture changes rewrite terminal modes",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Start();
                                string startup = backend.TakeOutput();
                                Check.True(startup.Contains("?1003h"), "Any-motion enabled at start by default");
                                Check.True(startup.Contains("?1004h"), "Focus reporting enabled at start");

                                app.MouseTrackingMode = MouseTrackingMode.ButtonsAndDrag;
                                string dragOnly = backend.TakeOutput();
                                Check.True(dragOnly.Contains("?1000h"), "Buttons re-enabled");
                                Check.True(dragOnly.EndsWith(Esc + "[?1003l"), "Ends with any-motion off");

                                app.MouseTrackingMode = MouseTrackingMode.None;
                                string none = backend.TakeOutput();
                                Check.True(none.Contains("?1000l"), "All tracking off");
                                Check.False(none.Contains("?1000h"), "No re-enable in None mode");

                                app.MouseTrackingMode = MouseTrackingMode.AnyMotion;
                                backend.TakeOutput();

                                app.MouseCaptureEnabled = false;
                                Check.True(backend.TakeOutput().Contains("?1000l"), "Capture off disables tracking");
                                app.MouseCaptureEnabled = true;
                                Check.True(backend.TakeOutput().Contains("?1003h"), "Capture on restores the tracking mode");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "NonInteractiveEmitsNothing", "A non-interactive backend receives no mouse escapes",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10, null, false);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Start();
                                string startup = backend.TakeOutput();
                                Check.False(startup.Contains("?1000h"), "No mouse enable");
                                Check.False(startup.Contains("?1004h"), "No focus enable");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseHoverRouting", "LinkHover", "Pointer motion over a registered link raises LinkHovered",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                LinkRegistry links = new LinkRegistry();
                                Link link = links.Add("docs", new Rect(5, 0, 10, 1), "https://example.com");
                                app.Links = links;

                                List<Link?> hovered = new List<Link?>();
                                app.LinkHovered += hovered.Add;

                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<35;7;1M");
                                app.PumpInputOnce();
                                Check.Equal(1, hovered.Count, "Hover raised once");
                                Check.Equal(link.Id, hovered[0]!.Id, "The registered link");
                                Check.Equal(link.Id, app.HoveredLink!.Id, "HoveredLink property tracks it");

                                backend.FeedInput(Esc + "[<35;7;1M");
                                app.PumpInputOnce();
                                Check.Equal(1, hovered.Count, "No duplicate while over the same link");

                                backend.FeedInput(Esc + "[<35;2;6M");
                                app.PumpInputOnce();
                                Check.Equal(2, hovered.Count, "Leaving the link raises again");
                                Check.True(hovered[1] == null, "With null");
                                Check.True(app.HoveredLink == null, "Property cleared");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static void BindTwoRegions(TuiApplication app, RecordingMouseWidget left, RecordingMouseWidget right)
        {
            app.Layout = Layout.Create()
                .Add("left", r => r.LeftAnchored(0, 20).FillHeight())
                .Add("right", r => r.RightAnchored(0, 20).FillHeight())
                .Build();
            app.Bind("left", left);
            app.Bind("right", right);
        }
    }
}
