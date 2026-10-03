# TUIKit Telemetry

TUIKit is a **library**, so it follows the library half of the telemetry standard: it emits metrics
and traces through the BCL `System.Diagnostics.Metrics.Meter` and `System.Diagnostics.ActivitySource`
APIs and **never** takes a dependency on an exporter, SDK, or collector. It opens no sockets and writes
nothing to the console. Your host decides whether anything listens. With no listener attached, every
recording is a near-zero-cost no-op. Instrumentation is best-effort: a listener that throws is
contained, and a telemetry failure never changes rendering, input, or command behavior.

| Contract | Value |
| --- | --- |
| Meter name | `TUIKit` (`TuiKitTelemetryNames.MeterName`) |
| ActivitySource name | `TUIKit` (`TuiKitTelemetryNames.ActivitySourceName`) |
| Meter/source version | the TUIKit assembly informational version |
| Names catalog (code) | `TUIKit.Diagnostics.TuiKitTelemetryNames` - every meter, metric, span, attribute key, and well-known value |
| Entry point | `TUIKit.Diagnostics.TuiKitTelemetry` (`Meter`, `ActivitySource`, `Enabled`, `TraceFrames`) |

These names are public contract. They change only in a major release.

---

## Table of Contents

1. [Enabling and subscribing](#enabling-and-subscribing)
2. [Configuration](#configuration)
3. [Metrics catalog](#metrics-catalog)
4. [Spans catalog](#spans-catalog)
5. [Label values](#label-values)
6. [What each signal answers](#what-each-signal-answers)
7. [Recommended PromQL alerts](#recommended-promql-alerts)
8. [Suggested dashboards](#suggested-dashboards)
9. [Cardinality, privacy, and cost](#cardinality-privacy-and-cost)
10. [Testing](#testing)

---

## Enabling and subscribing

Telemetry is on by default. To collect it, subscribe the host's pipeline to the `TUIKit` meter and
activity source.

**Radiant** (recommended for .NET hosts in this family; add `Radiant` from NuGet to your *application*,
not to TUIKit):

```csharp
RadiantSettings settings = new RadiantSettings("my-tui-app");
settings.Sources.AddMeter("TUIKit");
settings.Sources.AddActivitySource("TUIKit");

using (RadiantHost host = RadiantHost.Start(settings))
{
    await TuiApp.RunAsync(app => { /* build the UI */ }, cancellationToken);
}
```

**OpenTelemetry .NET SDK:**

```csharp
using MeterProvider meters = Sdk.CreateMeterProviderBuilder()
    .AddMeter(TuiKitTelemetryNames.MeterName)
    .AddOtlpExporter()
    .Build();

using TracerProvider traces = Sdk.CreateTracerProviderBuilder()
    .AddSource(TuiKitTelemetryNames.ActivitySourceName)
    .AddOtlpExporter()
    .Build();
```

**Raw BCL listener** (tests, custom sinks): a `MeterListener` that enables instruments whose
`Meter.Name == "TUIKit"`, and an `ActivityListener` whose `ShouldListenTo` matches
`source.Name == "TUIKit"`. `src/Test.Shared/TelemetryCapture.cs` is a complete example.

Remember that a full-screen TUI owns stdout: export over OTLP/HTTP or to a file, never to a console
exporter.

Runtime metrics (GC, thread pool, allocations) are a host concern. Radiant emits them, or add the
standard `OpenTelemetry.Instrumentation.Runtime`. TUIKit does not duplicate them.

## Configuration

All settings are static properties on `TUIKit.Diagnostics.TuiKitTelemetry` and are thread-safe.

| Property | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. `false` records no metric and starts no span, even with a listener attached. Observable gauges still report. |
| `TraceFrames` | `false` | When `true`, every rendered frame opens a `tuikit.frame` span with `stage:compose`, `stage:diff`, and `stage:write` children. Off by default because a loop renders up to `TargetFps` frames per second. Frame timing is always available from the histograms. Turn it on while profiling, ideally with a sampling tracer. |

TUIKit has no endpoint, port, or credential settings. Those belong to the host's exporter. Following
the repository standard, use `127.0.0.1` rather than `localhost` for local OTLP endpoints.

## Metrics catalog

Units are UCUM. A Prometheus exporter rewrites names to snake case with a unit suffix. For example,
`tuikit.frame.duration` becomes `tuikit_frame_duration_seconds` and `tuikit.frames` becomes
`tuikit_frames_total`. Histograms carry raw measurements. Derive p50/p95/p99 from buckets in the
backend; TUIKit computes no quantiles in-process.

### Render pipeline

| Name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `tuikit.frame.duration` | Histogram | `s` | `tuikit.frame.outcome` | Wall time of one frame (compose + diff + write). |
| `tuikit.frames` | Counter | `{frame}` | `tuikit.frame.outcome` | Frames rendered, by outcome. |
| `tuikit.frame.stage.duration` | Histogram | `s` | `tuikit.stage`, `outcome` | Per-stage time: `compose` (widgets paint the back buffer), `diff` (compare with the screen, build escapes), `write` (write + flush to the terminal). |
| `tuikit.frame.stage.runs` | Counter | `{run}` | `tuikit.stage`, `outcome` | Per-stage executions, by outcome. |
| `tuikit.frame.rows.repainted` | Histogram | `{row}` | - | Rows repainted by one frame. |
| `tuikit.frame.output.size` | Histogram | `{char}` | - | Escape-sequence characters written by one emitted frame. |
| `tuikit.frame.last_success.time` | Observable gauge | `s` | - | Unix time of the last frame that completed without error (0 before the first). |

### Input and commands

| Name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `tuikit.input.bytes` | Counter | `By` | - | Raw bytes read from the terminal. |
| `tuikit.input.events` | Counter | `{event}` | `tuikit.input.kind` | Decoded events dispatched. |
| `tuikit.input.coalesced` | Counter | `{event}` | - | Pointer moves collapsed into a later move (any-motion tracking). |
| `tuikit.input.dispatch.duration` | Histogram | `s` | `tuikit.input.kind`, `outcome`, `error.type` (on error) | Time to dispatch one event, including any handler it triggers. |
| `tuikit.input.key.routes` | Counter | `{key}` | `tuikit.key.route` | Where each key press went in the routing chain. |
| `tuikit.command.invocations` | Counter | `{invocation}` | `outcome`, `error.type` (on error) | Command/binding handler invocations. |
| `tuikit.command.duration` | Histogram | `s` | `outcome` | Command handler run time. |

### Cross-thread post queue

| Name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `tuikit.post.enqueued` | Counter | `{action}` | - | Actions queued with `TuiApplication.Post`. |
| `tuikit.post.queue.depth` | UpDownCounter | `{action}` | - | Actions waiting for the loop thread. |
| `tuikit.post.queue.wait` | Histogram | `s` | - | Time an action waited in the queue (the "queued" stage). |
| `tuikit.post.duration` | Histogram | `s` | `outcome` | Run time of a posted action. |

### Modals and notifications

| Name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `tuikit.modal.shown` | Counter | `{modal}` | `tuikit.modal.type` | Modals pushed. |
| `tuikit.modal.active` | UpDownCounter | `{modal}` | - | Modals currently open. |
| `tuikit.modal.duration` | Histogram | `s` | `tuikit.modal.type`, `tuikit.modal.outcome` | Time a modal stayed open. |
| `tuikit.notifications` | Counter | `{notification}` | `tuikit.notification.severity` | Toasts raised. |
| `tuikit.notifications.evicted` | Counter | `{notification}` | - | Toasts dropped early because `MaxConcurrent` was reached (limiter rejection). |

### Session lifecycle and configuration

| Name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `tuikit.session.starts` | Counter | `{session}` | `outcome` (`ok` / `rejected`) | Start attempts. `rejected` means another `TuiApplication` already owned the terminal. |
| `tuikit.sessions.active` | UpDownCounter | `{session}` | - | Started sessions (0 or 1). |
| `tuikit.session.duration` | Histogram | `s` | - | Session lifetime, start to teardown. |
| `tuikit.suspend.duration` | Histogram | `s` | `outcome` | Time the terminal was handed to an external program (`SuspendAsync`). |
| `tuikit.config.target_fps` | Observable gauge | `{frame}/s` | - | Target frame rate of the active session (0 when none). |
| `tuikit.terminal.columns` | Observable gauge | `{cell}` | - | Terminal width of the active session. |
| `tuikit.terminal.rows` | Observable gauge | `{cell}` | - | Terminal height of the active session. |
| `tuikit.build.info` | Observable gauge | `{info}` | `tuikit.version` | Always 1. Shows which TUIKit version is deployed. |

### Integrations, domain, and errors

| Name | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- |
| `tuikit.integration.requests` | Counter | `{request}` | `tuikit.integration.service`, `tuikit.integration.operation`, `outcome`, `error.type` (on error) | Outbound calls: the clipboard subprocess and file-system listings. |
| `tuikit.integration.duration` | Histogram | `s` | `tuikit.integration.service`, `tuikit.integration.operation`, `outcome` | Outbound call latency. |
| `tuikit.font.loads` | Counter | `{load}` | `outcome` | `FigletFontLoader.Load` calls. |
| `tuikit.font.load.duration` | Histogram | `s` | `outcome` | FIGlet parse time. |
| `tuikit.clipboard.writes` | Counter | `{write}` | - | OSC 52 copies issued by mouse text selection. |
| `tuikit.errors` | Counter | `{error}` | `tuikit.component`, `error.type` | Every failure TUIKit observes, including ones it recovers from (teardown I/O, clipboard, file-system access). |

## Spans catalog

All spans come from the `TUIKit` activity source. Status is always set explicitly (`Ok` or `Error`).
Failures add `error.type` and an OpenTelemetry `exception` event (`exception.type`,
`exception.message`, `exception.stacktrace`). A span started while `Activity.Current` is set (for
example inside a Watson request or your own job span) nests under it automatically.

| Name | Kind | When | Attributes |
| --- | --- | --- | --- |
| `tuikit.frame` | Internal | Each frame, **only when `TraceFrames = true`** | `tuikit.terminal.size`, `tuikit.frame.outcome` |
| `stage:compose` / `stage:diff` / `stage:write` | Internal | Children of `tuikit.frame` | - (widget code under `stage:compose` nests its own spans there) |
| `tuikit.command` | Internal | Each command/binding handler | `tuikit.command.id` |
| `tuikit.post` | Internal | Each drained `Post` action. **Parented to the `Activity.Current` of the thread that called `Post`**, so background work and the UI update it triggers form one trace (W3C context carried across the thread hand-off). | - |
| `tuikit.modal` | Internal | From modal push to removal. It is not made `Activity.Current`, so it never captures unrelated work. | `tuikit.modal.type`, `tuikit.modal.outcome` |
| `tuikit.suspend` | Internal | `SuspendAsync` (the external program's own spans nest under it) | - |
| `clipboard read` | Client | Each clipboard-tool subprocess attempt | `tuikit.integration.service`, `tuikit.integration.operation`, `process.executable.name` |
| `filesystem get_children` / `filesystem get_roots` | Client | `FileSystemProvider` listings | `tuikit.integration.service`, `tuikit.integration.operation`, `tuikit.result.count` (no paths) |
| `font load` | Internal | `FigletFontLoader.Load` | `tuikit.font.name` |

TUIKit deliberately opens **no session-long span** and **no per-input-event span**. A span lasting
hours exports only at exit, and per-keystroke spans would flood a trace backend. The metrics above
cover both.

## Label values

| Label | Values |
| --- | --- |
| `outcome` | `ok`, `error`, `timeout` (clipboard), `rejected` (session start), `unregistered` (command id with no handler) |
| `tuikit.frame.outcome` | `emitted`, `unchanged`, `line_mode`, `error` |
| `tuikit.stage` | `compose`, `diff`, `write` |
| `tuikit.input.kind` | `key`, `mouse`, `paste`, `focus_gained`, `focus_lost` |
| `tuikit.key.route` | `ctrl_c`, `selection_copy`, `modal`, `filter`, `sequence_command`, `scoped_command`, `widget`, `focus_traversal`, `sequence_prefix`, `global_command`, `unhandled` |
| `tuikit.modal.type` | CLR type name of the modal (bounded by the modal classes your app defines) |
| `tuikit.modal.outcome` | `completed` (non-null result), `dismissed` (null result) |
| `tuikit.notification.severity` | `info`, `success`, `warning`, `error` |
| `tuikit.integration.service` / `.operation` | `clipboard`/`read`, `filesystem`/`get_roots`, `filesystem`/`get_children` |
| `tuikit.component` | `render`, `input`, `command`, `post`, `teardown`, `suspend`, `integration`, `font` |
| `error.type` | Exception full type name, or `timeout`, `process_exit_nonzero`, `process_not_started` |

## What each signal answers

| Question | Look at |
| --- | --- |
| Is the UI janky? Which stage is slow? | p95 of `tuikit.frame.duration`, then `tuikit.frame.stage.duration` by `tuikit.stage`. A slow `compose` points at widget code, a slow `diff` at large repaints (check `rows.repainted`), and a slow `write` at a slow terminal or SSH link (check `output.size`). |
| Is the loop stuck? | `time() - tuikit_frame_last_success_time_seconds` keeps growing while `tuikit_sessions_active == 1`. |
| Is background work starving the loop? | `tuikit.post.queue.depth` rising and the `tuikit.post.queue.wait` p95. |
| Which command failed, and why? | `tuikit.command.invocations{outcome="error"}` by `error.type`, then the `tuikit.command` span (with `tuikit.command.id` and the exception event). |
| Did keys go where users expect? | `tuikit.input.key.routes`. A rising `unhandled` or `modal` share explains "my shortcut does nothing". |
| Is the clipboard or file browser failing? | `tuikit.integration.requests{outcome!="ok"}` by service, operation, and `error.type`. |
| What else went wrong quietly? | `tuikit.errors` by `tuikit.component` and `error.type`. |

## Recommended PromQL alerts

```promql
# Render loop stalled: no successful frame for 30s while a session is running.
(time() - tuikit_frame_last_success_time_seconds) > 30 and on() tuikit_sessions_active > 0

# Frame p95 above one 60 fps budget (16.7 ms) for 5 minutes.
histogram_quantile(0.95, sum by (le) (rate(tuikit_frame_duration_seconds_bucket[5m]))) > 0.0167

# Any frame errors.
sum(rate(tuikit_frames_total{tuikit_frame_outcome="error"}[5m])) > 0

# Command handlers failing.
sum by (error_type) (rate(tuikit_command_invocations_total{outcome="error"}[5m])) > 0

# Post queue backing up (loop thread starved).
max_over_time(tuikit_post_queue_depth[5m]) > 100

# Integration error ratio above 50% (clipboard tool missing, permission denied, ...).
sum by (tuikit_integration_service) (rate(tuikit_integration_requests_total{outcome!="ok"}[10m]))
  / sum by (tuikit_integration_service) (rate(tuikit_integration_requests_total[10m])) > 0.5
```

The exact label spelling depends on your exporter's translation (dots become underscores).

## Suggested dashboards

TUIKit is a library, so it ships no `compose.yaml` or Grafana provisioning. A host that runs the
standard stack should add a **TUIKit** folder split by domain, matching the metric families above:

- **Rendering:** frame rate by outcome, frame p50/p95/p99, per-stage p95, rows repainted, output size, and seconds since last success.
- **Input & Commands:** events by kind, key routes, dispatch p95, command rate and error rate by `error.type`.
- **Concurrency:** post queue depth, queue wait p95, posted-action error rate, active modals, notification evictions.
- **Integrations & Errors:** integration rate, error ratio, and p95 by service and operation; `tuikit.errors` by component.
- **Overview:** active sessions, `tuikit.build.info` version, target FPS, terminal size, and frame-error and command-error rates.

## Cardinality, privacy, and cost

- **Bounded labels only.** Command ids, font names, terminal size, and executable names appear **only
  on spans**. No metric carries ids, paths, or user text. File-system spans carry a result count, never
  a path.
- **No payloads or secrets.** Key contents, pasted text, clipboard contents, and pane text are never
  recorded.
- **Cost.** With no listener, an instrument call is a no-op and a span start returns `null`. With a
  listener, each frame records a handful of histogram points. Frame spans are opt-in.
- **Failure isolation.** Every recording path catches listener faults. A throwing `MeterListener` or
  `ActivityListener` cannot break the app; the `FaultyListener` test proves this.
  Runtime caveat: on .NET 8's inbox `System.Diagnostics.DiagnosticSource`, a `MeterListener` whose
  callback has thrown stays subscribed even after `Dispose()`. It keeps throwing, which TUIKit
  contains, and listeners registered after it stop receiving measurements. .NET 10 does not have this
  problem. Keep your own listener callbacks non-throwing.

## Testing

`src/Test.Shared/Suites/TelemetrySuite.cs` (the `Telemetry` Touchstone suite, run by
`Test.Automated`, `Test.Xunit`, and `Test.Nunit`) subscribes an in-memory
`MeterListener`/`ActivityListener` (`TelemetryCapture`) and proves emission for every category above:
frames and stages, frame spans, render failures, input/routing/commands, command failures, mouse
coalescing, post-queue context propagation and failures, modals, notifications, session lifecycle and
gauges, suspension success/failure, file-system and clipboard integrations, font loads and failures,
the no-listener path, a faulty listener, and `Enabled = false`.
