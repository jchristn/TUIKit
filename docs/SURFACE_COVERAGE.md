# Surface-Area and Coverage Audit (Phase 19)

> **Historical snapshot.** This report captured the surface at Phase 19 (the original 12 widgets, 111 Touchstone cases). The library has since grown substantially — the widget toolkit, charts, images, reactive/animation layers, and a large battery of validation suites were added, bringing the total to **254 Touchstone cases** run identically through the console, xUnit, and NUnit runners on net8.0 and net10.0. For the authoritative, current list of test suites see `src/Test.Shared/Suites/`, and for the feature/capability status see [`../CHANGELOG.md`](../CHANGELOG.md) and [`../BUILDING_TERMINAL_APPS.md`](../BUILDING_TERMINAL_APPS.md). The methodology below still applies.

This is the closing coverage pass required by `archive/TUIKIT_PLAN.md`. It enumerates the public surface by subsystem, records what is under test, and states — with justification — what is deliberately not covered by automated headless tests.

## 1.5.0 additions (feature/v1.5.0)

The 1.5.0 work (see `archive/IMPROVEMENTS_FOCUS_HINTS_TOASTS.md`) added the public surface below, each with positive, negative, and boundary Touchstone cases run identically through the console, xUnit, and NUnit runners on net8.0 and net10.0: **845 console cases** (846 through the xUnit/NUnit wrappers), all green. Rather than growing the 1.4.0 suites, every item got its own suite, so a failure names the feature.

| New/changed public surface | Test suite |
|---|---|
| `FocusFrame.ContentRect`/`OuterRect`/`UsesGutter`, `TitleAlignment`, `FocusFrameOptions.TitleAlignment`/`TitleInset`, `SurfaceExtensions.DrawBox` (alignment overload) | `FrameGeometry` |
| `FocusFrameOptions.FocusedGutterGlyph`/`AsciiFocusedGutterGlyph`/`UnfocusedGutterGlyph`/`AsciiUnfocusedGutterGlyph`/`MinimumGutterWidth`, `FocusFrame.ApplyNarrowFocus` | `GutterFallback` |
| `JoinMode`, `SurfaceExtensions.DrawJoinedBox` (mode overloads), `FocusFrameOptions.FocusedJoinMode`/`UnfocusedJoinMode` | `WholeFocusedFrame` |
| `DialogModal.FocusedBorderStyle`/`FocusedTitleStyle`/`UseThemeFocusStyles`/`ApplyFocusTheme` | `DialogFocusStyle` |
| `IFocusStop`, `FocusScope(bool)`, `FocusScope.RepairFocus`/`AutoRepair`/`IsTabStop`, `TuiApplication.AutoRepairFocus` | `FocusRepair` |
| `ISharedTerminalBackend`, `HeadlessBackend.ClaimsTerminal`, fresh `CurrentFocusPath` after a frame | `HeadlessHost` |
| `TailFollow.OnContentRemoved`/`OnJumpedAway`, `PaneLineHandle.Remove` lowering the count | `TailFollowShrink` |
| `NotificationAction.Key`, `NotificationOptions`, `CoalesceMatch`, `NotificationCenter.CoalesceBy`/`ShowSeverityLabels`/`SeverityLabels`/`InvokeLatestAction`/`TopOffset`, `Notification.CoalesceKey`, `TuiApplication.Notify(options)`/`InvokeLatestNotificationAction` | `ToastKey` |
| `FramedStack`, `StackSize`, `StackSizeKind` | `FramedStack` |
| `TabStrip`, `TabView.TabPrefix`/`TabSuffix`/`TabFocusPrefix`/`TabFocusSuffix` | `TabStrip` |
| `ITextEntryKeys`, `KeyHint.WorksWhileTyping`/`TypingAlternative`/`WhileTyping`/`WithTypingAlternative`, `IKeyHintSourceOptions`, `KeyHintOrder`, `StatusBar.RightText`/`RightTextStyle`/`ReservedHint`, `TextField`/`TextEditor` `ConsumesChord`/`LeaveHint` | `TypingHints` |

## 1.4.0 additions (feature/v1.4.0)

The 1.4.0 usability work (see `archive/IMPROVEMENTS_FROM_ARMADA.md`) added the public surface below. Each item has positive and negative Touchstone cases, run identically through the console, xUnit, and NUnit runners on net8.0 and net10.0: **776 console cases** (777 through the xUnit/NUnit wrappers), all green. Line coverage of the `TUIKit` assembly measured with coverlet on net10.0 is **81.6%** (`Test.Shared` 99.4%).

| New/changed public surface | Test suite |
|---|---|
| `MouseSequenceEncoder`, `HeadlessBackend.FeedMouse`/`FeedClick`/`FeedDoubleClick`/`FeedWheel`/`FeedMove`/`FeedDrag`, `WidgetTester.Mouse`/`Click`/`DoubleClick`/`Wheel`/`Move`/`Drag`/`CellAt`/`LastMouseHandled`, `TuiApplication.CaptureFrame`, `TerminalRenderer.CopyLastFrame` | `HeadlessMouse` |
| `NotificationCenter.CoalesceRepeats`/`RepeatSuffixFormat`, `Notification.RepeatCount`/`LastRaisedAtMilliseconds`, `tuikit.notifications.coalesced` | `NotificationCoalescing` |
| `FocusFrame`, `FocusFrameOptions`, `BorderStyle.AsciiHeavy`, `TuiApplication.HighlightFocusedRegion`/`FocusFrameOptions`/`CurrentFocusPath`/`FocusPathChanged`, `Region.FocusedBorder`, `RegionBuilder.WithFocusedBorder`, `FocusPath`, `IFocusPathNode`, `SplitView.ShowPaneFrames`/`FocusedFrameStyle`/`FrameStyle`/`AsciiFrames`/`FrameOptions`, `Theme.FocusBorderRole`/`FocusTitleRole`/`TabFocusedRole`/`InlineButtonHoverRole`, `SurfaceExtensions.DrawBox` (title style) | `FocusFrame` |
| `TabView.StripFocusStop`/`IsStripFocused`/`FocusedTabStyle`/`TabFocusMarker`/`FocusedChild` | `TabFocus` |
| `KeyHint`, `IKeyHintSource`, `KeyHintResolver`, `ITextEntry`, `KeyChord.InsertsTextWhenTyping`, `StatusBar.HintSource`, `TuiApplication.BindKeyHints`, widget hint sources | `KeyHints` |
| `TailFollow`, `TailFollowMode`, `Pane.TailFollow` and its indicator, `ListView.TailFollow`/`Append`/`AppendRange` | `TailFollow` |
| `ClickRegion<TAction>`, `ClickRegionMap<TAction>`, `InlineButton`, `InlineButtonStyle`, `tuikit.click_regions.invoked` | `ClickRegion` |
| `FocusAudit`, `FocusAuditOptions`, `FocusAuditResult`, `FocusAuditProblem`, `FocusAuditProblemKind` | `FocusAudit` |
| `IReadableSurface`, `SurfaceExtensions.DrawJoinedBox`, `FocusFrameOptions.JoinBorders`, `TuiApplication.JoinRegionBorders`/`GetRegionBounds`/`GetRegionContentBounds`, `Modal.IsTopmost`, `DialogModal.Border`/`FocusedBorder`/`ContentBounds`/`FrameBounds`, `IHideable`, `FocusAuditProblemKind.InvisibleStop` | `FocusFollowUp` |

## 0.6.0 additions (feature/v0.6.0)

The 0.6.0 horizontal-components work (see `IMPROVEMENTS_FOR_MUX.md`) added the public surface below, each covered by a dedicated Touchstone suite with positive and negative cases and run identically through the console, xUnit, and NUnit runners on net8.0 and net10.0. The total stands at **363 console cases** (364 through the xUnit/NUnit wrappers).

| New/changed public surface | Test suite |
|---|---|
| `Region.Background` / `BackgroundRole`, `RegionBuilder.Background`/`BackgroundRole`/`NoBackground`, `Theme.SidebarRole`/`StatusBarRole` | `RegionBackground` |
| `DialogModal` base | `DialogModal` |
| `CheckList<T>`, `MultiSelectModal<T>` | `MultiSelect` |
| `ListView<T>`, `FuzzyList<T>` (generic) | `GenericList` (+ existing widget/modal suites updated) |
| `ActionListView<T>`, `ListAction<T>`, `ReorderableList<T>` | `ListEditing` |
| `DefinitionList`, `DefinitionRow`, `ActivityIndicator` | `Panels` |
| `StreamingTranscript` | `StreamingTranscript` |
| `Command`, `CommandRegistry` | `CommandRegistry` |
| `ISuggestionProvider`, `PrefixSuggestionProvider`, `AutocompleteOverlay` | `Autocomplete` |
| `IScrollExtent`, `ScrollView.AutoScrollToFocus`/`EnsureVisible`, `Form` scroll-focus + `Clear`/`SetFocusedField`, `FocusManager.Clear` | `ScrollForm` |
| `HintText`, `ColumnFormatter`, `Rule`, `SubmitKeyResolver`/`SubmitDecision` | `Utilities` |

Not separately covered: `KeyLabel`/`KeyChord.ToLabel` OS-adaptive labels were audited (T4-5) and were already complete with coverage in the `Input` suite. The dedicated `--gallery` example mode remains a documentation/demo enhancement (the guided tour demonstrates the new widgets); it does not affect library coverage.

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
| Charts | `BarChart`, `LineChart`, `BrailleCanvas`, `BoxPlotChart` + `BoxSummary`, `Histogram`, `HeatMap` | ChartsIconsColor, DistributionCharts |
| Theming | `Theme` | Hosting, Coverage |
| Hosting | `TuiApplication` | Hosting |
| Diagnostics | `FrameStats`, `InputRecording`, `DebugOverlay` | Diagnostics, Coverage |
| Testing | `Snapshot` | Diagnostics |

## Deliberately excluded from headless tests (justified)

These paths cannot be exercised by a deterministic in-memory test and are verified by manual smoke testing on real terminals instead (see the CI matrix in `CONFORMANCE.md`).

- **`ConsoleBackend`, `NativeConsole`, and `PosixTerminal`.** Raw-mode setup (`SetConsoleMode` on Windows / libc `termios` on Unix), the background stdin reader thread, and real console sizing require an attached TTY. The behavior is isolated behind `ITerminalBackend`; every consumer of it is tested through `HeadlessBackend`.
- **`TuiApplication.RunAsync` loop body and interactive Start/Stop escape emission.** The loop's timing and the alternate-screen enter/leave sequences run only against an interactive backend. The composition, input dispatch, command routing, Ctrl+C policy, and non-TTY line mode that the loop drives are all covered by calling `PumpInputOnce`/`RenderOnce` directly against a headless backend (Hosting suite).
- **A few defensive branches** (out-of-range guards, rarely hit fallbacks) are present for robustness and are not all individually asserted.

## Result

Every public type has at least one exercising test, and the aspiration toward full coverage is met except for the platform- and interactivity-bound code enumerated above, which is covered by manual smoke testing rather than automated headless tests. No public capability ships wholly untested.
