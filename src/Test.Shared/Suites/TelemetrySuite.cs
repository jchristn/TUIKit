namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Ascii;
    using TUIKit.Content;
    using TUIKit.Diagnostics;
    using TUIKit.Hosting;
    using TUIKit.Modals;
    using TUIKit.Rendering;
    using TUIKit.Terminal;
    using TUIKit.Widgets;
    using N = TUIKit.Diagnostics.TuiKitTelemetryNames;

    /// <summary>
    /// Touchstone suite proving TUIKit's BCL telemetry is emitted: the render pipeline and its stages,
    /// input dispatch and key routing, command handlers, the cross-thread post queue (with trace
    /// context propagation), modals, notifications, session lifecycle and suspension, the clipboard
    /// and file-system integrations, font loading, failure paths, and the no-listener/faulty-listener
    /// paths. Uses an in-memory <see cref="MeterListener"/> and <see cref="ActivityListener"/>.
    /// </summary>
    public static class TelemetrySuite
    {
        private const string ParentSourceName = "Test.Shared.TelemetryParent";

        /// <summary>
        /// Builds the telemetry suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Telemetry",
                displayName: "Telemetry (Meter and ActivitySource)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Telemetry", "StableNames", "Meter and activity source use the documented stable names",
                        _ =>
                        {
                            Check.Equal("TUIKit", N.MeterName, "meter name constant");
                            Check.Equal("TUIKit", N.ActivitySourceName, "activity source name constant");
                            Check.Equal(N.MeterName, TuiKitTelemetry.Meter.Name, "meter instance name");
                            Check.Equal(N.ActivitySourceName, TuiKitTelemetry.ActivitySource.Name, "activity source instance name");
                            Check.Equal(TuiKitLibrary.Version, TuiKitTelemetry.Meter.Version, "meter version");
                            Check.False(TuiKitTelemetry.TraceFrames, "frame tracing is off by default");
                            Check.True(TuiKitTelemetry.Enabled, "telemetry is on by default");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "NoListener", "Every instrumented path runs without a listener",
                        async _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                bool fired = false;
                                app.Bind("ctrl+q", () => fired = true);
                                app.Start();
                                app.Post(() => { });
                                backend.FeedInput(new byte[] { 0x11 });
                                app.PumpInputOnce();
                                app.RenderOnce();
                                app.Notify("hello");
                                ProbeDialogModal modal = new ProbeDialogModal(6, 1);
                                app.Modals.Push(modal);
                                modal.RequestClose(null);
                                app.Modals.RemoveClosed();
                                await app.SuspendAsync(() => Task.CompletedTask);
                                app.Stop();
                                Check.True(fired, "command fired");
                            }

                            new FileSystemProvider().GetChildren(MissingDirectory(), true, false);
                        }),

                    new TestCaseDescriptor("Telemetry", "FrameMetrics", "Frames record duration, outcome, per-stage timing, rows, output, and last success",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.AddPane("main").WriteLine("hello");
                                    app.Start();
                                    app.RenderOnce();
                                    app.RenderOnce();
                                    capture.Observe();
                                    app.Stop();
                                }

                                Check.True(capture.Sum(N.Frames, N.AttrFrameOutcome, N.FrameEmitted) >= 1, "an emitted frame was counted");
                                Check.True(capture.Sum(N.Frames, N.AttrFrameOutcome, N.FrameUnchanged) >= 1, "an unchanged frame was counted");
                                Check.True(capture.Count(N.FrameDuration, N.AttrFrameOutcome, N.FrameEmitted) >= 1, "frame duration recorded");
                                foreach (string stage in new[] { N.StageCompose, N.StageDiff, N.StageWrite })
                                {
                                    Check.True(capture.Count(N.FrameStageDuration, N.AttrStage, stage, N.AttrOutcome, N.OutcomeOk) >= 1, "stage duration recorded for " + stage);
                                    Check.True(capture.Sum(N.FrameStageRuns, N.AttrStage, stage, N.AttrOutcome, N.OutcomeOk) >= 1, "stage run counted for " + stage);
                                }

                                Check.True(capture.Of(N.FrameRowsRepainted).Any(m => m.Value >= 1), "rows repainted recorded");
                                Check.True(capture.Of(N.FrameOutputSize).Any(m => m.Value > 0), "output size recorded");
                                Check.True(capture.Of(N.FrameLastSuccessTime).Any(m => m.Value > 1_000_000_000), "last-success gauge is a Unix time");
                                Check.True(capture.Of(N.BuildInfo).Any(m => m.Value == 1 && m.Has(N.AttrVersion, TuiKitLibrary.Version)), "build info gauge");
                                Check.True(capture.Of(N.FrameDuration).All(m => m.Value >= 0), "durations are non-negative seconds");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "LineModeFrames", "A non-interactive backend records line-mode frames",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5, null, false);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.AddPane("main").WriteLine("hello");
                                    app.Start();
                                    app.RenderOnce();
                                    app.Stop();
                                }

                                Check.True(capture.Sum(N.Frames, N.AttrFrameOutcome, N.FrameLineMode) >= 1, "line-mode frame counted");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "FrameSpans", "TraceFrames emits a frame span with one child span per stage",
                        _ =>
                        {
                            TuiKitTelemetry.TraceFrames = true;
                            try
                            {
                                using (TelemetryCapture capture = new TelemetryCapture())
                                {
                                    TerminalRenderer renderer = new TerminalRenderer(10, 2, TerminalColorDepth.TrueColor);
                                    HeadlessBackend backend = new HeadlessBackend(10, 2);
                                    renderer.Render(backend, surface => surface.Set(0, 0, Cell.Glyph("x", CellStyle.Default, 1)));

                                    Activity frame = Single(capture.Spans(N.SpanFrame), "frame span");
                                    Check.Equal(ActivityStatusCode.Ok, frame.Status, "frame span status");
                                    Check.Equal(N.FrameEmitted, frame.GetTagItem(N.AttrFrameOutcome) as string, "frame span outcome");
                                    foreach (string stage in new[] { N.StageCompose, N.StageDiff, N.StageWrite })
                                    {
                                        Activity child = Single(capture.Spans(N.SpanStagePrefix + stage), "stage span " + stage);
                                        Check.Equal(frame.SpanId, child.ParentSpanId, "stage " + stage + " is a child of the frame");
                                        Check.Equal(ActivityStatusCode.Ok, child.Status, "stage " + stage + " status");
                                    }
                                }
                            }
                            finally
                            {
                                TuiKitTelemetry.TraceFrames = false;
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "FrameFailure", "A throwing compose callback records an error frame, stage, span, and error counter and still propagates",
                        _ =>
                        {
                            TuiKitTelemetry.TraceFrames = true;
                            try
                            {
                                using (TelemetryCapture capture = new TelemetryCapture())
                                {
                                    TerminalRenderer renderer = new TerminalRenderer(10, 2, TerminalColorDepth.TrueColor);
                                    HeadlessBackend backend = new HeadlessBackend(10, 2);
                                    Check.Throws<InvalidOperationException>(
                                        () => renderer.Render(backend, surface => throw new InvalidOperationException("boom")),
                                        "compose failure propagates");

                                    Check.True(capture.Sum(N.Frames, N.AttrFrameOutcome, N.OutcomeError) >= 1, "error frame counted");
                                    Check.True(capture.Sum(N.FrameStageRuns, N.AttrStage, N.StageCompose, N.AttrOutcome, N.OutcomeError) >= 1, "compose stage error counted");
                                    Check.True(capture.Sum(N.Errors, N.AttrComponent, N.ComponentRender, N.AttrErrorType, typeof(InvalidOperationException).FullName!) >= 1, "render error counted by type");
                                    Activity frame = Single(capture.Spans(N.SpanFrame), "frame span");
                                    Check.Equal(ActivityStatusCode.Error, frame.Status, "frame span is an error");
                                    Check.True(frame.Events.Any(e => e.Name == "exception"), "exception event recorded");
                                    Activity compose = Single(capture.Spans(N.SpanStagePrefix + N.StageCompose), "compose span");
                                    Check.Equal(ActivityStatusCode.Error, compose.Status, "compose span is an error");
                                }
                            }
                            finally
                            {
                                TuiKitTelemetry.TraceFrames = false;
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "InputAndCommand", "Input bytes, events, routing, and a command invocation with its span",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.Bind("ctrl+q", () => { });
                                    app.Start();
                                    backend.FeedInput(new byte[] { 0x11 });
                                    app.PumpInputOnce();
                                    backend.FeedInput("z");
                                    app.PumpInputOnce();
                                    app.Stop();
                                }

                                Check.True(capture.Sum(N.InputBytes) >= 2, "input bytes counted");
                                Check.True(capture.Sum(N.InputEvents, N.AttrInputKind, "key") >= 2, "key events counted");
                                Check.True(capture.Count(N.InputDispatchDuration, N.AttrInputKind, "key", N.AttrOutcome, N.OutcomeOk) >= 2, "dispatch duration recorded");
                                Check.True(capture.Sum(N.KeyRoutes, N.AttrKeyRoute, "global_command") >= 1, "global command route counted");
                                Check.True(capture.Sum(N.KeyRoutes, N.AttrKeyRoute, "unhandled") >= 1, "unhandled route counted");
                                Check.True(capture.Sum(N.CommandInvocations, N.AttrOutcome, N.OutcomeOk) >= 1, "command invocation counted");
                                Check.True(capture.Count(N.CommandDuration, N.AttrOutcome, N.OutcomeOk) >= 1, "command duration recorded");
                                Activity span = Single(capture.Spans(N.SpanCommand), "command span");
                                Check.Equal("bind:" + TUIKit.Input.KeyChord.Parse("ctrl+q"), span.GetTagItem(N.AttrCommandId) as string, "command id is a span attribute");
                                Check.Equal(ActivityStatusCode.Ok, span.Status, "command span status");
                                Check.False(capture.Of(N.CommandInvocations).Any(m => m.Tags.ContainsKey(N.AttrCommandId)), "command id is never a metric label");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "CommandFailure", "A throwing command handler records error outcome, type, and span and still propagates",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.Bind("ctrl+q", () => throw new InvalidOperationException("handler failed"));
                                    app.Start();
                                    backend.FeedInput(new byte[] { 0x11 });
                                    Check.Throws<InvalidOperationException>(() => app.PumpInputOnce(), "handler exception propagates");
                                    app.Stop();
                                }

                                string type = typeof(InvalidOperationException).FullName!;
                                Check.True(capture.Sum(N.CommandInvocations, N.AttrOutcome, N.OutcomeError, N.AttrErrorType, type) >= 1, "command error counted by type");
                                Check.True(capture.Count(N.CommandDuration, N.AttrOutcome, N.OutcomeError) >= 1, "command error duration recorded");
                                Check.True(capture.Sum(N.Errors, N.AttrComponent, N.ComponentCommand) >= 1, "command error in errors counter");
                                Check.True(capture.Count(N.InputDispatchDuration, N.AttrOutcome, N.OutcomeError, N.AttrErrorType, type) >= 1, "dispatch error recorded");
                                Activity span = Single(capture.Spans(N.SpanCommand), "command span");
                                Check.Equal(ActivityStatusCode.Error, span.Status, "command span is an error");
                                Check.Equal(type, span.GetTagItem(N.AttrErrorType) as string, "command span error type");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "MouseCoalescing", "Collapsed pointer moves are counted",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.Start();
                                    backend.FeedInput("\u001b[<35;1;1M\u001b[<35;2;1M\u001b[<35;3;1M");
                                    app.PumpInputOnce();
                                    app.Stop();
                                }

                                Check.True(capture.Sum(N.InputCoalesced) >= 2, "two moves coalesced");
                                Check.True(capture.Sum(N.InputEvents, N.AttrInputKind, "mouse") >= 1, "mouse event counted");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "PostPropagation", "Posted actions measure queue wait and parent their span to the posting context",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture(ParentSourceName))
                            using (ActivitySource parentSource = new ActivitySource(ParentSourceName))
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5);
                                ActivityTraceId parentTrace;
                                ActivitySpanId parentSpan;
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.Start();
                                    Activity? previous = Activity.Current;
                                    using (Activity? parent = parentSource.StartActivity("background work"))
                                    {
                                        Check.True(parent != null, "parent activity sampled");
                                        parentTrace = parent!.TraceId;
                                        parentSpan = parent.SpanId;
                                        app.Post(() => { });
                                    }

                                    Activity.Current = previous;
                                    app.PumpInputOnce();

                                    app.Post(() => throw new InvalidOperationException("posted failure"));
                                    Check.Throws<InvalidOperationException>(() => app.PumpInputOnce(), "posted failure propagates");
                                    app.Stop();
                                }

                                Check.True(capture.Sum(N.PostEnqueued) >= 2, "posts counted");
                                Check.Equal(0.0, capture.Sum(N.PostQueueDepth), "queue depth returns to zero");
                                Check.True(capture.Count(N.PostQueueWait) >= 2, "queue wait recorded");
                                Check.True(capture.Count(N.PostDuration, N.AttrOutcome, N.OutcomeOk) >= 1, "posted success duration");
                                Check.True(capture.Count(N.PostDuration, N.AttrOutcome, N.OutcomeError) >= 1, "posted failure duration");
                                Check.True(capture.Sum(N.Errors, N.AttrComponent, N.ComponentPost) >= 1, "posted failure in errors counter");

                                IReadOnlyList<Activity> posts = capture.Spans(N.SpanPost);
                                Activity linked = posts.FirstOrDefault(a => a.TraceId == parentTrace)
                                    ?? throw new InvalidOperationException("post span did not join the posting trace");
                                Check.Equal(parentSpan, linked.ParentSpanId, "post span parent is the posting span");
                                Check.True(posts.Any(a => a.Status == ActivityStatusCode.Error), "failed post span is an error");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "Modals", "Modal show, active gauge, duration by outcome, and span",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                ModalStack stack = new ModalStack();
                                ProbeDialogModal completed = new ProbeDialogModal(6, 1);
                                ProbeDialogModal dismissed = new ProbeDialogModal(6, 1);
                                stack.Push(completed);
                                stack.Push(dismissed);
                                Check.True(Activity.Current == null || Activity.Current.OperationName != N.SpanModal, "modal span is not left as the current activity");
                                dismissed.RequestClose(null);
                                completed.RequestClose("ok");
                                stack.RemoveClosed();

                                string type = typeof(ProbeDialogModal).Name;
                                Check.Equal(2.0, capture.Sum(N.ModalShown, N.AttrModalType, type), "two modals shown");
                                Check.Equal(0.0, capture.Sum(N.ModalActive), "active modals return to zero");
                                Check.Equal(1, capture.Count(N.ModalDuration, N.AttrModalType, type, N.AttrModalOutcome, N.ModalCompleted), "completed modal duration");
                                Check.Equal(1, capture.Count(N.ModalDuration, N.AttrModalType, type, N.AttrModalOutcome, N.ModalDismissed), "dismissed modal duration");
                                Check.Equal(2, capture.Spans(N.SpanModal).Count, "one span per modal");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "Notifications", "Notifications count by severity and evictions by the concurrency cap",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                NotificationCenter center = new NotificationCenter();
                                center.MaxConcurrent = 1;
                                center.Add("a", NotificationSeverity.Warning, 0);
                                center.Add("b", NotificationSeverity.Error, 0);

                                Check.Equal(1.0, capture.Sum(N.Notifications, N.AttrSeverity, "warning"), "warning counted");
                                Check.Equal(1.0, capture.Sum(N.Notifications, N.AttrSeverity, "error"), "error counted");
                                Check.Equal(1.0, capture.Sum(N.NotificationsEvicted), "one eviction");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "SessionLifecycle", "Session starts, rejections, active sessions, duration, and config gauges",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(30, 7);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.TargetFps = 30;
                                    app.Start();
                                    capture.Observe();
                                    using (TuiApplication second = new TuiApplication(new HeadlessBackend(10, 3)))
                                        Check.Throws<InvalidOperationException>(() => second.Start(), "second session rejected");
                                    app.Stop();
                                }

                                Check.True(capture.Sum(N.SessionStarts, N.AttrOutcome, N.OutcomeOk) >= 1, "start counted");
                                Check.True(capture.Sum(N.SessionStarts, N.AttrOutcome, N.OutcomeRejected) >= 1, "rejection counted");
                                Check.Equal(0.0, capture.Sum(N.SessionsActive), "active sessions return to zero");
                                Check.True(capture.Count(N.SessionDuration) >= 1, "session duration recorded");
                                Check.True(capture.Of(N.ConfigTargetFps).Any(m => m.Value == 30), "target fps gauge");
                                Check.True(capture.Of(N.TerminalColumns).Any(m => m.Value == 30), "columns gauge");
                                Check.True(capture.Of(N.TerminalRows).Any(m => m.Value == 7), "rows gauge");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "Suspend", "Suspension records duration and span for success and failure",
                        async _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                HeadlessBackend backend = new HeadlessBackend(20, 5);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.Start();
                                    await app.SuspendAsync(() => Task.CompletedTask);
                                    await Check.ThrowsAsync<InvalidOperationException>(
                                        () => app.SuspendAsync(() => throw new InvalidOperationException("editor crashed")),
                                        "suspended failure propagates");
                                    app.Stop();
                                }

                                Check.Equal(1, capture.Count(N.SuspendDuration, N.AttrOutcome, N.OutcomeOk), "successful suspend");
                                Check.Equal(1, capture.Count(N.SuspendDuration, N.AttrOutcome, N.OutcomeError), "failed suspend");
                                Check.True(capture.Sum(N.Errors, N.AttrComponent, N.ComponentSuspend) >= 1, "suspend error counted");
                                IReadOnlyList<Activity> spans = capture.Spans(N.SpanSuspend);
                                Check.Equal(2, spans.Count, "two suspend spans");
                                Check.True(spans.Any(s => s.Status == ActivityStatusCode.Error), "failed suspend span is an error");
                            }
                        }),

                    new TestCaseDescriptor("Telemetry", "FileSystemIntegration", "File-system calls record client spans, outcomes, and error types",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                FileSystemProvider provider = new FileSystemProvider();
                                provider.GetChildren(Path.GetTempPath(), true, false);
                                provider.GetChildren(MissingDirectory(), true, false);
                                provider.GetRoots();

                                Check.True(capture.Sum(N.IntegrationRequests, N.AttrIntegrationService, N.ServiceFileSystem, N.AttrIntegrationOperation, N.OperationGetChildren, N.AttrOutcome, N.OutcomeOk) >= 1, "successful listing counted");
                                Check.True(capture.Sum(N.IntegrationRequests, N.AttrIntegrationService, N.ServiceFileSystem, N.AttrIntegrationOperation, N.OperationGetChildren, N.AttrOutcome, N.OutcomeError, N.AttrErrorType, typeof(DirectoryNotFoundException).FullName!) >= 1, "failed listing counted by error type");
                                Check.True(capture.Sum(N.IntegrationRequests, N.AttrIntegrationOperation, N.OperationGetRoots) >= 1, "roots counted");
                                Check.True(capture.Count(N.IntegrationDuration, N.AttrIntegrationService, N.ServiceFileSystem) >= 3, "latency recorded");
                                IReadOnlyList<Activity> spans = capture.Spans(N.ServiceFileSystem + " " + N.OperationGetChildren);
                                Check.Equal(2, spans.Count, "one span per listing");
                                Check.True(spans.All(s => s.Kind == ActivityKind.Client), "integration spans are client spans");
                                Check.True(spans.Any(s => s.Status == ActivityStatusCode.Error), "failed listing span is an error");
                                Check.True(spans.All(s => s.Tags.All(t => !t.Value!.Contains(Path.DirectorySeparatorChar))), "no file paths on spans");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "ClipboardIntegration", "Clipboard reads record a client span and an outcome whether or not a tool exists",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                SystemClipboard.TryReadText(out string _);

                                Check.True(capture.Sum(N.IntegrationRequests, N.AttrIntegrationService, N.ServiceClipboard, N.AttrIntegrationOperation, N.OperationRead) >= 1, "clipboard read counted");
                                Check.True(capture.Count(N.IntegrationDuration, N.AttrIntegrationService, N.ServiceClipboard) >= 1, "clipboard latency recorded");
                                IReadOnlyList<Activity> spans = capture.Spans(N.ServiceClipboard + " " + N.OperationRead);
                                Check.True(spans.Count >= 1, "clipboard span recorded");
                                Check.True(spans.All(s => s.Kind == ActivityKind.Client && s.GetTagItem(N.AttrProcessExecutable) != null), "client span names the tool");
                                Check.True(spans.All(s => s.Status != ActivityStatusCode.Unset), "clipboard span status is explicit");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "FontLoads", "Font loads record success and parse failure",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                Assembly assembly = typeof(TuiKitLibrary).Assembly;
                                string resource = assembly.GetManifestResourceNames().First(n => n.EndsWith(".flf", StringComparison.OrdinalIgnoreCase));
                                using (Stream? stream = assembly.GetManifestResourceStream(resource))
                                {
                                    Check.True(stream != null, "embedded font stream");
                                    FigletFontLoader.Load(stream!, "probe-font");
                                }

                                Check.Throws<AsciiFontException>(() => FigletFontLoader.Load("not a font"), "bad font throws");

                                Check.Equal(1.0, capture.Sum(N.FontLoads, N.AttrOutcome, N.OutcomeOk), "successful load counted");
                                Check.Equal(1.0, capture.Sum(N.FontLoads, N.AttrOutcome, N.OutcomeError), "failed load counted");
                                Check.True(capture.Count(N.FontLoadDuration) >= 2, "load duration recorded");
                                Check.True(capture.Sum(N.Errors, N.AttrComponent, N.ComponentFont, N.AttrErrorType, typeof(AsciiFontException).FullName!) >= 1, "font error by type");
                                Check.True(capture.Spans(N.SpanFontLoad).Any(s => s.Status == ActivityStatusCode.Error), "failed load span is an error");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "FaultyListener", "A listener that throws never breaks rendering, input, or commands",
                        _ =>
                        {
                            // The faulty callbacks disarm when the case ends: on .NET 8 a MeterListener
                            // whose callback has thrown stays subscribed after Dispose, so an armed callback
                            // would keep throwing and starve every later listener in the process.
                            bool armed = true;
                            using (MeterListener faulty = new MeterListener())
                            using (ActivityListener faultySpans = new ActivityListener())
                            {
                                faulty.InstrumentPublished = (instrument, listener) =>
                                {
                                    if (instrument.Meter.Name == N.MeterName)
                                        listener.EnableMeasurementEvents(instrument);
                                };
                                faulty.SetMeasurementEventCallback<long>((i, v, t, s) => { if (armed) throw new InvalidOperationException("listener fault"); });
                                faulty.SetMeasurementEventCallback<double>((i, v, t, s) => { if (armed) throw new InvalidOperationException("listener fault"); });
                                faulty.Start();
                                faultySpans.ShouldListenTo = source => source.Name == N.ActivitySourceName;
                                faultySpans.Sample = (ref ActivityCreationOptions<ActivityContext> o) => ActivitySamplingResult.AllDataAndRecorded;
                                faultySpans.ActivityStopped = a => { if (armed) throw new InvalidOperationException("listener fault"); };
                                ActivitySource.AddActivityListener(faultySpans);

                                try
                                {
                                    HeadlessBackend backend = new HeadlessBackend(20, 5);
                                    using (TuiApplication app = new TuiApplication(backend))
                                    {
                                        bool fired = false;
                                        app.Bind("ctrl+q", () => fired = true);
                                        app.Start();
                                        backend.FeedInput(new byte[] { 0x11 });
                                        app.PumpInputOnce();
                                        app.RenderOnce();
                                        app.Stop();
                                        Check.True(fired, "command fired despite a faulty listener");
                                    }
                                }
                                finally
                                {
                                    armed = false;
                                }
                            }

                            // Telemetry still reaches a healthy listener once the faulty one is gone.
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                new NotificationCenter().Add("after", NotificationSeverity.Info, 0);
                                Check.Equal(1.0, capture.Sum(N.Notifications, N.AttrSeverity, "info"), "healthy listener receives measurements");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Telemetry", "Disabled", "Enabled = false suppresses metrics and spans",
                        _ =>
                        {
                            TuiKitTelemetry.Enabled = false;
                            try
                            {
                                using (TelemetryCapture capture = new TelemetryCapture())
                                {
                                    HeadlessBackend backend = new HeadlessBackend(20, 5);
                                    using (TuiApplication app = new TuiApplication(backend))
                                    {
                                        app.Bind("ctrl+q", () => { });
                                        app.Start();
                                        backend.FeedInput(new byte[] { 0x11 });
                                        app.PumpInputOnce();
                                        app.RenderOnce();
                                        app.Stop();
                                    }

                                    Check.Equal(0, capture.Of(N.Frames).Count, "no frame metrics");
                                    Check.Equal(0, capture.Of(N.CommandInvocations).Count, "no command metrics");
                                    Check.Equal(0, capture.Activities.Count, "no spans");
                                }
                            }
                            finally
                            {
                                TuiKitTelemetry.Enabled = true;
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static string MissingDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "tuikit-missing-" + Guid.NewGuid().ToString("N"));
        }

        private static Activity Single(IReadOnlyList<Activity> spans, string what)
        {
            if (spans.Count != 1)
                throw new InvalidOperationException(what + ": expected exactly one span but found " + spans.Count + ".");
            return spans[0];
        }
    }
}
