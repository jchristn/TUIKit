namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite covering multi-click synthesis: the host wiring that stamps click counts on
    /// routed presses, and the <see cref="ClickSynthesizer"/> slop and reset rules.
    /// </summary>
    public static class MouseClickSynthesisSuite
    {
        private const string Esc = "\u001b";

        /// <summary>
        /// Builds the click synthesis suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseClickSynthesis",
                displayName: "Mouse Click Synthesis",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseClickSynthesis", "HostStampsClickCounts", "Rapid presses arrive at the widget with rising click counts",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget widget = new RecordingMouseWidget();
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", widget);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<0;3;2M" + Esc + "[<0;3;2m" + Esc + "[<0;3;2M" + Esc + "[<0;3;2m");
                                app.PumpInputOnce();

                                List<MouseEvent> presses = new List<MouseEvent>();
                                foreach (MouseEvent recorded in widget.Events)
                                {
                                    if (recorded.Kind == MouseEventKind.Press)
                                        presses.Add(recorded);
                                }

                                Check.Equal(2, presses.Count, "Two presses routed");
                                Check.Equal(1, presses[0].ClickCount, "First is a single click");
                                Check.Equal(2, presses[1].ClickCount, "Second is a double click");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseClickSynthesis", "WheelDoesNotBreakChain", "A wheel event between presses does not reset the count",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget widget = new RecordingMouseWidget();
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", widget);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<0;3;2M" + Esc + "[<64;3;2M" + Esc + "[<0;3;2M");
                                app.PumpInputOnce();

                                List<MouseEvent> presses = new List<MouseEvent>();
                                foreach (MouseEvent recorded in widget.Events)
                                {
                                    if (recorded.Kind == MouseEventKind.Press)
                                        presses.Add(recorded);
                                }

                                Check.Equal(2, presses[1].ClickCount, "Chain survived the wheel event");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseClickSynthesis", "DifferentCellResets", "A press on a different cell restarts the chain",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget widget = new RecordingMouseWidget();
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", widget);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<0;3;2M" + Esc + "[<0;20;8M");
                                app.PumpInputOnce();

                                List<MouseEvent> presses = new List<MouseEvent>();
                                foreach (MouseEvent recorded in widget.Events)
                                {
                                    if (recorded.Kind == MouseEventKind.Press)
                                        presses.Add(recorded);
                                }

                                Check.Equal(1, presses[1].ClickCount, "Distant press starts a new single click");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseClickSynthesis", "PositionSlop", "Slop widens the same-spot test per axis",
                        _ =>
                        {
                            ClickSynthesizer synth = new ClickSynthesizer();
                            synth.PositionSlopCells = 1;
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 5, 5, 0), "Single");
                            Check.Equal(2, synth.RegisterPress(MouseButton.Left, 6, 4, 50), "One-cell drift continues the chain");
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 8, 4, 100), "Two-cell drift resets");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseClickSynthesis", "SlopValidation", "Negative slop is rejected; zero slop demands the exact cell",
                        _ =>
                        {
                            ClickSynthesizer synth = new ClickSynthesizer();
                            Check.Throws<ArgumentOutOfRangeException>(() => synth.PositionSlopCells = -1, "Negative slop throws");

                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 5, 5, 0), "Single");
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 6, 5, 50), "Adjacent cell resets at slop 0");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseClickSynthesis", "ButtonChangeResets", "Switching buttons restarts the chain",
                        _ =>
                        {
                            ClickSynthesizer synth = new ClickSynthesizer();
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 5, 5, 0), "Left single");
                            Check.Equal(1, synth.RegisterPress(MouseButton.Right, 5, 5, 50), "Right press starts over");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
