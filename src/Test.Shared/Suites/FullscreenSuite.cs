namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Rendering;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite covering the fullscreen-rendering enhancements: synchronized output (DEC
    /// private mode 2026), the persistent full-repaint mode, their capability detection, and the
    /// cross-platform <see cref="TuiApplication.SuspendAsync"/> shell-out. Cases are both positive
    /// (the feature does what it claims) and negative (it stays off, degrades, or guards its inputs).
    /// </summary>
    public static class FullscreenSuite
    {
        /// <summary>
        /// Builds the fullscreen-enhancements suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Fullscreen",
                displayName: "Fullscreen Enhancements (sync output / full repaint / suspend)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Fullscreen", "SyncSequences", "Mode 2026 begin/end sequences are exact",
                        _ =>
                        {
                            Check.Equal("[?2026h", Ansi.BeginSynchronizedUpdate, "BSU is CSI ?2026h");
                            Check.Equal("[?2026l", Ansi.EndSynchronizedUpdate, "ESU is CSI ?2026l");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "CapabilityPresets", "Full enables and Minimal disables synchronized output",
                        _ =>
                        {
                            Check.True(TerminalCapabilities.Full.SynchronizedOutput, "Full advertises synchronized output");
                            Check.False(TerminalCapabilities.Minimal.SynchronizedOutput, "Minimal does not");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "CapabilityDetection", "Synchronized output is detected for modern terminals only",
                        _ =>
                        {
                            Check.False(CapabilityDetector.Detect(_ => null, false).SynchronizedOutput,
                                "non-interactive never advertises synchronized output");

                            Func<string, string?> dumb = n => n == "TERM" ? "dumb" : null;
                            Check.False(CapabilityDetector.Detect(dumb, true).SynchronizedOutput,
                                "TERM=dumb disables synchronized output");

                            Func<string, string?> windowsTerminal = n => n == "WT_SESSION" ? "1" : null;
                            Check.True(CapabilityDetector.Detect(windowsTerminal, true).SynchronizedOutput,
                                "Windows Terminal advertises synchronized output");

                            Func<string, string?> kitty = n => n == "KITTY_WINDOW_ID" ? "1" : null;
                            Check.True(CapabilityDetector.Detect(kitty, true).SynchronizedOutput,
                                "Kitty advertises synchronized output");

                            Func<string, string?> truecolor = n => n == "COLORTERM" ? "truecolor" : null;
                            Check.True(CapabilityDetector.Detect(truecolor, true).SynchronizedOutput,
                                "a truecolor terminal advertises synchronized output");

                            // GNU screen is the known holdout: even a truecolor screen must not advertise it,
                            // isolating the isGnuScreen guard from the general "modern" signal.
                            Func<string, string?> truecolorScreen = n =>
                                n == "TERM" ? "screen" :
                                n == "COLORTERM" ? "truecolor" : null;
                            Check.False(CapabilityDetector.Detect(truecolorScreen, true).SynchronizedOutput,
                                "GNU screen does not pass synchronized output through");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "SyncWrapsFrame", "A synchronized frame is wrapped in a balanced begin/end pair",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            TerminalRenderer renderer = new TerminalRenderer(20, 4, TerminalColorDepth.TrueColor);
                            Check.False(renderer.SynchronizedOutput, "synchronized output is off by default");

                            renderer.SynchronizedOutput = true;
                            bool emitted = renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            Check.True(emitted, "frame emitted");

                            string output = backend.PeekOutput();
                            Check.True(output.StartsWith(Ansi.BeginSynchronizedUpdate, StringComparison.Ordinal),
                                "frame opens with BSU");
                            Check.True(output.EndsWith(Ansi.EndSynchronizedUpdate, StringComparison.Ordinal),
                                "frame closes with ESU");
                            Check.True(output.IndexOf(Ansi.MoveTo(0, 0), StringComparison.Ordinal)
                                > output.IndexOf(Ansi.BeginSynchronizedUpdate, StringComparison.Ordinal),
                                "cursor moves happen inside the synchronized update");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "SyncSkipsEmptyFrame", "An unchanged synchronized frame emits no begin/end pair",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            TerminalRenderer renderer = new TerminalRenderer(20, 4, TerminalColorDepth.TrueColor)
                            {
                                SynchronizedOutput = true
                            };
                            renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            backend.TakeOutput();

                            bool emitted = renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            Check.False(emitted, "identical frame emits nothing");
                            Check.Equal("", backend.PeekOutput(), "no BSU/ESU wraps an empty diff");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "SyncDisabledNoWrap", "With synchronized output off no begin/end pair is emitted",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            TerminalRenderer renderer = new TerminalRenderer(20, 4, TerminalColorDepth.TrueColor);
                            renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));

                            string output = backend.PeekOutput();
                            Check.False(output.Contains(Ansi.BeginSynchronizedUpdate), "no BSU when disabled");
                            Check.False(output.Contains(Ansi.EndSynchronizedUpdate), "no ESU when disabled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "IncrementalByDefault", "Without full repaint an unchanged frame emits nothing",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            TerminalRenderer renderer = new TerminalRenderer(20, 4, TerminalColorDepth.TrueColor);
                            Check.False(renderer.ForceFullRepaint, "full repaint is off by default");

                            renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            backend.TakeOutput();

                            bool emitted = renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            Check.False(emitted, "identical frame is a no-op");
                            Check.Equal("", backend.PeekOutput(), "no bytes written for an unchanged frame");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "FullRepaintReemitsAllRows", "Full repaint re-emits every row of an unchanged frame",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            TerminalRenderer renderer = new TerminalRenderer(20, 4, TerminalColorDepth.TrueColor)
                            {
                                ForceFullRepaint = true
                            };
                            renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            backend.TakeOutput();

                            bool emitted = renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            Check.True(emitted, "identical frame still emits under full repaint");

                            string output = backend.PeekOutput();
                            Check.True(output.Contains(Ansi.MoveTo(0, 0)), "top row repainted");
                            Check.True(output.Contains(Ansi.MoveTo(0, 3)), "bottom row repainted despite no change");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "AppForceFullRepaintPassthrough", "TuiApplication.ForceFullRepaint drives the renderer",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Check.False(app.ForceFullRepaint, "default off");
                                app.AddPane("main").WriteLine("hello");
                                app.ForceFullRepaint = true;
                                app.Start();
                                backend.TakeOutput();

                                app.RenderOnce();
                                backend.TakeOutput();
                                app.RenderOnce();
                                Check.True(backend.PeekOutput().Length > 0,
                                    "an identical frame re-emits while ForceFullRepaint is on");
                                app.Stop();
                            }

                            HeadlessBackend backend2 = new HeadlessBackend(20, 5);
                            using (TuiApplication app2 = new TuiApplication(backend2))
                            {
                                app2.AddPane("main").WriteLine("hello");
                                app2.Start();
                                backend2.TakeOutput();

                                app2.RenderOnce();
                                backend2.TakeOutput();
                                app2.RenderOnce();
                                Check.Equal("", backend2.PeekOutput(),
                                    "an identical frame is a no-op with ForceFullRepaint off");
                                app2.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "AppSyncFromCapabilities", "The host wraps frames only when the backend advertises synchronized output",
                        _ =>
                        {
                            HeadlessBackend syncBackend = new HeadlessBackend(20, 5, TerminalCapabilities.Full);
                            using (TuiApplication app = new TuiApplication(syncBackend))
                            {
                                app.AddPane("main").WriteLine("hi");
                                app.Start();
                                syncBackend.TakeOutput();
                                app.RenderOnce();
                                Check.True(syncBackend.PeekOutput().Contains(Ansi.BeginSynchronizedUpdate),
                                    "sync-capable backend gets wrapped frames");
                                app.Stop();
                            }

                            TerminalCapabilities noSync = new TerminalCapabilities(
                                TerminalColorDepth.TrueColor, true, true, true, true, true, true, true, false);
                            HeadlessBackend plainBackend = new HeadlessBackend(20, 5, noSync);
                            using (TuiApplication app = new TuiApplication(plainBackend))
                            {
                                app.AddPane("main").WriteLine("hi");
                                app.Start();
                                plainBackend.TakeOutput();
                                app.RenderOnce();
                                Check.False(plainBackend.PeekOutput().Contains(Ansi.BeginSynchronizedUpdate),
                                    "backend without the capability gets unwrapped frames");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Fullscreen", "SuspendRestoresAndResumes", "SuspendAsync restores the terminal, runs the action, then re-enters",
                        async _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Start();
                                backend.TakeOutput();

                                bool ran = false;
                                string duringOutput = string.Empty;
                                await app.SuspendAsync(() =>
                                {
                                    ran = true;
                                    Check.True(app.IsSuspended, "IsSuspended is true inside the action");
                                    duringOutput = backend.TakeOutput();
                                    return Task.CompletedTask;
                                });

                                Check.True(ran, "the action ran");
                                Check.True(duringOutput.Contains(Ansi.ExitAltScreen), "leaves the alternate screen before the action");
                                Check.True(duringOutput.Contains(Ansi.ShowCursor), "shows the cursor before the action");
                                Check.True(duringOutput.Contains(Ansi.EndSynchronizedUpdate), "closes any open synchronized update");
                                Check.False(app.IsSuspended, "IsSuspended is false after resume");
                                Check.True(backend.PeekOutput().Contains(Ansi.EnterAltScreen), "re-enters the alternate screen after the action");
                                app.Stop();
                            }
                        }),

                    new TestCaseDescriptor("Fullscreen", "SuspendLoopInert", "The render and input loops are inert while suspended",
                        async _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.AddPane("main").WriteLine("hi");
                                app.Start();

                                await app.SuspendAsync(() =>
                                {
                                    backend.TakeOutput();
                                    app.RenderOnce();
                                    Check.Equal("", backend.PeekOutput(), "RenderOnce paints nothing while suspended");
                                    app.PumpInputOnce();
                                    Check.Equal("", backend.PeekOutput(), "PumpInputOnce writes nothing while suspended");
                                    return Task.CompletedTask;
                                });

                                app.Stop();
                            }
                        }),

                    new TestCaseDescriptor("Fullscreen", "SuspendNullAction", "SuspendAsync rejects a null action",
                        async _ =>
                        {
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 5)))
                            {
                                await Check.ThrowsAsync<ArgumentNullException>(
                                    () => app.SuspendAsync(null!), "null action rejected");
                            }
                        }),

                    new TestCaseDescriptor("Fullscreen", "SuspendNonInteractive", "SuspendAsync runs the action without escapes on a non-interactive backend",
                        async _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5, TerminalCapabilities.Full, interactive: false);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Start();
                                backend.TakeOutput();

                                bool ran = false;
                                await app.SuspendAsync(() => { ran = true; return Task.CompletedTask; });

                                Check.True(ran, "the action ran");
                                Check.Equal("", backend.PeekOutput(), "no escape sequences emitted on a non-interactive backend");
                                Check.False(app.IsSuspended, "never entered the suspended state");
                                app.Stop();
                            }
                        }),

                    new TestCaseDescriptor("Fullscreen", "SuspendActionThrows", "A throwing action still restores the terminal before propagating",
                        async _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Start();
                                backend.TakeOutput();

                                await Check.ThrowsAsync<InvalidOperationException>(
                                    () => app.SuspendAsync(() => throw new InvalidOperationException("boom")),
                                    "the action's exception propagates");

                                Check.False(app.IsSuspended, "resumed after the action threw");
                                Check.True(backend.PeekOutput().Contains(Ansi.EnterAltScreen),
                                    "the terminal is restored even when the action throws");
                                app.Stop();
                            }
                        })
                });
        }
    }
}
