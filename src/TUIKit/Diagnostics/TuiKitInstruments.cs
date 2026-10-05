namespace TUIKit.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;

    // Internal instrument registry and best-effort recording helpers. Every helper swallows faults
    // raised by a misbehaving listener so instrumentation can never break rendering or input.
    internal static class TuiKitInstruments
    {
        internal static readonly Histogram<double> FrameDuration;
        internal static readonly Counter<long> Frames;
        internal static readonly Histogram<double> FrameStageDuration;
        internal static readonly Counter<long> FrameStageRuns;
        internal static readonly Histogram<long> FrameOutputSize;
        internal static readonly Histogram<long> FrameRowsRepainted;
        internal static readonly Counter<long> InputBytes;
        internal static readonly Counter<long> InputEvents;
        internal static readonly Counter<long> InputCoalesced;
        internal static readonly Histogram<double> InputDispatchDuration;
        internal static readonly Counter<long> KeyRoutes;
        internal static readonly Counter<long> CommandInvocations;
        internal static readonly Histogram<double> CommandDuration;
        internal static readonly Counter<long> PostEnqueued;
        internal static readonly UpDownCounter<long> PostQueueDepth;
        internal static readonly Histogram<double> PostQueueWait;
        internal static readonly Histogram<double> PostDuration;
        internal static readonly Counter<long> ModalShown;
        internal static readonly UpDownCounter<long> ModalActive;
        internal static readonly Histogram<double> ModalDuration;
        internal static readonly Counter<long> Notifications;
        internal static readonly Counter<long> NotificationsEvicted;
        internal static readonly Counter<long> NotificationsCoalesced;
        internal static readonly Counter<long> ClickRegionsInvoked;
        internal static readonly Counter<long> SessionStarts;
        internal static readonly UpDownCounter<long> SessionsActive;
        internal static readonly Histogram<double> SessionDuration;
        internal static readonly Histogram<double> SuspendDuration;
        internal static readonly Counter<long> IntegrationRequests;
        internal static readonly Histogram<double> IntegrationDuration;
        internal static readonly Counter<long> FontLoads;
        internal static readonly Histogram<double> FontLoadDuration;
        internal static readonly Counter<long> ClipboardWrites;
        internal static readonly Counter<long> Errors;

        private static long _LastFrameSuccessUnixMs;
        private static int _TargetFps;
        private static int _Columns;
        private static int _Rows;

        static TuiKitInstruments()
        {
            Meter meter = TuiKitTelemetry.Meter;

            FrameDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.FrameDuration, "s", "Wall time of one rendered frame.");
            Frames = meter.CreateCounter<long>(TuiKitTelemetryNames.Frames, "{frame}", "Frames rendered, by outcome.");
            FrameStageDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.FrameStageDuration, "s", "Per-stage frame time.");
            FrameStageRuns = meter.CreateCounter<long>(TuiKitTelemetryNames.FrameStageRuns, "{run}", "Per-stage executions, by outcome.");
            FrameOutputSize = meter.CreateHistogram<long>(TuiKitTelemetryNames.FrameOutputSize, "{char}", "Escape-sequence characters written by one emitted frame.");
            FrameRowsRepainted = meter.CreateHistogram<long>(TuiKitTelemetryNames.FrameRowsRepainted, "{row}", "Screen rows repainted by one frame.");
            InputBytes = meter.CreateCounter<long>(TuiKitTelemetryNames.InputBytes, "By", "Raw input bytes read from the terminal backend.");
            InputEvents = meter.CreateCounter<long>(TuiKitTelemetryNames.InputEvents, "{event}", "Decoded input events dispatched, by kind.");
            InputCoalesced = meter.CreateCounter<long>(TuiKitTelemetryNames.InputCoalesced, "{event}", "Pointer moves collapsed before dispatch.");
            InputDispatchDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.InputDispatchDuration, "s", "Time to dispatch one input event.");
            KeyRoutes = meter.CreateCounter<long>(TuiKitTelemetryNames.KeyRoutes, "{key}", "Key presses by routing decision.");
            CommandInvocations = meter.CreateCounter<long>(TuiKitTelemetryNames.CommandInvocations, "{invocation}", "Command handler invocations, by outcome.");
            CommandDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.CommandDuration, "s", "Command handler run time.");
            PostEnqueued = meter.CreateCounter<long>(TuiKitTelemetryNames.PostEnqueued, "{action}", "Actions queued onto the application loop.");
            PostQueueDepth = meter.CreateUpDownCounter<long>(TuiKitTelemetryNames.PostQueueDepth, "{action}", "Actions waiting in the post queue.");
            PostQueueWait = meter.CreateHistogram<double>(TuiKitTelemetryNames.PostQueueWait, "s", "Time a posted action waited before running.");
            PostDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.PostDuration, "s", "Run time of a posted action.");
            ModalShown = meter.CreateCounter<long>(TuiKitTelemetryNames.ModalShown, "{modal}", "Modals pushed, by type.");
            ModalActive = meter.CreateUpDownCounter<long>(TuiKitTelemetryNames.ModalActive, "{modal}", "Modals currently open.");
            ModalDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.ModalDuration, "s", "Time a modal stayed open.");
            Notifications = meter.CreateCounter<long>(TuiKitTelemetryNames.Notifications, "{notification}", "Toast notifications raised, by severity.");
            NotificationsEvicted = meter.CreateCounter<long>(TuiKitTelemetryNames.NotificationsEvicted, "{notification}", "Notifications evicted by the concurrency cap.");
            NotificationsCoalesced = meter.CreateCounter<long>(TuiKitTelemetryNames.NotificationsCoalesced, "{notification}", "Notification raises merged into an identical visible toast, by severity.");
            ClickRegionsInvoked = meter.CreateCounter<long>(TuiKitTelemetryNames.ClickRegionsInvoked, "{click}", "Inline click regions invoked.");
            SessionStarts = meter.CreateCounter<long>(TuiKitTelemetryNames.SessionStarts, "{session}", "Terminal session start attempts, by outcome.");
            SessionsActive = meter.CreateUpDownCounter<long>(TuiKitTelemetryNames.SessionsActive, "{session}", "Terminal sessions currently started.");
            SessionDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.SessionDuration, "s", "Terminal session lifetime.");
            SuspendDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.SuspendDuration, "s", "Time the terminal was handed to an external program.");
            IntegrationRequests = meter.CreateCounter<long>(TuiKitTelemetryNames.IntegrationRequests, "{request}", "Outbound integration calls, by service, operation, and outcome.");
            IntegrationDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.IntegrationDuration, "s", "Outbound integration latency.");
            FontLoads = meter.CreateCounter<long>(TuiKitTelemetryNames.FontLoads, "{load}", "FIGlet font loads, by outcome.");
            FontLoadDuration = meter.CreateHistogram<double>(TuiKitTelemetryNames.FontLoadDuration, "s", "FIGlet font parse time.");
            ClipboardWrites = meter.CreateCounter<long>(TuiKitTelemetryNames.ClipboardWrites, "{write}", "OSC 52 clipboard writes from text selection.");
            Errors = meter.CreateCounter<long>(TuiKitTelemetryNames.Errors, "{error}", "Failures observed by TUIKit, by component and error type.");

            meter.CreateObservableGauge<long>(TuiKitTelemetryNames.BuildInfo, ObserveBuildInfo, "{info}", "TUIKit build information; always 1.");
            meter.CreateObservableGauge<double>(TuiKitTelemetryNames.FrameLastSuccessTime, ObserveLastFrameSuccess, "s", "Unix time of the last frame that completed without error.");
            meter.CreateObservableGauge<int>(TuiKitTelemetryNames.ConfigTargetFps, () => Volatile.Read(ref _TargetFps), "{frame}/s", "Configured target frame rate of the active session.");
            meter.CreateObservableGauge<int>(TuiKitTelemetryNames.TerminalColumns, () => Volatile.Read(ref _Columns), "{cell}", "Terminal width of the active session.");
            meter.CreateObservableGauge<int>(TuiKitTelemetryNames.TerminalRows, () => Volatile.Read(ref _Rows), "{cell}", "Terminal height of the active session.");
        }

        internal static bool Enabled
        {
            get { return TuiKitTelemetry.Enabled; }
        }

        internal static long Timestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        internal static double SecondsSince(long startTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            return elapsed <= 0 ? 0.0 : (double)elapsed / Stopwatch.Frequency;
        }

        internal static string ErrorType(Exception ex)
        {
            Type type = ex.GetType();
            return type.FullName ?? type.Name;
        }

        internal static void Add(Counter<long> counter, long value)
        {
            if (!Enabled)
                return;

            try
            {
                counter.Add(value);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Add(Counter<long> counter, long value, string key, object? tag)
        {
            if (!Enabled)
                return;

            try
            {
                counter.Add(value, new KeyValuePair<string, object?>(key, tag));
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Add(Counter<long> counter, long value, in TagList tags)
        {
            if (!Enabled)
                return;

            try
            {
                counter.Add(value, in tags);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Add(UpDownCounter<long> counter, long value)
        {
            if (!Enabled)
                return;

            try
            {
                counter.Add(value);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Record(Histogram<double> histogram, double value)
        {
            if (!Enabled)
                return;

            try
            {
                histogram.Record(value);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Record(Histogram<double> histogram, double value, string key, object? tag)
        {
            if (!Enabled)
                return;

            try
            {
                histogram.Record(value, new KeyValuePair<string, object?>(key, tag));
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Record(Histogram<double> histogram, double value, in TagList tags)
        {
            if (!Enabled)
                return;

            try
            {
                histogram.Record(value, in tags);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Record(Histogram<long> histogram, long value)
        {
            if (!Enabled)
                return;

            try
            {
                histogram.Record(value);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void RecordError(string component, Exception error)
        {
            TagList tags = new TagList();
            tags.Add(TuiKitTelemetryNames.AttrComponent, component);
            tags.Add(TuiKitTelemetryNames.AttrErrorType, ErrorType(error));
            Add(Errors, 1, in tags);
        }

        internal static void RecordIntegration(string service, string operation, string outcome, string? errorType, double seconds)
        {
            TagList tags = new TagList();
            tags.Add(TuiKitTelemetryNames.AttrIntegrationService, service);
            tags.Add(TuiKitTelemetryNames.AttrIntegrationOperation, operation);
            tags.Add(TuiKitTelemetryNames.AttrOutcome, outcome);
            Record(IntegrationDuration, seconds, in tags);
            if (errorType != null)
                tags.Add(TuiKitTelemetryNames.AttrErrorType, errorType);
            Add(IntegrationRequests, 1, in tags);
        }

        internal static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            if (!Enabled)
                return null;

            try
            {
                return TuiKitTelemetry.ActivitySource.StartActivity(name, kind);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
                return null;
            }
        }

        internal static Activity? StartActivity(string name, ActivityKind kind, ActivityContext parent)
        {
            if (!Enabled)
                return null;

            try
            {
                return TuiKitTelemetry.ActivitySource.StartActivity(name, kind, parent);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
                return null;
            }
        }

        // Starts an activity without making it Activity.Current on the calling thread. Used for spans
        // whose lifetime does not follow the caller's stack (a modal lives across many frames).
        internal static Activity? StartDetachedActivity(string name)
        {
            Activity? previous = Activity.Current;
            Activity? started = StartActivity(name);
            if (started != null)
                Activity.Current = previous;
            return started;
        }

        internal static void SetTag(Activity? activity, string key, object? value)
        {
            if (activity == null)
                return;

            try
            {
                activity.SetTag(key, value);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void MarkOk(Activity? activity)
        {
            if (activity == null)
                return;

            try
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void MarkError(Activity? activity, Exception error)
        {
            if (activity == null)
                return;

            try
            {
                activity.SetStatus(ActivityStatusCode.Error, error.Message);
                activity.SetTag(TuiKitTelemetryNames.AttrErrorType, ErrorType(error));
                ActivityTagsCollection eventTags = new ActivityTagsCollection();
                eventTags.Add("exception.type", ErrorType(error));
                eventTags.Add("exception.message", error.Message);
                eventTags.Add("exception.stacktrace", error.ToString());
                activity.AddEvent(new ActivityEvent("exception", default(DateTimeOffset), eventTags));
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void MarkError(Activity? activity, string errorType, string description)
        {
            if (activity == null)
                return;

            try
            {
                activity.SetStatus(ActivityStatusCode.Error, description);
                activity.SetTag(TuiKitTelemetryNames.AttrErrorType, errorType);
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void Stop(Activity? activity)
        {
            if (activity == null)
                return;

            try
            {
                activity.Dispose();
            }
            catch (Exception ex) when (IsListenerFault(ex))
            {
            }
        }

        internal static void MarkFrameSuccess()
        {
            long unixMs = (DateTime.UtcNow.Ticks - UnixEpochTicks) / TimeSpan.TicksPerMillisecond;
            Volatile.Write(ref _LastFrameSuccessUnixMs, unixMs);
        }

        internal static void SetSessionShape(int targetFps, int columns, int rows)
        {
            Volatile.Write(ref _TargetFps, targetFps);
            Volatile.Write(ref _Columns, columns);
            Volatile.Write(ref _Rows, rows);
        }

        private const long UnixEpochTicks = 621355968000000000L;

        private static Measurement<long> ObserveBuildInfo()
        {
            return new Measurement<long>(1, new KeyValuePair<string, object?>(TuiKitTelemetryNames.AttrVersion, TuiKitLibrary.Version));
        }

        private static double ObserveLastFrameSuccess()
        {
            return Volatile.Read(ref _LastFrameSuccessUnixMs) / 1000.0;
        }

        // A listener callback is third-party code; anything it throws other than a fatal runtime
        // condition is contained here.
        private static bool IsListenerFault(Exception ex)
        {
            return !(ex is OutOfMemoryException) && !(ex is StackOverflowException) && !(ex is ThreadAbortException);
        }
    }
}
