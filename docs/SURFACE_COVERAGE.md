# Surface-Area and Coverage Audit (Phase 19)

This is the closing coverage pass required by `TUIKIT_PLAN.md`. It enumerates the public surface by subsystem, records what is under test, and states — with justification — what is deliberately not covered by automated headless tests.

## Coverage snapshot

Measured with coverlet (`dotnet test src/Test.Xunit --collect:"XPlat Code Coverage"`) on net10.0:

| Assembly | Line coverage |
|---|---|
| `TUIKit` (library) | ~72% |
| `Test.Shared` (descriptors) | ~99% |
| Combined | ~80% |

111 Touchstone cases run identically through the console runner, xUnit, and NUnit.

## Public surface by subsystem, and where it is tested

| Subsystem | Public types | Test suite |
|---|---|---|
| Primitives | `Point`, `Size`, `Rect`, `Color`, `ColorKind`, `CellStyle`, `CellAttributes`, `Cell` | Geometry, ColorStyle, Coverage |
| Text | `StyledSpan`, `StyledText`, `Text`, `TextWidth`, `Grapheme`, `Graphemes` | Unicode, StyledText, Coverage |
| Buffer / surface | `CellBuffer`, `ISurface`, `BufferSurface`, `SurfaceExtensions` | BufferSurface |
| Terminal | `TerminalCapabilities`, `TerminalColorDepth`, `Ansi`, `ColorQuantizer`, `CapabilityDetector`, `HeadlessBackend` | Terminal, Coverage |
| Rendering | `TerminalRenderer` | Render |
| Layout | `Region`, `RegionBuilder`, `AxisConstraint`, `AxisMode`, `Layout`, `LayoutBuilder`, `LayoutBlockScreen` | Layout |
| Content | `Pane`, `PaneLineHandle`, `PaneBatch`, `TextWrapper`, `MarkdownRenderer`, `AnsiStripper`, `Selection`, `ClipboardWriter` | Content, Markdown, Selection, Coverage |
| Input | `KeyEvent`, `KeyChord`, `KeyCode`, `KeyModifiers`, `InputEvent`, `InputParser`, `MouseEvent`, routing table + router, `Link`, `LinkRegistry`, `LinkScanner`, `ClickSynthesizer`, policy enums | Input, MouseLink, Coverage |
| Modals | `Modal`, `ModalStack`, `MessageModal`, `Notification`, `NotificationCenter`, `NotificationSeverity` | Modal, Coverage |
| Widgets | `IWidget`, `Label`, `Gauge`, `Sparkline`, `ProgressBar`, `Spinner`, `ListView`, `Table`, `TextEditor`, `TextField`, `Checkbox`, `RadioGroup` | Widget, Coverage |
| Theming | `Theme` | Hosting, Coverage |
| Hosting | `TuiApplication` | Hosting |
| Diagnostics | `FrameStats`, `InputRecording`, `DebugOverlay` | Diagnostics, Coverage |
| Testing | `Snapshot` | Diagnostics |

## Deliberately excluded from headless tests (justified)

These paths cannot be exercised by a deterministic in-memory test and are verified by manual smoke testing on real terminals instead (see the CI matrix in `CONFORMANCE.md`).

- **`ConsoleBackend` and `NativeConsole`.** Raw-mode setup (`SetConsoleMode` / `stty`), the background stdin reader thread, and real console sizing require an attached TTY. The behavior is isolated behind `ITerminalBackend`; every consumer of it is tested through `HeadlessBackend`.
- **`TuiApplication.RunAsync` loop body and interactive Start/Stop escape emission.** The loop's timing and the alternate-screen enter/leave sequences run only against an interactive backend. The composition, input dispatch, command routing, Ctrl+C policy, and non-TTY line mode that the loop drives are all covered by calling `PumpInputOnce`/`RenderOnce` directly against a headless backend (Hosting suite).
- **A few defensive branches** (out-of-range guards, rarely hit fallbacks) are present for robustness and are not all individually asserted.

## Result

Every public type has at least one exercising test, and the aspiration toward full coverage is met except for the platform- and interactivity-bound code enumerated above, which is covered by manual smoke testing rather than automated headless tests. No public capability ships wholly untested.
