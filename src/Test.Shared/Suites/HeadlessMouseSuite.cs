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
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for driving the mouse in tests: <see cref="MouseSequenceEncoder"/>, the
    /// <see cref="HeadlessBackend"/> mouse helpers that route through the real parser and hit map, the
    /// <see cref="WidgetTester"/> mouse methods, and <see cref="TuiApplication.CaptureFrame"/>.
    /// </summary>
    public static class HeadlessMouseSuite
    {
        /// <summary>
        /// Builds the headless mouse suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "HeadlessMouse",
                displayName: "Headless Mouse and Frame Capture",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("HeadlessMouse", "EncoderRoundTrips", "Encoded presses, releases, moves, and wheels parse back to the same events",
                        _ =>
                        {
                            List<MouseEvent> events = new List<MouseEvent>
                            {
                                new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 0, KeyModifiers.None, 1),
                                new MouseEvent(MouseEventKind.Release, MouseButton.Right, 12, 7, KeyModifiers.None, 0),
                                new MouseEvent(MouseEventKind.Press, MouseButton.Middle, 3, 4, KeyModifiers.Ctrl | KeyModifiers.Shift, 1),
                                new MouseEvent(MouseEventKind.Move, MouseButton.None, 9, 2, KeyModifiers.None, 0),
                                new MouseEvent(MouseEventKind.Move, MouseButton.Left, 9, 3, KeyModifiers.Alt, 0),
                                new MouseEvent(MouseEventKind.Wheel, MouseButton.WheelUp, 5, 5, KeyModifiers.None, 0),
                                new MouseEvent(MouseEventKind.Wheel, MouseButton.WheelDown, 5, 6, KeyModifiers.None, 0)
                            };

                            foreach (MouseEvent original in events)
                            {
                                InputParser parser = new InputParser();
                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(MouseSequenceEncoder.Encode(original));
                                parser.Feed(bytes, bytes.Length);
                                IReadOnlyList<InputEvent> parsed = parser.Drain();
                                Check.Equal(1, parsed.Count, "one event for " + original.Kind);
                                MouseEvent decoded = parsed[0].Mouse!;
                                Check.Equal(original.Kind, decoded.Kind, "kind");
                                Check.Equal(original.Button, decoded.Button, "button");
                                Check.Equal(original.X, decoded.X, "x");
                                Check.Equal(original.Y, decoded.Y, "y");
                                Check.Equal(original.Modifiers, decoded.Modifiers, "modifiers");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "EncoderRejectsBadInput", "Null, negative, Enter/Leave, and wrong-button events are rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => MouseSequenceEncoder.Encode(null!), "null event");
                            Check.Throws<ArgumentOutOfRangeException>(() => MouseSequenceEncoder.Encode(new MouseEvent(MouseEventKind.Press, MouseButton.Left, -1, 0, KeyModifiers.None, 1)), "negative x");
                            Check.Throws<ArgumentOutOfRangeException>(() => MouseSequenceEncoder.Encode(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, -1, KeyModifiers.None, 1)), "negative y");
                            Check.Throws<ArgumentException>(() => MouseSequenceEncoder.Encode(new MouseEvent(MouseEventKind.Enter, MouseButton.None, 0, 0, KeyModifiers.None, 0)), "enter has no wire form");
                            Check.Throws<ArgumentException>(() => MouseSequenceEncoder.Encode(new MouseEvent(MouseEventKind.Leave, MouseButton.None, 0, 0, KeyModifiers.None, 0)), "leave has no wire form");
                            Check.Throws<ArgumentException>(() => MouseSequenceEncoder.Encode(new MouseEvent(MouseEventKind.Press, MouseButton.None, 0, 0, KeyModifiers.None, 1)), "press without a button");
                            Check.Throws<ArgumentException>(() => MouseSequenceEncoder.Encode(new MouseEvent(MouseEventKind.Wheel, MouseButton.Left, 0, 0, KeyModifiers.None, 0)), "wheel without a wheel button");
                            Check.Throws<ArgumentException>(() => MouseSequenceEncoder.EncodeClick(0, 0, MouseButton.WheelUp), "click with a wheel button");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "ClickRoutesThroughHitMapAndFocuses", "FeedClick activates a button through the hit map and focuses its region",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                int clicks = 0;
                                Button left = new Button("Left");
                                Button right = new Button("Right", () => clicks++);
                                app.Layout = Layout.Create()
                                    .Add("left", r => r.LeftAnchored(0, 20).FillHeight().WithPadding(0))
                                    .Add("right", r => r.LeftAnchored(20, 20).FillHeight().WithPadding(0))
                                    .Build();
                                app.Bind("left", left);
                                app.Bind("right", right);
                                app.Start();
                                app.RenderOnce();
                                Check.Equal("left", app.FocusedRegion, "first bound region starts focused");

                                backend.FeedClick(21, 0);
                                app.PumpInputOnce();

                                Check.Equal(1, clicks, "button activated by a routed click");
                                Check.Equal("right", app.FocusedRegion, "click moved focus to the clicked region");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "WheelScrollsScrollView", "FeedWheel over a ScrollView scrolls it",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                ScrollView view = new ScrollView(new Label(Text.From("content")), 30, 50);
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", view);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedWheel(2, 2, 2);
                                app.PumpInputOnce();
                                Check.True(view.ScrollY > 0, "wheel down scrolled the view");

                                int down = view.ScrollY;
                                backend.FeedWheel(2, 2, -1);
                                app.PumpInputOnce();
                                Check.True(view.ScrollY < down, "wheel up scrolled back");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "DoubleClickActivatesListRow", "FeedDoubleClick on a list row raises ItemActivated",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                ListView<string> list = new ListView<string>();
                                list.SetItems(new[] { "alpha", "beta", "gamma" });
                                int activated = -1;
                                list.ItemActivated += index => activated = index;
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", list);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedDoubleClick(1, 2);
                                app.PumpInputOnce();
                                Check.Equal(2, activated, "third row activated by a double click");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "DragResizesSplit", "FeedDrag on a SplitView divider changes the split ratio",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(41, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                SplitView split = new SplitView(SplitOrientation.Horizontal, new Label(Text.From("a")), new Label(Text.From("b")));
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", split);
                                app.Start();
                                app.RenderOnce();
                                double before = split.Ratio;

                                backend.FeedDrag(20, 2, 30, 2);
                                app.PumpInputOnce();
                                Check.True(split.Ratio > before, "dragging the divider right grew the first pane");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "ClickOutsideRegionsIgnored", "A click on no region reaches no widget and does not throw",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                int clicks = 0;
                                app.Layout = Layout.Create().Add("small", r => r.LeftAnchored(0, 10).TopAnchored(0, 3).WithPadding(0)).Build();
                                app.Bind("small", new Button("Go", () => clicks++));
                                app.Start();
                                app.RenderOnce();

                                backend.FeedClick(30, 8);
                                app.PumpInputOnce();
                                Check.Equal(0, clicks, "no widget under the click");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "RoutingDisabledReachesNoWidget", "With EnableMouseRouting off, a fed click activates nothing",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                int clicks = 0;
                                MouseEvent? unrouted = null;
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", new Button("Go", () => clicks++));
                                app.EnableMouseRouting = false;
                                app.MouseReceived += mouse => unrouted = mouse;
                                app.Start();
                                app.RenderOnce();

                                backend.FeedClick(1, 0);
                                app.PumpInputOnce();
                                Check.Equal(0, clicks, "button not activated");
                                Check.True(unrouted != null, "the click surfaced as an unrouted event instead");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "BackendRejectsOffScreen", "Backend helpers reject off-screen positions and a zero wheel delta",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 5);
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.FeedClick(10, 0), "column past the width");
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.FeedClick(0, 5), "row past the height");
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.FeedClick(-1, 0), "negative column");
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.FeedMove(0, -1), "negative row");
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.FeedDrag(0, 0, 11, 0), "drag end off screen");
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.FeedWheel(1, 1, 0), "zero wheel delta");
                            Check.Throws<ArgumentNullException>(() => backend.FeedMouse(null!), "null event");
                            Check.Throws<ArgumentException>(() => backend.FeedClick(1, 1, MouseButton.None), "click needs a button");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "WidgetTesterMouse", "WidgetTester delivers local clicks, double clicks, and wheels",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.SetItems(new[] { "one", "two", "three", "four" });
                            int activated = -1;
                            list.ItemActivated += index => activated = index;
                            WidgetTester tester = WidgetTester.For(list, 20, 4).Render();

                            tester.Click(0, 1);
                            Check.True(tester.LastMouseHandled, "click consumed");
                            Check.Equal(1, list.SelectedIndex, "click selected row two");

                            tester.DoubleClick(0, 2);
                            Check.Equal(2, activated, "double click activated row three");

                            tester.Wheel(0, 0, 1);
                            Check.Equal(3, list.SelectedIndex, "wheel down moved the selection");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "WidgetTesterMouseGuards", "WidgetTester mouse methods reject bad input and non-mouse widgets",
                        _ =>
                        {
                            WidgetTester list = WidgetTester.For(new ListView<string>(), 10, 3);
                            Check.Throws<ArgumentOutOfRangeException>(() => list.Click(10, 0), "column past the width");
                            Check.Throws<ArgumentOutOfRangeException>(() => list.Click(0, 3), "row past the height");
                            Check.Throws<ArgumentOutOfRangeException>(() => list.Wheel(0, 0, 0), "zero wheel delta");
                            Check.Throws<ArgumentNullException>(() => list.Mouse(null!), "null event");
                            Check.Throws<ArgumentOutOfRangeException>(() => list.CellAt(-1, 0), "cell off the buffer");

                            WidgetTester label = WidgetTester.For(new Label(Text.From("x")), 5, 1);
                            Check.Throws<InvalidOperationException>(() => label.Click(0, 0), "label does not handle the mouse");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessMouse", "CaptureFrame", "CaptureFrame returns the composed cells after a render and null before start",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Check.True(app.CaptureFrame() == null, "no frame before start");
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", new Label(Text.From("hello")));
                                app.Start();
                                app.RenderOnce();

                                CellBuffer? frame = app.CaptureFrame();
                                Check.True(frame != null, "frame captured");
                                Check.Equal("hello", Snapshot.ToText(frame!).Split('\n')[0], "first row text");
                                app.Stop();
                            }

                            HeadlessBackend lineMode = new HeadlessBackend(20, 3, null, false);
                            using (TuiApplication app = new TuiApplication(lineMode))
                            {
                                app.Start();
                                app.RenderOnce();
                                Check.True(app.CaptureFrame() == null, "line mode composes no frame");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
