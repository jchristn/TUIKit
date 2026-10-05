# TUIKit Improvements Drawn From Armada

Armada (`~/Code/Armada`, the `armada tui` terminal client) is TUIKit's largest consumer. During its 1.0 release, owner
testing turned up a cluster of usability failures that had nothing to do with Armada's domain:

- nobody could tell which pane had focus;
- single-key shortcuts silently typed into a text box;
- a chat transcript stopped following new messages after the user selected something;
- repeated toasts stacked with blank rows between them;
- clickable buttons drawn inside custom rows could not be exercised by tests.

Armada fixed every one of these privately, on top of TUIKit, and it needed eight new types to do it. That is the
signal this plan acts on: if a downstream app has to build it, every TUIKit app will need it.

The plan moves the general-purpose half of that work into TUIKit. It does not port Armada's classes. Armada's code is
cited only to show the shape that survived real use; every public API below is written in TUIKit's own vocabulary
(regions, panes, widgets, focus containers, notifications), and no Armada concept (missions, approvals, Ask) appears
in a TUIKit type, member, or doc comment.

Two rules from the earlier mux plan (`archive/IMPROVEMENTS_FOR_MUX.md`) carry over unchanged. Items are horizontal,
not vertical. And each item is a full product: code with XML docs, a worked example in `TUIKit.Example`, positive and
negative Touchstone coverage, and README and CHANGELOG updates, all in the same release.

Requirements in `~/Code/Agents/requirements` are authoritative, in particular `CODE_STYLE.md`, `VERSIONING.md`,
`BACKEND_TEST_ARCHITECTURE.md`, `REPOSITORY_REQUIREMENTS.md`, and `WRITING_DOCUMENTS.md` (no em dashes, anywhere).
The repository's `CLAUDE.md` restates the code style rules and must be followed for every `.cs` file.

---

## Summary

Simplicity scores ease of integration into TUIKit: 10 means small, additive, and isolated, and 1 means invasive or
risky to the host loop. Value scores how much a typical TUIKit app gains, judged by how often the problem appears and
how bad it is when it does. Rows are ordered by total, highest first. Ties are broken by value, then simplicity.

| ID | Item | Simplicity | Value | Total | Status |
|----|------|-----------:|------:|------:|--------|
| A1 | Focus-within frame for regions and containers | 6 | 10 | 16 | DONE |
| A2 | Focus-aware key hints and text-entry awareness | 6 | 9 | 15 | DONE |
| A3 | Tail-follow scrolling with a new-content indicator | 7 | 8 | 15 | DONE |
| A4 | Headless mouse routing in the test host | 8 | 7 | 15 | DONE |
| A5 | `TabView` focused-tab treatment | 9 | 6 | 15 | DONE |
| A6 | Notification coalescing and a gapless toast stack | 9 | 6 | 15 | DONE |
| A7 | Inline click regions for custom-rendered widgets | 5 | 8 | 13 | DONE |
| A8 | Focus audit test helper | 7 | 6 | 13 | DONE |

The table orders work by payoff, not by dependency. The order to build in is in [Implementation order](#implementation-order):
A4 lands before A7 because A7's tests need it, and A1 lands before A2, A5, and A8 because they read the focus path
that A1 introduces.

### Release at a glance

| Phase | Title | Items | Status |
|---|---|---|---|
| 0 | Release setup | branch reconciliation, version proposal | DONE |
| 1 | Focus foundation | A1, A5 | DONE |
| 2 | Input and hints | A2 | DONE |
| 3 | Scrolling and notifications | A3, A6 | DONE |
| 4 | Mouse and testing | A4, A7, A8 | DONE |
| 5 | Example application | E1 to E3 | DONE |
| 6 | Documentation | D1 to D4 | DONE |
| 7 | Verification and publishing | P1 to P5 | DONE (P3 manual check pending) |
| 8 | Armada adoption (tracked here, done in Armada) | X1 | TODO |

_Last updated: 2026-10-05. A1 to A8 and the A1 follow-ups shipped in 1.4.0; P3 (manual terminal check) and X1 (Armada adoption) remain._

---

## How to use this plan

Every item has an ID, a status line you edit in place, and checklists of concrete tasks. Tick boxes as work lands.
Flip an item to `DONE` only when its code, example, tests, and docs are complete and the solution builds clean with
`TreatWarningsAsErrors` on all three targets (`netstandard2.0`, `net8.0`, `net10.0`). Record anything that changes the
design under the item's **Notes** line rather than editing the design text, so the history stays readable.

Status values: `TODO` (not started), `WIP` (in progress), `DONE` (complete and tested), `BLOCKED` (write the blocker in
Notes), `N/A` (justify in Notes). Update the status columns in [Summary](#summary) and
[Release at a glance](#release-at-a-glance), and the "Last updated" line, whenever an item changes state.

---

## Global obligations

These apply to every code item. They are repeated here so a reviewer can check each item against one list instead of
re-reading five requirement documents.

**Code.** Follow `CLAUDE.md` exactly:

- usings inside the namespace, system usings first, both alphabetized;
- one class or enum per file;
- XML docs on every public member, with defaults, ranges, nullability, thread safety, and `<exception>` tags;
- no docs on private members;
- `_PascalCase` private fields, no `var`, no tuples;
- configurable public members with validated setters instead of constants;
- specific exception types;
- no `Console.Write*` in the library;
- `#if` only in the backend abstraction and the `netstandard2.0` shim.

Every new API is additive. Existing defaults keep the 1.3.0 behavior unless a row in the item says otherwise and the
CHANGELOG lists it under "Changed".

**Tests.** Touchstone descriptors live in `src/Test.Shared/Suites`, are registered in `TUIKitSuites.All`, and run
unchanged through `Test.Automated`, `Test.Xunit`, and `Test.Nunit`. Every item needs positive cases (the feature works)
and negative cases (bad input throws the documented exception, disabled or absent state does nothing, boundaries
hold). Tests are deterministic: they use the headless backend and `WidgetTester`, a manual clock where time matters,
and no fixed sleeps. Assertions inspect typed state or cell styles and glyphs, not fuzzy text matches. Shared test
code never writes to the console.

**Theming.** Every new style is a theme role with a fallback, registered on `Theme.Dark`, `Theme.Light`, and
`Theme.HighContrast`, and it renders legibly with `UseAsciiBorders`. No visual state may rely on color alone. Each
focus or emphasis treatment also changes a glyph, an attribute, or a marker.

**Observability.** Where an item adds a user-visible interaction (a click region activated, a notification
coalesced), emit it through the existing `TUIKit` `Meter`/`ActivitySource` conventions in `TELEMETRY.md`, or record in
Notes why it does not belong there.

**Docs.** Each item adds its rows to `docs/SURFACE_COVERAGE.md` (type to suite) and is checked against
`docs/CONFORMANCE.md` in Phase 7.

---

## Phase 0: Release setup

**Status:** DONE
**Notes:** Local `main` fast-forwarded, `v1.3.0` tagged on `73ead0e`, `work/armada-upstream` deleted, work done on `feature/v1.4.0`.

GitHub's `main` already carries 1.3.0 (`73ead0e`, which matches the published NuGet package). The local clone at
plan time had a stale local `main` (`bdfcf76`, v1.2.1) and was checked out on `work/armada-upstream`, which sits at
the same commit as `origin/main`. Start from a clean, current `main` so the 1.4.0 diff is reviewable.

- [x] `git checkout main && git pull --ff-only` so local `main` matches `origin/main` at `73ead0e` or later. Confirm
      it builds and the full suite passes at 1.3.0. Tag `v1.3.0` on that commit if the tag is missing. Delete the
      local `work/armada-upstream` branch once it is fully contained in `main`.
- [x] Create the working branch `feature/v1.4.0` from `main`.
- [x] **Version proposal (requires owner approval, `VERSIONING.md` section 4).** Every item is additive, so the
      proposed version is `1.4.0` (MINOR). Ask the owner and record the decision and date here before touching any
      version field. Do not change any version until approval is recorded.
      Approved version: 1.4.0  Approved by/on: owner, 2026-10-05
- [x] Once approved, set `Version`, `AssemblyVersion` (`1.4.0.0`), and `FileVersion` (`1.4.0.0`) in
      `src/TUIKit/TUIKit.csproj`. Update `PackageReleaseNotes` (summary of this release plus the CHANGELOG link) and
      add `PackageTags` entries only for terms the release really adds (for example `focus`, `accessibility`).
- [x] Open `## [1.4.0] - YYYY-MM-DD` under `## [Unreleased]` in `CHANGELOG.md` and keep it current as items land.

---

## Phase 1: Focus foundation

### A1: Focus-within frame for regions and containers

**Status:** DONE  **Simplicity 6, Value 10**
**Notes:** Done. `IFocusPathNode.FocusedChild` added because `IFocusContainer` cannot name the containers between region and leaf, and adding a member would break implementers. `IFocusWithinAware` dropped: containers already propagate `OnFocusChanged` to their focused child, so it would duplicate that. `Region` got a new overload rather than extra optional parameters (binary compatibility). The host switch is `HighlightFocusedRegion` (off) plus per-region `WithFocusedBorder`. The partial-edge highlight became joined frames (`DrawJoinedBox`, `JoinBorders`). All follow-ups below are done. Telemetry: focus-path changes are not measured (too frequent); noted in TELEMETRY.md.

The single most visible failure in Armada was that focus was invisible. The sidebar drew a highlighted frame when
focused. The conversation list, the transcript, the composer, and the content of every tabbed screen drew nothing,
so users pressed keys into the wrong pane.

TUIKit already tracks focus precisely. `TuiApplication.FocusedRegion` names the focused region, `IFocusContainer`
exposes `FocusedLeaf`, and `IFocusAware.OnFocusChanged` tells a widget when focus arrives. What is missing is the
rendering contract. Today a bordered `Region` draws the same border whether or not it holds focus, and there is no
notion of "focus is somewhere inside this container" that a renderer can query.

Armada's answer is `src/Armada.Tui/Widgets/FocusFrame.cs` and `FocusFrameKindEnum.cs` (commit `739d70e5`, extended on
branch `work/tui-focus-frames`). Every pane reserves its border cells whether focused or not, so the layout never
shifts. The pane holding focus draws heavy lines in a focus color. When focus sits in a sub-pane, only the stretch of
border that sub-pane touches is drawn as focused. Below three rows or columns, the frame degrades to a one-column
gutter bar.

**Design.**

- `FocusPath` (new, `Widgets/FocusPath.cs`): an immutable snapshot of the chain from the focused region's root widget
  down to the focused leaf. It exposes `Region` (string), `Leaf` (`IFocusable?`), `Contains(object widget)`, and
  `Depth`. `TuiApplication` gains `public FocusPath CurrentFocusPath { get; }`, rebuilt whenever focus moves, and an
  event `FocusPathChanged`.
- `IFocusWithinAware` (new, optional): `void OnFocusWithinChanged(bool focusWithin)`. The host calls it on every
  container in the old and new paths whose membership changed, so a container can render "focus is inside me" without
  polling.
- `Region` gains a focused border treatment through new optional constructor parameters, with defaults that keep
  today's look: `focusedBorder` (`BorderStyle?`, default null meaning "same as `border`") and `focusedBorderRole`
  (string, default `Theme.FocusBorderRole`). The host draws the focused variant while the region is the focused region.
  The border cells are reserved either way, so focus never changes layout.
- `FocusFrame` (new public static helper, `Widgets/FocusFrame.cs`): `Draw(ISurface surface, Rect rect, bool focused,
  Theme theme, FocusFrameOptions options)` so custom widgets and sub-panes get the identical treatment. It covers the
  partial-edge highlight for a focused sub-pane (`Draw(..., Rect focusedInner)`) and the narrow-space gutter fallback.
- `FocusFrameOptions` (new): `FocusedStyle` (`BorderStyle`, default `Heavy`), `UnfocusedStyle` (default `Single`),
  `TitleMarker` (string, default `"> "`, empty to disable), and `MinimumBoxSize` (int, default 3, minimum 2; below it
  the gutter bar is used).
- `Theme` gains `FocusBorderRole` and `FocusTitleRole`. Defaults: bright yellow on Dark, dark blue on Light, inverse on
  HighContrast. With `UseAsciiBorders`, heavy becomes `#` corners and `=`/`H` edges, so focus differs by glyph as well
  as color.
- `TabView`, `SplitView`, `ScrollView`, `Collapsible`, and `FocusScope` implement `IFocusWithinAware`. `SplitView`
  draws its pane borders through `FocusFrame` when `ShowPaneFrames` (new, default false) is on.

**Tasks.**

- [x] `FocusPath`, `IFocusWithinAware`, `FocusFrameOptions`, and `FocusFrame` with full XML docs.
- [x] `TuiApplication`: compute `CurrentFocusPath` after every focus move (region ring, `FocusManager`, container
      traversal, mouse focus), raise `FocusPathChanged`, and call `OnFocusWithinChanged` on path changes. Keep it
      allocation-free when the path is unchanged.
- [x] `Region` and the host's border renderer: draw the focused treatment and the title marker.
- [x] Theme roles on all three built-in themes, plus `ThemeApplier` support.
- [x] `IFocusWithinAware` on the five containers named above, and `SplitView.ShowPaneFrames`.
- [x] Telemetry: record that focus-path changes are not measured (too frequent), in Notes.

**Tests (`FocusFrameSuite`, new).**

- [x] Positive: a two-region layout draws the focused region's border with heavy glyphs and the focus style, and the
      other with single glyphs. Moving focus swaps them. The reserved border never moves content (compare content
      cell positions before and after).
- [x] Positive: focus inside a nested `FocusScope` in a `SplitView` pane calls `OnFocusWithinChanged(true)` on exactly
      the containers in the path, and `false` on the ones that left it.
- [x] Positive: `FocusFrame.Draw` with `focusedInner` highlights only the edge segments the inner rect touches.
- [x] Positive: `UseAsciiBorders` and `Theme.HighContrast` still differ by glyph between focused and unfocused.
- [x] Positive: the title marker appears only on the focused region's title.
- [x] Negative: `FocusFrameOptions.MinimumBoxSize` below 2 throws `ArgumentOutOfRangeException`. `Draw` with a null
      surface or theme throws `ArgumentNullException`. A zero-size rect draws nothing and does not throw.
- [x] Negative: a region with `BorderStyle.None` and no focused style draws no frame when focused (opt-in only).
- [x] Boundary: at 2 columns wide the gutter fallback is used. At 80x24 the Example's main layout still fits.

**Acceptance.** In the Example (E1), Tab through every region and every container: exactly one frame is highlighted,
it contains the focused widget, and nothing shifts.

**Additional findings from Armada's final focus implementation (merged as Armada `fc2e5528`).** These came from
applying one focus box to every screen of a large app. Fold them into A1 or track them as A1 follow-ups:

- [x] `DialogModal` hardcodes a rounded border (`ascii = false`). Make the border style settable, and expose the
      box rectangle publicly (`ContentBounds`/`FrameBounds`), so apps can draw the focus box exactly on a dialog's edge
      without duplicating its geometry.
- [x] Add a post-render overlay hook (draw after all widgets have rendered), or a container that draws boxes around
      its children. Armada draws every focus box from the shell after rendering, which only works because the shell
      owns all layout.
- [x] Give modals a focused or topmost state (an `IsTopmost`, or `OnFocusChanged` for modals), so the top dialog can
      mark itself and the panes behind it go plain.
- [x] Boxes share edges: neighbouring panes are separated by one line, and the focused box is drawn whole over the
      shared line. Document this as the expected behavior of `FocusFrame` with adjacent regions.
- [x] Hidden or empty widgets must not be focus stops. Armada found a hidden sidebar, empty button rows, empty
      filter bars, and actions hidden while loading, all still in the Tab order. Consider having `FocusManager` and
      containers skip widgets that render nothing (a `IsVisible` contract), and cover it in A8's audit
      (`FocusAuditProblemKind.InvisibleStop`).
- [x] Nested box drawing with automatic tee and cross joins (`FocusFrame.DrawNested` in Armada): adjacent and nested
      boxes share lines, and `BorderStyle` drawing picks the right junction glyph (single, heavy, and ASCII). Build it
      on the existing box drawing so `SplitView` panes and framed regions join cleanly.

---

### A5: `TabView` focused-tab treatment

**Status:** DONE  **Simplicity 9, Value 6**
**Notes:** Done. `StripFocusStop` is opt-in and needs `ForwardKeys`. `TabFocusMarker` must be empty or one cell (default `>`), so the strip never changes width. `FocusedTabStyle` is nullable; null derives bold and underline from `ActiveStyle`.

The owner's report called out tabbed screens by name. A `TabView` has two different things a user can be "on": the
tab strip, where Left and Right switch tabs, and the content below it. Today the selected tab looks the same in both
cases, so after pressing Tab the user cannot tell whether arrows will switch tabs or move inside the content. Armada
patched this per screen on the focus branch. It belongs in the widget.

**Design.**

- `TabView` gains `FocusedTabStyle` (`CellStyle`, default resolved from `Theme.TabFocusedRole`, falling back to
  `Selection` plus `Underline`), `TabFocusMarker` (string, default `"> "`, empty to disable), and a read-only
  `IsStripFocused` (true while keys go to the strip rather than the active content).
- While the strip holds focus, the selected tab draws with `FocusedTabStyle` and the marker. While content holds
  focus, the selected tab draws with the existing selected style, and the content area gets the A1 frame when the
  `TabView` sits inside a framed region.
- `Theme.TabFocusedRole` is added to the three built-in themes.

**Tasks.**

- [x] Properties, theme role, and rendering in `Widgets/TabView.cs`.
- [x] `IsStripFocused` transitions on Tab and Shift+Tab, mouse click on the strip, and `FocusEdge`.

**Tests (extend `FocusScopeSuite` or add `TabFocusSuite`).**

- [x] Positive: with focus on the strip, the selected tab cell carries `FocusedTabStyle` and the marker. After Tab
      into content, it carries the plain selected style, and `IsStripFocused` is false.
- [x] Positive: a mouse click on a tab focuses the strip and selects that tab.
- [x] Negative: an empty `TabView` with focus draws no marker and does not throw. A null `FocusedTabStyle` assignment
      throws `ArgumentNullException` if the property is reference-typed; otherwise document that the default applies.
- [x] Positive: the marker disappears when `TabFocusMarker` is empty, and the non-color cue (underline) remains.

---

## Phase 2: Input and hints

### A2: Focus-aware key hints and text-entry awareness

**Status:** DONE  **Simplicity 6, Value 9**
**Notes:** Depends on A1 (`FocusPath`). Done. Hints also come from `CommandRegistry` (`AddCommands`) and app hints. `IKeyHintSource.GetKeyHints()` takes no path. The help-key swap and the typing marker were cut in review. `LeaveTextHint` defaults to `Tab Next field`, since Tab is how every built-in text widget is left. Binding is `TuiApplication.BindKeyHints` plus `StatusBar.HintSource`; fixed hints stay pinned. Hint sources on `ListView`, `DataTable`, `TabView`, and `TextEditor`.

Armada users had to press Esc to leave the message box before `a` would approve anything, and nothing told them so.
The status bar listed the same keys whatever had focus. Worse, it advertised single-letter keys that would, at that
moment, be typed into a text field. The fix that tested well in Armada has three parts:

- a marker for widgets that accept typed text (`src/Armada.Tui/Widgets/ITextEntry.cs`);
- a way for the focused widget or its container to describe its own keys (`IFocusHintSource.cs`, `FocusHints.cs`,
  `ScreenBase.ResolveHints`);
- a rule for which chords still work while typing (`src/Armada.Tui/Input/KeyStroke.cs`, `WorksWhileTyping`).

The first hint in a text field always says how to leave it and what that unlocks. Single-letter keys are hidden while
typing. Help reads `F1 Help` instead of `? Help`, because `?` would type a question mark.

TUIKit has the raw parts: `StatusBar` (a static key/label list), `KeyChord`, `KeyLabel`, and `CommandRegistry`. It is
missing the link between focus and what the bar shows.

**Design.**

- `ITextEntry` (new): `bool AcceptsText { get; }`. Implemented by `TextField`, `TextEditor`, and `ComboBox` (when
  editable), and by any app widget that captures printable keys. `AcceptsText` is false while read-only or disabled.
- `KeyHint` (new class): `Key` (string label, e.g. `"Esc"`), `Description` (string), `Priority` (int, default 0;
  higher shows first), `Chord` (`KeyChord?`, used for the typing filter).
- `IKeyHintSource` (new): `IReadOnlyList<KeyHint> GetKeyHints(FocusPath path)`. Any widget or container in the focus
  path may implement it. The leaf contributes first, then each ancestor outward, then the app-level hints.
- `KeyHintResolver` (new class): merges hints along a `FocusPath`, de-duplicates by key (the innermost wins), and when
  the leaf is an `ITextEntry` with `AcceptsText` true it does three things:
  - drops chords that would type (printable, unmodified);
  - prepends the leave hint from `LeaveTextHint` (default `Esc`, with a description supplied by the nearest source or
    `"Leave the text field"`);
  - swaps any hint flagged `HelpKey` to its `TypingAlternative` (for example `?` becomes `F1`).

  Properties: `HideTypingChords` (bool, default true), `MaxHints` (int, default 12, range 1 to 64).
- `KeyChord.InsertsTextWhenTyping()` (new instance method): true for printable characters without Ctrl, Alt, or Meta.
  The resolver uses it instead of string inspection.
- `StatusBar` gains `Bind(TuiApplication app, KeyHintResolver resolver)`, so its contents follow `FocusPathChanged`
  automatically, and `PinnedHints` (always shown, last). The existing `Add`/`Clear` API keeps working for static bars.
- `TextField` and `TextEditor` gain `ShowTypingMarker` (bool, default false) and `TypingMarker` (string, default
  `"[typing]"`). When on, the marker renders in the field's frame or right edge while focused, in bold reverse, so
  "keys go into this box" is visible without color.

**Tasks.**

- [x] `ITextEntry`, `KeyHint`, `IKeyHintSource`, `KeyHintResolver`, and `KeyChord.InsertsTextWhenTyping` with XML docs.
- [x] `ITextEntry` on `TextField`, `TextEditor`, and editable `ComboBox`.
- [x] `IKeyHintSource` on `ListView`, `DataTable`, `TabView`, `TextEditor`, and `Form`, describing their real keys.
- [x] `StatusBar.Bind`, `PinnedHints`, and the help-key swap. Keep `?` visible for non-typing focus.
- [x] Typing marker on `TextField` and `TextEditor`.

**Tests (`KeyHintsSuite`, new).**

- [x] Positive: with a `DataTable` focused, the bound bar shows the table's hints. After Tab into a `TextField`, the
      first hint is the leave hint, no unmodified printable chord is listed, and Help reads `F1`.
- [x] Positive: innermost-wins de-duplication (a container and its leaf both define `Enter`; the leaf's text shows).
- [x] Positive: `Priority` ordering and `MaxHints` truncation keep pinned hints.
- [x] Positive: read-only and disabled text fields report `AcceptsText` false and get normal hints.
- [x] Positive: `ShowTypingMarker` renders the marker only while focused.
- [x] Negative: `MaxHints` outside 1 to 64 throws `ArgumentOutOfRangeException`. `KeyHint` with a null or empty `Key`
      throws `ArgumentException`. `StatusBar.Bind` with null arguments throws `ArgumentNullException`.
- [x] Negative: an `IKeyHintSource` that returns null is treated as empty and does not break the bar.
- [x] Table-driven: `KeyChord.InsertsTextWhenTyping` over letters, digits, punctuation, Space, Ctrl+letter, Alt+letter,
      F-keys, Esc, Enter, and arrows.

---

## Phase 3: Scrolling and notifications

### A3: Tail-follow scrolling with a new-content indicator

**Status:** DONE  **Simplicity 7, Value 8**
**Notes:** Done, rescoped. `Pane` already followed the tail; it now runs on `TailFollow` (thread-safe, not UI-thread only, because panes take writes from any thread) and draws the indicator. `ListView` gets opt-in `TailFollow` plus `Append`/`AppendRange`. `ScrollView` dropped. The indicator format lives on `TailFollow`. `DataTable` is a 1.4.x candidate.

A streaming view (chat, log, build output) should follow new content while the reader is at the bottom and stop the
moment they scroll up. Armada's first version got this wrong in a subtle way. Selecting a message to act on it counted
as "scrolled away", so after an approval the transcript stopped following, and the owner kept pressing End. The model
that tested well (`src/Armada.Tui/Screens/Ask/AskTranscriptView.cs`: `Following`, `NewBelow`, `FollowTail`) has three
rules:

- only a viewport move away from the tail detaches; selection alone never does;
- new content keeps following while the viewport is at the bottom, even with something selected;
- End, or any action the app marks as "return to tail", re-attaches.

While detached, an "N new below" counter shows.

TUIKit's `Pane` has `IsAtBottom`, `ScrollUp`, `ScrollDown`, and `ScrollToBottom`, and `ScrollView` has
`AutoScrollToFocus`. Neither owns the follow decision, so every app re-derives it.

**Design.**

- `TailFollow` (new class, `Content/TailFollow.cs`): a small state machine any scrollable view can own. It exposes:
  - members: `IsFollowing` (bool), `NewItemsBelow` (int), `Mode` (`TailFollowMode`, default `FollowAtBottom`);
  - `OnViewportMoved(int scrollY, int maxScrollY)`, which detaches when scrolled above the bottom and re-attaches on
    reaching it;
  - `OnContentAppended(int addedRows)`, which counts while detached and returns whether to scroll;
  - `ReturnToTail()`;
  - event `FollowingChanged`.

  Selection never calls it. That is the documented contract.
- `TailFollowMode` (new enum): `FollowAtBottom` (default), `AlwaysFollow`, `Never`.
- `Pane` and `ScrollView` gain `TailFollow` (a `TailFollow?`; null keeps today's behavior) and
  `ShowNewItemsIndicator` (bool, default true when `TailFollow` is set). The indicator line reads `{0} new below`,
  with the format in `NewItemsIndicatorFormat` (string, validated to contain `{0}`). Clicking it calls `ReturnToTail`.
- `StreamingTranscript` uses its pane's `TailFollow` when present.

**Tasks.**

- [x] `TailFollow` and `TailFollowMode` with XML docs and thread-safety notes (UI-thread only; document it).
- [x] Integration in `Pane`, `ScrollView`, and `StreamingTranscript`; End and Ctrl+End call `ReturnToTail`.
- [x] Indicator rendering, the click handler, and the `NewItemsIndicatorFormat` validation.

**Tests (`TailFollowSuite`, new).**

- [x] Positive: appending while at the bottom keeps the last line visible. After `ScrollUp`, appends do not move the
      viewport and `NewItemsBelow` counts them. End re-attaches and zeroes the counter.
- [x] Positive: changing a selected row (`ListView.SelectedIndex`) while at the bottom does not detach. The core
      regression: append after a selection change still follows.
- [x] Positive: `ReturnToTail()` after an app action re-attaches from a detached state.
- [x] Positive: `AlwaysFollow` ignores scroll-up, and `Never` never auto-scrolls.
- [x] Positive: clicking the indicator re-attaches (uses A4).
- [x] Negative: `NewItemsIndicatorFormat` without `{0}` throws `ArgumentException`, and null throws
      `ArgumentNullException`. `OnContentAppended` with a negative count throws `ArgumentOutOfRangeException`.
- [x] Negative: with `TailFollow` null, `Pane` behaves exactly as in 1.3.0 (existing pane suites unchanged and green).

---

### A6: Notification coalescing and a gapless toast stack

**Status:** DONE  **Simplicity 9, Value 6**
**Notes:** Done. `ToastSpacing` and the trimming were dropped: TUIKit already trims and stacks toasts without gaps. Actions match by instance, not label. `CoalesceRepeats` defaults to true and is listed under Changed.

Pressing a key that raises the same hint five times produced five identical toasts, each followed by a blank row. The
owner described it as the screen "skipping lines". Armada's fix (`src/Armada.Tui/Services/NotificationService.cs`
`Toast`, `ToastEntry.Repeat`, `src/Armada.Tui/Shell/ToastLayer.cs`) refreshes a still-visible toast with the same
severity, text, and action instead of adding one, shows a count (`(x3)`), restarts its timer, and draws toasts
without an empty separator row. TUIKit's `NotificationCenter` has the same stacking behavior, so the fix belongs there
and Armada's parallel toast layer can go away (X1).

**Design.**

- `Notification` gains `RepeatCount` (int, read-only, starts at 1) and `LastRaisedAtMilliseconds` (long).
- `NotificationCenter` gains `CoalesceRepeats` (bool, default true) and `RepeatSuffixFormat` (string, default
  `" (x{0})"`, validated to contain `{0}`). `Add` coalesces when an active, undismissed notification matches on
  severity, text, title, and the action labels. It increments `RepeatCount`, refreshes the timeout from now, moves the
  notification to newest, raises `Changed`, and returns the existing instance.
- `NotificationCenter` gains `ToastSpacing` (int, default 0, range 0 to 2) for the rows between stacked toasts. The
  default changes from the current implicit gap to 0. List it under "Changed" in the CHANGELOG.
- Text is trimmed of leading and trailing line breaks before layout, so a stray newline cannot add an empty row.
- Telemetry: count coalesced notifications in the existing TUIKit meter (`tuikit.notifications.coalesced`).

**Tasks.**

- [x] Members, coalescing in both `Add` overloads, the trimming, `ToastSpacing`, and the meter counter.
- [x] Update `NotificationHistoryModal` to show the repeat count.

**Tests (extend `BackendModalValidationSuite` or add `NotificationCoalescingSuite`).**

- [x] Positive: three identical `Add` calls inside the timeout produce one active notification with `RepeatCount` 3,
      the suffix renders, and the expiry moves to the last call plus the timeout (manual clock).
- [x] Positive: a different severity, text, title, or action set makes a separate notification.
- [x] Positive: an identical `Add` after the first expired creates a new one with `RepeatCount` 1.
- [x] Positive: with `ToastSpacing` 0, two stacked toasts occupy adjacent rows (cell inspection). Text with leading or
      trailing `\n` renders no empty row.
- [x] Positive: `CoalesceRepeats` false restores 1.3.0 stacking.
- [x] Negative: `ToastSpacing` outside 0 to 2 throws `ArgumentOutOfRangeException`. `RepeatSuffixFormat` without
      `{0}` throws `ArgumentException`.
- [x] Telemetry: the coalesced counter increments through the test `MeterListener` (see `TelemetryCapture`).

---

## Phase 4: Mouse and testing

### A4: Headless mouse routing in the test host

**Status:** DONE  **Simplicity 8, Value 7**
**Notes:** A7 and the click cases in A3 depend on this. Done, rescoped. Routing through the hit map already worked headlessly. Added `MouseSequenceEncoder` and `HeadlessBackend.Feed*` helpers instead of a public `InjectMouse`; `WidgetTester` mouse methods are local. Added `TuiApplication.CaptureFrame` for cell assertions.

Armada added clickable buttons to cards and rows, then could not test a real click. The headless host never draws
frames through TUIKit's hit map, so clicks cannot be routed. Armada's `TuiTestHost.Click` hands the click straight to
the shell instead (`src/Test.Shared/Infrastructure/TuiTestHost.cs`), which means its tests skip exactly the routing
code a user exercises. TUIKit owns the hit map (`TuiApplication._HitMap`, `HitTest`, `HitTestEntry`) and the input
parser, so TUIKit is the only place this can be fixed properly.

**Design.**

- `WidgetTester` gains `Click(int x, int y, MouseButton button = MouseButton.Left)`, `DoubleClick(int x, int y)`,
  `Wheel(int x, int y, int delta)`, `Move(int x, int y)`, and `Drag(int fromX, int fromY, int toX, int toY)`. Each
  renders first if the frame is stale, then dispatches through the same `IMouseAware` translation the host uses.
- `TuiApplication` gains a test-facing, public, documented `InjectMouse(MouseEvent mouse)` that goes through the real
  `HitTest`, focus-on-click, hover, and `ClickSynthesizer` paths on the headless backend. It is not a parallel
  shortcut. Doc comment: intended for tests and automation, thread-safe through the app's post queue.
- `HeadlessBackend` (or the existing headless terminal) renders frames on demand so the hit map is populated before
  injection.

**Tasks.**

- [x] `WidgetTester` mouse methods and `TuiApplication.InjectMouse`.
- [x] Hit map populated on headless renders. Document the ordering guarantee (render, then inject).

**Tests (`HeadlessMouseSuite`, new).**

- [x] Positive: `Click` on a `Button` inside a region activates it through the hit map, and focus moves to that region.
- [x] Positive: `Wheel` over a `ScrollView` scrolls it. `DoubleClick` on a `ListView` row raises `ItemActivated`.
- [x] Positive: `Drag` on a `SplitView` divider resizes the panes.
- [x] Negative: a click outside any region is ignored without throwing. Coordinates outside the screen throw
      `ArgumentOutOfRangeException`. `InjectMouse(null)` throws `ArgumentNullException`.
- [x] Negative: with `EnableMouseRouting` false, injected clicks reach no widget (asserted by state, not by absence of
      an exception).

---

### A7: Inline click regions for custom-rendered widgets

**Status:** DONE  **Simplicity 5, Value 8**
**Notes:** Depends on A4 for tests. Done. Areas are recorded in drawing coordinates on every render, so there is no scroll-offset logic; the map tracks hover. No `IClickRegionHost` or dispatcher type was needed. Toast clicks moved onto `ClickRegionMap`. The telemetry counter is unlabeled.

TUIKit routes a click to a widget. It does not help a widget that draws its own rows find out which sub-area was
clicked. Armada drew `[Approve] a  [Reject] r` inside list rows and transcript cards, and recorded each button's
rectangle by hand during render (`src/Armada.Tui/Approvals/ApprovalButton.cs`, `ApprovalsScreen.Buttons()`,
`src/Armada.Tui/Screens/Ask/AskCardButton.cs`, `AskCardActionEnum.cs`). It then hit-tested those rectangles itself in
`HandleMouse`. Any app with action rows, inline links, or chips writes the same code, and gets it subtly wrong when the
view scrolls.

**Design.**

- `ClickRegionMap<TAction>` (new, generic so no domain enum leaks in): a per-frame list of `ClickRegion<TAction>`
  (`Rect Area`, `TAction Action`, `string? Key`, `string? Tooltip`). It exposes `Clear()`, `Add(...)`,
  `HitTest(Point)`, and `Regions`.
- `InlineButton` (new static helper): `int Draw<TAction>(ISurface surface, int x, int y, string label, string? key,
  TAction action, ClickRegionMap<TAction> map, CellStyle style, CellStyle keyStyle)`. It draws `[Label] k`, records
  the region, and returns the width used. Truncation never draws half a button.
- `IClickRegionHost<TAction>` (new, optional): a widget exposes its map. A default `IMouseAware` adapter
  (`ClickRegionDispatcher<TAction>`) translates a click into `ActionInvoked(TAction, ClickRegion<TAction>)`, applying
  the widget's current scroll offset, so scrolled content clicks correctly.
- Hover feedback: regions under the pointer render with `Theme.InlineButtonHoverRole` when the host hover tracking
  is on. Keyboard parity: the `Key` is shown so every click has a key equivalent (documented requirement).
- Telemetry: `tuikit.click_regions.invoked` counter, tagged by the widget's type name only (no labels, which may be
  user content).

**Tasks.**

- [x] `ClickRegion<TAction>`, `ClickRegionMap<TAction>`, `InlineButton`, `IClickRegionHost<TAction>`,
      `ClickRegionDispatcher<TAction>`, and the theme role, with XML docs.
- [x] Scroll-offset handling in `ListView` and `ScrollView` hosting, and documentation of the rule.

**Tests (`ClickRegionSuite`, new; uses A4).**

- [x] Positive: a custom widget draws two inline buttons per row in a `ListView`. A click on the second row's second
      button raises `ActionInvoked` with that row's action.
- [x] Positive: after scrolling the list by 5 rows, clicking the same screen cell resolves to the newly visible row.
- [x] Positive: a narrow width truncates whole buttons, never half, and records no region for a dropped button.
- [x] Positive: hover style applies only under the pointer.
- [x] Negative: a click on the gap between buttons invokes nothing. `Add` with an empty rect is ignored. A null label
      throws `ArgumentNullException`.
- [x] Negative: regions from the previous frame are not hit after `Clear` (no stale clicks).

---

### A8: Focus audit test helper

**Status:** DONE  **Simplicity 7, Value 6**
**Notes:** Depends on A1. Done, smaller. It checks typed state (focus path, frame settings, hit map) instead of scanning cells. Kinds: `NoFocusedLeaf`, `NoFocusIndicator`, `TraversalStuck`, `TraversalDidNotCycle`, `TraversalNotSymmetric`, `LayoutShifted`, `InvisibleStop`. A multiple-frames check was dropped because it needed cell heuristics.

Armada's broadest test of the focus fix is a sweep: render every screen, cycle focus through every stop, and assert
that exactly one region carries the focused treatment and that it contains the focused widget
(`TuiFocusSweep.cs` on branch `work/tui-focus-frames`). Any TUIKit app with more than a few screens wants that
guarantee, and it is only practical once A1 makes "focused treatment" a typed fact instead of a look.

**Design.**

- `FocusAudit` (new, `Testing/FocusAudit.cs`): `FocusAuditResult Run(TuiApplication app, FocusAuditOptions options)`.
  It walks the region focus ring and every container stop (Tab, then Shift+Tab back). At each stop it reads
  `CurrentFocusPath` and inspects the rendered surface for focus-frame cells. The result holds `Stops` (count) and
  `Problems` (list of `FocusAuditProblem`, each with `Stop`, `Kind` (`FocusAuditProblemKind`: `NoFocusedFrame`,
  `MultipleFocusedFrames`, `FrameDoesNotContainLeaf`, `TraversalNotSymmetric`, `LayoutShifted`), and `Detail`).
- `FocusAuditOptions`: `MaxStops` (int, default 500, range 1 to 10000), `CheckSymmetry` (default true), and
  `CheckLayoutStable` (default true). The last compares content cell positions between stops.

**Tasks.**

- [x] `FocusAudit`, `FocusAuditOptions`, `FocusAuditResult`, `FocusAuditProblem`, and `FocusAuditProblemKind`.
- [x] Use it in the Example's own test (`--focus-audit-once`, see E2).

**Tests (`FocusAuditSuite`, new).**

- [x] Positive: a correct three-region layout with a nested `FocusScope` audits clean, and the stop count equals the
      known number of focusable leaves.
- [x] Negative (the audit catches each defect): a region whose widget draws its own unconditional highlight reports
      `MultipleFocusedFrames`; a container that never calls through to `FocusFrame` reports `NoFocusedFrame`; a
      widget that changes size on focus reports `LayoutShifted`; a container whose `MoveFocus` skips a child going
      backward reports `TraversalNotSymmetric`.
- [x] Negative: `MaxStops` out of range throws `ArgumentOutOfRangeException`, and `Run(null, ...)` throws
      `ArgumentNullException`.

---

## Considered and left in Armada

These pieces of Armada's work are app behavior built on the primitives above, and stay out of TUIKit:

- the pending-approval strip above the Ask composer;
- `Alt+Down` to jump to the oldest pending card;
- the chooser when one reply proposed several actions;
- deciding from whichever row is highlighted;
- the CLI permission cards.

A generic "attention strip" widget was considered. It was rejected: a `Label` in a region plus A2's hints already
covers it, and a dedicated widget would just be a styled label with an opinion about wording.

---

## Implementation order

1. Phase 0 (branch reconciliation and the version decision).
2. A4, because A3 and A7 need it in their tests.
3. A1, then A5 and A8, which read A1's focus path.
4. A2, which needs `FocusPath`.
5. A6 and A3 (independent; either order).
6. A7.
7. Phases 5, 6, and 7.

---

## Phase 5: Example application

The Example app is how most people first see TUIKit. Every capability above must be visible there and exercised by
its headless snapshot modes, which double as smoke tests.

### E1: Guided tour pages

**Status:** DONE
**Notes:** Four pages added after Welcome. The tour composes pages in an overlay, so a region-based focus showcase (`--focus`, `FocusShowcase.cs`) was added to show host-level focus faithfully. The typing marker was cut, so that page shows the key-hint change instead.

- [x] Add a "Focus you can see" page to `GuidedTour` (`src/TUIKit.Example/GuidedTour.cs`, `BuildPages`). It has three
      framed regions and a `TabView`, a live line showing `CurrentFocusPath`, and instructions to press Tab. Covers
      A1 and A5.
- [x] Add a "Keys follow focus" page: a `DataTable`, a `TextField` with `ShowTypingMarker`, and a bound `StatusBar`.
      The page text tells the reader to watch the bar change when they Tab into the field. Covers A2.
- [x] Add a "Streaming without losing your place" page: a pane fed by a timer with `TailFollow`, a selectable list
      beside it, and the "N new below" indicator. Covers A3.
- [x] Add a "Clickable rows" page: a list with `InlineButton` actions, and a toast on each invocation that repeats to
      show coalescing. Covers A6 and A7.
- [x] Each page's code snippet (the `TourPage` code array) shows the real API calls, with no `var`.

### E2: Headless snapshot coverage

**Status:** DONE
**Notes:** `--tour-once` covers the pages; `--focus-once` and `--focus-audit-once` run the showcase (audit: 5 stops, 0 problems).

- [x] `--tour-once` renders the four new pages (existing mode; extend the page list it snapshots).
- [x] New `--focus-audit-once` runs `FocusAudit` over the tour's layouts and exits non-zero on any problem. Add it to
      the README's headless modes list.

### E3: Example README

**Status:** DONE
**Notes:**

- [x] `src/TUIKit.Example/README.md` lists the new pages, their keys, and `--focus-audit-once`.

---

## Phase 6: Documentation

### D1: README

**Status:** DONE
**Notes:**

- [x] "What it does": add focus frames, focus-aware key hints, tail-follow, notification coalescing, inline click
      regions, and headless mouse plus focus audit for tests, woven into the existing bullets rather than a new list.
- [x] Add a short "Focus and key hints" subsection after "Quick start", with a 10 to 15 line example binding a
      `StatusBar` to an app with framed regions.
- [x] "Headless and non-interactive modes": document `WidgetTester` mouse methods, `InjectMouse`, and `FocusAudit`.
- [x] Check every README code sample still compiles against 1.4.0 (paste into a scratch test or the Example).

### D2: CHANGELOG

**Status:** DONE
**Notes:**

- [x] `## [1.4.0] - YYYY-MM-DD` with "Added" (A1 to A8, one bullet each naming the public types) and "Changed" (the
      A6 `ToastSpacing` default). Note that everything else is additive and that defaults keep 1.3.0 behavior. Credit
      Armada as the motivating client, as the 1.3.0 entry does.

### D3: Coverage and conformance

**Status:** DONE
**Notes:**

- [x] `docs/SURFACE_COVERAGE.md`: one row per new public type mapping to its suite.
- [x] `docs/CONFORMANCE.md`: a 1.4.0 addendum recording the requirements check from P2.

### D4: BUILDING_TERMINAL_APPS.md

**Status:** DONE
**Notes:** Also covers joined frames, hidden focus stops, inline click regions, and the testing helpers.

- [x] Add guidance under the existing focus and input material: "make focus visible" (A1), "never advertise a key
      that would type" (A2), and "selection is not scrolling" (A3). Explain each with the reason from this plan.

---

## Phase 7: Verification and publishing

### P1: Build and test

**Status:** DONE
**Notes:** 0 warnings on all three targets. 776 console cases on net8.0 and net10.0; 777 through xUnit and NUnit on both. All Example headless modes exit 0. Coverage: TUIKit 81.6%, Test.Shared 99.4%.

- [x] `dotnet build src/TUIKit.sln -c Release` with zero warnings on `netstandard2.0`, `net8.0`, and `net10.0`.
- [x] `dotnet run --project src/Test.Automated -c Release -f net8.0` and `-f net10.0`: all suites green. Record the
      totals here.
- [x] `dotnet test src/Test.Xunit` and `dotnet test src/Test.Nunit` on both frameworks: same descriptors, all green.
- [x] Example headless modes: `--once`, `--tour-once`, `--contract-once`, and `--focus-audit-once` exit 0.
- [x] Coverage run (`dotnet test src/Test.Xunit --collect:"XPlat Code Coverage"`); record the figure in
      `docs/SURFACE_COVERAGE.md`.

### P2: Requirements audit

**Status:** DONE
**Notes:** No var, tuples, or console writes in changed files; no em dashes or smart quotes in added text.

- [x] Walk `CODE_STYLE.md` against every new or changed file: one type per file, usings, docs, no `var`, no tuples,
      exceptions, no console output, and `.Any()`/`.FirstOrDefault()` usage.
- [x] `grep -nP '[^\x00-\x7F]'` over changed `.cs` and `.md` files to catch em dashes and smart quotes. Fix any hit.
- [x] Confirm no version field changed without the Phase 0 approval.

### P3: Manual check in real terminals

**Status:** TODO
**Notes:** Not run: needs real terminals. Left for the owner.

- [ ] Run the Example interactively in Windows Terminal, conhost, iTerm2, Terminal.app, GNOME Terminal, over SSH, and
      in tmux. Check that the focus frame, the tab marker, the typing marker, the indicator click, and the inline
      buttons render and respond. Record terminal-specific findings here.

### P4: Package and publish to NuGet

**Status:** DONE
**Notes:** Published from merged `main` with the symbols package.

Packages are built from the merged `main`, never from a feature branch. The project already sets `IncludeSymbols` and
`SymbolPackageFormat=snupkg`, so one pack produces both files.

- [x] Merge `feature/v1.4.0` into `main`. Tag `v1.4.0` and push the tag.
- [x] `dotnet pack src/TUIKit/TUIKit.csproj -c Release -o artifacts/1.4.0` produces `TUIKit.1.4.0.nupkg` and
      `TUIKit.1.4.0.snupkg`.
- [x] Inspect the `.nupkg`: README, `logo.png`, the `fonts/` license files, XML docs for all three targets, and the
      release notes. The `.snupkg` must hold portable PDBs for all three targets. Check that Source Link resolves
      (`dotnet-validate package local artifacts/1.4.0/TUIKit.1.4.0.nupkg` or NuGet Package Explorer).
- [x] Publish: `dotnet nuget push artifacts/1.4.0/TUIKit.1.4.0.nupkg --api-key $NUGET_API_KEY --source
      https://api.nuget.org/v3/index.json`. The `.snupkg` in the same folder is pushed to the symbol server
      automatically. If it is not, push it explicitly with the same command against the `.snupkg`. Keep the API key in
      an environment variable only; never write it to a file or the repository.
- [x] Verify `https://api.nuget.org/v3-flatcontainer/tuikit/index.json` lists `1.4.0`, and the symbol package shows
      as published on the package page.
- [x] Create the GitHub release for `v1.4.0` with the CHANGELOG section as its notes, attaching the `.nupkg` and
      `.snupkg`.

### P5: Template

**Status:** DONE
**Notes:** The template referenced 0.6.*; now 1.4.* with framed regions and a bound status bar. Verified to compile against the 1.4 library.

- [x] Update `templates/tuikit-app` to reference `TUIKit` 1.4.0, and show a framed region plus a bound `StatusBar` in
      its starter code, so new apps start with visible focus.

---

## Phase 8: Armada adoption (tracked here, done in Armada)

### X1: Replace Armada's private copies

**Status:** TODO
**Notes:**

After 1.4.0 is on NuGet, Armada bumps its `TUIKit` reference (an Armada 1.0.x change, under Armada's own versioning
approval). It then replaces its private implementations, keeping its tests green at every step:

- `FocusFrame`, `FocusFrameKindEnum`, and the focus sweep, with A1 and A8;
- `ITextEntry`, `IFocusHintSource`, `FocusHints`, the `ScreenBase.ResolveHints` logic, and
  `KeyStroke.WorksWhileTyping`, with A2;
- `AskTranscriptView` following, with A3;
- `ToastLayer` and the `NotificationService` toast list, with `NotificationCenter` and A6;
- the hand-rolled button rectangles in Approvals and Ask cards, with A7;
- `TuiTestHost.Click`, with real routing through A4.

- [ ] Open the Armada work item with this list and link it here.
- [ ] Record any API gap Armada hits during adoption as a 1.4.x follow-up here, rather than patching around it in
      Armada.
