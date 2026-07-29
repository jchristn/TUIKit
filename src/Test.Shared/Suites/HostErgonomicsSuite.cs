namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the host ergonomics: binding any widget to a region (10.1), the
    /// one-step chord binding and app verbs (10.9), and the AddPane/AddWidget helpers (10.10).
    /// </summary>
    public static class HostErgonomicsSuite
    {
        /// <summary>
        /// Builds the host ergonomics suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "HostErgo",
                displayName: "Host Ergonomics",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("HostErgo", "BindWidget", "Any widget can be bound to a region",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().Add("g", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                Gauge gauge = new Gauge { Value = 1.0 };
                                app.Bind("g", gauge);
                                app.Start();
                                app.RenderOnce();
                                Check.True(backend.PeekOutput().Contains("█"), "Gauge rendered via Bind");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostErgo", "AddPaneAndWidget", "AddPane and AddWidget create and bind",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Pane log = app.AddPane("log", r => r.FillWidth().FillHeight(0, 1).WithPadding(0));
                                log.WriteLine("HELLO");
                                app.AddWidget("bar", new Gauge { Value = 1.0 }, r => r.FillWidth().BottomAnchored(0, 1).WithPadding(0));
                                Check.Equal(2, app.Layout!.Regions.Count, "Two regions added");
                                app.Start();
                                app.RenderOnce();
                                string output = backend.PeekOutput();
                                Check.True(output.Contains("HELLO"), "Pane content rendered");
                                Check.True(output.Contains("█"), "Widget rendered");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostErgo", "BindChord", "One-step chord binding fires an action",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                bool fired = false;
                                app.Bind("ctrl+q", () => fired = true);
                                app.Start();
                                backend.FeedInput(new byte[] { 0x11 }); // Ctrl+Q
                                app.PumpInputOnce();
                                Check.True(fired, "Bound action fired");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostErgo", "BindOverwrites", "Re-binding a chord replaces the action",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                int which = 0;
                                app.Bind("ctrl+q", () => which = 1);
                                app.Bind("ctrl+q", () => which = 2);   // overwrites, no throw
                                app.Start();
                                backend.FeedInput(new byte[] { 0x11 });
                                app.PumpInputOnce();
                                Check.Equal(2, which, "Latest binding wins");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostErgo", "Notify", "Notify raises a toast",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Notify("saved", NotificationSeverity.Success, 5000);
                                IReadOnlyList<Notification> active = app.Notifications.Active(0);
                                Check.Equal(1, active.Count, "One toast");
                                Check.Equal("saved", active[0].Text, "Toast text");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostErgo", "BindArgumentGuards", "Bind rejects null arguments",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Check.Throws<ArgumentException>(() => app.Bind("", () => { }), "Empty chord");
                                Check.Throws<ArgumentNullException>(() => app.Bind("ctrl+x", (Action)null!), "Null action");
                                Check.Throws<ArgumentNullException>(() => app.Bind("region", (IWidget)null!), "Null widget");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostErgo", "MouseCaptureToggle", "Mouse capture can be toggled to hand the terminal native selection",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Check.True(app.MouseCaptureEnabled, "capture on by default");
                                app.Start();
                                Check.True(backend.TakeOutput().Contains(Ansi.EnableMouse), "start enables mouse when captured");

                                bool state = app.ToggleMouseCapture();
                                Check.False(state, "toggle returns the new (off) state");
                                Check.False(app.MouseCaptureEnabled, "capture now off");
                                Check.True(backend.TakeOutput().Contains(Ansi.DisableMouse), "disabling emits the disable escape");

                                Check.True(app.ToggleMouseCapture(), "toggle returns the new (on) state");
                                Check.True(backend.TakeOutput().Contains(Ansi.EnableMouse), "re-enabling emits the enable escape");

                                app.MouseCaptureEnabled = true; // no-op
                                Check.False(backend.TakeOutput().Contains(Ansi.EnableMouse), "setting the same value writes nothing");

                                app.Stop();
                            }

                            HeadlessBackend startOff = new HeadlessBackend(20, 4);
                            using (TuiApplication app = new TuiApplication(startOff))
                            {
                                app.MouseCaptureEnabled = false; // before start: field only, no write
                                app.Start();
                                Check.False(startOff.TakeOutput().Contains(Ansi.EnableMouse), "starting with capture off does not enable the mouse");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
