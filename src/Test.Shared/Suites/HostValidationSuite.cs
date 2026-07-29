namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Hosting;
    using TUIKit.Terminal;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Validation coverage for the application host: <see cref="TuiApplication"/> argument and range
    /// guards, and the <see cref="TuiApp"/> one-call entry point.
    /// </summary>
    public static class HostValidationSuite
    {
        /// <summary>
        /// Builds the host-validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "HostValidation",
                displayName: "Host Validation (TuiApplication / TuiApp)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("HostValidation", "Ctor", "TuiApplication rejects a null backend",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new TuiApplication(null!), "null backend");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostValidation", "TargetFps", "TargetFps clamps to the documented 1..240 range",
                        _ =>
                        {
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 5)))
                            {
                                Check.Equal(60, app.TargetFps, "default frame rate");
                                app.TargetFps = 120;
                                Check.Equal(120, app.TargetFps, "in-range value accepted");
                                Check.Throws<ArgumentOutOfRangeException>(() => app.TargetFps = 0, "zero rejected");
                                Check.Throws<ArgumentOutOfRangeException>(() => app.TargetFps = 500, "above 240 rejected");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostValidation", "ThemeAndCommands", "Theme, region, and command binding reject bad arguments",
                        _ =>
                        {
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 5)))
                            {
                                Check.Throws<ArgumentNullException>(() => app.Theme = null!, "null theme");
                                app.Theme = Theme.Light;
                                Check.Equal("Light", app.Theme.Name, "theme applied");

                                Check.Throws<ArgumentNullException>(() => app.AddRegion("r", null!), "null region configure");
                                Check.Throws<ArgumentNullException>(() => app.AddWidget("w", (IWidget)null!), "null widget");
                                Check.Throws<ArgumentException>(() => app.RegisterCommand("", () => { }), "empty command id");
                                Check.Throws<ArgumentNullException>(() => app.RegisterCommand("cmd", null!), "null command handler");
                                Check.Throws<ArgumentException>(() => app.BindPane("", new TUIKit.Content.Pane("p")), "empty region id");
                                Check.Throws<ArgumentNullException>(() => app.BindPane("r", null!), "null pane");
                                Check.Throws<ArgumentException>(() => app.Bind("", () => { }), "empty chord");
                                Check.Throws<ArgumentNullException>(() => app.Bind("ctrl+x", (Action)null!), "null chord action");
                                Check.Throws<ArgumentNullException>(() => app.Notify(null!), "null notification text");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HostValidation", "AsyncPrompts", "Async prompt helpers reject null arguments",
                        async _ =>
                        {
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 5)))
                            {
                                await Check.ThrowsAsync<ArgumentNullException>(() => app.ShowAsync(null!), "null modal");
                                await Check.ThrowsAsync<ArgumentNullException>(() => app.ConfirmAsync(null!), "null confirm message");
                                await Check.ThrowsAsync<ArgumentNullException>(() => app.PromptAsync(null!), "null prompt title");
                                await Check.ThrowsAsync<ArgumentNullException>(() => app.SelectAsync(null!, "a", "b"), "null select title");
                                await Check.ThrowsAsync<ArgumentNullException>(() => app.SelectAsync("t", (string[])null!), "null select options");
                            }
                        }),

                    new TestCaseDescriptor("HostValidation", "TuiAppRunAsync", "TuiApp.RunAsync rejects a null configure delegate",
                        async _ =>
                        {
                            await Check.ThrowsAsync<ArgumentNullException>(() => TuiApp.RunAsync(null!, CancellationToken.None), "null configure");
                        })
                });
        }
    }
}
