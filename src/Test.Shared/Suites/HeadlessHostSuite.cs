namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;
    using N = TUIKit.Diagnostics.TuiKitTelemetryNames;

    /// <summary>
    /// Coverage for headless applications that do not claim the terminal, and for focus path freshness
    /// (1.5.0, B4): <see cref="ISharedTerminalBackend"/>, <see cref="HeadlessBackend.ClaimsTerminal"/>, the
    /// per-session claim in <see cref="TuiApplication.Start"/>, and <see cref="TuiApplication.CurrentFocusPath"/>
    /// after a frame that moved focus.
    /// </summary>
    public static class HeadlessHostSuite
    {
        /// <summary>
        /// Builds the headless host suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "HeadlessHost",
                displayName: "Headless Applications That Share the Process",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("HeadlessHost", "TwoAppsInParallel", "Two headless applications started in one process render, take clicks, and capture frames independently, in parallel",
                        async ct =>
                        {
                            Task<string> first = Task.Run(() => DriveClickApp("alpha", 3));
                            Task<string> second = Task.Run(() => DriveClickApp("beta", 1));
                            string[] results = await Task.WhenAll(first, second).ConfigureAwait(false);
                            Check.Equal("alpha:selected=3", results[0], "first app");
                            Check.Equal("beta:selected=1", results[1], "second app");
                        }),

                    new TestCaseDescriptor("HeadlessHost", "FocusPathFreshAfterFrame", "Hiding the focused widget and rendering one frame gives a repaired CurrentFocusPath, with FocusPathChanged once",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            ToggleLeaf b = scope.Add(new ToggleLeaf("b"));
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(30, 6) { ClaimsTerminal = false }))
                            {
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", new PathScopeWidget(scope));
                                app.Start();
                                app.RenderOnce();
                                int changes = 0;
                                FocusPath? last = null;
                                app.FocusPathChanged += path =>
                                {
                                    changes++;
                                    last = path;
                                };

                                a.IsVisible = false;
                                app.RenderOnce();
                                Check.Equal(1, changes, "FocusPathChanged once");
                                Check.True(last != null && ReferenceEquals(b, last.Leaf), "the event carries the repaired path");
                                Check.True(ReferenceEquals(b, app.CurrentFocusPath.Leaf), "CurrentFocusPath is fresh right after the frame");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessHost", "AuditBesideAnotherApp", "FocusAudit runs on a second headless application while another one is running",
                        _ =>
                        {
                            using (TuiApplication running = BuildLists(new HeadlessBackend(40, 8)))
                            using (TuiApplication audited = BuildLists(new HeadlessBackend(40, 8) { ClaimsTerminal = false }))
                            {
                                running.Start();
                                audited.Start();
                                FocusAuditResult result = FocusAudit.Run(audited);
                                Check.True(result.IsClean, "clean layout audits clean: " + string.Join("; ", result.Problems.Select(p => p.ToString())));
                                Check.Equal(2, result.Stops, "both regions visited");
                                audited.Stop();
                                running.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessHost", "SharedAppsSkipSessionTelemetry", "A non-claiming app does not count as a session or overwrite the real session's shape",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                using (TuiApplication real = new TuiApplication(new HeadlessBackend(30, 7)))
                                using (TuiApplication shared = new TuiApplication(new HeadlessBackend(11, 3) { ClaimsTerminal = false }))
                                {
                                    real.Start();
                                    shared.Start();
                                    shared.RenderOnce();
                                    capture.Observe();
                                    Check.Equal(1.0, capture.Sum(N.SessionsActive), "only the claiming app is an active session");
                                    Check.False(capture.Of(N.TerminalColumns).Any(m => m.Value == 11), "the shared app did not overwrite the columns gauge");
                                    Check.True(capture.Of(N.TerminalColumns).Any(m => m.Value == 30), "the real session's columns remain");
                                    shared.Stop();
                                    Check.Equal(0, capture.Count(N.SessionDuration), "stopping the shared app records no session duration");
                                    real.Stop();
                                }

                                Check.Equal(0.0, capture.Sum(N.SessionsActive), "active sessions return to zero");
                                Check.Equal(1, capture.Count(N.SessionDuration), "the claiming session recorded its duration");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessHost", "ClaimIsReleasedExactly", "Stopping a non-claiming app never releases the terminal slot of a claiming one",
                        _ =>
                        {
                            using (TuiApplication shared = new TuiApplication(new HeadlessBackend { ClaimsTerminal = false }))
                            using (TuiApplication claiming = new TuiApplication(new HeadlessBackend()))
                            using (TuiApplication second = new TuiApplication(new HeadlessBackend()))
                            {
                                shared.Start();
                                shared.Stop();
                                claiming.Start();
                                shared.Start();
                                shared.Stop();
                                Check.Throws<InvalidOperationException>(() => second.Start(), "the claiming app still holds the slot");
                                claiming.Stop();
                                second.Start();
                                second.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("HeadlessHost", "ClaimingBackendsStillExclusive", "A second claiming application still throws, and a backend without ISharedTerminalBackend claims",
                        _ =>
                        {
                            Check.True(new HeadlessBackend().ClaimsTerminal, "HeadlessBackend claims by default, as in 1.4.0");
                            using (TuiApplication first = new TuiApplication(new PlainBackend(new HeadlessBackend())))
                            using (TuiApplication second = new TuiApplication(new HeadlessBackend()))
                            using (TuiApplication shared = new TuiApplication(new HeadlessBackend { ClaimsTerminal = false }))
                            {
                                first.Start();
                                Check.Throws<InvalidOperationException>(() => second.Start(), "plain backend claimed the slot");
                                shared.Start();
                                shared.RenderOnce();
                                Check.True(shared.CaptureFrame() != null, "the shared app started alongside and rendered");
                                shared.Stop();
                                first.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static string DriveClickApp(string name, int row)
        {
            HeadlessBackend backend = new HeadlessBackend(20, 8) { ClaimsTerminal = false };
            using (TuiApplication app = new TuiApplication(backend))
            {
                ListView<string> list = new ListView<string>();
                list.SetItems(new[] { "r0", "r1", "r2", "r3", "r4" });
                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                app.Bind("main", list);
                app.Start();
                for (int i = 0; i < 20; i++)
                {
                    app.RenderOnce();
                    backend.FeedClick(2, row);
                    app.PumpInputOnce();
                }

                app.RenderOnce();
                CellBuffer? frame = app.CaptureFrame();
                if (frame == null)
                    throw new InvalidOperationException(name + ": no frame captured.");
                if (!Snapshot.ToText(frame).Contains("r4"))
                    throw new InvalidOperationException(name + ": frame does not show the list.");

                app.Stop();
                return name + ":selected=" + list.SelectedIndex;
            }
        }

        private static TuiApplication BuildLists(HeadlessBackend backend)
        {
            TuiApplication app = new TuiApplication(backend);
            app.HighlightFocusedRegion = true;
            app.Layout = Layout.Create()
                .Add("a", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                .Add("b", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                .Build();
            ListView<string> a = new ListView<string>();
            a.SetItems(new[] { "one", "two" });
            ListView<string> b = new ListView<string>();
            b.SetItems(new[] { "three", "four" });
            app.Bind("a", a);
            app.Bind("b", b);
            return app;
        }
    }
}
