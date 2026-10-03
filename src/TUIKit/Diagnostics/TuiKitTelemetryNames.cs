namespace TUIKit.Diagnostics
{
    /// <summary>
    /// The complete, stable vocabulary of TUIKit's telemetry: the meter and activity source names, every
    /// metric instrument name, every span name, and every attribute (label) key and well-known value.
    /// These strings are public contract consumed by collectors and dashboards; they change only in a
    /// major release. All members are compile-time constants and therefore thread-safe.
    /// </summary>
    /// <remarks>
    /// Instrument names follow OpenTelemetry conventions (dotted, lower case). A Prometheus exporter
    /// rewrites them to snake case with a unit suffix, for example <c>tuikit.frame.duration</c> becomes
    /// <c>tuikit_frame_duration_seconds</c>. Every metric label is bounded; identifiers and free-form text
    /// appear only as span attributes.
    /// </remarks>
    public static class TuiKitTelemetryNames
    {
        /// <summary>The name of the <see cref="System.Diagnostics.Metrics.Meter"/> TUIKit emits on.</summary>
        public const string MeterName = "TUIKit";

        /// <summary>The name of the <see cref="System.Diagnostics.ActivitySource"/> TUIKit emits on.</summary>
        public const string ActivitySourceName = "TUIKit";

        /// <summary>Histogram (s): wall time of one rendered frame, labeled by <see cref="AttrFrameOutcome"/>.</summary>
        public const string FrameDuration = "tuikit.frame.duration";

        /// <summary>Counter ({frame}): frames rendered, labeled by <see cref="AttrFrameOutcome"/>.</summary>
        public const string Frames = "tuikit.frames";

        /// <summary>Histogram (s): per-stage frame time, labeled by <see cref="AttrStage"/> and <see cref="AttrOutcome"/>.</summary>
        public const string FrameStageDuration = "tuikit.frame.stage.duration";

        /// <summary>Counter ({run}): per-stage executions, labeled by <see cref="AttrStage"/> and <see cref="AttrOutcome"/>.</summary>
        public const string FrameStageRuns = "tuikit.frame.stage.runs";

        /// <summary>Histogram ({char}): characters of escape-sequence output written by one emitted frame.</summary>
        public const string FrameOutputSize = "tuikit.frame.output.size";

        /// <summary>Histogram ({row}): screen rows repainted by one frame.</summary>
        public const string FrameRowsRepainted = "tuikit.frame.rows.repainted";

        /// <summary>Observable gauge (s): Unix time of the last frame that completed without error; 0 before the first.</summary>
        public const string FrameLastSuccessTime = "tuikit.frame.last_success.time";

        /// <summary>Counter (By): raw input bytes read from the terminal backend.</summary>
        public const string InputBytes = "tuikit.input.bytes";

        /// <summary>Counter ({event}): decoded input events dispatched, labeled by <see cref="AttrInputKind"/>.</summary>
        public const string InputEvents = "tuikit.input.events";

        /// <summary>Counter ({event}): pointer-move events collapsed into a later move before dispatch.</summary>
        public const string InputCoalesced = "tuikit.input.coalesced";

        /// <summary>Histogram (s): time to dispatch one input event, labeled by <see cref="AttrInputKind"/>, <see cref="AttrOutcome"/>, and <see cref="AttrErrorType"/> on failure.</summary>
        public const string InputDispatchDuration = "tuikit.input.dispatch.duration";

        /// <summary>Counter ({key}): where each key press was routed, labeled by <see cref="AttrKeyRoute"/>.</summary>
        public const string KeyRoutes = "tuikit.input.key.routes";

        /// <summary>Counter ({invocation}): command handler invocations, labeled by <see cref="AttrOutcome"/> and <see cref="AttrErrorType"/> on failure.</summary>
        public const string CommandInvocations = "tuikit.command.invocations";

        /// <summary>Histogram (s): command handler run time, labeled by <see cref="AttrOutcome"/>.</summary>
        public const string CommandDuration = "tuikit.command.duration";

        /// <summary>Counter ({action}): actions queued with <c>TuiApplication.Post</c>.</summary>
        public const string PostEnqueued = "tuikit.post.enqueued";

        /// <summary>Up-down counter ({action}): actions currently waiting in the post queue.</summary>
        public const string PostQueueDepth = "tuikit.post.queue.depth";

        /// <summary>Histogram (s): time an action waited in the post queue before running (the "queued" stage).</summary>
        public const string PostQueueWait = "tuikit.post.queue.wait";

        /// <summary>Histogram (s): run time of a posted action, labeled by <see cref="AttrOutcome"/> and <see cref="AttrErrorType"/> on failure.</summary>
        public const string PostDuration = "tuikit.post.duration";

        /// <summary>Counter ({modal}): modals pushed, labeled by <see cref="AttrModalType"/>.</summary>
        public const string ModalShown = "tuikit.modal.shown";

        /// <summary>Up-down counter ({modal}): modals currently on a modal stack.</summary>
        public const string ModalActive = "tuikit.modal.active";

        /// <summary>Histogram (s): time a modal stayed open, labeled by <see cref="AttrModalType"/> and <see cref="AttrModalOutcome"/>.</summary>
        public const string ModalDuration = "tuikit.modal.duration";

        /// <summary>Counter ({notification}): toast notifications raised, labeled by <see cref="AttrSeverity"/>.</summary>
        public const string Notifications = "tuikit.notifications";

        /// <summary>Counter ({notification}): notifications evicted early because the concurrency cap was reached.</summary>
        public const string NotificationsEvicted = "tuikit.notifications.evicted";

        /// <summary>Counter ({session}): session start attempts, labeled by <see cref="AttrOutcome"/> (<see cref="OutcomeOk"/> or <see cref="OutcomeRejected"/>).</summary>
        public const string SessionStarts = "tuikit.session.starts";

        /// <summary>Up-down counter ({session}): terminal sessions currently started.</summary>
        public const string SessionsActive = "tuikit.sessions.active";

        /// <summary>Histogram (s): session lifetime from start to teardown.</summary>
        public const string SessionDuration = "tuikit.session.duration";

        /// <summary>Histogram (s): time the terminal was handed to an external program, labeled by <see cref="AttrOutcome"/>.</summary>
        public const string SuspendDuration = "tuikit.suspend.duration";

        /// <summary>Counter ({request}): outbound integration calls, labeled by <see cref="AttrIntegrationService"/>, <see cref="AttrIntegrationOperation"/>, <see cref="AttrOutcome"/>, and <see cref="AttrErrorType"/> on failure.</summary>
        public const string IntegrationRequests = "tuikit.integration.requests";

        /// <summary>Histogram (s): outbound integration latency, labeled by <see cref="AttrIntegrationService"/>, <see cref="AttrIntegrationOperation"/>, and <see cref="AttrOutcome"/>.</summary>
        public const string IntegrationDuration = "tuikit.integration.duration";

        /// <summary>Counter ({load}): FIGlet font loads, labeled by <see cref="AttrOutcome"/>.</summary>
        public const string FontLoads = "tuikit.font.loads";

        /// <summary>Histogram (s): FIGlet font parse time, labeled by <see cref="AttrOutcome"/>.</summary>
        public const string FontLoadDuration = "tuikit.font.load.duration";

        /// <summary>Counter ({write}): OSC 52 clipboard writes issued by the mouse text-selection layer.</summary>
        public const string ClipboardWrites = "tuikit.clipboard.writes";

        /// <summary>Counter ({error}): failures observed by TUIKit (including ones it recovers from), labeled by <see cref="AttrComponent"/> and <see cref="AttrErrorType"/>.</summary>
        public const string Errors = "tuikit.errors";

        /// <summary>Observable gauge ({info}): always 1, labeled by <see cref="AttrVersion"/>.</summary>
        public const string BuildInfo = "tuikit.build.info";

        /// <summary>Observable gauge ({frame}/s): the configured target frame rate of the active session; 0 when none.</summary>
        public const string ConfigTargetFps = "tuikit.config.target_fps";

        /// <summary>Observable gauge ({cell}): the terminal width of the active session; 0 when none.</summary>
        public const string TerminalColumns = "tuikit.terminal.columns";

        /// <summary>Observable gauge ({cell}): the terminal height of the active session; 0 when none.</summary>
        public const string TerminalRows = "tuikit.terminal.rows";

        /// <summary>Span: one rendered frame (emitted only when <see cref="TuiKitTelemetry.TraceFrames"/> is true).</summary>
        public const string SpanFrame = "tuikit.frame";

        /// <summary>Span name prefix for a frame stage; the full name is <c>stage:compose</c>, <c>stage:diff</c>, or <c>stage:write</c>.</summary>
        public const string SpanStagePrefix = "stage:";

        /// <summary>Span: one command handler invocation.</summary>
        public const string SpanCommand = "tuikit.command";

        /// <summary>Span: one action drained from the post queue, parented to the context that posted it.</summary>
        public const string SpanPost = "tuikit.post";

        /// <summary>Span: the life of one modal, from push to removal.</summary>
        public const string SpanModal = "tuikit.modal";

        /// <summary>Span: one terminal suspension (<c>TuiApplication.SuspendAsync</c>).</summary>
        public const string SpanSuspend = "tuikit.suspend";

        /// <summary>Span: one FIGlet font load.</summary>
        public const string SpanFontLoad = "font load";

        /// <summary>Stage value: running the composition callback that paints the back buffer.</summary>
        public const string StageCompose = "compose";

        /// <summary>Stage value: diffing the back buffer against the screen and building escape output.</summary>
        public const string StageDiff = "diff";

        /// <summary>Stage value: writing and flushing the frame to the terminal backend.</summary>
        public const string StageWrite = "write";

        /// <summary>Integration service value: the host clipboard tool (pbpaste, xclip, xsel, PowerShell).</summary>
        public const string ServiceClipboard = "clipboard";

        /// <summary>Integration service value: the local file system used by the file browser and pickers.</summary>
        public const string ServiceFileSystem = "filesystem";

        /// <summary>Integration operation value: reading the clipboard.</summary>
        public const string OperationRead = "read";

        /// <summary>Integration operation value: enumerating file-system roots.</summary>
        public const string OperationGetRoots = "get_roots";

        /// <summary>Integration operation value: listing a directory.</summary>
        public const string OperationGetChildren = "get_children";

        /// <summary>Component value: the frame renderer.</summary>
        public const string ComponentRender = "render";

        /// <summary>Component value: input dispatch.</summary>
        public const string ComponentInput = "input";

        /// <summary>Component value: command handlers.</summary>
        public const string ComponentCommand = "command";

        /// <summary>Component value: posted actions.</summary>
        public const string ComponentPost = "post";

        /// <summary>Component value: session teardown (terminal restore).</summary>
        public const string ComponentTeardown = "teardown";

        /// <summary>Component value: terminal suspension.</summary>
        public const string ComponentSuspend = "suspend";

        /// <summary>Component value: outbound integrations.</summary>
        public const string ComponentIntegration = "integration";

        /// <summary>Component value: FIGlet font loading.</summary>
        public const string ComponentFont = "font";

        /// <summary>Attribute key: generic operation outcome.</summary>
        public const string AttrOutcome = "outcome";

        /// <summary>Attribute key: frame outcome (<see cref="FrameEmitted"/>, <see cref="FrameUnchanged"/>, <see cref="FrameLineMode"/>, or <see cref="OutcomeError"/>).</summary>
        public const string AttrFrameOutcome = "tuikit.frame.outcome";

        /// <summary>Attribute key: frame stage name.</summary>
        public const string AttrStage = "tuikit.stage";

        /// <summary>Attribute key: input event kind (<c>key</c>, <c>mouse</c>, <c>paste</c>, <c>focus_gained</c>, <c>focus_lost</c>).</summary>
        public const string AttrInputKind = "tuikit.input.kind";

        /// <summary>Attribute key: key routing decision.</summary>
        public const string AttrKeyRoute = "tuikit.key.route";

        /// <summary>Span attribute key: the command id (span only; never a metric label).</summary>
        public const string AttrCommandId = "tuikit.command.id";

        /// <summary>Attribute key: the CLR type name of a modal (bounded by the modal classes an application defines).</summary>
        public const string AttrModalType = "tuikit.modal.type";

        /// <summary>Attribute key: how a modal closed (<see cref="ModalCompleted"/> with a result, or <see cref="ModalDismissed"/> with none).</summary>
        public const string AttrModalOutcome = "tuikit.modal.outcome";

        /// <summary>Attribute key: notification severity.</summary>
        public const string AttrSeverity = "tuikit.notification.severity";

        /// <summary>Attribute key: integration service.</summary>
        public const string AttrIntegrationService = "tuikit.integration.service";

        /// <summary>Attribute key: integration operation.</summary>
        public const string AttrIntegrationOperation = "tuikit.integration.operation";

        /// <summary>Attribute key: the component that observed a failure.</summary>
        public const string AttrComponent = "tuikit.component";

        /// <summary>Attribute key (OpenTelemetry semantic convention): the exception type's full name.</summary>
        public const string AttrErrorType = "error.type";

        /// <summary>Attribute key: the TUIKit library version.</summary>
        public const string AttrVersion = "tuikit.version";

        /// <summary>Span attribute key (OpenTelemetry semantic convention): the subprocess executable name.</summary>
        public const string AttrProcessExecutable = "process.executable.name";

        /// <summary>Span attribute key: the number of entries a file-system listing returned.</summary>
        public const string AttrResultCount = "tuikit.result.count";

        /// <summary>Span attribute key: the registered font name.</summary>
        public const string AttrFontName = "tuikit.font.name";

        /// <summary>Span attribute key: terminal size as <c>columns x rows</c> on frame spans.</summary>
        public const string AttrTerminalSize = "tuikit.terminal.size";

        /// <summary>Outcome value: the operation succeeded.</summary>
        public const string OutcomeOk = "ok";

        /// <summary>Outcome value: the operation threw or failed.</summary>
        public const string OutcomeError = "error";

        /// <summary>Outcome value: the operation timed out.</summary>
        public const string OutcomeTimeout = "timeout";

        /// <summary>Outcome value: a session start was rejected because another session was running.</summary>
        public const string OutcomeRejected = "rejected";

        /// <summary>Outcome value: a resolved command id had no registered handler.</summary>
        public const string OutcomeUnregistered = "unregistered";

        /// <summary>Frame outcome value: the frame produced output that was written to the terminal.</summary>
        public const string FrameEmitted = "emitted";

        /// <summary>Frame outcome value: the frame matched the screen and nothing was written.</summary>
        public const string FrameUnchanged = "unchanged";

        /// <summary>Frame outcome value: a non-interactive backend appended plain pane lines instead of a frame.</summary>
        public const string FrameLineMode = "line_mode";

        /// <summary>Modal outcome value: the modal closed with a non-null result.</summary>
        public const string ModalCompleted = "completed";

        /// <summary>Modal outcome value: the modal closed with a null result (cancel or dismiss).</summary>
        public const string ModalDismissed = "dismissed";
    }
}
