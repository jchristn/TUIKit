# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-09-19

First stable release. TUIKit graduates out of alpha: the public API is now considered stable and the
project follows [Semantic Versioning](https://semver.org/). This release also lands the
fullscreen-rendering enhancements — the terminal capabilities a full-viewport, flicker-free TUI
depends on — all of which are cross-platform across Windows, macOS, and Linux.

### Added
- **Synchronized output (DEC private mode 2026).** `Ansi.BeginSynchronizedUpdate` /
  `Ansi.EndSynchronizedUpdate` and a new `TerminalRenderer.SynchronizedOutput` flag wrap each emitted
  frame in a begin/end pair so the terminal presents the repaint atomically, eliminating tearing and
  partial-frame flicker on fast streams. Gated on a new `TerminalCapabilities.SynchronizedOutput`
  capability that `CapabilityDetector` infers for modern tier-1 terminals (Windows Terminal, iTerm2,
  WezTerm, Kitty, Ghostty, Alacritty, foot, tmux ≥ 3.4) and withholds from GNU screen. An unchanged
  (empty) frame is never wrapped, and the closing ESU is folded into every terminal-restore path
  (`TuiApplication` teardown and the `ConsoleBackend` process-exit net) so a torn frame can never
  leave the terminal in a held state. The host wires the renderer flag from the backend's capabilities
  automatically.
- **Persistent full-repaint mode.** `TerminalRenderer.ForceFullRepaint`, surfaced as
  `TuiApplication.ForceFullRepaint`, repaints every row on every frame regardless of whether content
  changed — insurance for backends that drop or corrupt incremental updates, such as some ConPTY /
  Windows Terminal configurations that leave stale cells behind. Defaults to off (incremental
  diffing); distinct from the one-shot `Invalidate()`, which forces only the next frame.
- **Cross-platform `TuiApplication.SuspendAsync(Func<Task>)`.** Hands the terminal back in its
  pristine cooked state, runs the supplied action to completion, then restores the session and forces
  a full repaint — the vim/pager shell-out pattern. Works identically on Windows, macOS, and Linux
  (unlike a Ctrl+Z / SIGTSTP job-control suspend, which does not exist on Windows). The render and
  input loops are held inert (`TuiApplication.IsSuspended`) while the external program owns the
  terminal, and the terminal is restored even if the action throws.

### Changed
- **`TerminalCapabilities` constructor gained a ninth parameter, `synchronizedOutput`.** The `Full`
  and `Minimal` presets and `CapabilityDetector` are updated accordingly. This is the only source-level
  break; everything else in this release is additive and backward compatible.

### Tests
- 17 new Touchstone cases, positive and negative, across a new **Fullscreen** suite covering the mode
  2026 sequences, capability detection, synchronized-frame wrapping (including the empty-frame and
  disabled negatives), full-repaint behavior, the host passthroughs, and `SuspendAsync` (restore/
  resume, loop-inert guard, null-argument, non-interactive, and throwing-action paths). 580 total
  across console/xUnit/NUnit on net8.0/net10.0.

## [0.13.2] - 2026-09-17

### Added
- **`TextEditor.WordWrap`.** An opt-in property (default off, so existing behavior is unchanged) that wraps long
  logical lines to the render width instead of clipping at the right edge. Wrapping breaks after spaces where
  possible and hard-breaks words longer than the width, preserving every character so the caret stays accurate.
  New `TextEditor.VisualLineCount(width)` reports the wrapped row count for hosts that grow a composer to fit its
  content; `Measure` and mouse hit-testing account for wrapping when it is on.

## [0.13.1] - 2026-09-17

### Added
- **`BoxPlotChart` vertical orientation.** `BoxPlotOrientation.Vertical` draws one column per summary (the
  default stays horizontal), better suited to compact KPI panels.

## [0.13.0] - 2026-09-16

Distribution widgets. Adds the chart widgets needed to render the *shape* of a metric — the min/avg/p95/p99/max
summary, the bucketed frequency, and two-dimensional intensity — that latency, time-to-first-token, streaming
time, and throughput are read through in practice. All three are static, dependency-free render widgets drawn with
block/line glyphs on `ISurface`, exactly like the existing charts.

### Added
- **`BoxPlotChart`** and its `BoxSummary` value — a horizontal box-and-whisker chart, one row per category, over a
  caller-supplied five-number summary (`Min`, `Low`, `Mid`, `High`, `Max`; generic names, so the host decides
  whether "box" means quartiles or a min / avg / p95 / p99 / max quintuple). All rows share one value axis so the
  boxes are comparable, with a `SetRange(min, max)` override for a fixed scale; `WhiskerColor` / `BoxColor` /
  `MidColor`, an optional bottom `ShowAxis` tick row, and an optional right-edge `ShowValues` readout. The five
  values are sorted on construction, a degenerate (all-equal) summary renders a single mid marker, and non-finite
  samples clamp on the draw path instead of throwing.
- **`Histogram`** — bins a numeric series into `BucketCount` buckets (default 10, clamped ≥ 1) and draws bucket
  frequency as vertical columns with the eighth-block ramp scaled to the tallest bucket. `SetValues` for a batch or
  `Push(value, capacity)` for a live feed (mirroring `Sparkline`), an optional `SetRange` to fix the domain,
  `BarColor`, an opt-in `ShowCounts`, and a `ComputeBucketCounts()` accessor. Empty renders nothing; all-equal
  values fall into one bucket; out-of-range samples clamp to the end buckets.
- **`HeatMap`** — a grid of intensity cells shaded by magnitude (`░▒▓█`) with optional row and column labels, a
  `SetRange` override, and `CellColor` / `LabelColor`. Empty or zero-size grids render nothing.
- Three guided-tour pages (box plot, histogram, heat map) seeded with representative latency-style data.
- 14 new Touchstone cases in a new `DistributionCharts` suite (559 total across console/xUnit/NUnit on
  net8.0/net10.0).

Additive and fully backward compatible: three new widgets and their supporting types only. No existing API changed.

## [0.12.0] - 2026-09-12

Mouse everywhere. 0.11.0 let modals receive the mouse; this release completes the pointer story across the
**widget** set — every widget that was keyboard-navigable is now also mouse-aware, so a host that routes the
mouse (region-bound widgets, and modals via 0.11.0) gets click, select, and wheel behavior for free.

### Added
- **`IMouseAware` on the remaining interactive widgets.** New implementations, each additive:
  - **`TextField`** / **`TextEditor`** — a left click places the caret at the clicked column (and row); the
    editor also scrolls on the wheel.
  - **`RadioGroup`** — click an option to select it; the wheel steps the selection.
  - **`Form`** — a left click focuses the field under the pointer and forwards the event, translated into the
    field's local coordinates, to that field's widget when it is itself mouse-aware.
  - **`CheckList<T>`** / **`CheckTree<T>`** — click selects a row and toggles its check (tri-state for the tree).
  - **`DataTable<T>`**, **`FuzzyList<T>`**, **`KeyBindingEditor`**, **`FileBrowser`** — click selects the row
    under the pointer; the wheel steps the selection.
  - **`ActionListView<T>`** / **`ReorderableList<T>`** — forward to their inner list.
  - **`Collapsible`** — click the header to expand/collapse; clicks in the body forward to the child.
  - **`ColorPicker`** — click a channel row to select it and set its value from the horizontal click position.
  - **`SplitView`** — routes a click to the pane under the pointer, forwarding it in that pane's local
    coordinates.
  - **`AutocompleteOverlay`** — click a suggestion to select it.
- A **Clickable form** guided-tour page demonstrating click-to-focus fields plus radio and checkbox selection.
- 9 new Touchstone cases in a new `WidgetMouseCoverage` suite (545 total across console/xUnit/NUnit on
  net8.0/net10.0).

Additive and fully backward compatible: these are new interface implementations and methods only. Every widget
renders and behaves exactly as before until a host forwards a mouse event, and every keyboard path is unchanged.

## [0.11.0] - 2026-09-12

Clickable modals. The mouse layer previously routed only to region-bound `IMouseAware` widgets; a modal
dialog received keys but never the pointer, so modal-heavy apps could not be driven by clicking. This
release adds a mouse hook to the modal layer.

### Added
- **`Modal.HandleMouse(MouseEvent)`** — a virtual (default no-op, returns `false`) that receives presses,
  releases, wheel, and hover in **absolute screen coordinates**. A modal renders into a full-screen surface
  and draws its own centered box, so it hit-tests clicks against the same geometry it computed in `Render`.
- **`ModalStack.HandleMouse(MouseEvent)`** — routes a mouse event to the topmost modal and prunes it if the
  click closed it, mirroring `HandleKey`/`HandlePaste`.

### Changed
- **The host traps the mouse to the active modal.** While a modal is active, `TuiApplication` offers each
  mouse event to `ModalStack.HandleMouse` before any region-bound widget and consumes it there, mirroring the
  existing key trap — so clicks land on the dialog and never leak to the interface behind it.

Additive and fully backward compatible: the new virtual defaults to unconsumed, so existing modals render and
behave identically until they override it. 4 new Touchstone cases across a new `ModalMouseRouting` suite
(536 total across console/xUnit/NUnit on net8.0/net10.0).

## [0.10.3] - 2026-09-10

Corrected package for the configurable-widget-styles release. **0.10.2 was published with a stale
assembly** — its `TUIKit.dll` was the 0.10.1 binary and did not contain the new APIs (`pack` reused
an out-of-date Release build). 0.10.3 is that same source rebuilt cleanly and repackaged; the packed
assembly was verified to report `AssemblyVersion 0.10.3.0` and to contain the new members before
publishing. **0.10.2 has been unlisted; use 0.10.3.** No source changes from the intended 0.10.2 —
the feature set below is unchanged.

## [0.10.2] - 2026-09-10 [UNLISTED]

> **Unlisted:** this package shipped a stale `TUIKit.dll` (the 0.10.1 binary) and does not contain
> the APIs described below. Superseded by [0.10.3](#0103---2026-09-10).

Developer-configurable widget styles. Several widgets previously hardcoded their colors — a fixed
palette for selection, borders, scrollbars, diff lines, menu chrome, and, most visibly, no way to
give an editor or list a background. Those colors are now exposed as settable `CellStyle`
properties. Every new property defaults to the exact style rendered before, so this is additive and
fully backward compatible: a widget renders byte-identically until a style is assigned.

### Added
- **`TextEditor.NormalStyle` and `TextField.NormalStyle`.** The base style for the surface fill and
  the text; the caret is drawn as this style with reverse video, so it inverts against whatever
  foreground/background is set. This makes the composer background controllable — for example
  `editor.NormalStyle = CellStyle.Default.WithBackground(Color.FromRgb(0x2A, 0x2A, 0x2A))` for a dark
  grey editor.
- **`Table.HeaderStyle` / `Table.RowStyle` / `Table.BorderStyle`.** Header row, data rows (and the
  surface fill), and the box-drawing border lines.
- **`DataTable<T>.HeaderStyle` and `DataTable<T>.NormalStyle`.**
- **`ListView<T>.NormalStyle`, `Tree<T>.NormalStyle`, `CheckList<T>.NormalStyle`,
  `CheckTree<T>.NormalStyle`.** A base style for unselected rows and the surface fill; each widget's
  existing selection/hover/checked styles compose over it, so a background flows through consistently.
- **`RadioGroup.NormalStyle` and `RadioGroup.SelectedStyle`.**
- **`FileBrowser.HeaderStyle` / `SelectionStyle` / `DirectoryStyle` / `NormalStyle`.**
- **`ScrollView.TrackStyle` and `ScrollView.ThumbStyle`** for the scrollbar groove and thumb.
- **`DiffView.AddedStyle` / `RemovedStyle` / `ContextStyle`** for added, removed, and context lines.
- **`MenuBar.BarStyle` / `ActiveStyle` / `ItemStyle` / `DisabledStyle` / `DropdownStyle` /
  `DropdownBorderStyle`** for the bar strip, active title and highlighted item, normal items, disabled
  items, and the drop-down panel and its border.

### Behavior
- Widgets that already painted a solid background (`TextEditor`, `TextField`, `ListView<T>`) fill it
  with `NormalStyle`; at the default this is the terminal-default background, exactly as before.
  Widgets that did **not** previously paint a solid background only paint one when a `NormalStyle`
  background is assigned, so with the default they continue to inherit the region background the host
  paints behind them.
- 28 new Touchstone cases (positive and negative), 532 total across console/xUnit/NUnit on
  net8.0/net10.0.

## [0.10.1] - 2026-09-02

Exit-path teardown hardening. A process-exit handler must never throw, but the final
terminal-restore `Flush()` on the teardown path caught only `IOException` — so if the process
exited without a clean `Stop` while stdout was already disposed or closed for writing, the
exception could escape the `AppDomain.ProcessExit` handler. This is a fallback-path robustness
fix; correct usage (a clean `Stop`/`Dispose` with stdout open) was never affected.

### Fixed
- **Teardown `Flush()` no longer throws on a closed/non-writable stream.**
  `TuiApplication.Teardown` and `ConsoleBackend.Stop` now swallow `ObjectDisposedException` and
  `NotSupportedException` in addition to `IOException` around the final restore flush, so an app
  that exits without a clean stop can't crash from the exit-handler fallback path. Normal-operation
  flushes are unchanged and still propagate errors.

## [0.10.0] - 2026-09-02

Full mouse support. TUIKit previously decoded clicks, drags, and the wheel; this release completes
the pointer story with hover (any-motion tracking, on by default, with per-frame move coalescing),
host-synthesized Enter/Leave events routed through the existing `IMouseAware` interface, horizontal
wheel, terminal focus reporting, host-stamped multi-click counts, hover visual states on a
representative widget set, link hover, and a QuickEdit fix so legacy Windows consoles stop
swallowing mouse input. The guided tour gains a **Mouse playground** page demonstrating all of it.

### Added
- **Hover tracking (DECSET 1003), on by default.** `Ansi.EnableMouse` now enables any-motion
  reporting alongside 1000/1002/1006; terminals without it degrade silently to drag-only motion.
  `Ansi.EnableAnyMotion`/`DisableAnyMotion` toggle just that mode.
- **`MouseEventKind.Enter` / `MouseEventKind.Leave`** — synthesized by the host from hit-test
  transitions (never by the parser) and delivered through `IMouseAware.HandleMouse` in a
  documented order: Leave to the old widget, Enter to the new widget, then the triggering event.
  Enter/Leave return values never swallow the triggering event.
- **`TuiApplication.MouseTrackingMode`** (`None` / `ButtonsAndDrag` / `AnyMotion`, default
  `AnyMotion`) — rewrites the terminal modes live; `MouseCaptureEnabled` still trumps it.
- **Move coalescing.** Each drained run of consecutive pointer moves collapses to its final
  position (presses/releases/wheel act as barriers), bounding hover cost to one hit-test per pump.
- **Horizontal wheel.** `MouseButton.WheelLeft`/`WheelRight` decode from SGR buttons 66/67;
  `ScrollView` maps them to horizontal scrolling.
- **Terminal focus reporting (DECSET 1004).** `CSI I`/`CSI O` decode as
  `InputEventKind.FocusGained`/`FocusLost`; `TuiApplication.TerminalFocusChanged` surfaces them,
  and focus loss clears hover with a synthetic Leave.
- **Host-stamped click counts.** `TuiApplication` now runs presses through `ClickSynthesizer`
  before routing, so widgets receive real single/double/triple `ClickCount` values.
  `ClickSynthesizer.PositionSlopCells` (default 0) tolerates pointer drift between multi-clicks.
- **Link hover.** Assign a `LinkRegistry` to `TuiApplication.Links` and the host tracks
  `HoveredLink` and raises `LinkHovered` — for underlines and status-bar URL previews.
- **Widget hover states and click activation** on the representative set: `TabView` (click
  activates a tab; hovered header styled), `MenuBar` (titles open on click, drop-down items
  hover-highlight and activate on click, outside clicks dismiss), `ListView<T>` (click selects,
  wheel steps, hovered row styled), `Tree<T>` (click selects, click-again toggles expansion, wheel
  steps, hovered node styled), and `Checkbox` (click toggles, hover emphasis) — each with a
  configurable `HoverStyle`.
- **`TerminalCapabilities.AnyMotionMouse` / `FocusReporting`** flags, detected per terminal
  (conservatively false for GNU screen and Apple Terminal).
- **Mouse playground** page in `TUIKit.Example`'s guided tour: live pointer readout, Enter/Leave
  counters, click-count and modifier display, four-axis wheel counters, drag-to-paint (triple-click
  clears), and terminal-focus logging in the Actions pane.

### Fixed
- **Legacy conhost QuickEdit.** `ConsoleBackend` clears `ENABLE_QUICK_EDIT_MODE` (with
  `ENABLE_EXTENDED_FLAGS`) on start and restores the original mode on stop, so conhost's drag
  selection no longer swallows every mouse report.
- **SGR wheel decode.** Buttons 66/67 previously mis-decoded as WheelUp/WheelDown; the two low
  bits now select the axis and direction correctly.
- **Parser hardening.** SGR reports with a final byte other than `M`/`m`, too few parameters, or
  zero (invalid one-based) wire coordinates are dropped instead of emitting bogus events.

### Mouse support by environment (as of 0.10.0)

Modes a terminal lacks are silently ignored, so every ⚠️/❌ degrades gracefully.

| Environment | Click / drag / wheel | Hover / any-motion | Horizontal wheel | Focus in/out | Coords > 223 cols |
|---|---|---|---|---|---|
| Windows Terminal (Win 10/11) | ✅ | ✅ | ✅ | ✅ | ✅ |
| Legacy conhost (Win 10+) | ⚠️ partial VT translation; QuickEdit cleared | ⚠️ | ❌ | ❌ | ✅ |
| macOS Terminal.app | ✅ | ⚠️ varies by macOS version | ❌ | ⚠️ | ✅ |
| iTerm2 / kitty / Alacritty / WezTerm / Ghostty | ✅ | ✅ | ✅ | ✅ | ✅ |
| VTE terminals (GNOME Terminal, Tilix, …) | ✅ | ✅ | ✅ | ✅ | ✅ |
| xterm | ✅ | ✅ | ✅ | ✅ | ✅ |
| tmux (`mouse on`) | ✅ | ✅ | ⚠️ version-dependent | ✅ | ✅ |
| GNU screen | ⚠️ basic tracking only | ❌ | ❌ | ❌ | ⚠️ |
| SSH / WSL | transparent — the client/hosting terminal's row applies | — | — | — | — |
| Headless / redirected / CI | ❌ by design (`IsInteractive` false; no escapes emitted) | ❌ | ❌ | ❌ | — |

Deliberately not supported: pixel-precision coordinates (DECSET 1016 — spotty support, and
TUIKit's model is a cell grid), legacy X10/1005/1015 mouse encodings (SGR 1006 is ubiquitous;
non-SGR terminals fall back to keyboard-only via `TerminalCapabilities.SgrMouse`), pointer events
outside the terminal window, cursor shape changes on hover, and pre-Windows-10 consoles (no
`ENABLE_VIRTUAL_TERMINAL_INPUT` to translate mouse input).

### Notes
- **Compatibility:** `MouseEventKind`, `MouseButton`, and `InputEventKind` gained members —
  consumer `switch` statements without a `default:` arm will now see new values. Existing
  `IMouseAware` widgets that return `false` for unrecognized kinds are unaffected; hover moves
  arrive as `Move` with `Button == None`. The `TerminalCapabilities` constructor gained two
  parameters (`anyMotionMouse`, `focusReporting`). Unconsumed hover moves flow to `MouseReceived`,
  which is now a higher-volume stream — keep handlers cheap or set
  `MouseTrackingMode.ButtonsAndDrag`.
- 44 new Touchstone cases across five new suites (`MouseProtocol`, `MouseProtocolNegative`,
  `MouseHoverRouting`, `MouseClickSynthesis`, `MouseWidget`) — positive protocol decoding,
  malformed/truncated/hostile input, host hover synthesis and coalescing, click-count stamping,
  and widget-level click/hover/wheel behavior — bringing the suite to 504 cases across all three
  runners.

## [0.9.0] - 2026-08-29

Bracketed paste now reaches the focused input. Pasting into a prompt or an inline add field was
silently dropped — the paste was decoded correctly but only application-global handlers were offered
it, while the focus-trapping modal stack that owns the text field during a prompt never saw it. So
typing worked but pasting an access key, secret key, password, or token did nothing.

### Added
- **`TextField.Insert(string)`** — inserts literal text at the caret (as produced by a bracketed
  paste) and advances the caret past the run. Control characters — CR, LF, Tab, and other C0/C1
  codes — are stripped, so a multi-line or newline-terminated clipboard payload collapses onto the
  field's single line instead of submitting the prompt or corrupting the value. `null`/empty are
  no-ops.
- **`Modal.HandlePaste(string)`** — a virtual paste hook on the modal base, defaulting to a no-op
  that reports the paste as unconsumed (the prior behavior for any modal). `PromptModal` and
  `ListEditorModal<T>` override it to insert the pasted text into their field.
- **`ModalStack.HandlePaste(string)`** — routes a paste to the topmost modal, mirroring
  `HandleKey`.

### Fixed
- **Paste into prompts and inline add fields.** `TuiApplication` now dispatches a bracketed-paste
  event the same way it dispatches keys: the active modal is offered the paste first, and the global
  `PasteReceived` event fires only when no modal is trapping focus. Previously a `Paste` event fired
  `PasteReceived` unconditionally and never reached the focused `TextField`, so Ctrl/Cmd+V into a
  prompt (for example an S3 Access key or Secret key) inserted nothing.

### Notes
- Additive and backward compatible: the new `Modal.HandlePaste` virtual defaults to the previous
  drop-the-paste behavior, and applications that subscribe to `PasteReceived` for their own focused
  widgets still receive pastes whenever no modal is active.
- 4 new Touchstone cases (the widget insert, the stack routing, the modal-trap-vs-fallback dispatch,
  and the end-to-end prompt paste) — 460 total across console/xUnit/NUnit on net8.0/net10.0.

## [0.8.4] - 2026-08-24

A reusable list editor and hierarchical file selection, plus two `Tree<T>` performance/correctness
fixes. Each piece is a composition of primitives TUIKit already owned, landed here so applications
stop re-implementing them per app.

### Added
- **`ListEditorModal<T>`** — a single-screen editor for an ordered list of `T`. Items are added
  through an inline text field that is parsed, validated, and previewed live; a rejected commit keeps
  the buffer so a mistyped value is fixed in place. The selected item can be removed, and — when
  enabled — items reorder with Alt+Up/Alt+Down. Enter finishes with a fresh `IReadOnlyList<T>`;
  Escape cancels with `null` (distinct from an empty list). Configured with `ListEditorOptions<T>`
  (parser, describe, legend, dedupe, reorder, empty policy, key chords); parse outcomes flow through
  the named `ParseResult<T>` rather than a tuple.
- **`CheckTree<T>`** (with the `CheckState` enum) — a forest of expandable nodes with cascading
  tri-state checkboxes. Checking a node makes its subtree effectively checked; unchecking a
  descendant carves a hole and marks ancestors `Partial`; effective state is inherited from the
  nearest explicit ancestor. `IncludedRoots()` and `ExcludedHoles()` derive the result; children load
  lazily and cache once per node; state is keyed through a comparer; `Space` toggles and `Enter` is
  left for the host.
- **`FileSelectModal`** (with `FileSelectOptions`, `FileSelection`, `FileExclusion`, the
  `IFileSystemProvider` seam, and the default disk-backed `FileSystemProvider`) — a bordered dialog
  wrapping `CheckTree<string>` over absolute paths. Roots default to the machine's ready drives,
  unreadable directories are tolerated, saved includes and holes are pre-seeded and revealed on open,
  and the result maps to a `FileSelection` (top-most includes plus excluded holes with a
  directory/file flag).
- **`FileBrowser.SelectionMode`** — a `FileSelectionMode` flag (None/Single/Multiple) with
  `SelectedPaths` and a `Confirmed` event, for flat multi-select within the current directory. The
  default (None) keeps the classic single-activate behavior unchanged.

### Fixed
- **`Tree<T>` no longer enumerates per render.** The children delegate is invoked at most once per
  node and cached (with `Invalidate`/`Refresh` to drop the cache); the disclosure glyph uses an
  optional cheap `hasChildren` probe instead of enumerating children on every visible row per frame.
- **`Tree<T>` expansion is keyed by a comparer.** An optional `IEqualityComparer<T>` keys all
  expansion and cache state, so regenerated nodes that compare equal keep their expansion. Both
  constructor parameters are optional and additive.

### Tests
- 38 new Touchstone cases across new `ListEditorModal`, `CheckTree`, and `FileSelectModal` suites plus
  additions to `NewWidgets`, `SplitMenuFile`, and `BackendModalValidation` — including a disk-free
  in-memory file-system provider for deterministic selection tests (456 total across console/xUnit/
  NUnit on net8.0/net10.0).

## [0.8.3] - 2026-08-21

Finishes the navigation-uniformity work from 0.8.2 by extending page/jump keys to the three small
widgets that were left out, so every navigable list and scroll widget now responds to the same keys.

### Added
- **`RadioGroup`** — Home/End jump to the first and last option.
- **`MenuBar`** — Home/End jump the highlight to the first and last item of the open drop-down.
- **`AutocompleteOverlay`** — Home/End jump to the first and last suggestion, and PageUp/PageDown move
  by a page (`MaxRows`); Up/Down keep their existing wrapping behavior.

### Tests
- 4 new Touchstone cases: RadioGroup Home/End, AutocompleteOverlay paging and Home/End, the hidden
  overlay ignoring navigation, and MenuBar Home/End (418 total across console/xUnit/NUnit on
  net8.0/net10.0).

## [0.8.2] - 2026-08-21

Page and jump navigation across the list and scroll widgets. Long lists could only be walked one
row at a time — PageUp/PageDown and Home/End were missing from most selection widgets, and the two
scroll widgets that paged had no jump-to-top/bottom.

### Added
- **`ListView` pages and jumps.** PageUp/PageDown move the selection by the last-rendered viewport
  height; Home/End select the first and last item, via new `SelectFirst`, `SelectLast`, `PageUp`, and
  `PageDown` methods. Because `SelectModal` (and therefore `TuiApplication.SelectAsync`),
  `ActionListView`, and `ReorderableList` delegate their key handling to `ListView`, all three gain
  the same keys.
- **`CheckList` paging.** PageUp/PageDown complement the existing Home/End, so `MultiSelectModal` pages
  too.
- **All four keys on the remaining selection widgets.** `FuzzyList`, `DataTable`, `Tree`,
  `FileBrowser`, and `KeyBindingEditor` now handle PageUp/PageDown (by their visible-row count) and
  Home/End (first/last item).
- **Home/End on the scroll widgets.** `ScrollView` and `DiffView` already paged; they now jump to the
  top and bottom of their content with Home and End.

### Notes
- Paging steps by the widget's viewport height as of its most recent render (at least one row), and
  every move clamps at the ends. All changes are additive and backward compatible.

### Tests
- 16 new Touchstone cases (positive and negative) covering paging and jump keys for each widget, the
  `SelectAsync`/`SelectModal` and `MultiSelectModal` routing, empty-list safety, and the
  `ReorderableList` delegation (414 total across console/xUnit/NUnit on net8.0/net10.0).

## [0.8.1] - 2026-08-21

Terminal-restore hardening. A TUIKit app that exited via Ctrl+C — or an unhandled exception — could
leave the shell wedged: the scroll wheel emitted raw SGR mouse reports (for example
`^[[<64;64;15M`) because mouse tracking was never turned off, and on Windows the arrow keys echoed
their raw escapes (`^[[A`) because the console was still in raw/VT input mode. The teardown that
undoes all of this lived only in `TuiApplication.Stop()`, which the runtime's default Ctrl+C
handling skipped.

### Fixed
- **Terminal is restored on every exit path.** `TuiApplication.Start()` now installs a cross-platform
  safety net: a `Console.CancelKeyPress` handler that cancels the runtime's abrupt process kill and
  routes Ctrl+C into a graceful stop, and an `AppDomain.ProcessExit` handler as a last-ditch restore
  for exits that bypass `Stop()` (including an unhandled exception or the graceful shutdown the
  runtime runs after Ctrl+C). Mouse reporting, bracketed paste, the enhanced-keyboard flags, the
  cursor, and the alternate screen are all restored.
- **Restore is idempotent and thread-safe.** `Stop()`, `Dispose()`, and the safety-net callbacks share
  a single guarded teardown that runs at most once per session and races safely between the run loop
  and a `ProcessExit` callback on another thread. The output writes during teardown tolerate a
  closing stream.
- **`ConsoleBackend` process-exit net now covers Windows** (previously Unix-only) and additionally
  emits `DisableMouse` and `DisableBracketedPaste` before restoring the console/termios mode, so a
  backend used without the host is protected too.

### Tests
- 3 new Touchstone cases asserting that stop disables mouse reporting and restores the cursor and
  screen, that the restore is idempotent, and that it still runs when mouse capture was disabled
  (398 total across console/xUnit/NUnit on net8.0/net10.0).

## [0.8.0] - 2026-08-19

A reusable text-to-ASCII-art component: turn a string and a named font into large, multi-row banner
art, managed through a font library so applications can offer many fonts without hand-rolling glyph
tables.

### Added
- **ASCII-art font engine (`TUIKit.Ascii`).** `AsciiArt.Render(text, font, options)` composes text
  into multi-row art using any `IAsciiFont`, implementing the FIGlet layout model: full-width,
  kerning (fitting), and smushing with all six horizontal rules (equal-character, underscore,
  hierarchy, opposite-pair, big-X, and hardblank) plus universal smushing. `AsciiArtOptions` controls
  the layout mode, alignment within a target width, blank-column trimming, and an optional max width.
- **`IAsciiFont` contract and `AsciiFontBase`.** A small font contract (name, metrics, per-character
  glyph lookup, and an async supported-characters companion) with a base class that stores glyph data
  so a concrete font supplies only data. The engine works against the interface, so third-party fonts
  are first-class.
- **`FigletFontLoader`.** Parses FIGlet `.flf` and TOIlet `.tlf` fonts (from a stream or string) into
  an `IAsciiFont`, so consumers can register their own fonts. Malformed and truncated fonts throw
  `AsciiFontException` with context.
- **`AsciiFontLibrary` manager.** A thread-safe (`ReaderWriterLockSlim`), case-insensitive font
  registry with register/try-register/unregister/get/contains/enumerate (and an async enumerate). The
  shared `Default` instance is lazily pre-populated with 84 built-in fonts, each exposed as a discrete
  class over an embedded resource — Standard, Slant, the full Small family, Doom, Colossal, Big Money
  (all four), ANSI Compact/Regular/Shadow, Sub-Zero, Varsity, and many more.
- **`AsciiArtText` widget.** A font-aware `IWidget` (the successor to `BannerText`) with a settable
  `Font`, color, and alignment; measures as tall as the font and as wide as the composed art.
- **Guided-tour demo.** A new "ASCII art fonts" tour page in `TUIKit.Example` with an interactive
  `FontGallery` that scrolls the whole font library with the Left/Right arrow keys.

### Licensing
- Every requested font was scanned for restrictive license terms; none was found, so the full
  requested set is bundled. Per-font attribution ships in `Ascii/Fonts/Data/LICENSE.figlet.txt`
  (packed into the NuGet package). The policy stands — a font with restrictive licensing would be
  excluded — and `Ascii/Fonts/Data/REMOVED.txt` records the (currently empty) exclusion list.

### Unchanged
- `Banner`, `BannerFont`, and `BannerText` are untouched; the new engine is additive.

## [0.6.0] - 2026-08-13

Horizontal component expansion: a set of general-purpose components, drawn from patterns downstream
consumers had to build by hand, plus per-region background colors.

### Added
- **Per-region background colors.** A layout `Region` can now carry a background painted across its
  whole resolved rectangle, behind the border and any bound widget. Set an explicit color with
  `RegionBuilder.Background(Color)`, or bind to a named theme style with
  `RegionBuilder.BackgroundRole(string)` so a theme switch restyles the region without a code change;
  `NoBackground()` clears it. Regions with no background stay transparent and inherit the theme text
  background, so the change is backward compatible. The built-in themes register conventional
  `Theme.SidebarRole` and `Theme.StatusBarRole` styles for panels and status strips.
- **`DialogModal` base class.** A reusable base for centered, bordered dialogs that owns box
  measurement, min/max content clamping, centering, background fill, border, an optional title and a
  dim footer hint, and hands subclasses a clipped inner surface through `RenderContent`. Subclasses
  report their natural content size and draw content; they no longer compute box geometry by hand.
- **`CheckList<T>` widget and `MultiSelectModal<T>` dialog.** A vertical list whose items can each be
  checked independently (Space toggles, a configurable key toggles all), of any item type with a
  display selector, plus a dialog wrapper that completes with the checked indices (Enter) or null
  (Escape).
- **`DefinitionList` widget.** A thread-safe list of labeled values with optional section headers for
  status panels and telemetry sidebars; setting the same label updates its value in place, and values
  truncate (never the label) when space is tight.
- **`ActivityIndicator` widget.** A spinner-plus-rotating-phrase "working…" line advanced by explicit
  ticks (deterministic and testable), exposing its current line so it can also be pushed into a pane.
- **`StreamingTranscript` helper.** Projects a stream of text and keyed status lines onto a `Pane`:
  buffer streaming text into a block shown on a live line, re-render the finished block as Markdown,
  and update named lines in place (for example flipping a task line from "running…" to "done").
- **`ActionListView<T>` widget.** A list whose rows expose keyboard actions (Enter to activate, plus
  consumer-registered chords like "e"/Delete) with an optional per-row enabled predicate; firing an
  action raises a typed `ListAction<T>` carrying the row index, item, and action id.
- **`ReorderableList<T>` widget.** A list that moves the selected item up/down (Alt+Up/Down or "["/"]")
  and removes it (Delete/"d"), exposing the current order and raising `Reordered`/`Removed` events.
- **`Command` and `CommandRegistry`.** One command descriptor (id, title, category, optional chord,
  slash aliases, handler, enabled predicate) projected onto every surface: `ApplyTo` binds chords and
  handlers into the host, `BuildMenuBar` groups enabled commands by category, `BuildPalette` returns a
  `FuzzyList<Command>` for a command palette, and `ResolveSlash` routes `/name args` input.
- **Autocomplete / typeahead.** An `ISuggestionProvider` contract (with a built-in
  `PrefixSuggestionProvider`) and an `AutocompleteOverlay` that shows ranked, caret-anchored
  suggestions for a text input — Up/Down to move, Tab/Enter to accept, Escape to dismiss — flipping
  above the caret when there is no room below. This is the one capability the original build plan had
  deliberately excluded.
- **Text and input utilities.** `HintText.Wrap` wraps a separator-delimited hint footer to a width
  without splitting a segment; `ColumnFormatter.Format` aligns rows into columns sized to the widest
  cell; the `Rule` widget draws a horizontal or vertical divider with an optional centered caption;
  and `SubmitKeyResolver` resolves a key to submit / insert-newline / ignore, encoding the
  cross-terminal Enter-vs-Ctrl+J-vs-Shift+Enter reality so multi-line editors stop reinventing it.

### Changed
- **`ScrollView` follows focus.** When its child implements the new `IScrollExtent` contract,
  `ScrollView` scrolls the child's focused region into view automatically (`AutoScrollToFocus`, on by
  default), with a public `EnsureVisible(top, height)` that clamps rather than throwing. `Form`
  implements `IScrollExtent`, so a form taller than its viewport keeps the focused field visible.
- **`Form` field sets can be rebuilt at runtime.** `Form.Clear()` empties the fields and resets the
  focus ring (paired with `FocusManager.Clear()`), and `Form.SetFocusedField(index)` restores focus
  after a rebuild — the basis for dependent forms that swap fields when a selection changes.
- **`KeyLabel` audit.** Confirmed OS-adaptive modifier labels are complete: `KeyChord.ToLabel` renders
  `⌃/⌥/⇧/⌘` in symbol style and `Ctrl+/Alt+/Shift+/Super+` in ASCII, and `KeyLabel.Recommended`
  follows the platform. No code change was needed; existing coverage stands.

### Breaking
- **Generic list widgets.** `ListView`, `FuzzyList`, and (internally) the list picker are now generic
  over the item type: `ListView<T>` and `FuzzyList<T>` take an optional `Func<T,string>` display
  selector (the identity when `T` is `string`) and expose `SelectedItem` as `T`, so the selection is
  the original object rather than a string that had to be mapped back. Migration: `new ListView()`
  becomes `new ListView<string>()` and `new FuzzyList(items)` becomes `new FuzzyList<string>(items)`;
  a non-string type supplies a selector, e.g. `new ListView<FileInfo>(f => f.Name)`. `SelectModal`
  keeps its string+index API (it is backed by `ListView<string>`), so its call sites are unchanged.

## [0.5.1] - 2026-08-04

Documentation fixes. No code changes from 0.5.0.

### Fixed
- **README version references.** The header banner and the `<PackageReference>` install snippet now
  reflect the 0.5 line (masked text input) instead of 0.4.1.

### Changed
- **Archived internal notes.** `USABILITY_IMPROVEMENTS.md` moved into `archive/`.

## [0.5.0] - 2026-08-04

Masked text input for secret entry.

### Added
- **`TextField` value masking.** `TextField` gains a `MaskChar` property and an `IsMasked`
  convenience flag so a field can obscure its rendered value — for passwords, API keys, bearer
  tokens, and other secrets. When `MaskChar` is `'\0'` (the default) the field renders as typed, so
  the change is additive and backward compatible. Set it to a visible character (for example `'•'`)
  and every value character, including the glyph shown under the caret, renders as the mask while
  `Value` and all editing and caret behavior stay unchanged.

## [0.4.1] - 2026-07-30

Input decoding fix so applications can offer a terminal-independent "insert newline" chord.

### Fixed
- **Carriage return and line feed are decoded distinctly.** `InputParser` previously folded both
  `0x0D` (CR) and `0x0A` (LF) into `Enter`. Now CR decodes as `Enter` while LF decodes as `Ctrl+J`
  (`Char` `'j'` + `Ctrl`). In raw mode Enter always transmits as CR, so this is lossless — LF arrives
  only from Ctrl+J. Because no terminal reports `Shift+Enter` or `Ctrl+Enter` without the enhanced
  keyboard protocol (Windows Terminal, macOS Terminal.app, and legacy xterm all send a bare CR), an
  application can now bind `Ctrl+J` as a newline chord that works on every platform. Bracketed paste
  is unaffected: pasted newlines are still captured as literal paste text, not key events.

## [0.4.0] - 2026-07-29

The interaction-contract release. The host now assembles the interactive skeleton — focus, key
precedence, mouse hit-testing, and modal marshalling — on the consumer's behalf, turning "read the
example and replicate the wiring" into "bind widgets, set focus, run." Everything is additive: the
raw escape hatches (`KeyReceived`, `MouseReceived`, `RenderOverlay`) still work unchanged.

### Added
- **Host-owned focus ring.** Focusable widgets bound with `Bind`/`AddWidget` join a focus ring in
  bind order; the first is focused automatically. `TuiApplication.Focus(regionId)`, `FocusNext()`,
  `FocusPrevious()`, the `FocusedRegion`/`FocusOrder` properties, and the `FocusChanged` event drive
  and observe focus. `FocusContext` now follows focus automatically, so focus-scoped commands apply
  to whatever is focused. `Tab`/`Shift+Tab` traverse the ring when the focused widget declines them.
- **Focus contract (`IFocusAware`).** A new optional companion to `IFocusable`: the host and
  `FocusManager` call `OnFocusChanged(bool)` on every focus transition so a widget's rendered focus
  state never diverges from routing. `TextField`, `TextEditor`, and `ListView` implement it;
  `FocusManager` now drives visual focus.
- **Explicit input-precedence chain.** Keys route in a defined order: modal trap → optional
  `KeyFilter` pre-filter → focus-scoped commands → **focused widget (first refusal)** → host focus
  traversal → global commands → `KeyReceived`. Giving the focused widget first refusal fixes the
  global-chord-vs-widget-key collision (e.g. a global `Ctrl+K` sequence no longer steals the editor's
  kill-to-end-of-line).
- **Sequence timeout is wired.** A dangling two-key sequence prefix is cleared after
  `SequenceTimeoutMilliseconds` (default 800), and an abandoned prefix no longer swallows the
  following keystroke — the next key falls through and is processed normally.
- **Host-owned mouse routing.** A per-frame, host-owned hit-test map (rebuilt every draw pass, never
  stored on widgets) powers click-to-focus for `IFocusable` widgets and wheel/click forwarding to the
  new optional `IMouseAware` interface, with coordinates translated into each widget's own rectangle.
  `Pane` and `ScrollView` scroll on the wheel out of the box. Toggle with `EnableMouseRouting`.
- **Typed modals and a loop scheduler.** `ShowAsync<T>(Modal)` returns the modal result as `T` with no
  cast, and `Post(Action)` queues work onto the loop thread (drained each frame) so a modal
  continuation or a background task can safely mutate UI state.
- **Application-shell layout helpers.** `LayoutBuilder.DockTop`/`DockBottom`/`DockLeft`/`DockRight`/
  `Fill` build a four-way shell (header, footer, sidebar, main) as real, non-overlapping regions you
  bind `StatusBar`/`MenuBar`/content into — no hand-computed rectangles or overlay math.
- **Interaction-contract example.** A new `--contract` demo (and `--contract-once` snapshot) stands up
  a full interactive app in ~120 lines using the dock shell, the focus ring, click-to-focus, a
  focus-scoped `Enter`, a two-key theme chord, and a typed picker modal marshalled back with `Post`.
- Touchstone `Usability` suite covering the focus ring, precedence chain, sequence timeout,
  click-to-focus, wheel routing, typed modals, `Post`, multi-key `Bind`, the layout guard, and the
  dock helpers.

### Fixed
- **`Bind` now parses the documented multi-key syntax.** `app.Bind("ctrl+k ctrl+t", …)` registers a
  two-key sequence instead of throwing; a single chord still binds a single command. Rebinding a
  sequence is idempotent (`CommandRoutingTable.UnregisterSequence`).
- **Assigning `Layout` after incremental construction is now rejected** with a clear
  `InvalidOperationException` instead of silently discarding regions added via
  `AddRegion`/`AddPane`/`AddWidget`. Appending with `AddRegion` after assigning a layout still works.
- **Version/documentation drift** corrected: the README and package metadata now agree on the current
  version.

### Changed
- `CommandRoutingTable` gained `ResolveFocusScoped` and `ResolveGlobalSingle` (scope-specific
  resolution) and `CommandRouter` gained `BeginPending`/`TryCompletePending` so a host can own the
  precedence chain. The existing `Process`/`ResolveSingle` methods are unchanged and still available.

## [0.3.1] - 2026-07-29

Unix keyboard input follow-up to v0.3.0.

### Fixed
- **macOS/Linux: keystrokes no longer echo and the screen no longer scrolls.** The Unix `ConsoleBackend` now performs all terminal I/O directly on the standard file descriptors (libc `read`/`write`) instead of through `System.Console`. `System.Console`'s Unix implementation echoed input and re-cooked the terminal (re-enabling `ISIG`/`OPOST`/echo) *behind* the `termios` raw mode we set, which is why typing, Enter, and Tab echoed and scrolled the view even after raw mode was applied. Window size still comes from `Console.WindowWidth`/`Height`, which queries the tty without re-cooking it.
- **Function keys and help are reliable.** F1–F4 are now decoded in their CSI form (`ESC [ P`, `ESC [ 1;5 P`) in addition to SS3 (`ESC O P`), so F1 opens help under the enhanced-keyboard protocol iTerm2 and others use. Kitty key-release events and modifier sub-parameters (`5:3`) are parsed, so a key release no longer double-fires a command or dismisses the modal its press just opened — the cause of the phantom command and the settings dialog not appearing.

### Added
- The example binds `?` as a help alias (macOS frequently reserves F1–F12 for system functions) and renders footer/help key hints via `KeyChord.ToLabel(KeyLabel.Recommended)`.
- Input-decoder tests for F1–F4 via CSI, a Cursor Position Report not being mistaken for F3, and Kitty release-event suppression.

### Validated
- Interactive keyboard input, rendering, and terminal restoration confirmed working on **Windows** (Windows Terminal), **macOS** (iTerm2), and **Linux**, including over an **SSH** session.

## [0.3.0] - 2026-07-29

Native cross-platform keyboard input.

### Fixed
- **Unix raw mode now works natively.** `ConsoleBackend` previously shelled out to `stty` in a child process to enter raw mode on Linux and macOS; the .NET runtime's terminal-state save/restore around child processes silently reverted the change, leaving the terminal in cooked mode with echo on. The result was that on macOS (iTerm2, Terminal.app) and Linux, typing echoed and scrolled the screen, Tab and Page Up/Down were consumed by the terminal, and function keys, `Ctrl` combinations, and the help/settings shortcuts never reached the app. Raw mode is now set **in-process** via the libc `termios` API (`tcgetattr`/`cfmakeraw`/`tcsetattr`), so keyboard handling matches the Windows console path across Windows Terminal/Command Prompt/PowerShell, iTerm2/Terminal.app, Linux terminals, and SSH/tmux sessions. When standard input is not a terminal, raw mode is skipped gracefully.

### Added
- **`KeyChord.ToLabel(KeyLabelStyle)`** plus the `KeyLabelStyle` enum (`Ascii`, `Symbols`) and `KeyLabel.Recommended`, for rendering key hints with the conventions of the host OS — `Ctrl+G` / `PgUp` on Windows and Linux, `⌃G` / `⇞` on macOS. The example app's footer and help now use it.
- **Process-exit safety net**: on Unix, `ConsoleBackend` restores cooked mode, shows the cursor, and leaves the alternate screen if the process exits without calling `Stop`, so a crash no longer leaves a broken terminal.
- Input-decoder coverage for the full non-text key set (Tab, Shift+Tab, F1–F12 via SS3 and CSI, arrows via CSI and SS3, Page Up/Down, Home/End, Insert/Delete, `Ctrl` letters, split escape sequences) and for the new key-label formatting, plus a documented manual interactive test matrix in the Building Terminal Apps guide.

## [0.2.0] - 2026-07-29

Styled one-shot output, plus a hardening and documentation pass.

### Added
- **Styled one-shot output (no full-screen app).** A new surface for printing styled text and tables to a `TextWriter` inline — the building blocks for a CLI to render color without a `TuiApplication`:
  - `Markup.Escape(text)` — escapes `[`/`]` so arbitrary text renders literally.
  - `TUIKit.Terminal.AnsiText.Render(StyledText | markup, TerminalColorDepth)` — styled text → a flowing SGR string (no cursor moves); plain when depth is `None`.
  - `TUIKit.Rendering.InlineRenderer.ToAnsiLines(CellBuffer, TerminalColorDepth)` — a cell buffer → colored inline lines (coalesced SGR runs), matching `Snapshot.ToText` when plain.
  - `CapabilityDetector` now honors `NO_COLOR`, and `ResolveOutputColorDepth(TextWriter)` picks the depth for a writer (plain when redirected / `NO_COLOR` / `TERM=dumb`).
  - `TUIKit.StyledConsole` — the writer itself: `Write`/`WriteLine`/`Markup`/`MarkupLine` and `Write(IWidget)`, over an explicit `TextWriter` or `ForStandardOutput()`/`ForStandardError()`.
  - `Table` gained borders (`TableBorder` None/Square/Rounded), styled/markup cells (`AddRow(params StyledText[])`, `AddMarkupRow`), content-fit sizing (`ColumnSizing`), and per-column `CellAlignment` — the original even-column, borderless behavior is unchanged.
- **Comprehensive validation coverage**: eight suites asserting that every documented argument guard and range bound across the host (`TuiApplication`/`TuiApp`), core primitives (geometry, buffers, styled text, surface drawing), layout, theming, input/routing, links, backends (including `ConsoleBackend`), modals, content, diagnostics, and widgets either throws the declared exception or clamps. Added `Check.ThrowsAsync`, behavioral tests for `PromptModal`/`SelectModal` Enter/Escape results, and the styled-output suite above — **264 Touchstone cases** total, green across the console, xUnit, and NUnit runners on net8.0 and net10.0.

### Changed
- Documentation overhaul: README feature catalog refreshed to the full widget set, added quick links, a "How it compares" section, an ergonomic Quick Start, and an accurate "Project status"; the Building Terminal Apps guide gained a "Data, navigation, and visuals" widget gallery and a corrected diagnostics snippet; stale coverage/conformance reports annotated with the current state; internal design docs (`POSSIBLE_IMPROVEMENTS.md`, `TUIKIT.md`, `TUIKIT_PLAN.md`) moved to `archive/`.

## [0.1.0] - 2026-07-29

First public preview.

### Added
- **Core primitives**: `Point`/`Size`/`Rect` geometry, `Color` (truecolor/palette/default), `CellStyle`/`CellAttributes`, `Cell`, `CellBuffer` with per-row dirty tracking, `ISurface`/`BufferSurface` with clipped views, and text-drawing extensions.
- **Unicode**: self-contained `TextWidth` (wcwidth interval tables) and `Graphemes` segmentation (combining marks, emoji ZWJ, regional-indicator flags), identical across target frameworks.
- **Styled text**: fluent `Text.From("x").Bold().Red()` builder with per-span styles.
- **Terminal backend layer**: `ITerminalBackend`, in-memory `HeadlessBackend`, real `ConsoleBackend` (Windows `SetConsoleMode` / Unix `stty` raw mode), capability detection, ANSI/SGR sequence builders, and truecolor→256/16 quantization.
- **Rendering**: double-buffered `TerminalRenderer` with row-level diffing and SGR coalescing.
- **Layout**: developer-defined region model with per-axis constraints (fixed, end-anchored, stretch, proportional), a solver, minimum-size derivation, and a "terminal too small" block screen.
- **Content**: thread-safe `Pane` with mutable line handles, smart scroll lock, capacity eviction, atomic batches; word/hard text wrapping; a Markdown renderer; ANSI stripping; text selection with OSC 52 clipboard.
- **Input**: byte decoder (UTF-8, control keys, CSI/SS3, CSI u, SGR mouse, bracketed paste), key chords with a string parser, a command routing table with scopes/conflict policy/multi-key sequences, and a configurable Ctrl+C policy.
- **Mouse and links**: link registry with per-frame hit-testing, security-aware auto-linkify allowlist, and click-count synthesis.
- **Modals and notifications**: focus-trapping modal stack with async results and close refusal, plus non-focus-stealing toasts.
- **Widgets**: label, gauge, sparkline, progress bar, spinner, list, table, multi-line editor (undo/redo, kill/yank), text field, checkbox, and radio group.
- **Theming**: dark/light/high-contrast themes with named styles and an ASCII-border fallback.
- **Hosting**: `TuiApplication` with render and input loops, command dispatch, modal integration, non-TTY line degradation, terminal restoration, and a single-instance guard.
- **Diagnostics and testing**: frame statistics, input record/replay, a debug overlay, and a `Snapshot` helper for headless snapshot testing.
- **Example**: a full simulated agent-control harness demonstrating every capability, with a capability-coverage matrix.
- Repository scaffolding, four Touchstone test projects, CI workflow, and documentation (README, LICENSE, CLAUDE, conformance and coverage reports).
- **Ergonomics layer** (from `archive/POSSIBLE_IMPROVEMENTS.md`): `Pane` now implements `IWidget`, and `TuiApplication.Bind(regionId, IWidget)` binds any widget to a region (10.1). App verbs `Bind(chord, action)`, `Quit`, `Notify`, and the `AddPane`/`AddWidget`/`AddRegion` helpers (10.9); the `TuiApp.RunAsync` one-call bootstrap (10.10). Inline markup: `Markup.Parse` and `Pane.WriteMarkup` for `[bold red]…[/]` styling (10.4). Expanded theme roles — success/warning/error/info/selection/disabled — and `StyledText.Style` (10.16). A `Layout.Row`/`Layout.Column` split DSL with `LayoutSlot` (10.6).
- **New widgets**: `StatusBar` (10.7), `ScrollView` with vertical and horizontal scrollbars (10.2), `Collapsible` (10.13), `TabView` (10.14), `Tree<T>` (10.11), and `FuzzyList` (10.3).
- **Focus, forms, prompts, find** (tranche 2): a global `FocusManager` (10.20); `Form`/`FormField` with tab navigation and validation (10.15); async `ConfirmAsync`/`PromptAsync`/`SelectAsync` prompts (10.5); `Pane` incremental find with match highlighting and `TextEditor.Find`/`ReplaceAll`, surfaced as `Ctrl+F`/`Ctrl+H` bindings (10.21).
- **Mouse-capture toggle for native text selection**: `TuiApplication.MouseCaptureEnabled` / `ToggleMouseCapture()` hand the mouse back to the terminal at runtime, so users can drag-select and copy text with their terminal (to paste into another program) and then resume. The sample app binds it to **F12** and documents it in the help overlay; `Start()` honors the flag and setting it after start emits the enable/disable escape immediately.
- **Terminal image protocols (10.40)**: `SixelEncoder` and `KittyImageEncoder` turn a truecolor pixel grid into raw sixel / kitty escape sequences (palette-quantized + run-length-encoded for sixel; chunked base64 RGB for kitty) for terminals that support graphics, complementing the everywhere-portable `HalfBlockImage`.
- **POSIX signals on `netstandard2.0` (10.31)**: `AppLifecycle.HookPosixSignals` now falls back to a libc `signal()` compatibility shim on `netstandard2.0` (SIGINT/SIGWINCH/SIGTSTP/SIGCONT with per-OS numbers), where `PosixSignalRegistration` is unavailable; .NET 8+ continues to use the safer registration API.
- **Guided-tour live UI**: the example now surfaces the modal system directly — **F1**/**?** help overlay, **Ctrl+G** settings & actions menu (theme, icon mode, notifications, confirmation dialog), **Ctrl+T** theme cycle, **Ctrl+K** confirmation dialog, **Ctrl+N** notification toast — plus a landing page describing them.
- **Test suites renamed** from `TrancheNSuite` to descriptive names (e.g. `RichContentSuite`, `ChartsIconsColorSuite`, `ValueValidationSuite`, `ImageProtocolSuite`).
- **Guided-tour example**: running `TUIKit.Example` now launches a self-describing guided tour — a header names each feature, the left pane renders the live widget, and the right pane shows the code that builds it (PageUp/PageDown to browse 15 feature pages, arrows/Enter to interact, Ctrl+Q to quit). The original agent-control harness moved behind `--harness`, with a headless `--tour-once --page N` snapshot for CI/screenshots.
- **Key-binding editor & templates** (tranche 9): `KeyBindingSet`/`KeyBinding` model an editable, conflict-checked keymap, and `KeyBindingEditor` is an interactive settings widget that captures a key press to rebind a command (the user-configurable key bindings). Added a `tuikit-app` `dotnet new` template under `templates/` for scaffolding new apps (10.8).
- **Modal editing, links, lifecycle** (tranche 8): `ModalDispatcher`/`EditMode` route keys through vi-style modes with per-mode bindings and transitions (10.32); `LinkHints` assigns keyboard labels to links and `SystemClipboard` adds best-effort native clipboard read alongside the existing OSC 8 hyperlink and OSC 52 write support (10.34); `AppLifecycle` surfaces suspend/resume/resize/interrupt as events and wires POSIX signals on .NET 8+ Unix (10.31).
- **Banners and visual effects** (tranche 7): `Banner`/`BannerText` render large block-letter text from a built-in 5×5 font (10.36); `ISurface.DrawShadow` casts a drop shadow under a box (10.38); `Backdrop.Dim` dims a rendered buffer behind a modal (10.39); and `HalfBlockImage` renders truecolor pixel images at double vertical resolution with the ▀ glyph (10.40).
- **Splits, menus, files** (tranche 6): `SurfaceView` maps a sub-region of any surface into local coordinates, and `SplitView` uses it to divide an area between two children with an arrow-key-resizable divider that nests arbitrarily (10.22, 10.28); `MenuBar`/`Menu`/`MenuItem` provide a keyboard-driven menu bar with drop-downs (10.25); `FileBrowser` lists and navigates the file system with selection and activation events (10.35).
- **Reactive, animation, testing** (tranche 5): `Observable<T>` gives thread-safe one-way data binding with `Subscribe`/`Bind` (10.30); `Easing`, `Tween`, and a tick-driven `FrameTimer` provide deterministic animation and periodic callbacks (10.33); `MultiProgress`/`ProgressTask` render concurrent progress bars (10.24); and `WidgetTester` is a fluent headless harness for driving keys and asserting rendered output (10.23).
- **Charts, icons, color** (tranche 4): `BrailleCanvas` packs a 2×4 dot grid per cell for high-resolution drawing, with `LineChart` and `BarChart` built on it (10.27); an `Icons` catalog exposes common glyphs as Nerd Font, portable Unicode, and ASCII renderings selectable via `IconMode` (10.26); `ColorPicker` is an interactive RGB picker with channel sliders and a hex preview (10.37).
- **Diff, table, syntax, Markdown** (tranche 3): `DiffView` renders an LCS-based unified line diff with colored add/remove/context lines and optional syntax-highlighted context (10.12); `DataTable<T>` gives a columnar, sortable, virtualized table over any typed source (10.17); `SyntaxHighlighter` colors keywords, strings, numbers, and comments for C#/JS/TS/Python/JSON (10.18); the Markdown renderer now handles task lists, ordered lists, nested bullets, and tables (10.29).
- **User documentation**: `BUILDING_TERMINAL_APPS.md`, an exhaustive guide to building apps with the library, including a worked live-dashboard example and cross-platform/SSH/tmux guidance.
- Regions can declare a border via `WithBorder(BorderStyle, title)`: `None`, `Ascii`, `Line`, `Rounded`, `Double`, or `Thick`. The host draws the border, honors the theme's ASCII fallback, insets content for it, and folds it into the region's minimum size. `DrawBox` gained a `BorderStyle` overload. The example's tool, telemetry, and composer panels now use declarative rounded borders.
- `archive/POSSIBLE_IMPROVEMENTS.md`: a capability analysis versus the agent CLIs, the awesome-tuis catalog, and the C#/.NET frameworks (Spectre.Console, Terminal.Gui, Consolonia, Terminaux), with a tiered improvement roadmap.

### Changed
- `SplitView.MinRatio`/`MaxRatio` now clamp assignments to `[0.0, 1.0]`, `ResizeStep` rejects values outside `(0.0, 1.0]` with `ArgumentOutOfRangeException`, and the divider clamp is robust to an inverted min/max. Added dedicated value-validation and layout-DSL test suites asserting that every range-constrained input across the widgets (gauges, progress, sparkline, scroll view, split, color picker, braille canvas, tween, timer, cell buffer) and layout primitives (`AxisConstraint`, `LayoutSlot`, `Layout.Column`/`Row`) either clamps or throws, plus more markup, tab, tree, fuzzy-list, and form-input (text field, checkbox, radio group, editor/pane search) edge cases.
- Layout regions default to one cell of interior padding on every side, and `Padding` supports vertical as well as horizontal insets. Content and debug outlines no longer touch region edges.
- Themes now set an explicit background, so switching between dark, light, and high-contrast reverses the whole palette. Pane content composes over the theme background via `CellStyle.Over`.

### Fixed
- Modals (`Modal.ContentPadding`) inset their content from the border; the example's confirmation, palette, settings, and help dialogs now keep a cell of padding on every side.
- `DrawBox` clips an over-long title, and the example help overlay renders into a clipped, sized box so its text no longer spills outside the frame.
- The example transcript no longer stacks blank lines between streamed paragraphs and list items.

[Unreleased]: https://github.com/jchristn/TUIKit/commits/main
