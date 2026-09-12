namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite for modal mouse routing: while a modal is active the host offers the mouse to the
    /// topmost modal (via <see cref="Modal.HandleMouse"/>) before any region-bound widget, mirroring the key
    /// trap, so clicks land on the dialog and never leak to the interface behind it.
    /// </summary>
    public static class ModalMouseRoutingSuite
    {
        private const string Esc = "";

        /// <summary>
        /// Builds the modal mouse routing suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ModalMouseRouting",
                displayName: "Modal Mouse Routing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ModalMouseRouting", "ModalReceivesClick", "A click reaches the active modal with absolute coordinates",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 12);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingModal modal = new RecordingModal();
                                app.Modals.Push(modal);
                                app.Start();
                                app.RenderOnce();

                                // SGR left-button press at column 6, row 4 (1-based on the wire).
                                backend.FeedInput(Esc + "[<0;6;4M");
                                app.PumpInputOnce();

                                Check.Equal(1, modal.Events.Count, "modal received one event");
                                Check.Equal((int)MouseEventKind.Press, (int)modal.Events[0].Kind, "it was a press");
                                Check.Equal((int)MouseButton.Left, (int)modal.Events[0].Button, "left button");
                                Check.Equal(5, modal.Events[0].X, "absolute X (0-based)");
                                Check.Equal(3, modal.Events[0].Y, "absolute Y (0-based)");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ModalMouseRouting", "ModalTrapsMouseFromBackground", "A click behind a modal never reaches the background widget",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 12);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget behind = new RecordingMouseWidget { ConsumeEvents = true };
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", behind);

                                RecordingModal modal = new RecordingModal();
                                app.Modals.Push(modal);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<0;10;5M");
                                app.PumpInputOnce();

                                Check.Equal(1, modal.Events.Count, "modal got the click");
                                Check.Equal(0, behind.Events.Count, "background widget was trapped out");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ModalMouseRouting", "WidgetReceivesWhenNoModal", "With no modal active, a click routes to the region widget",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 12);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget widget = new RecordingMouseWidget { ConsumeEvents = true };
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", widget);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedInput(Esc + "[<0;10;5M");
                                app.PumpInputOnce();

                                Check.True(widget.Events.Count >= 1, "widget received the click when no modal traps it");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ModalMouseRouting", "ClosedModalDropsAfterRemoval", "A modal that closes on click is removed from the stack",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 12);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingModal modal = new RecordingModal { CloseOnClick = true };
                                app.Modals.Push(modal);
                                app.Start();
                                app.RenderOnce();

                                Check.True(app.Modals.IsActive, "modal active before click");
                                backend.FeedInput(Esc + "[<0;6;4M");
                                app.PumpInputOnce();

                                Check.False(app.Modals.IsActive, "modal removed after it closed on the click");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        /// <summary>
        /// A minimal modal that records the mouse events routed to it and can optionally close on a press.
        /// </summary>
        private sealed class RecordingModal : Modal
        {
            private readonly List<MouseEvent> _Events = new List<MouseEvent>();

            public IReadOnlyList<MouseEvent> Events
            {
                get { return _Events; }
            }

            public bool CloseOnClick { get; set; }

            public override void Render(ISurface surface)
            {
            }

            public override bool HandleKey(KeyEvent key)
            {
                return true;
            }

            public override bool HandleMouse(MouseEvent mouse)
            {
                _Events.Add(mouse);
                if (CloseOnClick && mouse.Kind == MouseEventKind.Press)
                    Close(mouse);

                return true;
            }
        }
    }
}
