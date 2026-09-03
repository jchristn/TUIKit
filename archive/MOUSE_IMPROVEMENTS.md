# MOUSE_IMPROVEMENTS.md — Full Mouse Support Plan (v0.10.0)

Status: **implemented** (feature/mouse-improvements) · Target version: **0.10.0** (minor increment; alpha) · Owner: TUIKit core

Implementation notes (deviations from the plan as written):
- Widget mouse cases live in one new `MouseWidgetSuite` rather than being appended to each
  widget's existing suite, keeping the mouse behavior contract in one place; backend
  enable/disable and capability-detector cases live in `MouseProtocolSuite` and
  `MouseHoverRoutingSuite` rather than `TerminalSuite`.
- Focus events are surfaced as two payload-less `InputEventKind` values
  (`FocusGained`/`FocusLost`) plus `TuiApplication.TerminalFocusChanged(bool)`.
- Link hover: the application assigns its per-frame `LinkRegistry` to `TuiApplication.Links`;
  the host tracks `HoveredLink` and raises `LinkHovered`. Underline rendering stays in the
  application's overlay (the registry is app-owned and rebuilt per frame).
- The guided tour's Mouse playground forwards `MouseReceived` into the demo rectangle (the tour
  composes pages in its overlay rather than binding them), synthesizing Enter/Leave at the
  boundary — the same contract the host provides for bound widgets.

## 1. Summary

TUIKit already ships a working cross-platform mouse pipeline: SGR (DECSET 1006) decoding in
`InputParser`, button/drag/wheel tracking (modes 1000 + 1002), double/triple-click synthesis
(`ClickSynthesizer`), per-frame hit-testing (`HitTestEntry`), and widget routing through
`IMouseAware` with widget-local coordinates. This release completes the mouse story:

1. **Hover / any-motion tracking** (DECSET 1003) — pointer movement with *no* button held,
   **on by default**, with per-drain move coalescing to prevent redraw storms.
2. **Enter/Leave synthesis** — new `MouseEventKind.Enter` / `MouseEventKind.Leave` values,
   synthesized by the host from hit-test transitions and routed through the existing
   `IMouseAware.HandleMouse`. No new interface.
3. **Horizontal wheel** — `MouseButton.WheelLeft` / `WheelRight`, decoded from SGR buttons 66/67.
4. **Terminal focus reporting** (DECSET 1004) — `FocusGained` / `FocusLost` input events and a
   host-level `TerminalFocusChanged` event so apps can dim UI or pause spinners when unfocused.
5. **conhost QuickEdit fix** — clear `ENABLE_QUICK_EDIT_MODE` on legacy Windows consoles so mouse
   input is not swallowed by selection mode; restore on exit.
6. **Hover visual states for a representative widget set** — `TabView`, `MenuBar`, `ListView`,
   `Tree`, `Checkbox`, and link hover underline. (There is no `Button` widget; `Checkbox` and
   `MenuBar` items are the closest click-to-activate equivalents.) Remaining widgets adopt hover
   in later releases.
7. **Example, tests, docs** — a dedicated mouse showcase pane in `TUIKit.Example`, new positive
   and negative Touchstone suites, README / BUILDING_TERMINAL_APPS / CHANGELOG updates, and the
   version bump to 0.10.0.

Decisions already made (do not re-litigate during implementation): hover on by default;
enter/leave via `MouseEventKind` extension, not a new interface; representative widget set only;
all three extras (QuickEdit, horizontal wheel, focus reporting) included; Example must showcase
the capabilities.

## 2. Support matrix

Legend: ✅ supported · ⚠️ partial / depends on configuration · ❌ not delivered in that
environment. Unsupported DECSET modes are ignored by terminals that lack them, so every ❌/⚠️
degrades silently — the app still runs, that feature simply does not fire.

| Environment | Click / drag / wheel (1000+1002+1006) | Hover / any-motion (1003) | Horizontal wheel (66/67) | Focus in/out (1004) | Coords > 223 cols (SGR) | Notes |
|---|---|---|---|---|---|---|
| Windows Terminal (Win 10/11) | ✅ | ✅ | ✅ | ✅ | ✅ | Primary Windows target; full VT input translation. |
| Legacy conhost (Win 10+) | ⚠️ | ⚠️ | ❌ | ❌ | ✅ | VT mouse translation is partial and version-dependent; requires the QuickEdit fix in this release. Pre-VT conhost (Win 8.1 and earlier) gets no mouse at all. |
| macOS Terminal.app | ✅ | ⚠️ | ❌ | ⚠️ | ✅ | Drag tracking works; any-motion and focus reporting vary by macOS version — hover degrades to drag-only where 1003 is ignored. |
| iTerm2 | ✅ | ✅ | ✅ | ✅ | ✅ | Full support. |
| kitty / Alacritty / WezTerm / Ghostty | ✅ | ✅ | ✅ | ✅ | ✅ | Full support. |
| VTE terminals (GNOME Terminal, Tilix, xfce4-terminal) | ✅ | ✅ | ✅ | ✅ | ✅ | Full support. |
| xterm | ✅ | ✅ | ✅ | ✅ | ✅ | Reference implementation of every mode used. |
| tmux (`set -g mouse on`) | ✅ | ✅ | ⚠️ | ✅ | ✅ | Requires mouse enabled in tmux config; tmux consumes some events for its own panes; horizontal wheel forwarding depends on tmux version. With `mouse off`, no events reach the app. |
| GNU screen | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | screen's mouse pass-through is limited to basic tracking; treat as keyboard-first. |
| SSH (any client) | — | — | — | — | — | Transparent: mouse capability is that of the *client* terminal emulator; the rows above apply to whatever the user runs locally. |
| WSL | — | — | — | — | — | Transparent: capability is that of the hosting console (usually Windows Terminal → full support). |
| Headless / redirected / CI (`HeadlessBackend`, non-TTY) | ❌ (by design) | ❌ | ❌ | ❌ | — | `IsInteractive` is false; no escape sequences are emitted. Tests inject synthetic bytes/events instead. |

### Explicitly not supported, and why

| Capability | Why not |
|---|---|
| Pixel-precision coordinates (DECSET 1016) | Terminal support is spotty and TUIKit's entire rendering model is a cell grid; pixel coordinates have no consumer in the layout or hit-test layers. Cell granularity is the reliable cross-platform contract. |
| Legacy mouse encodings (X10, UTF-8 1005, urxvt 1015) | SGR 1006 is ubiquitous in every terminal that reports the mouse at all today. Supporting three additional ambiguous encodings adds parser complexity and coordinate-ceiling bugs for terminals that effectively no longer exist. Terminals without SGR fall back to keyboard-only operation via the existing `TerminalCapabilities.SgrMouse` flag. |
| Mouse events outside the terminal window / global position | No terminal protocol reports the pointer outside the window. `Leave` is synthesized only for transitions between hit-test regions and for motion into unbound screen area. |
| Pointer cursor shape changes on hover | No standardized escape sequence; OSC 22 exists but support is too narrow to build API on. Revisit if adoption grows. |
| Hover tooltips as a framework primitive | Out of scope for this release; apps can build them from Enter/Leave + a widget. Candidate for 0.11+. |
| Pre-Windows-10 consoles | No `ENABLE_VIRTUAL_TERMINAL_INPUT`, so no VT mouse translation exists to consume. |

## 3. Work breakdown

### Phase 1 — Protocol layer (`src/TUIKit/Terminal`)

- [x] **`Ansi.cs`**: extend `EnableMouse` to emit `CSI ?1000h ?1002h ?1003h ?1006h` and
  `DisableMouse` to emit the reverse order (`?1006l ?1003l ?1002l ?1000l`). Add
  `EnableAnyMotion` / `DisableAnyMotion` (`?1003h/l`) so the host can drop to drag-only at
  runtime, and `EnableFocusReporting` / `DisableFocusReporting` (`?1004h/l`). XML docs on all.
- [x] **`ConsoleBackend.cs` (Windows path)**: in `ApplyWindowsMode`, also set
  `ENABLE_EXTENDED_FLAGS` (0x0080) and clear `ENABLE_QUICK_EDIT_MODE` (0x0040) on the input
  handle; restore the original mode in `RestoreWindowsMode` (already saved in
  `_OriginalInputMode`). Constants go in `NativeConsole.cs`.
- [x] **`TerminalCapabilities.cs`**: add `bool AnyMotionMouse` and `bool FocusReporting`
  properties; extend the constructor, `Full`, and `Minimal`. Alpha status permits the signature
  change; update all call sites.
- [x] **`CapabilityDetector.cs`**: populate the two new flags from the same environment heuristics
  used for `SgrMouse` (conservative: default true wherever `SgrMouse` is true, false for screen
  and known-limited terminals via `TERM`/`TERM_PROGRAM`).

### Phase 2 — Event model (`src/TUIKit/Input`)

- [x] **`MouseEventKind.cs`**: add `Enter = 4` and `Leave = 5` (host-synthesized; never produced
  by the parser). Document that.
- [x] **`MouseButton.cs`**: add `WheelLeft = 6`, `WheelRight = 7`.
- [x] **`InputParser.cs`**:
  - Decode SGR buttons 66/67 as horizontal wheel. Note the current wheel branch tests `(b & 64)`
    and then `(b & 1)` — 66/67 would today mis-decode as WheelUp/WheelDown; fix the mapping to
    inspect the two low bits (64→up, 65→down, 66→left, 67→right) before modifier extraction.
  - Verify motion-with-no-button (b = 35 + modifier bits) decodes as
    `MouseEventKind.Move` / `MouseButton.None` (the code path exists; cover it with tests).
  - Decode `CSI I` / `CSI O` as focus-gained / focus-lost events.
- [x] **`InputEventKind.cs`**: add `FocusGained = 3`, `FocusLost = 4`.
- [x] **`InputEvent.cs`**: add static factories `InputEvent.FromFocus(bool gained)` (or two
  cached singletons — the events carry no payload).
- [x] **`ClickSynthesizer.cs`**: ensure hover `Move` events do not perturb click-count timing;
  reset the multi-click chain when the pointer moves off the press cell between clicks (guarded
  by a configurable slop, default 0 cells).

### Phase 3 — Host (`src/TUIKit/Hosting/TuiApplication.cs`)

- [x] **`MouseTrackingMode` enum** (new file, `src/TUIKit/Input/MouseTrackingMode.cs`):
  `None`, `ButtonsAndDrag`, `AnyMotion`. Host property `MouseTrackingMode` defaults to
  `AnyMotion`; setting it live rewrites the enable/disable sequences the same way
  `MouseCaptureEnabled` does today. `MouseCaptureEnabled = false` still trumps everything
  (retains the hand-the-mouse-back-to-the-terminal toggle).
- [x] **Enable sequences on start/stop**: emit 1003 + 1004 alongside the existing mouse enable in
  the start path (`~TuiApplication` line 694) and the reverse in the stop path (line 780) and in
  `ConsoleBackend`'s panic-reset string (`Ansi.DisableMouse` already flows there once Phase 1
  lands).
- [x] **Move coalescing**: when draining parsed events for a frame, collapse consecutive
  `MouseEventKind.Move` events into the last one (presses/releases/wheel act as barriers so
  ordering is preserved). This bounds hover cost to one hit-test per drain.
- [x] **Enter/Leave synthesis in `RouteMouse`**: track the previously hovered `HitTestEntry`
  region id. On any routed mouse event whose region differs from the previous one, first deliver
  a synthetic `Leave` (widget-local coordinates clamped to the old rect) to the old widget, then
  a synthetic `Enter` to the new one, then the real event. Motion into unbound screen area or a
  hit-map rebuild that drops the region also produces `Leave`. Enter/Leave return values do not
  swallow the triggering event.
- [x] **Terminal focus**: dispatch `FocusGained`/`FocusLost` input events to a new public
  `event Action<bool>? TerminalFocusChanged` (named to avoid colliding with widget focus). On
  `FocusLost`, synthesize a `Leave` for the hovered region and clear hover state.
- [x] **`MouseReceived` fallback** continues to receive unconsumed events, now including hover
  moves; document the volume implication on the event's XML doc.

### Phase 4 — Widgets (representative hover set, `src/TUIKit/Widgets`)

Pattern for each: a private hover-index/flag updated from `Enter`/`Move`/`Leave` in
`HandleMouse`, a public hover style property backed by a private field with a reasonable default,
hover state rendered only when it differs from focus/selection state, and hover cleared on
`Leave`. No behavior change for keyboard users; hover never moves focus or selection by itself.

- [x] **`TabView`** — hovered tab header highlight; click already activates.
- [x] **`MenuBar`** (and open `Menu` dropdowns) — hovered item highlight.
- [x] **`ListView`** — hovered row style (distinct from selected row).
- [x] **`Tree`** — hovered node style.
- [x] **`Checkbox`** — hovered visual affordance (e.g., bracket emphasis).
- [x] **Links** (`LinkRegistry` / pane link rendering) — underline the link run under the
  pointer; `LinkHovered` host event (link + screen position) for status-bar URL preview.
- [x] **Wheel**: `ScrollView` and scrollable widgets map `WheelLeft`/`WheelRight` to horizontal
  scroll where a horizontal extent exists; otherwise ignore (return false).

### Phase 5 — Tests (`src/Test.Shared`)

All suites are Touchstone descriptors registered in `TUIKitSuites.All`, automatically consumed by
`Test.Automated`, `Test.Xunit`, and `Test.Nunit`. Use `HeadlessBackend` (captures written
sequences, accepts injected input bytes) — no console I/O.

New suites (one file each, alongside the existing `MouseLinkSuite`):

- [x] **`MouseProtocolSuite`** — parser-level, positive:
  - SGR press/release for left/middle/right at exact coordinates (1-based wire → 0-based event).
  - Move with button held (drag), move with no button (hover, b=35).
  - Wheel up/down; horizontal wheel 66/67 → `WheelLeft`/`WheelRight`.
  - Modifier bits (Shift 4, Alt 8, Ctrl 16) individually and combined, on press and on move.
  - Coordinates beyond 223 columns (the reason SGR exists) and large rows.
  - `CSI I` / `CSI O` → `FocusGained` / `FocusLost`.
  - Sequence split across two `Advance` calls (incomplete tail retained, completed next drain).
- [x] **`MouseProtocolNegativeSuite`** — parser-level, negative:
  - Truncated sequence at every prefix length → no event emitted, no exception, buffer intact.
  - Malformed parameters: non-numeric, missing semicolons, empty params, param overflow.
  - Unknown final byte (neither `M` nor `m`) → sequence discarded without event.
  - Unknown button codes → no crash; event dropped or mapped to `None` per documented policy.
  - Coordinate 0 on the wire (invalid 1-based) → clamped or dropped per documented policy.
  - Interleaved garbage between valid sequences → valid neighbors still decode.
- [x] **`MouseHoverRoutingSuite`** — host-level:
  - Enter fired once when pointer moves into a bound `IMouseAware` widget; not re-fired for
    moves within the same region.
  - Leave then Enter (in that order) when crossing between adjacent regions.
  - Leave fired when the pointer moves to unbound screen area, and on `FocusLost`.
  - Widget-local coordinate translation for Enter/Move/Leave.
  - Overlapping regions: topmost (last-drawn) wins, matching existing `HitTest` order.
  - Move coalescing: N consecutive moves in one drain → one routed Move (the last), and a
    press between moves prevents coalescing across it.
  - `MouseTrackingMode` transitions write the correct enable/disable sequences to the backend;
    `MouseCaptureEnabled = false` suppresses all of them.
  - `EnableMouseRouting = false` → raw `MouseReceived` only, no Enter/Leave synthesis.
  - Negative: widget that is not `IMouseAware` receives nothing and event falls through to
    `MouseReceived`; consumed events never reach `MouseReceived`.
- [x] **`MouseClickSynthesisSuite`** — double/triple click within threshold, chain reset on
  timeout, chain reset when the second press lands on a different cell; wheel and hover moves
  do not disturb the chain.
- [x] **Widget hover cases** appended to the owning widgets' existing suites (`WidgetSuite`,
  `SplitMenuFileSuite`, `GenericListSuite`, …): hover index set on Enter/Move, cleared on Leave,
  hover style rendered into the snapshot, hover never changes selection or focus, horizontal
  wheel scrolls `ScrollView` when horizontal extent exists and returns false when it does not.
- [x] **Backend cases** in `TerminalSuite`: start emits 1000/1002/1003/1006/1004 enables when
  interactive; stop and panic-reset emit the disables; nothing emitted when
  `IsInteractive == false`. Windows QuickEdit constants verified (mode math is pure and testable
  without a console).
- [x] **Coverage goal**: every new public member exercised; overall suite count grows
  accordingly (record exact numbers in the CHANGELOG as done for 0.9.0).

### Phase 6 — Example, docs, release

- [x] **`TUIKit.Example`**: add a **Mouse Playground** pane (new `TourPage` in the guided tour
  and/or a `HarnessApp` pane) showcasing, live: current pointer cell readout, hover highlighting
  across a `TabView` + `ListView` + `Checkbox` cluster, Enter/Leave event log, single vs
  double vs triple click detection, drag to paint cells on a small canvas, vertical + horizontal
  wheel scrolling, link hover underline with URL preview in the status bar, terminal focus
  dim-on-blur, and the existing mouse-capture toggle key for native text selection.
- [x] **README.md**: update the "Mouse, links, and selection" feature bullet (hover, enter/leave,
  horizontal wheel, focus reporting, QuickEdit note) and the architecture section's host
  description; add a **"Mouse support by environment"** section containing the full support
  matrix from §2 (both tables: the environment matrix and the not-supported-and-why table).
  The README copy is the canonical user-facing version going forward; keep it in sync with §2.
- [x] **BUILDING_TERMINAL_APPS.md**: short "responding to the mouse" section — `IMouseAware`,
  Enter/Leave lifecycle, `MouseTrackingMode`, degradation guidance for tmux/screen/conhost.
- [x] **CHANGELOG.md**: 0.10.0 entry in the established Keep-a-Changelog format — narrative
  paragraph, Added / Changed / Fixed (QuickEdit, 66/67 wheel decode), followed by the §2 support
  matrix (environment table plus the not-supported-and-why table) so the release entry records
  exactly what was and wasn't supported at 0.10.0, and Notes on compatibility:
  new enum members mean consumer `switch` statements over `MouseEventKind` / `MouseButton` /
  `InputEventKind` must tolerate unknown values; `TerminalCapabilities` constructor gained
  parameters.
- [x] **Version bump**: `TUIKit.csproj` → `Version` 0.10.0, `AssemblyVersion`/`FileVersion`
  0.10.0.0.
- [x] **Verification**: `dotnet build` clean (zero warnings, `TreatWarningsAsErrors=true`) on
  `netstandard2.0`, `net8.0`, `net10.0`; all three test runners green; manual smoke pass of the
  Example pane on Windows Terminal (this machine) — note in the PR which matrix rows were
  manually verified vs. inferred from protocol documentation.

## 4. Design constraints and risks

- **Event volume.** 1003 emits an event per cell traversed. Mitigations: per-drain move
  coalescing (Phase 3), hover redraws only when the hovered region or cell actually changes, and
  the `MouseTrackingMode` escape hatch for high-latency links.
- **Enum additions are the compatibility surface.** Alpha status makes this acceptable, but the
  CHANGELOG must call out that `default:`-less switches on the three extended enums will now see
  new values (hover moves arrive at existing `IMouseAware` implementations as `Move` with
  `Button == None`, and Enter/Leave as new kinds — existing widgets that return `false` for
  unrecognized kinds are unaffected).
- **Ordering guarantees.** Documented contract: for any pointer transition the widget sees
  `Leave` (old) → `Enter` (new) → the triggering event (new), in that order, on the input thread
  that dispatches all other events. No mouse event is ever delivered between a widget's `Leave`
  and the next `Enter` for that widget.
- **CLAUDE.md style rules apply throughout**: one type per file, explicit types (no `var`),
  `_PascalCase` private fields, XML docs on all public members (document defaults, thread-safety,
  and the Enter/Leave ordering contract), no `#if` outside the terminal-backend abstraction —
  the QuickEdit change lives entirely in `ConsoleBackend`/`NativeConsole`, keeping that rule.
- **screen/old-conhost realities.** The matrix is honest that two environments degrade; the
  framework's keyboard path remains fully functional there, which is the actual mitigation.

## 5. Suggested implementation order

Phases 1–2 land together (protocol + events, fully unit-testable). Phase 3 next (host routing,
the largest behavioral change). Phase 4 in one or more small PRs per widget. Phase 5 grows in
lockstep with each phase rather than trailing. Phase 6 last, with the version bump as the final
commit.
