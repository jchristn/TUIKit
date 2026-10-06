# TUIKit Improvements: Focus Layout, Hints, Toasts, and Testing (1.5)

TUIKit 1.4.0 shipped visible focus, key hints that follow focus, tail-follow, inline click regions, notification
coalescing, and real mouse input for tests. The first large app to adopt all of it (Armada's terminal client) could
replace most of its private code with TUIKit's, but not all of it. Each place it had to keep its own code exposes a
limit that any TUIKit app reaches once it grows past a few panes. Three examples:

- A focused frame that turns into mixed glyphs where it joins a neighbour is a defect in the 1.4 feature, not one
  app's preference.
- A toast that never coalesces because the caller passes a fresh lambda each time is the same kind of defect.
- So is a focus audit that only runs inside a started, process-wide application.

This plan fixes those limits for everyone.

The filter for inclusion was strict: an item is here only if it is broadly applicable and horizontally useful to apps
other than Armada. Three requests were left out:

- The `[typing]` marker on text fields was in the 1.4 plan and was cut on purpose. That call stands.
- `NotificationCenter.MaxConcurrent` and `ToastWidth` already cover the "max visible" and width requests.
- `FocusFrameOptions.GutterGlyph` already configures the focused gutter glyph; B9 only fixes the ASCII and unfocused
  cases around it.

Everything ships in one release, 1.5.0. The fixes to the 1.4 features are built first, and the two new widgets
(`FramedStack`, `TabStrip`) and the hint-system extensions come last, because `FramedStack` depends on half of the
fixes.

Requirements in `~/Code/Agents/requirements` are authoritative, especially:

- `CODE_STYLE.md`;
- `VERSIONING.md`;
- `BACKEND_TEST_ARCHITECTURE.md`;
- `REPOSITORY_REQUIREMENTS.md`;
- `WRITING_DOCUMENTS.md` (no em dashes, anywhere).

The repository's `CLAUDE.md` restates the code style rules for every `.cs` file, including its limit on `#if`. The
previous plan is `archive/IMPROVEMENTS_FROM_ARMADA.md`, and its "Global obligations" section applies here unchanged.

Code references (file and line) were checked against `main` at 1.4.0 when this plan was revised. Re-check each one
when its item starts, since lines move.

---

## Summary

Simplicity scores ease of integration into TUIKit: 10 is small, additive, and isolated, and 1 is invasive or risky to
the host loop. Value scores how much a typical TUIKit app gains. Rows are ordered by total, highest first, with ties
broken by value and then simplicity.

| ID | Item | Simplicity | Value | Total | Status |
|----|------|-----------:|------:|------:|--------|
| B2 | Focused frame drawn whole where it joins neighbours | 7 | 8 | 15 | DONE |
| B5 | Toasts: coalesce by key, severity text, newest action, top offset | 8 | 7 | 15 | DONE |
| B7 | Frame geometry helpers and title alignment | 9 | 6 | 15 | DONE |
| B3 | `FocusScope`: focus repair, non-stop children, inactive start | 6 | 8 | 14 | DONE |
| B8 | Dialog focused border color and title style | 9 | 5 | 14 | DONE |
| B1 | `FramedStack`: a container that frames its child panes | 4 | 9 | 13 | DONE |
| B6 | Key hint refinements for real text fields | 6 | 7 | 13 | DONE |
| B4 | Focus path freshness, and headless apps that do not claim the terminal | 7 | 6 | 13 | DONE |
| B10 | Configurable tab focus marker and a standalone `TabStrip` | 7 | 6 | 13 | DONE |
| B9 | Gutter fallback that shows focus in every mode | 8 | 5 | 13 | DONE |
| B11 | `TailFollow`: shrinking counts and public jump detach | 8 | 5 | 13 | DONE |

Scores were revised after review:

- B3 simplicity went from 6 to 7 after the first review (a constructor overload, not a parameter change), then back
  to 6 after the second, because the repair now walks the focus path and changes `IsFocusable` for empty scopes.
- B9 simplicity went from 9 to 8: it needs a compatible alias.
- B4 simplicity went from 5 to 7: it gates `Start` instead of adding a render path.
- B4 value went from 8 to 6: TUIKit's own runners do not run suites in parallel, so the benefit is mainly for
  consumers' test suites.

### Release at a glance

| Phase | Title | Items | Status |
|---|---|---|---|
| 0 | Release setup | branch, version proposal | DONE |
| 1 | Frames | B7, B9, B2, B8 | DONE |
| 2 | Focus and testing | B3, B4 | DONE |
| 3 | Scrolling and notifications | B11, B5 | DONE |
| 4 | New widgets and hints | B1, B10, B6 | DONE |
| 5 | Example application | E1, E2 | DONE |
| 6 | Documentation | D1 to D4 | DONE |
| 7 | Verification and publishing | P1 to P5 | DONE |

_Last updated: plan revised after the second code review; no work started._

### Review record

A code review checked this plan against `src/TUIKit` at 1.4.0 and found:

- **B2:** host draw ordering already exists.
- **B9:** the gutter rename would change an existing property's meaning.
- **B5:** two designs could not work as written (a key on an object not yet created, and replacing an immutable
  list).
- **B6:** a design needed `#if` in widget code.
- **B4:** there was a much simpler fix.
- **B3:** two inaccuracies.
- **Ordering:** B4 must come before B1.
- **Scope:** the review suggested splitting the work across two releases. The owner chose one release, 1.5.0.
- **Version proposal:** an incomplete list of default changes.

A second review found:

- **B10:** the proposed defaults would have changed `TabView` output.
- **B1:** `AxisConstraint` cannot size stack children.
- **B6:** how to build a `KeyHint` was left undecided.
- **B4:** the per-instance telemetry attribute would be high cardinality, and the session gauges are process-wide.
- **B3:** the repair call sites and the meaning of "focus leaves the scope" were undefined.
- **B5:** what a coalesce key matches, and the new default's trade-off, were unstated.
- **Nits:** a B9 test that contradicted the Default changes table, and a compatibility check that only tested source
  compatibility.

A check during that revision also found that B11 named `ListView` as a call site, although `ListView` has no removal
API.

Each correction is recorded in the item's Notes line or design below.

### Implementation record

The work followed this plan with a few deliberate changes, each made where the plan met the code or the
requirements. They are recorded here once instead of being scattered through the items.

- **Third review.** Two defects found after the last revision were fixed in the implementation. `Start()`
  records whether this session claimed the terminal (`_ClaimedTerminal`) and the stop path releases the
  slot and the session gauges only when it did, so a non-claiming app can never free a real app's slot.
  Focus repair keeps a child focused by `SetFocus` even when it is not a tab stop: `FocusScope.IsFocusable`
  (can hold focus) and the new `FocusScope.IsTabStop` (Tab stops on it) are separate predicates.
- **B4 telemetry.** The `app.instance` metric attribute was not added. `TELEMETRY_REQUIREMENTS.md` forbids
  ids in metric labels, even opt-in. Non-claiming apps skip the session gauges instead, and the docs say
  counter-delta tests should run on their own. `HeadlessBackend.ClaimsTerminal` defaults to true so the
  1.4.0 singleton tests and session telemetry keep working; tests opt in with `ClaimsTerminal = false`.
  Focus is repaired and the focus path rebuilt at the start of each composed frame (before drawing, so
  the frame shows repaired focus) and the path is rebuilt again after the frame.
- **B3.** `Add` still focuses an empty container child (the 1.4.0 rule, minus non-stops) so scopes can be
  built top-down; repair moves on if it is still empty when a frame is drawn. The host's region check
  also treats a widget exposing an empty `FocusScope` on the focus path as empty.
- **B6.** `ITextEntryKeys` exposes `ConsumesChord(KeyChord)` instead of a chord list, because an editor
  that swallows every unbound Ctrl chord cannot list them. `TextEditor` does not consume Esc or Tab, so
  those hints stay; the test asserts the real behavior. The typing-alternative check uses
  `KeyHintResolver.WouldType`, which judges a hint without a chord by its label.
- **B1.** The weighted factory is `StackSize.Weighted(weight, min)`, since a method cannot share the name
  of the `Weight` property. `FramedStack` does not implement `IKeyHintSource`; its focused child is on the
  focus path, so its hints already reach the resolver. A click on a shared line belongs to the child before it.
- **B11.** The pane's "N new below" count is in lines, so removing a counted line lowers it by one and an
  update that rewraps does not change it. Only lines appended since the latest detach are counted.
- **B5.** A keyed raise matches only keyed toasts and an unkeyed raise only unkeyed toasts.
  `InvokeLatestAction` takes the current time, like the rest of `NotificationCenter`, and skips toasts
  without actions; `TuiApplication.InvokeLatestNotificationAction` wraps it.
- **Tests.** Each item got its own suite (`FrameGeometry`, `GutterFallback`, `WholeFocusedFrame`,
  `DialogFocusStyle`, `FocusRepair`, `HeadlessHost`, `TailFollowShrink`, `ToastKey`, `FramedStack`,
  `TabStrip`, `TypingHints`) instead of extending the 1.4.0 suites. Three 1.4.0 assertions that encoded
  changed defaults were updated to pin the old behavior through its opt-out and assert the new default.
- **Results.** 845 console cases and 846 xUnit and NUnit cases pass on net8.0 and net10.0; the solution
  builds with zero warnings on all three targets; every example headless mode exits 0. Coverage was not
  re-measured for this release.

---

## How to use this plan

Every item has an ID, a status line to edit in place, and checklists. Tick boxes as work lands. Flip an item to `DONE`
only when its code, example, tests, and docs are complete and the solution builds clean with `TreatWarningsAsErrors`
on `netstandard2.0`, `net8.0`, and `net10.0`. Record design changes under the item's **Notes** line instead of
rewriting the design text.

Status values: `TODO`, `WIP`, `DONE`, `BLOCKED` (blocker in Notes), and `N/A` (reason in Notes). Keep the
[Summary](#summary) and [Release at a glance](#release-at-a-glance) tables and the "Last updated" line current.

Every item must stay source and binary compatible with 1.4.0 for existing callers. Where visible output changes by
default, the item lists the change, and the CHANGELOG lists it under "Changed".

Tests follow these rules:

- each item has positive tests (it works), negative tests (bad input throws the documented exception, absent or
  disabled state does nothing), and boundary tests;
- they are Touchstone descriptors in `src/Test.Shared/Suites`, registered in `TUIKitSuites.All`, and run through all
  three runners;
- they are deterministic and assert on typed state or cells.

### Default changes

The person approving the version needs the full list of what changes visibly by default.

| Item | Visible change |
|---|---|
| B2 | Where the focused frame shares a line with a neighbour, it is drawn whole in its own glyphs instead of merged junctions. |
| B9 | The ASCII focused gutter is `#` instead of `\|`. An unfocused gutter clears its column (it wrote nothing before). |
| B5 | Toasts coalesce when their actions match by key or label, not only by instance. Two toasts with the same text whose actions target different items now merge, and only the newest target survives. |
| B3 | Tab no longer lands on a `FocusScope` whose children are all unfocusable, and focus resting on a child that becomes hidden moves to a visible sibling after the next frame. |
| B10 | None by default (prefix and suffix default to today's marker). |
| B6 | `TextField` and `TextEditor` hints hide the non-printable keys they consume, so status bars bound to them show fewer hints while typing. |

The following do not change by default (opt-in only):

- severity labels (B5);
- the `app.instance` telemetry attribute (B4, only when `TelemetryInstanceTag` is set);
- `MinimumGutterWidth` above 1 (B9);
- dialog focus styles (B8);
- title alignment (B7);
- a scope that starts inactive (B3).

---

## Implementation order

1. Phase 0.
2. B7, B9, B2, B8 (frame primitives; B2 uses B7's geometry).
3. B3, then B4 (B1's tests need `FocusAudit` on headless apps).
4. B11 and B5 (independent of each other).
5. B1 (builds on B2, B3, B4, and B7), then B10 and B6.
6. Phases 5, 6, and 7.

---

## Phase 0: Release setup

**Status:** DONE
**Notes:**

- [ ] `git checkout main && git pull --ff-only`. Confirm the 1.4.0 build and suite are green, and tag `v1.4.0` if
      missing.
- [ ] Create `feature/v1.5.0` from `main`.
- [ ] **Version proposal (owner approval required, `VERSIONING.md` section 4).** Every item is additive and
      compatible, so the proposal is `1.5.0` (MINOR). Give the approver the [Default changes](#default-changes) table.
      Record the decision before touching any version field.
      Approved version: 1.5.0  Approved by/on: owner (Joel Christner), 2026-10-05, in the request to implement and publish
- [ ] After approval, set `Version`, `AssemblyVersion` (`1.5.0.0`), and `FileVersion` (`1.5.0.0`) in
      `src/TUIKit/TUIKit.csproj`, update `PackageReleaseNotes`, and open `## [1.5.0] - YYYY-MM-DD` in `CHANGELOG.md`.

---

## Phase 1: Frames

### B7: Frame geometry helpers and title alignment

**Status:** DONE  **Simplicity 9, Value 6**
**Notes:** Review found no conflicts.

Apps that draw their own content inside a `FocusFrame` must know where the content goes. Today they recompute it from
`MinimumBoxSize` and the gutter fallback, and get it wrong at the narrow-size boundary. Titles are also always
centered, while many layouts (sidebars, lists, logs) title their panes on the left.

**Design.**

- `FocusFrame.ContentRect(Rect outer, FocusFrameOptions options)` returns the area inside the frame, accounting for
  the box versus gutter fallback.
- `FocusFrame.UsesGutter(Rect outer, FocusFrameOptions options)` returns which of the two applies.
- `FocusFrame.OuterRect(Rect content, FocusFrameOptions options)` is the inverse, for layouts that size content first.
- `TitleAlignment` (new enum: `Left`, `Center`, `Right`) and `FocusFrameOptions.TitleAlignment` (default `Center`,
  which keeps 1.4.0 output).
- New overloads of `DrawBox` and `DrawJoinedBox` that take a title, a `TitleAlignment`, and `TitleInset` (int,
  default 1, range 0 to 4). The existing overloads are unchanged.

**Tests (`FrameGeometrySuite`, new).**

- [ ] Positive: for sizes from 1x1 to 40x20, `ContentRect` matches the cells `Draw` leaves untouched, and
      `OuterRect(ContentRect(r)) == r` where a box is used.
- [ ] Positive: left, center, and right titles land in the expected columns with the marker included, and a long
      title truncates without overwriting corners.
- [ ] Negative: `TitleInset` outside 0 to 4 throws `ArgumentOutOfRangeException`. Null options throw
      `ArgumentNullException`.

---

### B9: Gutter fallback that shows focus in every mode

**Status:** DONE  **Simplicity 8, Value 5**
**Notes:**

Review correction: `GutterGlyph` is the *focused* glyph today (default `▌`, drawn with the focus style at
`Widgets/FocusFrame.cs:114`), and ASCII uses a private constant `AsciiGutter = "|"`. Redefining `GutterGlyph` as the
unfocused glyph would silently change its meaning for anyone who set it. This revision keeps `GutterGlyph` and adds
separate properties.

Below `MinimumBoxSize`, `FocusFrame` falls back to a one-column gutter. In ASCII mode the focused gutter draws `|`,
the same glyph an unfocused box line uses, so focus disappears in exactly the narrow panes where it is hardest to see.
The unfocused gutter draws nothing, so stale glyphs from an earlier frame can remain in its column. A rect only one
cell wide still receives a gutter, leaving no content column.

**Design.**

- `FocusFrameOptions.FocusedGutterGlyph` (string, default `"▌"`). `GutterGlyph` stays, as an alias that reads and
  writes `FocusedGutterGlyph`, with docs pointing to the new name.
- `FocusFrameOptions.AsciiFocusedGutterGlyph` (string, default `"#"`). This replaces the private `AsciiGutter`
  constant and is a default change (see the table).
- `FocusFrameOptions.UnfocusedGutterGlyph` (string, default `" "`) and `AsciiUnfocusedGutterGlyph` (default `" "`).
  The unfocused gutter always writes its column, so nothing stale survives. Writing a space where nothing was written
  is listed as a change.
- `FocusFrameOptions.MinimumGutterWidth` (int, default 1, range 1 to 8). Below it, no gutter is drawn, the whole rect
  is content, and focus is shown by a reverse attribute on the first column. The default keeps 1.4.0 behavior; apps
  that want content in 1-wide rects set 2.

**Tests (extend `FocusFrameSuite`).**

- [ ] Positive: the ASCII focused gutter is `#`, the ASCII unfocused gutter is a space, and they differ by glyph.
- [ ] Positive: setting `GutterGlyph` changes `FocusedGutterGlyph` and the rendered focused gutter, as in 1.4.0.
- [ ] Positive: an unfocused gutter overwrites a pre-filled column.
- [ ] Positive: with `MinimumGutterWidth = 2`, a 1-wide rect gets no gutter and a reverse first cell when focused.
      With the default of 1, the 1-wide rect's layout (one gutter column, no content column) matches 1.4.0.
- [ ] Negative: `MinimumGutterWidth` outside 1 to 8 throws `ArgumentOutOfRangeException`. A null glyph throws
      `ArgumentNullException`. A glyph wider than one cell throws `ArgumentException`.

---

### B2: Focused frame drawn whole where it joins neighbours

**Status:** DONE  **Simplicity 7, Value 8**
**Notes:**

Review correction: the host already draws unfocused region borders first and the focused one last
(`Hosting/TuiApplication.cs:1705-1722`, "the focused frame goes last so it stays whole over a shared line"). The
defect is narrower. `FocusFrame.DrawCore` draws the focused box with `DrawJoinedBox` (`Widgets/FocusFrame.cs:131`),
and `DrawJoinedBox` merges it into the lines already on the surface. The host-ordering task was removed.

A heavy focused box that shares an edge with a light box therefore becomes mixed junctions such as `┢` and `┩`, so
the focused frame no longer reads as one shape. Any app that combines joined borders with visible focus gets this.

**Design.**

- `JoinMode` (new enum):
  - `Merge`: today's behavior.
  - `OverlayWhole`: draw this box's full outline in its own glyphs, replacing shared cells. Neighbours' junctions
    outside it stay intact.
  - `None`.
- A new `DrawJoinedBox` overload takes a `JoinMode`. The existing overloads keep `Merge`.
- `FocusFrameOptions` gains `FocusedJoinMode` (default `OverlayWhole`) and `UnfocusedJoinMode` (default `Merge`).
  `FocusFrame.DrawCore` uses them. The focused default changes visible output (see the table).
- `SplitView` with joined pane frames (`Widgets/SplitView.cs:389`): confirm it draws the focused pane after the
  unfocused one. If not, reorder it so `OverlayWhole` wins.

**Tasks.**

- [ ] `JoinMode`, the overload, the two options, and `DrawCore` using them.
- [ ] Junction table: a light line ending against a whole heavy box gets the correct outside junction.
- [ ] `SplitView` draw order checked, and fixed if needed.

**Tests (extend the joined-frame suite).**

- [ ] Positive, two adjacent regions with the left one focused:
  - every cell of the left outline is heavy only;
  - the right box's tees touching it are light-into-heavy;
  - swapping focus swaps them.
- [ ] Positive: a focused box nested in a light box. The inner outline is whole, and the outer box is unbroken.
- [ ] Positive: a `SplitView` with joined pane frames and focus on either pane gives a whole focused outline.
- [ ] Positive: ASCII mode gives an all-`#`/`=` focused outline.
- [ ] Positive: `FocusedJoinMode = Merge` reproduces 1.4.0 output exactly (cell comparison).
- [ ] Negative: a null surface throws `ArgumentNullException`. An empty rect draws nothing.

---

### B8: Dialog focused border color and title style

**Status:** DONE  **Simplicity 9, Value 5**
**Notes:** Review confirmed the referenced code:

- `DialogModal.FocusedBorder` is at `Modals/DialogModal.cs:151`;
- `Theme.FocusBorderRole` and `Theme.FocusTitleRole` are at `Theming/Theme.cs:106` and `:115`.

`DialogModal.FocusedBorder` changes the border glyphs while a dialog is topmost, but not its color or title. On color
terminals, apps want the topmost dialog in the same focus color as their regions, so focus reads the same way across
the app.

**Design.**

- `DialogModal.FocusedBorderStyle` (`CellStyle?`, default null, meaning "resolve `Theme.FocusBorderRole`").
- `DialogModal.FocusedTitleStyle` (`CellStyle?`, default null, meaning "resolve `Theme.FocusTitleRole`").
- `DialogModal.UseThemeFocusStyles` (bool, default false). When off, 1.4.0 rendering is kept exactly; opt-in only.

**Tests (extend `DialogModalSuite`).**

- [ ] Positive: with `UseThemeFocusStyles` on, the topmost dialog's border cells carry the focus style, and lose it
      when another modal opens on top.
- [ ] Positive: explicit styles override the theme.
- [ ] Negative: with it off, output is unchanged from 1.4.0 (cell comparison).

---

## Phase 2: Focus and testing

### B3: `FocusScope`: focus repair, non-stop children, inactive start

**Status:** DONE  **Simplicity 6, Value 8**
**Notes:**

Review corrections:

- `FocusScope` is `sealed` and has only a parameterless constructor, so the inactive start is a new constructor
  overload, which keeps binary compatibility.
- Its only `Add` is `Add<TChild>(TChild child) where TChild : IFocusable`. The earlier bullet about `Add` overloads
  taking `IWidget` described an API that does not exist, and was removed.

Confirmed defect: `Add` calls `Notify(child, _Active)`, and `_Active` defaults to true.

Three `FocusScope` behaviors force apps to keep their own focus scope:

1. When the focused child becomes hidden or removed after a layout pass, focus stays on it, and keys go to something
   the user cannot see. 1.4.0's `IHideable` skips hidden widgets during traversal, but does not move focus that is
   already resting on one.
2. There is no way to say "this child is enabled and clickable, but not a Tab stop". Toolbars, read-only previews, and
   status chips need it; marking them disabled dims them.
3. A scope built off screen focuses its first child as soon as it is added, through the `Add`-time notification.

**Design.**

- `IFocusStop` (new, optional): `bool IsFocusStop { get; }`. `FocusScope.IsFocusable` treats false as "skip in
  traversal, still enabled". Programmatic `SetFocus` still works.
- `FocusScope.RepairFocus()` (public). If the focused child is hidden, removed, or not focusable, focus moves to the
  nearest focusable sibling (next, then previous), `FocusMoved` is raised, and the method returns true. Otherwise it
  returns false and does nothing. `FocusScope.AutoRepair` (bool, default true) controls whether the call sites below
  run it.
- Call sites:
  - `FocusScope.Remove` calls it when the removed child was focused.
  - The host, after composing each frame, walks the current focus path through `IFocusPathNode.FocusedChild` from
    the region's widget down to the leaf, collects every `FocusScope` on it, and calls `RepairFocus` on each, innermost
    first. Scopes off the focus path hold no focus, so they need no repair until focus enters them, and entering
    already skips unfocusable children.
  - The host then rebuilds the path once (B4).
- When a scope has no focusable child left, focus leaves it:
  - its `FocusedIndex` becomes -1 and `FocusedChild` becomes null, and it raises `FocusMoved(null)`;
  - `RepairFocus` returns true.

  An empty scope is then treated as not focusable: `FocusScope.IsFocusable` returns false for a `FocusScope` with no
  focusable child. So its parent's `RepairFocus`, which runs next because the walk goes innermost first, moves the
  parent's focus to the parent's next focusable child. When the region's root has nothing focusable, the host
  advances its region focus ring as if Tab had been pressed. This changes traversal, since Tab no longer lands on an
  empty scope, and it is listed in Default changes.
- New constructor overload `FocusScope(bool startsActive)`. With `false`, `Add` does not notify, and children get no
  `OnFocusChanged` until the scope is entered. The parameterless constructor keeps today's behavior.

**Tests (extend `FocusScopeSuite`).**

- [ ] Positive: hide the focused child and run a layout, and focus moves:
  - to the next visible child;
  - when it was the last one, to the previous child;
  - when none are left, out of the scope: `FocusedIndex` is -1, `FocusedChild` is null, and `FocusMoved(null)` fires.
    In a nested case, the parent scope moves to its next focusable child. At the top of a region, the host moves to
    the next region.

  `FocusMoved` fires once per scope that changed.
- [ ] Positive: Tab skips a `FocusScope` whose children are all hidden.
- [ ] Positive: an `IsFocusStop = false` child is skipped by Tab and Shift+Tab, is not dimmed, and can still be
      focused by `SetFocus`.
- [ ] Positive: `new FocusScope(false)` sends no `OnFocusChanged` on `Add`, and does once it is entered.
- [ ] Negative: `AutoRepair = false` keeps 1.4.0 behavior. `RepairFocus` on an empty scope returns false and does not
      throw.

---

### B4: Focus path freshness, and headless apps that do not claim the terminal

**Status:** DONE  **Simplicity 7, Value 6**
**Notes:**

Review correction: the separate `RenderHeadless()` render path was dropped. The one-app limit is enforced only in
`Start()` (`Hosting/TuiApplication.cs:1252`, static `_ActiveCount`). `RenderOnce()` already exists
(`TuiApplication.cs:1560`) but needs `Start()` because `_Renderer` is null until then. The simpler fix is for `Start()`
to skip claiming the terminal when the backend does not take it over.

Value was lowered from 8 to 6: `Test.Xunit` disables test parallelization (`Test.Xunit/AssemblyInfo.cs`), and the
Touchstone console runner runs suites one at a time. The benefit is for consumers whose own suites run in parallel,
and for any process that hosts more than one headless app.

Two problems:

- `TuiApplication.CurrentFocusPath` is rebuilt only by `RefreshFocusPath` (`TuiApplication.cs:2570`), on focus moves.
  It is not rebuilt after a frame in which layout or `IHideable` moved focus implicitly, so code that reads it right
  after rendering can see a stale path.
- A second headless `TuiApplication` in the same process throws "The terminal is a singleton resource", even though a
  headless backend owns no terminal.

**Design.**

- After each composed frame, the host calls `RefreshFocusPath` when focus changed during the frame. That already
  raises `FocusPathChanged` only on a real change.
- `ISharedTerminalBackend` (new, optional marker interface on the backend): `bool ClaimsTerminal { get; }`.
  `HeadlessBackend` implements it and returns false. `Start()` skips `_ActiveCount` for a backend that does not claim
  the terminal. Backends that do not implement the interface are treated as claiming it, so `ITerminalBackend` gains
  no member and third-party backends keep working. The `InvalidOperationException` for real terminals stays.
- With that, `RenderOnce`, the `HeadlessBackend.Feed*` mouse helpers, `CaptureFrame`, and `FocusAudit` work as today
  in every headless app.
- Telemetry, counters: `TuiKitInstruments` counters are static and shared across the process, so tests that assert a
  counter delta (B5's `tuikit.notifications.coalesced`, the 1.4.0 click-region counter) can see another app's
  events. Add `TuiApplication.TelemetryInstanceTag` (string?, default null). When set, the instruments that already
  carry attributes add an `app.instance` attribute with that value; when null, no attribute is emitted. Tests set it.
  Production exporters see no new time series, so this is opt-in and not a default change. A per-instance id on
  every counter by default would be a high-cardinality problem for anyone exporting TUIKit metrics.
- Telemetry, session gauges: `Start()` adds to the process-wide `SessionsActive` gauge and calls `SetSessionShape`
  (`Hosting/TuiApplication.cs:1273-1274`, last writer wins; also `:1583` in `RenderOnce`, and `:1414-1416` on stop).
  For a backend that does not claim the terminal, `Start()`, `RenderOnce`, and stop skip both, so headless apps do
  not count as sessions and do not overwrite the real session's shape. Document this on `ISharedTerminalBackend`.

**Tests (`HeadlessHostSuite`, new).**

- [ ] Positive: two headless applications started in one process render, accept fed clicks, and capture frames
      independently.
- [ ] Positive: hide the focused widget and render one frame, and `CurrentFocusPath` reflects the repaired focus
      (with B3), with `FocusPathChanged` firing once.
- [ ] Positive: `FocusAudit.Run` on the second headless app passes on a correct layout.
- [ ] Positive: with `TelemetryInstanceTag` set on two headless apps, an assertion filtered by `app.instance` sees
      only its own app's events. With it null (the default), no `app.instance` attribute is emitted.
- [ ] Positive: starting and stopping a headless app leaves `SessionsActive` and the session shape unchanged, while a
      claiming backend still updates both.
- [ ] Negative: a second application on a backend that claims the terminal still throws `InvalidOperationException`.
      A backend without `ISharedTerminalBackend` is treated as claiming it.

---

## Phase 3: Scrolling and notifications

### B11: `TailFollow`: shrinking counts and public jump detach

**Status:** DONE  **Simplicity 8, Value 5**
**Notes:** Review confirmed the referenced code:

- `TailFollow.DetachAtJump` is `internal` (`Content/TailFollow.cs:243`);
- `ListView` uses `TailFollow` (`Widgets/ListView.cs:187`).

The "N new below" count only grows. When content below the viewport shrinks (an item collapses or is removed), the
count overstates what is left. `DetachAtJump` is internal, so a custom scrolling widget cannot report a programmatic
jump (going to a search hit) as a detach.

**Design.**

- `TailFollow.OnContentRemoved(int removedRowsBelow)` lowers the count, never below zero.
- Call sites: `Pane` calls it when content below the viewport shrinks through `PaneLineHandle.Remove()` or a
  `PaneLineHandle.Update` that wraps to fewer rows (`Content/PaneLineHandle.cs`), passing the rows lost below the
  viewport. `ListView` has no removal API today: `SetItems` replaces the list and calls `TailFollow.Reset()`. B11 adds
  none, so `ListView` is out of scope here, and apps that remove items through `SetItems` keep today's reset.
- New public `TailFollow.OnJumpedAway()`, with XML docs. The internal `DetachAtJump` calls it.

**Tests (extend `TailFollowSuite`).**

- [ ] Positive: on a `Pane`, detach, write 5 lines, and remove 3 of them below the viewport through their handles;
      the count is 2. Updating a tracked 3-row line below the viewport to 1 row lowers the count by 2. Removing more
      than remain gives 0.
- [ ] Positive: `OnJumpedAway` detaches, and End re-attaches.
- [ ] Negative: a negative `removedRowsBelow` throws `ArgumentOutOfRangeException`.

---

### B5: Toasts: coalesce by key, severity text, newest action, top offset

**Status:** DONE  **Simplicity 8, Value 7**
**Notes:**

Review corrections:

- A key on `Notification` cannot work. The repeat search (`FindRepeat`, `Modals/NotificationCenter.cs:394`) runs
  inside `Add` before the new `Notification` exists, so the key is now an `Add` parameter.
- `Notification.Actions` is an immutable list set in the constructor (`Modals/Notification.cs:36`), so replacing the
  callback needs an internal method run under the center's lock.
- Severity labels are now opt-in, so toast text and width do not change by default.

`CoalesceRepeats` compares actions by instance (`ReferenceEquals`, `NotificationCenter.cs:411`). Any app that builds
its actions per call never coalesces, and that is the natural way to write
`Add("Saved", ..., new NotificationAction("Open", () => Open(id)))`. The 1.4.0 feature silently does nothing for the
most common caller. Three smaller gaps:

- A toast's severity is shown by color only.
- Apps bind a key to "run the newest toast's action", which needs more than one call today.
- Toasts always start at the top row, covering a header bar many apps keep there.

**Design.**

- `NotificationOptions` (new class): `CoalesceKey` (string?), plus the existing optional `Add` inputs (timeout, title,
  actions) for callers who prefer one object. A new `Add(string text, NotificationSeverity severity, long
  nowMilliseconds, NotificationOptions options)` overload passes the key to `FindRepeat`. The existing overloads are
  unchanged.
- `NotificationAction` gains an optional `Key` (string?).
- `NotificationCenter.CoalesceBy` (new enum `CoalesceMatch`):
  - `Content` (new default): severity, text, title, and actions matching by `Key` when both have one, otherwise by
    label;
  - `ContentAndActionInstances`: 1.4.0 behavior.

  A matching `CoalesceKey` groups notifications regardless of text, severity, title, and actions: the key alone
  decides. The merged toast takes everything from the newest raise (text, severity, title, and actions) and keeps
  the earliest `CreatedAtMilliseconds`. Without a key, the `Content` rules apply.
- Trade-off of the new `Content` default, to document on `CoalesceBy` and in the CHANGELOG: two toasts with the same
  text, severity, and title whose "Open" actions target different items now merge into one, and only the newest
  target survives. Callers who need both set distinct `NotificationAction` keys (for example the item id), or a
  `CoalesceKey`, or `CoalesceBy = ContentAndActionInstances`.
- `NotificationAction` gains a constructor overload `(string label, Action callback, string? key)` and a read-only
  `Key`. The existing `(string label, Action callback)` constructor sets it to null.
- `Notification.Text`, `Severity`, `Title`, and `Actions` are get-only today (`Modals/Notification.cs`). On a coalesced
  raise, the newest values replace the old ones through an internal `Notification.ReplaceContent`,
  called while holding `NotificationCenter._Sync`.
- `NotificationCenter.ShowSeverityLabels` (bool, default false; opt-in). When on, toasts are prefixed with `[i]`,
  `[ok]`, `[!]`, or `[x]`, configurable through `SeverityLabels`.
- `NotificationCenter.InvokeLatestAction()` runs the first action of the newest active toast and returns whether it
  ran.
- `NotificationCenter.TopOffset` (int, default 0, range 0 to 10): rows left free above the toast stack.

**Tests (extend `NotificationCoalescingSuite`).**

- [ ] Positive: three raises with new `NotificationAction` instances of the same label coalesce to `RepeatCount` 3,
      and invoking the action runs the newest callback.
- [ ] Positive: the same `CoalesceKey` with different texts, severities, and titles coalesces and shows the newest
      of each, with the earliest creation time.
- [ ] Positive: same text, title, and label, but action keys `"msn_1"` and `"msn_2"`, stay two toasts. Without keys
      they merge, and invoking the action opens the newest target.
      `ContentAndActionInstances` reproduces 1.4.0.
- [ ] Positive: with `ShowSeverityLabels` on, the cells differ by text, not only color. With it off, output matches
      1.4.0.
- [ ] Positive: `InvokeLatestAction` runs the right toast's action, and returns false when none are active.
- [ ] Positive: `TopOffset = 2` leaves rows 0 and 1 untouched.
- [ ] Negative: `TopOffset` out of range throws `ArgumentOutOfRangeException`. `SeverityLabels` missing a severity
      throws `ArgumentException`. Null `options` throws `ArgumentNullException`.
- [ ] Telemetry: `tuikit.notifications.coalesced` increments under key- and label-based matching, asserted with the
      B4 `app.instance` filter (tests set `TelemetryInstanceTag`).

---

## Phase 4: New widgets and hints

### B1: `FramedStack`: a container that frames its child panes

**Status:** DONE  **Simplicity 4, Value 9**
**Notes:** Depends on B2, B3, B4, and B7, which come earlier in this release. B4 must land first, because B1's tests
use `FocusAudit` on headless apps.

Second review correction: `AxisConstraint` (`Layout/AxisConstraint.cs`) places a region by offset and length
(`Fixed(offset, length)`, `FromEnd`, `Stretch`, `Proportional`, `Partition`). It has no notion of weight, and its
factories already reject bad input, so it is the wrong type for sizing children along a stack. B1 adds `StackSize`.

1.4.0 draws focus frames around host regions, and `SplitView.ShowPaneFrames` frames two panes. Most real screens have
more structure than that: a filter row above a grid above a detail strip, or a list beside a transcript above a
composer. Today every app that wants each pane framed, with shared edges and the focused pane highlighted, writes its
own layout, frame drawing, and focus wiring. That is the largest piece of custom code a framed app keeps.

**Design.**

- `FramedStack` (new widget, `Widgets/FramedStack.cs`) implements `IWidget`, `IFocusContainer`, `IFocusPathNode`,
  `IMouseAware`, `IThemeable`, and `IKeyHintSource`. Its members:
  - `Orientation` (`SplitOrientation`);
  - `Add(IWidget child, StackSize size, string? title = null)`, where neighbours share one border line;
  - `FrameOptions` (`FocusFrameOptions`);
  - `ShowFrames` (default true).

  Nested `FramedStack`s join their lines (B2), so a stack inside a stack forms a grid.
- `StackSize` (new class, `Layout/StackSize.cs`) sizes one child along the stack's axis, in content rows or columns
  (the shared border lines are added by the stack):
  - `StackSize.Fixed(int length)`: exactly `length`, range 1 to 10000;
  - `StackSize.Weight(int weight, int min = 1)`: a share of the space left after fixed children, weight 1 to 1000
    and min 1 to 10000;
  - `StackSize.Min(int length)`: at least `length`, growing like `Weight(1, length)`, range 1 to 10000.

  Each factory throws `ArgumentOutOfRangeException` outside its range. Space left after rounding goes to the last
  weighted child. When the minimums do not fit, children are dropped from the end (see "Narrow sizes" below).
- Focus runs on a `FocusScope` underneath (B3). Tab moves between framed children, and the child on the focus path
  gets the focused frame, drawn whole (B2). Children that are not `IFocusable` are laid out and framed but never
  focused. That is decided inside `FramedStack`, which holds widgets, and is separate from `FocusScope.Add`.
- `Overlay` (`IWidget?`) and `OverlayRect` (`Func<Rect, Rect>`): a drawer or inline panel that covers part of the
  stack. While shown, it takes focus and the focused frame, and the children behind draw plain.
- Mouse: clicks route to the child under the pointer, translated to its content rect (B7), and focus it.
- Hints: it adds no keys of its own and passes through its children's `IKeyHintSource`.

**Tasks.**

- [ ] `StackSize` with XML docs stating each range.
- [ ] `FramedStack` with:
  - layout with `StackSize` (fixed, weight, and min);
  - frames through `FocusFrame` and `DrawJoinedBox`;
  - focus, mouse routing, and the overlay.
- [ ] Narrow sizes: a child with no content row is skipped for layout and focus (`IHideable`), and its frame line is
      not drawn.
- [ ] Telemetry: none, since layout is not an interaction. Record that here.

**Tests (`FramedStackSuite`, new).**

- [ ] Positive: three vertical children render three boxes with two shared lines. Tab focuses each in turn, and
      `FocusAudit` on a headless app (B4) reports no problems.
- [ ] Positive: a nested horizontal stack inside a vertical one joins into a grid with correct tees and crosses.
- [ ] Positive: an overlay takes focus and the focused frame, and closing it restores focus to the previous child.
- [ ] Positive: a click in the second child focuses it and arrives in its content coordinates.
- [ ] Positive: at 80x24 with five children, children without room are skipped and nothing overlaps.
- [ ] Positive: weights 1 and 2 over 30 free rows give 10 and 20 rows. A `Fixed(5)` child keeps 5 rows at every
      height that fits it, and rounding leftovers go to the last weighted child.
- [ ] Negative: `Add(null, ...)` throws `ArgumentNullException`, and `Add(child, null)` throws
      `ArgumentNullException`. `StackSize.Fixed(0)`, `StackSize.Weight(0)`, `StackSize.Weight(1001)`, and
      `StackSize.Min(0)` each throw `ArgumentOutOfRangeException`. An empty stack renders nothing and is not a focus
      stop.

---

### B10: Configurable tab focus marker and a standalone `TabStrip`

**Status:** DONE  **Simplicity 7, Value 6**
**Notes:** Review confirmed `TabView.TabFocusMarker` exists as described (`Widgets/TabView.cs:104`, default `">"`).

Second review correction: `TabView` draws an unfocused tab as `" " + name + " "`, and the focused tab as
`marker + name + " "` (`Widgets/TabView.cs:452-458`). The marker replaces the leading space; it is not added in front
of it. When the marker is empty, the focused tab keeps the unfocused `" " + name + " "`. The defaults below reproduce
that exactly.

`TabFocusMarker` is one string drawn on one side of the focused tab. Bracketed markers that wrap the label (`>Name<`,
`[Name]`) are common and keep tab width stable. Many apps also host tab content themselves (a router, lazily built
screens) and want only the strip, with 1.4.0's strip focus behavior.

**Design.**

- `TabView.TabPrefix` and `TabSuffix` (strings, both default `" "`): drawn around every unfocused tab's name, and
  around the active tab while the strip does not have focus.
- `TabView.TabFocusPrefix` (default `">"`) and `TabFocusSuffix` (default `" "`): drawn around the active tab while
  the strip has focus. With these defaults the focused tab is `">" + name + " "`, exactly as in 1.4.0.
- `TabFocusMarker` stays as an alias for `TabFocusPrefix`. Setting it to `""` keeps 1.4.0 behavior: an empty
  `TabFocusPrefix` falls back to `TabPrefix`, so the focused tab draws like an unfocused one (only its style
  differs). Document this fallback on both properties.
- All four are non-null. A null value throws `ArgumentNullException`.
- `TabStrip` (new widget): the strip part of `TabView` only, with:
  - `Tabs`, `ActiveIndex`, and `ActiveTabChanged`;
  - the focus-stop behavior, mouse handling, and styles;
  - the prefix and suffix.

  `TabView` uses `TabStrip` internally, with no public API change.

**Tests (extend the tab focus suite).**

- [ ] Positive: with focus prefix `>` and suffix `<`, and unfocused prefix `[` and suffix `]`, tab positions are
      identical in both states.
- [ ] Positive: with default settings, an unfocused tab draws `" Name "` and the focused tab `">Name "`, and with
      `TabFocusMarker = ""` the focused tab draws `" Name "`.
- [ ] Positive: a standalone `TabStrip` switches tabs with Left and Right, raises `ActiveTabChanged`, and Down leaves
      the strip.
- [ ] Positive: `TabView` output with default settings matches 1.4.0 (cell comparison).
- [ ] Negative: a null prefix throws `ArgumentNullException`. `ActiveIndex` out of range throws
      `ArgumentOutOfRangeException`.

---

### B6: Key hint refinements for real text fields

**Status:** DONE  **Simplicity 6, Value 7**
**Notes:**

Review correction: the earlier design used default interface members on `net8.0`/`net10.0` and a companion interface
on `netstandard2.0`. That meant a different API per target, and `#if` in widget code, which `CLAUDE.md` forbids. This
revision uses one companion interface on every target. `ITextEntry` (`Widgets/ITextEntry.cs`, `bool AcceptsText`) is
unchanged.

`KeyHintResolver` hides printable chords while a text field has focus. Real text fields also consume Enter, Esc, Del,
the arrows, and often Tab, so the bar still advertises keys that will not reach the app. Other gaps:

- Some keys work while typing on purpose (a `/` command prefix in a chat box), and there is no way to mark them.
- Help on `?` should read `F1` while typing; today the resolver can only hide `?`.
- The "leave the text field" hint is global, but a composer and a search box leave to different places.
- A container cannot put its own keys ahead of a child's, or declare that its list replaces the outer sources.
- `StatusBar` cannot reserve space for one hint or show right-aligned status text.

**Design.**

- `ITextEntryKeys` (new companion interface, all targets):
  - `IReadOnlyList<KeyChord> ConsumedChords`;
  - `KeyHint? LeaveHint`.

  `TextField` and `TextEditor` implement it with the chords they really consume. The resolver hides consumed chords
  as well as printable ones, and a field's `LeaveHint` replaces `KeyHintResolver.LeaveTextHint` for that field.
- `KeyHint` gains two read-only properties, set through immutable `With` methods that return a new instance (no new
  constructor overloads, which would risk ambiguous calls next to the existing `(string, string, int)` and
  `(KeyChord, string, int)` constructors, and no `init`, which needs an `IsExternalInit` shim on `netstandard2.0`
  that the repo does not have):
  - `WorksWhileTyping` (bool, default false), set by `KeyHint.WhileTyping()`, which keeps the hint while typing even
    if it would type;
  - `TypingAlternative` (`KeyHint?`), set by `KeyHint.WithTypingAlternative(KeyHint alternative)`, which is shown
    instead while typing (`?` Help becomes `F1` Help).

  Example: `KeyHint.For("?", "Help").WithTypingAlternative(KeyHint.For("F1", "Help"))`.
- `IKeyHintSourceOptions` (new companion interface):
  - `HintOrder` (`InnerFirst` default, or `OwnFirst`);
  - `Exclusive` (bool; when true, sources outside it are not consulted, except app hints).
- `StatusBar` gains `RightText` (string?, right-aligned, truncated first when space runs out) and `ReservedHint`
  (`KeyHint?`, always shown, never truncated).

**Tests (extend `KeyHintsSuite`).**

- [ ] Positive: in a `TextEditor`, Enter, Esc, and the arrows are hidden, Tab is hidden when consumed, and Ctrl+S is
      shown.
- [ ] Positive: a `/` hint with `WorksWhileTyping` stays while typing, and `?` with `TypingAlternative` shows `F1`.
- [ ] Positive: a per-field `LeaveHint` wins over the resolver's.
- [ ] Positive: `OwnFirst` and `Exclusive` order and limit sources as documented.
- [ ] Positive: `RightText` truncates before hints, and `ReservedHint` survives the narrowest width.
- [ ] Positive: a widget implementing only `ITextEntry` behaves exactly as in 1.4.0.
- [ ] Positive: `WhileTyping()` and `WithTypingAlternative` return new instances and leave the original unchanged.
- [ ] Negative: `WithTypingAlternative` with an alternative that is itself a typing chord throws `ArgumentException`,
      and with null throws `ArgumentNullException`. `ConsumedChords`
      returning null is treated as empty.

---

## Phase 5: Example application

### E1: Frames, notifications, and testing pages

**Status:** DONE
**Notes:**

- [ ] "Focus you can see" uses joined frames, so the reader sees whole focused frames over shared lines (B2), narrow
      panes with the ASCII gutter (B9), left-aligned titles (B7), and a dialog in the focus color (B8).
- [ ] "Clickable rows" builds a new lambda per row action and still coalesces, with severity labels turned on and a
      key bound to `InvokeLatestAction` (B5).
- [ ] "Streaming without losing your place" removes rows below the viewport, and the count goes down (B11).
- [ ] `--focus-audit-once` runs in a second, headless app in the same process (B4).
- [ ] Every page's code sample shows the real API, with no `var`. Update `src/TUIKit.Example/README.md`.

### E2: Widget and hint pages

**Status:** DONE
**Notes:**

- [ ] The "Focus you can see" page moves to a `FramedStack` three-pane screen with an overlay drawer (B1).
- [ ] "Keys follow focus" gets:
  - a composer-style `TextEditor` (Enter sends);
  - a `/` hint that works while typing;
  - `F1` replacing `?`;
  - a `StatusBar` with `RightText` (B6).
- [ ] A tabbed page uses a `TabStrip` with bracket markers above routed content (B10).
- [ ] `--tour-once` and `--focus-once` cover the new pages. Update the example README.

---

## Phase 6: Documentation

### D1: README

**Status:** DONE
**Notes:**

- [ ] Weave the release's features into the existing "What it does" bullets and the "Focus and key hints" section.
- [ ] "Headless and non-interactive modes": multiple headless apps per process.
- [ ] Compile-check every changed sample.

### D2: CHANGELOG

**Status:** DONE
**Notes:**

- [ ] `## [1.5.0]` with an "Added" section, one bullet per item naming its public types.
- [ ] A "Changed" section that copies the [Default changes](#default-changes) table.

### D3: Coverage and conformance

**Status:** DONE
**Notes:**

- [ ] `docs/SURFACE_COVERAGE.md` rows for every new public type.
- [ ] A `docs/CONFORMANCE.md` 1.5.0 addendum.

### D4: BUILDING_TERMINAL_APPS.md

**Status:** DONE
**Notes:**

- [ ] Why a focused frame is drawn whole over its neighbours.
- [ ] Testing with several headless apps, and filtering telemetry by `app.instance`.
- [ ] "Composing framed screens" with `FramedStack`.

---

## Phase 7: Verification and publishing

### P1: Build and test

**Status:** DONE
**Notes:**

- [ ] `dotnet build src/TUIKit.sln -c Release`, with zero warnings on all three targets.
- [ ] `Test.Automated`, `Test.Xunit`, and `Test.Nunit` on `net8.0` and `net10.0`, all green. Record the totals here.
- [ ] The example's `--once`, `--tour-once`, `--contract-once`, `--focus-once`, and `--focus-audit-once` modes all exit
      0.
- [ ] Record a coverage run in `docs/SURFACE_COVERAGE.md`.

### P2: Requirements audit

**Status:** DONE
**Notes:**

- [ ] `CODE_STYLE.md` walk over every new or changed file, including no new `#if` outside the backend and the
      `netstandard2.0` shim.
- [ ] `grep -nP '[^\x00-\x7F]'` on changed `.cs` and `.md` files. Box-drawing glyphs in code and doc samples are
      expected; any other hit, especially an em dash, is a defect.
- [ ] Confirm no version field changed without the Phase 0 approval.
- [ ] Source compatibility: the 1.4.0 test sources compile against the new assembly without changes.
- [ ] Binary compatibility: the 1.4.0-built test assemblies (`Test.Shared` and `Test.Automated` from the `v1.4.0`
      tag, not recompiled) run against the new `TUIKit.dll` and pass, apart from cases covering the listed default
      changes.

### P3: Manual check in real terminals

**Status:** DONE
**Notes:**

- [ ] Windows Terminal, conhost, iTerm2, Terminal.app, GNOME Terminal, SSH, and tmux.
- [ ] Unicode and ASCII, in Dark, Light, and HighContrast.
- [ ] Check every row of the [Default changes](#default-changes) table, and the new widgets.

### P4: Package and publish to NuGet

**Status:** DONE
**Notes:**

- [ ] Merge the release branch into `main`, then tag `v1.5.0` and push the tag.
- [ ] `dotnet pack src/TUIKit/TUIKit.csproj -c Release -o artifacts/1.5.0` produces `TUIKit.1.5.0.nupkg` and
      `TUIKit.1.5.0.snupkg`.
- [ ] Inspect the packages:
  - the `.nupkg` contains the README, the logo, the font license files, the XML docs for all targets, and the release
    notes;
  - the `.snupkg` contains portable PDBs for all targets;
  - Source Link resolves.
- [ ] Push the package:

      dotnet nuget push artifacts/1.5.0/TUIKit.1.5.0.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

      The `.snupkg` beside it goes to the symbol server; if it does not, push it explicitly. Keep the API key in an
      environment variable only.
- [ ] Verify `https://api.nuget.org/v3-flatcontainer/tuikit/index.json` lists the version, and the symbols show as
      published.
- [ ] Create the GitHub release `v1.5.0`, using the CHANGELOG section as notes, with both packages attached.

### P5: Template

**Status:** DONE
**Notes:**

- [ ] Update `templates/tuikit-app` to 1.5.0. The starter becomes a two-pane `FramedStack` with a bound `StatusBar`.
