namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Theming;

    /// <summary>
    /// Touchstone suite covering the application host: frame composition of panes bound to regions,
    /// command dispatch from decoded input, the Ctrl+C policy, non-TTY line degradation, the singleton
    /// guard, and theme roles.
    /// </summary>
    public static class HostingSuite
    {
        private const string Esc = "\u001b";

        /// <summary>
        /// Builds the hosting suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Hosting",
                displayName: "Application Host and Theme",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Hosting", "ComposePanes", "Panes render into their bound regions",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create()
                                    .Add("left", r => r.LeftAnchored(0, 20).FillHeight())
                                    .Add("right", r => r.RightAnchored(0, 20).FillHeight())
                                    .Build();

                                Pane left = new Pane("left");
                                left.WriteLine("LEFTPANE");
                                Pane right = new Pane("right");
                                right.WriteLine("RIGHTPANE");
                                app.BindPane("left", left);
                                app.BindPane("right", right);

                                app.Start();
                                app.RenderOnce();
                                string output = backend.PeekOutput();
                                Check.True(output.Contains("LEFTPANE"), "Left pane content emitted");
                                Check.True(output.Contains("RIGHTPANE"), "Right pane content emitted");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "CommandDispatch", "A decoded chord invokes a command",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                bool fired = false;
                                app.Commands.Register(KeyChord.Parse("ctrl+q"), "quit");
                                app.RegisterCommand("quit", () => fired = true);
                                app.Start();

                                backend.FeedInput(new byte[] { 0x11 }); // Ctrl+Q
                                app.PumpInputOnce();
                                Check.True(fired, "Command handler invoked");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "CtrlCKill", "Ctrl+C stops under the Kill policy",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.CtrlCPolicy = CtrlCPolicy.Kill;
                                app.Start();
                                backend.FeedInput(new byte[] { 0x03 }); // Ctrl+C
                                app.PumpInputOnce();
                                Check.False(app.IsRunning, "Not running after request");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "KeyFallthrough", "Unbound keys fall through to text input",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                StringBuilder typed = new StringBuilder();
                                app.KeyReceived += key =>
                                {
                                    if (key.Code == KeyCode.Character)
                                        typed.Append(char.ConvertFromUtf32(key.Rune));
                                };
                                app.Start();
                                backend.FeedInput("hi");
                                app.PumpInputOnce();
                                Check.Equal("hi", typed.ToString(), "Characters delivered to text handler");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "NonTtyLineMode", "Non-interactive backend degrades to line output",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(80, 24, TerminalCapabilities.Full, false);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().Add("log", r => r.FillWidth().FillHeight()).Build();
                                Pane pane = new Pane("log");
                                app.BindPane("log", pane);
                                app.Start();

                                pane.WriteLine("first line");
                                pane.WriteLine("second line");
                                app.RenderOnce();

                                string output = backend.PeekOutput();
                                Check.True(output.Contains("first line\n"), "Plain line output, no escapes");
                                Check.False(output.Contains(Esc), "No escape sequences in line mode");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "AutoNotificationsToggle", "Host can opt out of rendering toasts",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(60, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().Add("log", r => r.FillWidth().FillHeight()).Build();
                                app.BindPane("log", new Pane("log"));
                                app.AutoRenderNotifications = false;
                                app.Notifications.Add("UNIQUE_TOAST", NotificationSeverity.Info, 0, 0);
                                app.Start();
                                app.RenderOnce();
                                Check.False(backend.PeekOutput().Contains("UNIQUE_TOAST"), "Host suppressed the toast");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "SingletonGuard", "Only one application runs at a time",
                        _ =>
                        {
                            HeadlessBackend backendA = new HeadlessBackend();
                            HeadlessBackend backendB = new HeadlessBackend();
                            using (TuiApplication first = new TuiApplication(backendA))
                            using (TuiApplication second = new TuiApplication(backendB))
                            {
                                first.Start();
                                Check.Throws<InvalidOperationException>(() => second.Start(), "Second start throws");
                                first.Stop();
                                second.Start(); // now allowed
                                second.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "ThemeRoles", "Themes expose distinct role styles",
                        _ =>
                        {
                            Check.False(Theme.Dark.Text.Foreground == Theme.Light.Text.Foreground, "Dark and light differ");
                            Check.True(Theme.HighContrast.UseAsciiBorders, "High contrast uses ASCII borders");
                            Theme custom = Theme.Dark;
                            custom.SetStyle("banner", CellStyle.Default.WithForeground(Color.FromPalette(5)));
                            Check.Equal(Color.FromPalette(5), custom.GetStyle("banner").Foreground, "Named style stored");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Hosting", "ThemeBackgroundEmitted", "Switching to the light theme reverses the background",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().Add("log", r => r.FillWidth().FillHeight()).Build();
                                Pane pane = new Pane("log");
                                pane.WriteLine("hi");
                                app.BindPane("log", pane);
                                app.Theme = Theme.Light;
                                app.Start();
                                app.RenderOnce();
                                string output = backend.PeekOutput();
                                Check.True(output.Contains("48;2;232;232;232"), "Light background color is emitted");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
