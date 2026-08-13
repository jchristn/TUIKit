# TUIKit Improvements — Horizontal Components Drawn From mux

This plan turns a set of components that the mux application (`c:\code\mux`) had to build by
hand into first-class, **general-purpose** TUIKit components. mux is a downstream consumer of the
published `TUIKit` package (currently pinned to `0.5.1`), so everything it reimplements privately is,
by definition, a gap in the framework. The goal here is not to port mux's classes. It is to take the
*shape* of each pattern mux proved out and ship a version that any consumer — an installer wizard, a
log viewer, a package manager, a database console — would reach for without knowing mux exists.

Two rules govern every item in this plan:

1. **Horizontal, not vertical.** No mux vocabulary in public APIs. No "endpoint", "prompt profile",
   "MCP server", "voyage", or agent-specific assumptions leak into TUIKit. Where mux hard-coded a
   concept, the TUIKit component takes a generic type parameter, a delegate, or a configuration
   object instead. mux is cited only as the motivating example.
2. **Full product, not just code.** Each component lands with XML documentation, a worked example in
   `TUIKit.Example`, positive *and* negative Touchstone coverage, and the surrounding documentation
   (README, CHANGELOG, packaging metadata, coverage/conformance audits) updated in the same release.

Target release: **0.6.0** (minor increment; the project is alpha, so the breaking API changes called
out below are acceptable within a minor bump per the project's stated `0.x` policy).

---

## How to use this plan

- **Status legend:** `TODO` (not started) · `WIP` (in progress) · `DONE` (complete + tested) ·
  `BLOCKED` (record the blocker in Notes) · `N/A` (justify in Notes).
- Every work item carries an ID (for example `R1`, `T1-2`), a status line to edit in place, and a
  checklist of concrete sub-tasks. Tick the boxes as you land them; flip the item status to `DONE`
  only when its code, example, tests, and docs are all complete and the solution builds clean.
- Work top-to-bottom within a phase. Phase 1 (region backgrounds) and the Tier 1 primitives are
  dependencies for later items, so do not reorder them ahead of their prerequisites.
- The two closing audits (Phase 8) mirror the original build plan: surface-area → coverage, then
  requirements conformance. The release is not done until both pass and the version metadata is
  bumped.

### Progress at a glance

| Phase | Title | Items | Status |
|---|---|---|---|
| 0 | Release setup & conventions | version bump, branch, manifests | **DONE** (0.6.0 set; branch `feature/v0.6.0`) |
| 1 | Region backgrounds | R1–R3 | **DONE** |
| 2 | Tier 1 — core primitives | T1-1 … T1-4 | **DONE** |
| 3 | Tier 2 — composition | T2-1 … T2-5 | **DONE** |
| 4 | Tier 3 — streaming & typeahead | T3-1 … T3-3 | **DONE** |
| 5 | Tier 4 — utilities | T4-1 … T4-5 | **DONE** |
| 6 | Example gallery integration | E1–E2 | **PARTIAL** (guided-tour pages/commands for the new widgets; dedicated `--gallery` mode not built) |
| 7 | Documentation & packaging | D1–D5 | **DONE** (README, CHANGELOG, csproj, coverage + conformance addenda; example README carries the tour) |
| 8 | Final passes | F1–F3 | **DONE** (363 console + 364 xUnit/NUnit cases green on net8.0 and net10.0; coverage/conformance addenda recorded) |

_Last updated: **all 18 components shipped.** Region backgrounds, DialogModal, CheckList/MultiSelectModal,
generic ListView&lt;T&gt;/FuzzyList&lt;T&gt;, ActionListView, ReorderableList, dynamic-form rebuild,
DefinitionList, ActivityIndicator, StreamingTranscript, CommandRegistry, autocomplete, focus-follow
scroll, KeyLabel audit, and the four utilities. All committed on `feature/v0.6.0`, building clean across
netstandard2.0/net8.0/net10.0 (0 warnings, warnings-as-errors), with 363 Touchstone cases green in the
console runner and 364 through the xUnit and NUnit runners on net8.0 and net10.0._

**Deviations (all intentional, low-risk):**
- **T1-1:** `DialogModal` shipped and is proven by `MultiSelectModal<T>`; the optional behavior-preserving
  migration of the three existing modals (`MessageModal`/`PromptModal`/`SelectModal`) was left undone to
  avoid destabilizing their passing snapshot tests for no functional gain.
- **T1-4:** `SelectModal` kept its string+index API (backed by `ListView<string>`) rather than becoming
  `SelectModal<T>`; it returns an index, so a type parameter adds churn without value. The generic
  widgets that benefit — `ListView<T>`, `FuzzyList<T>` — are generic.
- **T2-2:** `ReorderableList<T>` ships reorder + delete (the universal core); inline rename was left out
  as it overlaps the existing `Form`/`TextField` editing story.
- **T2-3:** dynamic forms ship as runtime field-set rebuild (`Form.Clear`/`SetFocusedField`); the
  per-field `VisibleWhen` predicate was not added (it needs focus-ring surgery for tab skipping).
- **E1:** the new widgets are demonstrated through guided-tour pages and commands rather than a dedicated
  `--gallery` mode.

---

## Global obligations (apply to every code item)

These are not optional and are not repeated inside each item. Treat them as the definition of done.

**Code style (`c:\code\agents\requirements\CODE_STYLE.md`, enforced by the build).** The library
compiles with `TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true` across
`netstandard2.0;net8.0;net10.0`. Every new file: `namespace` first, `using` directives *inside* the
namespace, Microsoft/system usings alphabetized first then the rest; one public class or enum per
file; private fields `_PascalCase`; never `var`; no tuples unless genuinely unavoidable; XML docs on
every public member, constructor, and method (documenting defaults, ranges, nullability, thread
safety, and `<exception>` tags), and **no** docs on private members. Guard clauses at the top of
public methods — `ArgumentNullException.ThrowIfNull` on `net8.0`/`net10.0`, manual null checks on
`netstandard2.0`. No default interface methods (blocked on `netstandard2.0`). No `#if` outside the
terminal backend and the ns2.0 compat shim. No `Console.Write*` anywhere in the library. Prefer
`ReaderWriterLockSlim` over `lock` on read-heavy paths and `Interlocked` for simple atomics. Async
methods take a `CancellationToken` (unless the type already owns one) and use `.ConfigureAwait(false)`.

**Widget contract.** Non-modal components implement `IWidget` (`Measure`/`Render`). Interactive ones
also implement `IFocusable`, and `IFocusAware`/`IMouseAware` where they react to focus changes or
mouse input, matching the existing widget set. Modals derive from `TUIKit.Modals.Modal` (or the new
`DialogModal` base from item T1-1).

**Examples.** Each component gets a runnable demonstration in `TUIKit.Example`: a `TourPage` entry in
`GuidedTour` for widgets, and a launch command wired into the tour (or the new gallery mode from
item E1) for modals. Every example must also be reachable from a deterministic headless snapshot mode
so it can be asserted in tests and in CI.

**Tests (`c:\code\agents\requirements\BACKEND_TEST_ARCHITECTURE.md`).** Each component adds a
Touchstone suite (or extends an adjacent one) in `src/Test.Shared/Suites`, registered in
`TUIKitSuites.All` (`src/Test.Shared/TUIKitSuites.cs`). Every suite carries **positive** cases
(the component does what it should — construction, rendering into a `BufferSurface`/`HeadlessBackend`,
key handling, selection results) and **negative** cases (null/empty/out-of-range arguments throw the
specific documented exception, using the `Check.Throws<T>` helper in `src/Test.Shared/Check.cs`).
Shared test code never writes to the console. The suite must pass in all three runners
(`Test.Automated`, `Test.Xunit`, `Test.Nunit`) on `net8.0` and `net10.0`.

**Documentation is a per-item gate, not only a closing phase.** No component item may be marked
`DONE` until its **README** entry and its **CHANGELOG** line both exist. The moment a component lands,
add it to the running `## [0.6.0]` CHANGELOG section (Phase 0 creates the skeleton) and to the README
feature/widget listing. Phase 7 is then a *consolidation and voice pass* over already-present entries —
it reconciles wording, the version banner, and the install snippet — not the first time README and
CHANGELOG are touched. The full documentation set each item feeds is: `README.md`, `CHANGELOG.md`, the
`TUIKit.csproj` `Version`/`AssemblyVersion`/`FileVersion`/`Description`/`PackageReleaseNotes`, the
example README, and the `docs/` coverage/conformance audits (Phases 7 and 8). The per-component
README/CHANGELOG checklists in items D1 and D2 are the tracking grid for this gate.

---

## Phase 0 — Release setup & conventions

**Status:** `TODO` — **Notes:**

- [ ] Create a working branch off `main` (e.g. `feature/0.6.0-horizontal-components`).
- [ ] Add a `## [0.6.0] - <date>` skeleton under `## [Unreleased]` in `CHANGELOG.md` with `Added`,
      `Changed`, and `Removed`/`Breaking` sections to fill in as items land.
- [ ] Decide the display-selector convention once and record it here so every generic widget uses the
      same shape: **`Func<T, string>` display selector, defaulting to identity when `T` is `string`.**
- [ ] Confirm the `Test.Shared/Suites` naming convention for the new suites (see the manifest in the
      Appendix) so they do not collide with the 40+ existing suites.

---

## Phase 1 — Region backgrounds

A region can currently carry padding, a border, and a border title, but its interior always paints in
the theme's default text background. A consumer that wants a dark-grey sidebar against a black
transcript, a tinted status strip, or a "card" look for a panel has no way to express it. mux worked
around this with a bespoke transparent-background theme plus per-pane background painting
(`ApplyPaneBackgrounds`). The framework should own this.

The rendering contract: a region resolves an **effective background** — an explicit `Color` wins;
otherwise a named **theme role** is looked up so themes can restyle backgrounds without code changes;
otherwise the region is transparent and inherits the theme text background exactly as today. The
background fills the region's whole resolved rectangle *before* the border and content draw, so the
border sits on top of the fill and bound widgets inherit the tint.

Keep the layer boundary clean: `Region` (in `TUIKit.Layout`) must not take a dependency on `Theme`
(in `TUIKit.Theming`). `Region` stores the raw inputs; the host resolves the role to a color.

### R1 — `Region` background inputs

**Status:** `TODO` — **Notes:**

- [ ] `src/TUIKit/Layout/Region.cs`: add `public Color? Background { get; }` and
      `public string? BackgroundRole { get; }` (both default `null`). Extend the constructor with two
      optional trailing parameters so existing positional callers keep working; document that an
      explicit `Background` takes precedence over `BackgroundRole`, and that `null`/`null` means the
      region is transparent (inherits the theme text background). Throw `ArgumentException` when
      `BackgroundRole` is a non-null empty/whitespace string.
- [ ] `src/TUIKit/Layout/RegionBuilder.cs`: add `Background(Color color)` and `BackgroundRole(string role)`
      fluent methods with XML docs and the same empty-role guard, and a `NoBackground()` reset for
      symmetry with `NoBorder()`.
- [ ] Verify `MinimumSize`/`ContentRect` are unaffected (background does not change geometry).

### R2 — Host paints the background

**Status:** `TODO` — **Notes:**

- [ ] `src/TUIKit/Hosting/TuiApplication.cs`, `Compose(ISurface root)` (the per-region loop around
      lines 818–851): before drawing the border and content for each region, resolve the effective
      background style and, when the region is not transparent, `root.Fill(frame, Cell.Blank(bgStyle))`
      over the intersected resolved frame. When a background is set, the content-view fill (currently
      `Cell.Blank(_Theme.Text)`) and the `Pane.Render(view, style)` call must use the region background
      instead of `_Theme.Text` so widget cells inherit the tint rather than repainting it away.
- [ ] Add a private resolver `CellStyle ResolveRegionBackground(Region region)`:
      explicit `Background` → a `CellStyle` with that background and the theme text foreground;
      else `BackgroundRole` → `_Theme.GetStyle(role)` (which already falls back to `Text` for unknown
      roles); else `_Theme.Text`. Keep it null-safe and allocation-light.
- [ ] Confirm `LayoutBlockScreen` (the too-small screen) and the modal `Backdrop` paths are left
      unchanged — backgrounds apply only to the normal region render.

### R3 — Theme role support, example, tests, docs

**Status:** `TODO` — **Notes:**

- [ ] `src/TUIKit/Theming/Theme.cs`: document that named styles registered with `SetStyle` double as
      background roles for regions, and add a couple of conventional role names (for example
      `"sidebar"`, `"statusbar"`) to the `Dark`/`Light`/`HighContrast` presets so
      `.BackgroundRole("sidebar")` renders something sensible out of the box. No new API is required if
      the existing named-style dictionary suffices; if a typed accessor reads better, add
      `Color BackgroundFor(string role)` returning the role style's background.
- [ ] **Example:** in `GuidedTour` (`src/TUIKit.Example/GuidedTour.cs`) give the `demo`/`interactive`
      panels a background (one explicit `Color`, one `BackgroundRole`) so the tour visibly shows a
      tinted panel, and add a short `TourPage` explaining region backgrounds with the builder snippet.
- [ ] **Tests:** extend `LayoutSuite`/`LayoutConstraintSuite` (or add `RegionBackgroundSuite`).
      Positive: a region with an explicit `Background` fills every cell of its resolved rect with that
      background color (compose into a `BufferSurface` via a `HeadlessBackend` and read `Cell` styles);
      `BackgroundRole` resolves through the active theme; a bound widget's cells carry the region
      background; a transparent region still paints the theme text background. Negative:
      `RegionBuilder.Background`/`BackgroundRole` and the `Region` constructor reject an empty/whitespace
      role with `ArgumentException`; switching themes re-resolves a role-based background.
- [ ] **Docs:** add region backgrounds to the README layout section and the CHANGELOG `Added` list.

---

## Phase 2 — Tier 1 core primitives

The four highest-leverage additions. Every one removes boilerplate that mux repeated across many
call sites, and each is something a general TUI consumer needs on day one.

### T1-1 — `DialogModal` auto-sizing bordered base

**Status:** `TODO` — **Notes:**

mux has eleven custom modals, and every one privately reimplements the same centered-box sizing,
padding, border draw, footer-hint line, and text truncation. TUIKit's `Modal` base offers only
`ContentPadding`. A reusable bordered-dialog base eliminates that duplication for everyone.

- [ ] Add `src/TUIKit/Modals/DialogModal.cs` — `public abstract class DialogModal : Modal`. It owns:
      a `Title` (nullable), an optional dim `FooterHint`, `MinWidth`/`MaxWidth`/`MinHeight`/`MaxHeight`,
      an auto-size mode that fits the box to content within those bounds and centers it, and a
      `ContentPadding` inherited from `Modal`. It implements `Render(ISurface)` to draw the box, title,
      and footer, then calls a protected abstract `RenderContent(ISurface inner)` handing the subclass
      an already-inset, already-clipped content surface. Provide protected helpers `Truncate(string, int)`
      and `MeasureContent(...)` so subclasses stop hand-rolling them.
- [ ] Keep `HandleKey` abstract (dialogs still own their keys) but provide a protected
      `HandleDismiss(KeyEvent)` that closes on Escape honoring `CanClose`.
- [ ] Migrate the existing `MessageModal`, `PromptModal`, and `SelectModal` to derive from
      `DialogModal` to prove the base is sufficient and to shed their private box code. This is an
      internal refactor; their public behavior and results must not change (existing modal suites must
      stay green).
- [ ] **Example:** a `DialogModal` subclass in `TUIKit.Example` (e.g. a small "about this key" info
      dialog) launched from the tour, plus a gallery entry.
- [ ] **Tests:** new `DialogModalSuite`. Positive: auto-size fits content and respects
      min/max bounds; title and footer render; `RenderContent` receives a correctly inset surface;
      Escape closes when `CanClose` and refuses when overridden false. Negative: negative or inverted
      min/max bounds throw `ArgumentOutOfRangeException`; null title is allowed but null content
      writes are rejected where applicable.

### T1-2 — `CheckList<T>` widget and `MultiSelectModal<T>`

**Status:** `TODO` — **Notes:**

TUIKit has single-select (`SelectModal`) but nothing for choosing several items. mux built
`MultiSelectModal` from scratch (Space toggles, `a` toggles all, checked coloring, scroll windowing).
Both a reusable widget and a modal wrapper are worth shipping.

- [ ] Add `src/TUIKit/Widgets/CheckList.cs` — `public sealed class CheckList<T> : IWidget, IFocusable, IFocusAware`.
      Items plus a `Func<T,string>` display selector; Up/Down move, Space toggles, an optional
      "toggle all" key; exposes `CheckedIndices`/`CheckedItems`, `SelectedIndex`, per-item checked
      state, and configurable check glyphs and checked/selection styles. Scroll-to-cursor windowing so
      long lists stay navigable.
- [ ] Add `src/TUIKit/Modals/MultiSelectModal.cs` — `public sealed class MultiSelectModal<T> : DialogModal`
      wrapping a `CheckList<T>`; Enter completes with the chosen `IReadOnlyList<int>` (or `IReadOnlyList<T>`),
      Escape cancels with an empty result. Use the T1-1 base for sizing/footer.
- [ ] **Example:** a `CheckList` tour page and a `MultiSelectModal` launched from a command (choose
      from a fixed set of options; echo the result into the events log).
- [ ] **Tests:** new `MultiSelectSuite`. Positive: toggling updates checked state; toggle-all flips
      all; Enter returns the exact checked set; windowing keeps the cursor visible; display selector is
      honored for non-string `T`. Negative: null items/selector throw `ArgumentNullException`; empty
      item list is handled (renders empty, returns empty) without throwing on navigation.

### T1-3 — Focus-following scroll (`ScrollView` + tall `Form`)

**Status:** `TODO` — **Notes:**

`ScrollView` scrolls but does not track focus, so a `Form` (or any content) taller than its viewport
can move focus to an off-screen field. mux solved this only for its forms, by rendering the `Form`
into an off-screen `CellBuffer` and copying a scrolled window that follows the focused field
(`ComputeScrollOffset`). Generalize it.

- [ ] Add a small contract `src/TUIKit/Widgets/IScrollExtent.cs` — a child can report its total content
      height and the bounds of the currently focused sub-region (for example
      `int ContentHeight { get; }` and `bool TryGetFocusRect(out Rect rect)`). No default interface
      methods (ns2.0), so it is a plain interface.
- [ ] `src/TUIKit/Widgets/ScrollView.cs`: add `AutoScrollToFocus` (default true) and an
      `EnsureVisible(int top, int height)` method; when the child implements `IScrollExtent` and focus
      moves, scroll the minimum amount to bring the focused rect fully into view. Preserve the existing
      wheel/`IMouseAware` behavior.
- [ ] `src/TUIKit/Widgets/Form.cs`: implement `IScrollExtent` so a `Form` inside a `ScrollView` scrolls
      to its focused field automatically, deleting the need for consumer-side offset math.
- [ ] **Example:** a tall `Form` (more fields than fit) inside a `ScrollView` as a tour page; Tab moves
      through fields and the viewport follows.
- [ ] **Tests:** extend `FocusFormsPromptsSuite` or add `ScrollFocusSuite`. Positive: focusing an
      off-screen field scrolls it into view; already-visible fields do not scroll; wheel scrolling
      still works. Negative: `EnsureVisible` with out-of-range/negative arguments clamps rather than
      throwing (documented), and a child without `IScrollExtent` behaves exactly as before.

### T1-4 — Generic `ListView<T>`, `SelectModal<T>`, `FuzzyList<T>` (breaking)

**Status:** `TODO` — **Notes:**

`ListView`, `SelectModal`, and `FuzzyList` are string-only, forcing every consumer to keep a parallel
array mapping the selected index back to a real object. mux does this repeatedly. Per the chosen
alpha stance, make them generic with no compatibility shim; the plain-string case becomes
`ListView<string>`.

- [ ] `src/TUIKit/Widgets/ListView.cs` → `ListView<T>`: constructor takes an optional
      `Func<T,string>` display selector (identity default when `T` is `string`); items are `T`;
      `SelectedItem` returns `T?`. Preserve focus/selection/scroll behavior and styling.
- [ ] `src/TUIKit/Modals/SelectModal.cs` → `SelectModal<T>`; `src/TUIKit/Widgets/FuzzyList.cs` →
      `FuzzyList<T>` (fuzzy match runs over the display text; match highlighting preserved).
- [ ] Update every internal call site to the generic form. Known references (from a repo grep of
      `ListView`/`SelectModal`/`FuzzyList`): `src/TUIKit/Modals/SelectModal.cs`,
      `src/TUIKit/Hosting/TuiApplication.cs`, and in `src/TUIKit.Example`: `ContractDemo.cs`,
      `GuidedTour.cs`, `ChoiceModal.cs`. In `src/Test.Shared/Suites`: `WidgetSuite.cs`,
      `WidgetGuardValidationSuite.cs`, `BackendModalValidationSuite.cs`, `LayoutConstraintSuite.cs`,
      `ReactiveAnimationSuite.cs`, `NewWidgetsSuite.cs`. Re-run the grep after editing to catch any
      missed usage.
- [ ] **Example:** convert a tour page to bind a `ListView<T>` over a small record type (not strings)
      to show the selector, and keep a `ListView<string>` example for the simple case.
- [ ] **Tests:** update the existing widget/modal suites to the generic form, and add cases proving a
      non-string `T` selects the correct item and renders via the display selector. Negative: null
      selector for non-string `T` throws `ArgumentNullException`; null/empty item collections keep the
      existing documented guards (`SetItems(null)` throws).

---

## Phase 3 — Tier 2 composition

Higher-order patterns that compose the primitives. Each generalizes cleanly beyond mux's use.

### T2-1 — List with inline row actions

**Status:** `TODO` — **Notes:**

mux's endpoint list supports per-row shortcuts (`e` edit, `d`/Del remove) and returns a typed result
saying which action fired on which row. Generalize to a list whose rows expose consumer-registered
key→action bindings.

- [ ] Add `src/TUIKit/Widgets/ActionListView.cs` — `public sealed class ActionListView<T> : IWidget, IFocusable`
      built on `ListView<T>`. Register actions as `(KeyChord chord, string actionId, Func<T,bool> isEnabled)`.
      Raise an event or expose a result struct `ListAction` (avoid tuples per style) carrying the
      selected index/item and the fired `actionId`; Enter is the default "activate" action.
- [ ] Provide a `SelectListModal<T>` wrapper (on `DialogModal`) that returns the typed activation so a
      consumer gets "row 3, action edit" in one result.
- [ ] **Example:** a tour page / gallery entry: a list of items with `Enter`=open, `d`=delete,
      `r`=rename, echoing the fired action.
- [ ] **Tests:** new `ActionListSuite`. Positive: registered chords fire on eligible rows and return
      the right index+action; disabled rows swallow the action; Enter maps to activate. Negative: null
      chord/action id/predicate throw `ArgumentNullException`; duplicate action bindings follow a
      documented conflict policy (throw or last-wins — pick and document).

### T2-2 — `ReorderableList<T>` / list editor

**Status:** `TODO` — **Notes:**

mux's queue editor supports inline edit, delete, and reorder with a two-mode (navigate/edit) state
machine. A reusable reorderable list covers playlists, priority queues, column ordering, and keybind
ordering.

- [ ] Add `src/TUIKit/Widgets/ReorderableList.cs` — `public sealed class ReorderableList<T> : IWidget, IFocusable`.
      Move-up/move-down keys (default `[`/`]` or Alt+Up/Down — document the default and make them
      configurable), optional inline rename via an embedded `TextField` when `T` is editable through a
      supplied getter/setter delegate, delete key, and a `Reordered` event. Exposes the current
      ordering as `IReadOnlyList<T>`.
- [ ] Optional `ListEditorModal<T>` on `DialogModal` returning the edited/reordered list.
- [ ] **Example:** reorder a short list in the gallery; show the resulting order.
- [ ] **Tests:** new `ReorderableListSuite`. Positive: move up/down reorders and clamps at the ends;
      delete removes the selected item and fixes selection; rename writes back through the setter.
      Negative: moving past either end is a no-op (not an exception); null delegates where required
      throw `ArgumentNullException`.

### T2-3 — Dynamic / dependent form fields

**Status:** `TODO` — **Notes:**

mux's MCP form rebuilds its field set when a selector changes (transport, auth type) and restores
focus afterward. Give `Form` a supported way to swap field groups at runtime.

- [ ] `src/TUIKit/Widgets/Form.cs`: add `SetFields(...)`/`ReplaceFields(...)` that rebuilds the field
      list and tab order in place, preserving focus on a stable field where possible (by field key).
      Add an optional `VisibleWhen(Func<bool>)` predicate on `FormField` so groups can show/hide
      without a full rebuild. Ensure validation and tab traversal respect visibility.
- [ ] **Example:** a form whose visible fields change based on a `RadioGroup` selection (e.g. "connection
      type: local / network" toggling a host+port group), in the gallery.
- [ ] **Tests:** extend `FocusFormsPromptsSuite` or add `DynamicFormSuite`. Positive: rebuilding fields
      updates tab order and keeps focus sensible; hidden fields are skipped by Tab and by validation;
      showing a field restores it to traversal. Negative: null field arrays throw; a `VisibleWhen`
      that throws is surfaced (not swallowed) or documented.

### T2-4 — `DefinitionList` / status panel widget

**Status:** `TODO` — **Notes:**

mux's sidebar is a clean labeled key/value telemetry panel (idempotent clear+rewrite, width fitting,
sections). Generalize to a labeled-rows widget any dashboard can use.

- [ ] Add `src/TUIKit/Widgets/DefinitionList.cs` — `public sealed class DefinitionList : IWidget`.
      Ordered rows of `(label, value)` with optional section headers, right-aligned or left-aligned
      values, per-row style overrides, and width fitting that truncates the value (not the label) when
      space is tight. Thread-safe row updates (it will often be written from a background thread), using
      `ReaderWriterLockSlim`.
- [ ] **Example:** a live status panel in the tour/gallery showing a few labeled values that update on
      a tick (reuse the existing animation/timer infrastructure).
- [ ] **Tests:** new `DefinitionListSuite`. Positive: rows render aligned; sections render; values
      truncate with an ellipsis while labels stay intact; updating a row re-renders. Negative: null
      label/section throws `ArgumentNullException`; negative width requests clamp; concurrent writes do
      not corrupt output (a stress case that writes from multiple tasks and asserts a consistent frame).

### T2-5 — `CommandRegistry` → surfaces fan-out

**Status:** `TODO` — **Notes:**

mux drives keybindings, the menu bar, a command palette, and a slash router from one `CommandDescriptor`
catalog. TUIKit has the routing table and `MenuBar` but no single command abstraction feeding all
surfaces. Provide one; keep it optional so the existing `Commands.Register`/`RegisterCommand` API still
works for consumers who do not want it.

- [ ] Add `src/TUIKit/Input/Command.cs` — an immutable descriptor: id, title, category, optional
      `KeyChord`, optional slash aliases, `Action` (or `Func<Task>` with `CancellationToken`),
      and an `IsEnabled` predicate. One class per file; add a separate `CommandCategory` type only if
      an enum is warranted (otherwise a string category).
- [ ] Add `src/TUIKit/Input/CommandRegistry.cs` — holds the ordered commands and offers projections:
      `RegisterAll(TuiApplication)` (binds chords + handlers into the existing router),
      `BuildMenuBar()` (grouped by category, first-seen order → a `MenuBar`),
      `BuildPalette()` (a `FuzzyList<Command>` for a command palette), and
      `ResolveSlash(string input)` (routes `/name args`). No mux naming.
- [ ] **Example:** wire a small `CommandRegistry` in the gallery so one registration lights up a menu
      bar, an F1 palette, and `/`-commands together.
- [ ] **Tests:** new `CommandRegistrySuite`. Positive: one registration produces matching chord binding,
      menu entry, palette entry, and slash resolution; disabled commands are filtered from palette/menu;
      slash matching is case-insensitive. Negative: null command/id throws; duplicate ids or duplicate
      chords follow a documented conflict policy; unknown slash returns a "not found" resolution rather
      than throwing.

---

## Phase 4 — Tier 3 streaming & typeahead

The framework's stated primary consumer is an agent control harness with streaming output, yet the
streaming glue is exactly what mux had to build itself. These items raise the abstraction one level.

### T3-1 — `ActivityIndicator` (animated status line)

**Status:** `TODO` — **Notes:**

mux built an animated "thinking" line twice — once for full-screen (braille spinner + rotating
phrases, updated in place via `PaneLineHandle`) and once for line mode (a sliding-window animator).
TUIKit has `Spinner` but no higher-level activity line that owns the animation loop, rotating message,
and in-place update.

- [ ] Add `src/TUIKit/Widgets/ActivityIndicator.cs` — `public sealed class ActivityIndicator : IWidget`.
      Configurable spinner frames (default a braille set), an optional rotating phrase list with a
      configurable interval, and a `Tick()`/frame-advance method that advances deterministically (drive
      it from the existing `FrameTimer`, not wall-clock, so it stays testable). Expose the current
      rendered line so it can also be pushed into a `Pane` via a `PaneLineHandle` for in-transcript use.
- [ ] Provide a thin helper to bind an `ActivityIndicator` to a `PaneLineHandle` so "update this one
      line in place while work runs" is a one-liner.
- [ ] **Example:** a tour page with a running indicator; a gallery command that starts/stops one in the
      transcript pane.
- [ ] **Tests:** new `ActivityIndicatorSuite`. Positive: ticking advances frames and rotates phrases on
      schedule; rendering is stable for a given tick; the pane-handle binding updates in place.
      Negative: null/empty frame or phrase arrays throw the documented exception; a zero/negative
      interval is rejected or clamped (document which).

### T3-2 — `StreamingTranscript` / pane projector

**Status:** `TODO` — **Notes:**

mux's `AgentEventProjector` is generic scrollback plumbing wearing agent clothes: buffer streaming
text per block, re-render the finished block through `MarkdownRenderer`, and keep sub-lines (tool
status, etc.) updatable in place keyed by an id. Extract the reusable core.

- [ ] Add `src/TUIKit/Content/StreamingTranscript.cs` — a helper over `Pane` that offers:
      `AppendText(string)` for live streaming into a single growing line/block; `FinalizeBlock()` to
      re-render the buffered block as Markdown; and keyed updatable lines
      (`PaneLineHandle Track(string key)` / `Update(string key, ...)`) so a consumer can flip a
      "running…" line to "done" without knowing pane internals. Thread-safe (panes already are; keep the
      key map guarded). Strictly no agent/LLM vocabulary — it projects *text and keyed status lines*,
      nothing more.
- [ ] **Example:** a gallery command that simulates a stream (timer-driven) writing partial text, then
      finalizing it as Markdown, with a couple of keyed status lines flipping state.
- [ ] **Tests:** new `StreamingTranscriptSuite`. Positive: appended text accumulates then renders as
      Markdown on finalize; a tracked key updates the same line rather than appending; interleaved
      streams stay separated. Negative: null text/keys throw; updating an unknown key throws or is a
      documented no-op; finalizing with nothing buffered is safe.

### T3-3 — Autocomplete / typeahead overlay

**Status:** `TODO` — **Notes:**

This is the one capability the original plan intentionally excluded, and mux felt its absence: it
routes `/`-commands with no completion popup. Reversing the exclusion pays off directly alongside the
`CommandRegistry` (T2-5).

- [ ] Add a candidate contract `src/TUIKit/Widgets/ISuggestionProvider.cs` —
      `IReadOnlyList<string> Suggest(string input)` plus an async variant taking a `CancellationToken`
      per the style rule for `IEnumerable`-returning members. A simple prefix/substring provider ships
      in the box.
- [ ] Add `src/TUIKit/Widgets/AutocompleteOverlay.cs` — an overlay that attaches to a `TextField`/
      `TextEditor` caret position, shows ranked suggestions, supports Up/Down + Tab/Enter to accept and
      Escape to dismiss, and renders above/below the input depending on available space. Draw it via the
      host `RenderOverlay` hook so it composes over existing content without a modal.
- [ ] **Example:** a tour page with an input that suggests from a fixed word list; a gallery command
      that wires autocomplete to the `CommandRegistry` slash names.
- [ ] **Tests:** new `AutocompleteSuite`. Positive: provider results render ranked; Tab/Enter accept the
      highlighted suggestion into the input; Escape dismisses; the overlay flips above the caret when
      there is no room below. Negative: null input/provider throws; an empty suggestion set hides the
      overlay rather than drawing an empty box; a provider that returns null is treated as empty.

---

## Phase 5 — Tier 4 utilities

Small, reusable pieces mux hand-rolled. Each is a low-risk addition that many consumers re-invent.

### T4-1 — Hint-text wrapper

**Status:** `TODO` — **Notes:**

- [ ] Add `src/TUIKit/Content/HintText.cs` — a greedy word wrapper for separator-delimited hint
      strings (default separator `·`) that never splits a segment, wrapping to a width instead of
      truncating. Return `IReadOnlyList<string>` lines (plus the async style variant only if a consumer
      would ever wrap a huge set; otherwise document why the sync-only exception applies).
- [ ] **Example:** use it for the `StatusBar`/footer hint in the tour so long hint sets wrap.
- [ ] **Tests:** new or extended suite. Positive: wraps at the boundary, never mid-segment; single long
      segment occupies its own line; custom separators. Negative: null input throws; zero/negative width
      is rejected or clamped (document).

### T4-2 — Multi-column list formatter

**Status:** `TODO` — **Notes:**

- [ ] Add `src/TUIKit/Content/ColumnFormatter.cs` — formats rows of N cells into aligned columns with
      per-column padding and alignment, computing column widths from content (the logic behind mux's
      three-column command menu). Returns formatted lines usable by any list widget or `Label`.
- [ ] **Example:** render an aligned two/three-column reference (key → description) in the tour.
- [ ] **Tests:** positive: columns align to the widest cell; alignment and padding honored; ragged rows
      handled. Negative: null rows/cells throw; mismatched column counts follow a documented rule.

### T4-3 — `Rule` / `Separator` widget

**Status:** `TODO` — **Notes:**

- [ ] Add `src/TUIKit/Widgets/Rule.cs` — a horizontal or vertical rule (`SplitOrientation` reuse) with
      an optional centered caption, a configurable glyph, and a style. mux drew these by hand via
      `RenderOverlay`; a widget makes them layout-native and also usable as a thin region.
- [ ] **Example:** separate sections in the tour with a captioned rule.
- [ ] **Tests:** positive: horizontal/vertical rules fill their extent; caption centers and truncates;
      custom glyph/style. Negative: null caption allowed, empty style handled; zero-length extent is a
      no-op.

### T4-4 — Submit-vs-newline key resolver

**Status:** `TODO` — **Notes:**

Deciding whether a keypress in a multi-line editor submits or inserts a newline is genuinely tricky
across terminals (Shift+Enter vs Ctrl+J vs bare CR, depending on the keyboard protocol). mux got it
right by hand in its key filter. Centralize it so no `TextEditor` consumer reinvents it.

- [ ] Add `src/TUIKit/Input/SubmitKeyResolver.cs` — given a `KeyEvent` and a configurable policy
      (which chord submits, which inserts newline), return a small `SubmitDecision` enum
      (`Submit`/`InsertNewline`/`Ignore`). Encode the CR-vs-Ctrl+J reality documented in the 0.4.1
      changelog so the default "Enter submits, Ctrl+J / Shift+Enter inserts newline" works on every
      platform. No tuples; a named enum + a small options object.
- [ ] **Example:** wire the resolver into the tour's `Interactive` box so Enter submits and Ctrl+J adds
      a line, and show the decision in the events log.
- [ ] **Tests:** new `SubmitKeyResolverSuite`. Positive: CR → Submit, Ctrl+J → InsertNewline under the
      default policy; a custom policy remaps them; unrelated keys → Ignore. Negative: null key/policy
      throws `ArgumentNullException`.

### T4-5 — OS-adaptive modifier labels (verify/extend)

**Status:** `TODO` — **Notes:**

mux renders `CTRL`/`OPTION`/`CMD` based on OS. TUIKit already has `KeyLabel`/`KeyLabelStyle`. Confirm
it produces platform-appropriate modifier names and, if there is a gap, close it rather than adding a
parallel API.

- [ ] Audit `src/TUIKit/Input/KeyLabel.cs` (and `KeyLabelStyle`): confirm macOS renders `⌘/⌥/⌃` and
      Windows/Linux render `Ctrl/Alt`. If a style or platform is missing, extend the existing type; do
      not introduce a second labeling API.
- [ ] **Example:** show environment-appropriate hints in the tour footer (already partly done via
      `KeyLabel.Recommended`); make the coverage explicit.
- [ ] **Tests:** extend `KeyBindingSuite`. Positive: each `KeyLabelStyle` produces the expected string
      for representative chords across platforms (drive the platform as an input, not `Environment.OS`,
      so it is deterministic). Negative: null chord throws.

---

## Phase 6 — Example gallery integration

Every component above needs a runnable, screenshot-able demonstration. The example project already has
three modes (guided tour, harness, contract). Add a fourth that is purpose-built as a component
gallery, and make each new interactive piece reachable and headless-snapshottable.

### E1 — Component gallery mode

**Status:** `TODO` — **Notes:**

- [ ] Add `src/TUIKit.Example/ComponentGallery.cs` — a mode that lists the new components and lets the
      user open each (modals via commands, widgets as focusable panels). Mirror the structure of
      `GuidedTour`/`ContractDemo` so it shares the host wiring.
- [ ] `src/TUIKit.Example/Program.cs`: add a `--gallery` live mode and a `--gallery-once` (and, if
      needed, `--gallery-page N`) deterministic headless snapshot mode following the existing
      `--tour-once`/`--contract-once` pattern, feeding scripted input through `HeadlessBackend.FeedInput`
      + `PumpInputOnce` so each component renders a stable frame.
- [ ] Ensure the gallery covers **every** item: region backgrounds (R1–R3), DialogModal (T1-1),
      CheckList/MultiSelect (T1-2), scroll-follows-focus form (T1-3), generic list over a record
      (T1-4), action list (T2-1), reorderable list (T2-2), dynamic form (T2-3), definition list (T2-4),
      command registry fan-out (T2-5), activity indicator (T3-1), streaming transcript (T3-2),
      autocomplete (T3-3), and the Tier 4 utilities (T4-1…T4-5).

### E2 — Guided tour pages

**Status:** `TODO` — **Notes:**

- [ ] Add a `TourPage` (`src/TUIKit.Example/TourPage.cs` / `GuidedTour.cs`) for each new **widget**
      (non-modal) so the default `TUIKit.Example` run walks a first-time user through them with a live
      demo on the left and the building code on the right, matching the existing tour convention.
- [ ] Cross-check the tour and gallery against the component manifest in the Appendix so nothing ships
      without a visible example.

---

## Phase 7 — Documentation & packaging

The README and CHANGELOG are touched twice, by design. Each component item updates them *as it lands*
(the per-item documentation gate in the Global Obligations), and this phase is the reconciliation pass
that reads the accumulated entries as a whole and fixes voice, ordering, the version banner, and the
install snippet. The two matrices below are the checklist for that gate: tick README and CHANGELOG for
each shipped component so the final pass starts from a complete set rather than an empty one.

### D1 — README

**Status:** `TODO` — **Notes:**

The README currently advertises the widget toolkit in its feature narrative and (per the section grep)
carries `## What it is` / `## What it does` prose, an install snippet, and a "Screenshots" section.
Region backgrounds and every new component must appear in that narrative, and the version banner and
`<PackageReference ... Version="0.6.0" />` snippet must move off `0.5.1`.

Per-component README coverage (tick when the component is named/described in the README):

- [ ] Region backgrounds (R1–R3) — layout section mentions explicit color + theme-role backgrounds.
- [ ] `DialogModal` base (T1-1)
- [ ] `CheckList<T>` / `MultiSelectModal<T>` (T1-2)
- [ ] Focus-following `ScrollView` / tall `Form` (T1-3)
- [ ] Generic `ListView<T>` / `SelectModal<T>` / `FuzzyList<T>` (T1-4) — note the generic form in prose.
- [ ] `ActionListView<T>` (T2-1) · `ReorderableList<T>` (T2-2) · dynamic `Form` fields (T2-3) ·
      `DefinitionList` (T2-4) · `CommandRegistry` fan-out (T2-5)
- [ ] `ActivityIndicator` (T3-1) · `StreamingTranscript` (T3-2) · autocomplete overlay (T3-3)
- [ ] Tier 4 utilities: `HintText` (T4-1) · `ColumnFormatter` (T4-2) · `Rule` (T4-3) ·
      `SubmitKeyResolver` (T4-4) · OS-adaptive key labels (T4-5)

Reconciliation tasks:

- [ ] Version banner and the `<PackageReference Include="TUIKit" Version="..."/>` snippet read `0.6.0`.
- [ ] Add gallery screenshots (or at least a gallery mention) to the "Screenshots" section, referencing
      the `--gallery` mode from item E1.
- [ ] Read the changed sections aloud for voice per `WRITING_DOCUMENTS.md`: concrete claims, no generic
      filler ("robust", "powerful", "seamless"), varied rhythm, real prose around any new list.

### D2 — CHANGELOG

**Status:** `TODO` — **Notes:**

Phase 0 creates the `## [0.6.0]` skeleton under `## [Unreleased]`. Each component item files its line
into that section the moment it lands; this task confirms the section is complete, correctly grouped,
and follows the "Keep a Changelog" structure already in the file.

Per-component CHANGELOG entry (tick when the line exists under `## [0.6.0]`):

- [ ] Region backgrounds (R1–R3) → `Added`
- [ ] `DialogModal` base (T1-1) → `Added`; the `MessageModal`/`PromptModal`/`SelectModal` migration → `Changed`
- [ ] `CheckList<T>` / `MultiSelectModal<T>` (T1-2) → `Added`
- [ ] Focus-following scroll (T1-3) → `Added`/`Changed` (ScrollView gains `AutoScrollToFocus`)
- [ ] Generic list widgets (T1-4) → **`Breaking`** — a clearly labeled block with the migration note
      (`new ListView()` → `new ListView<string>()`, `SelectModal`/`FuzzyList` likewise).
- [ ] `ActionListView<T>` (T2-1) · `ReorderableList<T>` (T2-2) · dynamic `Form` fields (T2-3) ·
      `DefinitionList` (T2-4) · `CommandRegistry` (T2-5) → `Added` (T2-3 also `Changed` on `Form`)
- [ ] `ActivityIndicator` (T3-1) · `StreamingTranscript` (T3-2) · autocomplete (T3-3) → `Added`
- [ ] Tier 4 utilities (T4-1…T4-4) → `Added`; any `KeyLabel` extension (T4-5) → `Added`/`Changed`
- [ ] Theme presets gain conventional background roles (R3) → `Added`/`Changed`

Reconciliation tasks:

- [ ] Confirm `Added` / `Changed` / `Breaking` grouping is correct and the date is set on release.
- [ ] Confirm the `Breaking` block is prominent (generic list migration is the only breaking change).
- [ ] Cross-check every Appendix A file and Appendix B suite has a corresponding CHANGELOG mention or a
      deliberate reason it needs none.

### D3 — Packaging metadata

**Status:** `TODO` — **Notes:**

- [ ] `src/TUIKit/TUIKit.csproj`: set `Version`/`AssemblyVersion`/`FileVersion` to `0.6.0`, extend the
      `Description` to mention the new capability areas, and prepend a `v0.6.0 (Alpha)` entry to
      `PackageReleaseNotes` summarizing the additions and the one breaking change.

### D4 — Example README

**Status:** `TODO` — **Notes:**

- [ ] Update the `TUIKit.Example` README (the capability matrix referenced by the build plan) to
      document the `--gallery`/`--gallery-once` modes and list which component each demonstrates.

### D5 — Other markdown

**Status:** `TODO` — **Notes:**

- [ ] `docs/SURFACE_COVERAGE.md`: add the new public surface and its coverage status.
- [ ] `docs/CONFORMANCE.md`: add the new files to the conformance audit table (PASS or justified N/A).
- [ ] `archive/TUIKIT_PLAN.md`: add a short note that the 0.6.0 horizontal-components work (this plan)
      extends the completed 20-phase build; link to this file rather than duplicating it.
- [ ] Grep the repo for stale `0.5.1` references in markdown and correct any that should track the
      current release.

---

## Phase 8 — Final passes

### F1 — Build & test green

**Status:** `TODO` — **Notes:**

- [ ] `dotnet build src` (Release) is clean: zero warnings, zero errors, across
      `netstandard2.0;net8.0;net10.0` with `TreatWarningsAsErrors=true`.
- [ ] `dotnet run --project src/Test.Automated` passes; `dotnet test src/Test.Xunit` and
      `dotnet test src/Test.Nunit` pass on `net8.0` and `net10.0`. Record the new total case count.
- [ ] Every new suite is registered in `TUIKitSuites.All` and every item has both positive and negative
      cases.

### F2 — Surface-area → coverage audit

**Status:** `TODO` — **Notes:**

- [ ] Re-run the surface/coverage review used for the original Phase 19; confirm each new public type
      is either covered or has a justified exclusion recorded in `docs/SURFACE_COVERAGE.md`.

### F3 — Requirements conformance audit

**Status:** `TODO` — **Notes:**

- [ ] Walk every new/changed file against `c:\code\agents\requirements\CODE_STYLE.md` and record the
      result in `docs/CONFORMANCE.md`. Verify: usings inside namespace and ordered; one type per file;
      XML docs on all publics with `<exception>` tags; `_PascalCase` fields; no `var`; no tuples; async
      methods take `CancellationToken` and use `ConfigureAwait(false)`; `IEnumerable`-returning members
      have async variants; guard clauses present; no `Console.Write*`; no `#if` outside the allowed
      shims.
- [ ] Flip this plan's front-matter status table and per-item statuses to `DONE`, and update the
      _Last updated_ line.

---

## Appendix A — New file manifest

Library (`src/TUIKit`):

- `Layout/Region.cs` (edit), `Layout/RegionBuilder.cs` (edit), `Hosting/TuiApplication.cs` (edit),
  `Theming/Theme.cs` (edit) — region backgrounds (R1–R3).
- `Modals/DialogModal.cs` — T1-1. Edits to `Modals/MessageModal.cs`, `Modals/PromptModal.cs`,
  `Modals/SelectModal.cs` to derive from it.
- `Widgets/CheckList.cs`, `Modals/MultiSelectModal.cs` — T1-2.
- `Widgets/IScrollExtent.cs`, `Widgets/ScrollView.cs` (edit), `Widgets/Form.cs` (edit) — T1-3.
- `Widgets/ListView.cs`, `Modals/SelectModal.cs`, `Widgets/FuzzyList.cs` → generic (edit) — T1-4.
- `Widgets/ActionListView.cs` — T2-1.
- `Widgets/ReorderableList.cs` — T2-2.
- `Widgets/Form.cs` (edit), `Widgets/FormField.cs` (edit) — T2-3.
- `Widgets/DefinitionList.cs` — T2-4.
- `Input/Command.cs`, `Input/CommandRegistry.cs` — T2-5.
- `Widgets/ActivityIndicator.cs` — T3-1.
- `Content/StreamingTranscript.cs` — T3-2.
- `Widgets/ISuggestionProvider.cs`, `Widgets/AutocompleteOverlay.cs` — T3-3.
- `Content/HintText.cs` — T4-1.
- `Content/ColumnFormatter.cs` — T4-2.
- `Widgets/Rule.cs` — T4-3.
- `Input/SubmitKeyResolver.cs` (+ a `SubmitDecision` enum file) — T4-4.
- `Input/KeyLabel.cs` (audit/edit) — T4-5.

Example (`src/TUIKit.Example`): `ComponentGallery.cs` (new), `Program.cs` (edit), `GuidedTour.cs` /
`TourPage.cs` (edit), plus small per-component demo helpers as needed.

## Appendix B — New/extended test suites

Register each in `src/Test.Shared/TUIKitSuites.cs`:

- `RegionBackgroundSuite` (or extend `LayoutSuite`/`LayoutConstraintSuite`) — R1–R3.
- `DialogModalSuite` — T1-1. Keep `ModalSuite`/`ModalLinkLifecycleSuite`/`BackendModalValidationSuite`
  green through the migration.
- `MultiSelectSuite` — T1-2.
- `ScrollFocusSuite` (or extend `FocusFormsPromptsSuite`) — T1-3.
- Updates to `WidgetSuite`, `WidgetGuardValidationSuite`, `NewWidgetsSuite`, `ReactiveAnimationSuite`,
  `LayoutConstraintSuite`, `BackendModalValidationSuite` — T1-4 generic migration.
- `ActionListSuite` — T2-1; `ReorderableListSuite` — T2-2; `DynamicFormSuite` — T2-3;
  `DefinitionListSuite` — T2-4; `CommandRegistrySuite` — T2-5.
- `ActivityIndicatorSuite` — T3-1; `StreamingTranscriptSuite` — T3-2; `AutocompleteSuite` — T3-3.
- `HintTextSuite`, `ColumnFormatterSuite`, `RuleSuite`, `SubmitKeyResolverSuite` — T4-1…T4-4;
  `KeyBindingSuite` extension — T4-5.

Each suite must include negative cases (null/empty/out-of-range → specific documented exception, via
`Check.Throws<T>`) alongside its positive cases.
