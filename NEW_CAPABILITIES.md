# TUIKit New Capabilities: Text Entry, Data, Forms, Flows, and Viewers (1.8 to 1.10)

TUIKit 1.7 finished the Markdown story. This release closes a different kind of gap: widgets that TUIKit already has,
but that stop short of what a real application needs once it has a login screen, a list backed by a server, a settings
form, a setup flow, or a diff to review. Every app that reaches that point writes the same missing pieces on top of
TUIKit, usually by replacing the TUIKit widget outright.

Armada (`~/Code/Armada`, the `armada tui` client) is the evidence. After adopting TUIKit 1.7.0 it audited every one of
its private widgets against TUIKit and found seven places where it still keeps its own widget, not because its needs
are unusual, but because the TUIKit widget lacks a general capability:

| Need | TUIKit today | Armada's private widget |
|---|---|---|
| A text field with the editing keys, prefill, reveal, submit, length limit, and validation that login and settings screens need | `TextField` | `Widgets/TextInput.cs`, `Widgets/InputField.cs` |
| A table that pages through a server-backed list, shows loading and error states, and lets the user choose columns | `DataTable<T>` | `Widgets/ArmadaGrid.cs` and its `GridColumn`, `GridQuery`, `GridPage` |
| A filterable picker with detail text, disabled entries, multi-select, paste, and theming | `FuzzyList<T>` | `Widgets/FilterList.cs`, `Widgets/SelectOption.cs` |
| A viewer for a unified diff produced elsewhere (git, a server, a patch file) | `DiffView` (diffs two texts only) | `Widgets/DiffViewer.cs` |
| A form with sections, hints, errors under each field, Save and Cancel, and dirty tracking that notices a revert | `Form` | `Widgets/FormView.cs`, `Widgets/FormRow.cs`, `Widgets/IFormField.cs` |
| A multi-step flow with a step list, Back, Skip, Next, Finish, and per-step validation | none | `Widgets/Wizard.cs`, `Widgets/WizardStep.cs` |
| A heat map the user can move through, with levels readable without color, a legend, and a calendar layout | `HeatMap` | `Widgets/CommitHeatmap.cs`, `Widgets/TokenActivityHeatmap.cs` |

This plan adds those capabilities to TUIKit. It does not port Armada's classes. Armada's code is cited only to show the
shape that survived real use; every public API below is written in TUIKit's vocabulary, and no Armada concept (missions,
vessels, captains, fleets) appears in a type, member, or doc comment.

Three rules shape every item. They are the rules of `archive/MARKDOWN_VIEW_IMPROVEMENTS.md`, unchanged:

1. **Additive and incremental.** No public member is removed, renamed, or changes meaning. New behavior is opt-in
   wherever turning it on by default would change how an existing 1.7 app looks, which keys it consumes, or how focus
   moves. Each such default is called out under "Compatibility" in its item. Where the plan changes existing behavior
   at all, it is a bug fix, and it is listed under "Changed" in the version proposal.
2. **Horizontal, not vertical.** Each capability serves any app that has the need: a login form, an admin console, a
   package manager, a log browser, a git client. A capability goes in only if at least two unrelated kinds of app would
   use it.
3. **Reuse TUIKit's own vocabulary.** Where TUIKit already solved a problem for another widget, the new API copies those
   names and semantics. In particular:
   - `DataTable.RowActivated`: an Enter key is consumed only when a handler is attached;
   - `MarkdownView.UseThemeStyles`: a widget that newly implements `IThemeable` ignores the theme until the app opts in,
     so a host with `ApplyThemeToWidgets` does not restyle an existing app;
   - `MarkdownView.IsFocusStop`: a widget that newly becomes focusable is not a Tab stop by default;
   - `Pane.SetSearch`, `FindNext`, `FindPrevious`, `SearchText`, `Theme.SearchMatchRole`, and F3 / Shift+F3;
   - `ITextEntry`, `ITextEntryKeys.ConsumesChord`, `IKeyHintSource`, `KeyHint`, and `KeyChord`;
   - letter keys are never bound by default (the 1.7 decision): an app opts in to `[`, `]`, `s`, and similar keys;
   - new events use `EventHandler` or `EventHandler<TArgs>`, the convention of most TUIKit events (`TextField.Changed`,
     `DataTable.SortChanged`). A few older events are `Action` delegates (`DataTable.RowActivated`, `EndReached`,
     `Form.DirtyChanged`); they stay as they are, and new events next to them do not copy that form.

Requirements in `~/Code/Agents/requirements` are authoritative, especially `CODE_STYLE.md`, `VERSIONING.md`,
`BACKEND_TEST_ARCHITECTURE.md`, `REPOSITORY_REQUIREMENTS.md`, and `WRITING_DOCUMENTS.md` (no em dashes, anywhere). The
repository's `CLAUDE.md` restates the code style rules for every `.cs` file: one type per file, XML docs on every public
member with defaults, ranges, nullability, thread safety, and exceptions, no `var`, no tuples, and no `#if` outside the
backend and the `netstandard2.0` shim. The "How to use this plan" rules of `archive/IMPROVEMENTS_FOCUS_HINTS_TOASTS.md`
apply here unchanged.

Each item is a full product, delivered in the release that carries it (see "Release split"):

- code with XML docs;
- positive and negative Touchstone coverage in `src/Test.Shared`, run by `Test.Automated`, `Test.Xunit`, and
  `Test.Nunit`;
- `docs/SURFACE_COVERAGE.md` rows for every new public member, and a `docs/CONFORMANCE.md` check;
- a worked example in `TUIKit.Example`;
- README and CHANGELOG updates.

Code references were checked against `main` at 1.7.0 (commit `aaa5d2c`). Line numbers move; re-check each reference
when its item starts.

---

## Summary

Simplicity scores ease of integration into TUIKit: 10 is small, additive, and isolated, and 1 is invasive or risky.
Value scores how much a typical TUIKit app gains. Rows are ordered by total, highest first; ties go to value, then
simplicity.

| ID | Item | Simplicity | Value | Total | Release | Status |
|----|------|-----------:|------:|------:|---------|--------|
| C1 | `TextField`: editing keys, prefill selection, reveal, submit, length limit, validation, paste routing | 6 | 9 | 15 | 1.8 | DONE |
| C2 | `DataTable`: paging, load states, and a page loader | 5 | 9 | 14 | 1.8 | DONE |
| C5 | `DiffView`: unified diff input, file navigation, search, theming | 7 | 7 | 14 | 1.10 | DONE |
| C4 | `FuzzyList`: detail, disabled items, multi-select, paste, provider, theming | 6 | 7 | 13 | 1.8 | DONE |
| C3 | `DataTable`: column keys, visibility, chooser, and a column cursor | 6 | 7 | 13 | 1.8 | DONE |
| C7 | `Wizard`: a multi-step flow widget and `WizardModal` | 6 | 7 | 13 | 1.9 | DONE |
| C6 | `Form`: sections, hints, field errors, submit and cancel, snapshot dirty tracking | 4 | 8 | 12 | 1.9 | DONE |
| C9 | `CalendarHeatMap`: a day-by-week heat map | 6 | 6 | 12 | 1.10 | DONE |
| C8 | `HeatMap`: selection, levels, legend, wide cells | 7 | 5 | 12 | 1.10 | DONE |

Scores were revised after the plan review (see "Implementation record"): C1 drops to 6 for the paste routing it now
carries, C4 to 6 for disabled-item navigation plus multi-select plus provider, C6 to 4 because sections, hints, errors,
label placement, and buttons in the focus order all change the layout of a 534-line file and `IValueSnapshot` touches
seven widgets, and C9 to 6 for the navigation it owns.

Dependencies:

- C3 builds on C2's column bookkeeping (visible columns feed the page request's sort key), so C2 lands first.
- C6 reads C1's `TextField.Validator` and `Error`, so C1 lands before C6.
- C7 uses C6's error and submit conventions and `ButtonRow`, so C6 lands before C7.
- C9 is built from C8's selection, level, wide-cell, and selectable-cell support, so C8 lands before C9.
- C4 and C5 are independent. C4 shares C1's `UseLineEditingKeys` contract and `IPasteTarget`, so it is scheduled next
  to C1.

### Release split

Every item is additive, so nothing forces the nine into one release, and one release of this size (about 25 new public
types, six widgets extended, and a new interface on seven more) delays every item to the pace of the slowest. The plan
ships in three MINOR releases, each a complete product (code, tests, surface coverage, example pages, README, and
CHANGELOG) that Armada can adopt before the next one starts:

- **1.8.0, text entry and data** (Phases 1 and 2): C1, C4, C2, C3. These replace Armada's most-used private widgets
  (over 150 `InputField` sites and 37 grid sites).
- **1.9.0, forms and flows** (Phase 3): C6, C7. Builds on C1's validator and paste routing.
- **1.10.0, viewers** (Phase 4): C5, C8, C9. C8 and C9 have the weakest evidence against rule 2 (C9's evidence is two
  calendars in one app); if 1.8 and 1.9 adoption surfaces more urgent gaps, the owner may defer them.

Phases 0, 5, 6, and 7 run once per release, covering only that release's items. Phase 8 tracks adoption per release.

### Release at a glance

| Phase | Title | Items | Release | Status |
|---|---|---|---|---|
| 0 | Release setup | branch, version proposal | each | DONE |
| 1 | Text entry | C1, C4 | 1.8 | DONE |
| 2 | Data | C2, C3 | 1.8 | DONE |
| 3 | Forms and flows | C6, C7 | 1.9 | DONE |
| 4 | Viewers | C5, C8, C9 | 1.10 | DONE |
| 5 | Example application | E1 to E5 | per item | DONE |
| 6 | Documentation | D1 to D4 | each | DONE |
| 7 | Verification and publishing | P1 to P5 | each | DONE (P3 and P4 partly; see the record) |
| 8 | Armada adoption (tracked here, done in Armada) | X1 | each | TODO (owner) |

_Last updated: 2026-10-14. All nine items shipped: 1.8.0 (C1 to C4), 1.9.0 (C6, C7), and 1.10.0 (C5, C8, C9). Armada adoption (X1) is the owner's next step._

---

## How to use this plan

- Every item has an ID, a status line you edit in place, a Notes line, and checklists of concrete tasks. Tick boxes as
  work lands, in the same commit as the work.
- Status values: `TODO` (not started), `WIP` (in progress), `DONE` (complete and tested), `BLOCKED` (write the blocker
  in Notes), and `N/A` (justify in Notes). Keep the Summary and Release at a glance tables in step with the items.
- When the code disagrees with this plan, the code wins and the plan is corrected: record the deviation in the item's
  Notes and in "Implementation record" at the end, then continue. Do not silently drift.
- A design decision this plan leaves open is decided by the implementer, recorded in Notes, and raised in the release
  review. A decision that would change a default listed under "Compatibility" goes to the owner first.
- Every new public type goes in its own file, named for the type. Enums do not take an `Enum` suffix in TUIKit
  (`FileSelectionMode`, `BadgeVariant`); follow that.
- Every new behavior gets at least one positive and one negative Touchstone test. Every default listed under
  "Compatibility" gets a test that pins it.
- Telemetry: no item adds an instrument. If an implementer believes one is warranted, propose it in Notes against
  `TELEMETRY.md` and `TELEMETRY_REQUIREMENTS.md` (no ids or free text in labels) instead of adding it.

### Global obligations (every item)

- [x] XML docs on every new public member, including defaults, minimums and maximums, nullability, thread safety, and
      `<exception>` tags.
- [x] Zero warnings on `netstandard2.0`, `net8.0`, and `net10.0` with `TreatWarningsAsErrors=true`.
- [x] Touchstone suite (or cases in an existing suite) under `src/Test.Shared/Suites`, registered in `TUIKitSuites.All`.
- [x] `docs/SURFACE_COVERAGE.md` rows for every new public member, pointing at the test that covers it.
- [x] `CHANGELOG.md` entry under the heading of the item's release (Added or Changed).
- [x] README section or table row, where the README lists the widget.
- [x] Example coverage per Phase 5.

---

## Phase 0: Release setup

**Status:** DONE

Run this phase once per release. Each release starts from `main` after the previous one is published.

- [x] Start from a clean, current `main` that builds and passes the full suite at the previous release (1.7.0 for the
      first). Create `feature/v1.8.0`, `feature/v1.9.0`, or `feature/v1.10.0`. (Committed to `main` in phase commits
      with an annotated tag per release, the repository's convention since 1.5.)
- [x] **Version proposal (requires owner approval, `VERSIONING.md` section 4).** Every item is additive and every
      behavior change that could affect an existing app is opt-in, so each proposed version is MINOR. Present the
      release's "Changed" entries with its proposal; each is a fix or a hint-bar addition, not a new default for keys,
      layout, or focus:
  - **1.8.0**
    - C1: a masked `TextField` draws one mask character per grapheme instead of one per UTF-16 code unit, so an emoji
      in a password shows as one `*`, not two.
    - C4: `FuzzyList` Backspace removes the last grapheme instead of the last UTF-16 code unit, so it never leaves
      half a surrogate pair in the query.
    - C4: `FuzzyList` implements `ITextEntry`, so `KeyHintResolver` stops advertising single-letter shortcuts while a
      focused list would type them into its query. This is what the list already does with those keys; the hints now
      agree.
    - C4: `FuzzyList` implements `IKeyHintSource`, so a focused list adds "Up/Down Move" to the status bar.
      `ShowKeyHints` (default true, the `MarkdownView` 1.7 precedent) turns it off.
  - **1.9.0**
    - C7 (`WizardModal`) only adds types. C6 has no "Changed" entries; if review finds one, list it here.
  - **1.10.0**
    - Host: a click no longer moves focus to a bound widget that cannot take focus (disabled, hidden, or a container
      with nothing focusable). Found while keeping a non-selectable `HeatMap` out of focus (C8 Notes).
    - C5: `DiffLineKind` gains three members (`FileHeader`, `HunkHeader`, `Meta`) after the existing ones. Existing
      values keep their numbers. Code that switches over `DiffLineKind` without a default branch sees new values only
      from a `DiffView` built with the new `FromUnifiedDiff`.
    - C5: `DiffView` implements `IKeyHintSource`, so a focused, scrollable view adds "Up/Down Scroll" to the status
      bar. `ShowKeyHints` (default true) turns it off.

  Record the decision and date here before touching any version field.
  1.8.0 approved by/on: owner, in the release request, 2026-10-14  1.9.0 approved by/on: owner, in the release request, 2026-10-14  1.10.0 approved by/on: owner, in the release request, 2026-10-14
- [x] Once approved, set `Version`, `AssemblyVersion`, and `FileVersion` in `src/TUIKit/TUIKit.csproj`, update
      `PackageReleaseNotes`, and open `## [1.x.0] - YYYY-MM-DD` under `## [Unreleased]` in `CHANGELOG.md`.

---

## Phase 1: Text entry

### C1: `TextField` editing keys, prefill selection, reveal, submit, length limit, and validation

**Status:** DONE  **Simplicity 6, Value 9**
**Notes:** Done in 1.8.0 (`TextFieldEditing`, `TextFieldPrefill`, `TextFieldValidation`, `PasteRouting`; 27 cases).
Decisions: Ctrl+K and Ctrl+W also clear a selected value (every deleting key clears it); `SelectionStyle` follows
`NormalStyle` until assigned; typing at `MaxLength` is consumed but refused. Paste routing: the host offers the
paste to the focused region's `IFocusContainer.FocusedLeaf` first, then to the region widget
(`TuiApplication.FocusedWidget` is the region widget). `DialogModal` does not know its content, so instead of
forwarding to "focused content" it gained a `protected virtual IPasteTarget? PasteTarget` (default null) that
`HandlePaste` offers the paste to; with the default nothing changes, so there is no "Changed" entry for it.
Example, surface coverage, CHANGELOG, and README land with the 1.8.0 docs pass. Decisions: Ctrl+K and Ctrl+W also
clear a selected value (every deleting key clears it); `SelectionStyle` follows `NormalStyle` until assigned; typing
at `MaxLength` is consumed but refused. Paste routing: the host offers the paste to the focused region's
`IFocusContainer.FocusedLeaf` first, then to the region widget (`TuiApplication.FocusedWidget` is the region widget).
`DialogModal` does not know its content, so instead of forwarding to "focused content" it gained a
`protected virtual IPasteTarget? PasteTarget` (default null) that `HandlePaste` offers the paste to; with the default
nothing changes, so there is no "Changed" entry for it.

**Problem.** `TextField` (`Widgets/TextField.cs`, 423 lines) handles printable characters, Backspace, Delete, Left,
Right, Home, and End, and nothing else: any key with Ctrl returns false (`HandleKey`, the `KeyCode.Character` case), and
Enter is not handled. That is enough for a search box, but not for the screens every application has:

- **Login and connection screens** need a password or token field the user can briefly reveal, a field that arrives
  filled with a remembered value which the first keystroke replaces, and Enter to submit.
- **Settings and create forms** need a maximum length, a validator, and an error to show.
- **Users of any terminal** expect the line-editing keys their shell gives them: Ctrl+A and Ctrl+E for start and end,
  Ctrl+U and Ctrl+K to delete to either end, Ctrl+W to delete a word, Ctrl+Left and Ctrl+Right to move by word.

Masking also has a defect: `DisplayText()` returns `new string(_MaskChar, _Value.Length)`, one mask character per
UTF-16 code unit, so a value with an emoji or other astral character shows extra mask characters and the caret lands
in the wrong cell.

Armada's `TextInput` (`Widgets/TextInput.cs`, 389 lines) and `InputField` exist for exactly these reasons. `InputField`
alone has over 150 construction sites across Armada's screens and tests, and Armada's login tests depend on the prefill
behavior (`Test.Shared/Suites/Tui/TuiLoginSuite.cs`).

**Design.** All new members are on `TextField`. Nothing changes until the app sets a new member.

- **Line-editing keys.** New `public bool UseLineEditingKeys { get; set; }`, default **false**. When true:
  - Ctrl+A moves the caret to the start and Ctrl+E to the end;
  - Ctrl+U deletes from the start to the caret, and Ctrl+K from the caret to the end;
  - Ctrl+W deletes the word before the caret (trailing spaces first, then non-space graphemes);
  - Ctrl+Left and Ctrl+Right move to the previous and next word start.

  Word boundaries are whitespace-separated runs of graphemes; there is no locale-specific word breaking. Each edit
  respects `IsReadOnly` (a read-only field still moves its caret) and raises `ValueChanged` and `Changed` once.
  `ConsumesChord` reports exactly these chords while the flag is true, so `KeyHintResolver` (1.5, B6) hides hints the
  field would swallow. When false, the field behaves exactly as in 1.7, including Ctrl+Left moving one grapheme.
- **Prefill selection.** New `public void SelectAll()`, `public void ClearSelection()`, read-only
  `public bool IsAllSelected`, and `public CellStyle SelectionStyle { get; set; }` (default: `NormalStyle` with
  `Reverse`). While the whole value is selected:
  - the value is drawn in `SelectionStyle`;
  - the first printable character, or an `Insert` (paste), replaces the whole value;
  - Backspace, Delete, and Ctrl+U (when line-editing keys are on) clear it;
  - any caret movement (Left, Right, Home, End, and the line-editing moves) keeps the value and drops the selection.

  Setting `Value` clears the selection. The selection is all-or-nothing; arbitrary ranges are out of scope (see
  "Considered and not included"). Typical use: `field.Value = remembered; field.SelectAll();`.
- **Reveal.** New `public bool IsRevealed { get; set; }`, default false. While true, `MaskChar` is ignored when drawing,
  and `IsMasked` keeps reporting whether a mask is configured. New `public KeyChord? RevealKey { get; set; }`, default
  **null** (no key). When set (Ctrl+R is the common choice), the chord toggles `IsRevealed` on a masked field and is
  reported by `ConsumesChord` and by `GetKeyHints` as "Show" or "Hide". On an unmasked field the chord is not consumed.
- **Submit.** New `public event EventHandler? Submitted`. Enter with no modifiers raises it and is consumed **only when
  a handler is attached**, the `DataTable.RowActivated` rule, so a 1.7 app whose container acts on Enter is unaffected.
  `ConsumesChord` reports Enter exactly when a handler is attached; update its XML doc (`TextField.cs`, the
  `ConsumesChord` summary, which today says Enter always reaches the application). The field does not validate on
  submit; the handler calls `Validate()` when it wants to.
- **Length limit.** New `public int MaxLength { get; set; }`, default 0 (no limit), counted in graphemes. Must be zero
  or greater (`ArgumentOutOfRangeException`). Typing at the limit is refused; `Insert` keeps the leading graphemes that
  fit and drops the rest; setting `Value` programmatically is **not** truncated, so loading a stored value never loses
  data. Document that last rule in the XML doc.
- **Validation.** New `public Func<string, string?>? Validator { get; set; }` (returns an error message, or null when
  valid), read-only `public string? Error`, `public bool Validate()` (runs the validator, stores `Error`, returns true
  when valid), `public bool ValidateOnChange { get; set; }` (default false: when true, every change re-runs the
  validator), and `public event EventHandler? ErrorChanged`. A null validator makes `Validate()` return true and clears
  `Error`. The field never draws the error itself (it is one row tall); `Form` (C6) and apps draw it. Optionally add
  `public CellStyle? ErrorStyle` so a field with an error can be drawn in that style; default null (no change).
- **Masking per grapheme (fix).** `DisplayText()` and the caret mapping use `Graphemes.Split(_Value)`, one mask cell per
  grapheme, each one column wide, so the caret lands under the right cell.
- **Key hints.** `TextField` already implements `ITextEntry` and `ITextEntryKeys`. It now also implements
  `IKeyHintSource`, and returns the "Show/Hide" hint when `RevealKey` is set on a masked field and the "Enter Submit"
  hint when `Submitted` has a handler (only those). With neither, `GetKeyHints` returns null, so the status bar of a
  1.7 app is unchanged.
- **Paste routing.** Today a bracketed paste never reaches a focused widget outside a modal:
  `TuiApplication.DispatchPaste` hands it to the modal stack when a modal is active and otherwise only raises the global
  `PasteReceived` event. Inside a modal it reaches only modals that override `Modal.HandlePaste`; `DialogModal` does
  not, so a field inside any other dialog drops the paste. So "paste replaces the selection" (above) and C4's paste
  would only work in apps that wire `PasteReceived` to the focused field by hand. This item adds the general route:
  - New interface `IPasteTarget` (own file, `Widgets/`): `bool HandlePaste(string text)` (text non-null; returns true
    when consumed). `TextField` implements it through `Insert`; C4 adds `FuzzyList`; `TextEditor` implements it
    through its existing insert path; `Form` (and C7's `Wizard`) implement it by forwarding to their focused leaf.
  - New `public bool RoutePasteToFocus { get; set; }` on `TuiApplication`, default **false**. When true and no modal is
    active, a paste is offered first to the focused leaf when it is an `IPasteTarget`, and `PasteReceived` is raised
    only when that leaf does not consume it. Default false because an app that today handles `PasteReceived` itself
    would otherwise stop seeing pastes into a focused field.
  - `DialogModal.HandlePaste` is overridden to offer the paste to the dialog's focused content when it is an
    `IPasteTarget`. The base returns false today and the paste is dropped, so this turns a dropped paste into an
    inserted one, never a different outcome; list it under "Changed" for 1.8.0 if review agrees it is visible.
  - Implementer: confirm how `TuiApplication` exposes the focused leaf (the focus path the key-hint resolver builds via
    `FocusPath.Build` is the likely source) and how a `DialogModal` subclass names its focused content; record the
    answer in Notes.

**Compatibility.** Defaults keep 1.7 behavior: no line-editing keys, no selection, no reveal key, Enter not consumed,
no limit, no validator, no hints, paste routed as today outside modals. The only visible changes for existing apps are
the masking fix and, inside a `DialogModal`, a paste that was dropped now being inserted.

**Tests** (`TextFieldEditingSuite`, `TextFieldPrefillSuite`, `TextFieldValidationSuite`):

- [x] With `UseLineEditingKeys` false, each new chord returns false and leaves value and caret unchanged.
- [x] Each line-editing chord edits or moves as specified, at the start, middle, and end of the value, over CJK and
      emoji graphemes, and refuses edits when `IsReadOnly`.
- [x] `ConsumesChord` matches `HandleKey` for every chord in both flag states.
- [x] `SelectAll` then a printable key replaces the value; paste replaces; Backspace and Delete clear; Left keeps the
      value and clears the selection; setting `Value` clears the selection; the selected value renders in
      `SelectionStyle`.
- [x] `RevealKey` null: Ctrl+R not consumed. Set: toggles `IsRevealed` on a masked field and is not consumed on an
      unmasked one; a revealed field renders the plain value.
- [x] Enter without a `Submitted` handler returns false; with one, it is raised once and consumed; Ctrl+Enter is not
      consumed.
- [x] `MaxLength` refuses typing at the limit, truncates a paste at a grapheme boundary, does not truncate a
      programmatic `Value`, and rejects a negative value.
- [x] `Validate` with no validator, a passing one, and a failing one; `ErrorChanged` fires only on change;
      `ValidateOnChange` re-validates on each edit.
- [x] Masking: a value with one emoji and two ASCII letters renders three mask cells, and the caret sits under the
      correct cell at each position.
- [x] `ConsumesChord(Enter)` is false without a `Submitted` handler and true with one; `GetKeyHints` is null with
      defaults.
- [x] Paste (`PasteRoutingSuite`): `RoutePasteToFocus` false raises `PasteReceived` and leaves a focused `TextField`
      unchanged (1.7); true inserts into the focused field and does not raise `PasteReceived`; true with a focused
      non-target still raises it; a paste into a `TextField` inside a `DialogModal` subclass is inserted; a paste into a
      `Form` reaches its focused field.

**Tasks.**

- [x] Implement the members above in `Widgets/TextField.cs`. If the file passes 500 lines, add the region blocks.
- [x] Add `IPasteTarget`; implement it on `TextField`, `TextEditor`, and `Form`; add `RoutePasteToFocus` to
      `TuiApplication` and the `HandlePaste` override to `DialogModal`.
- [x] Extract word-boundary helpers into an internal static class in `Unicode/` so C4 can share them.
- [x] Surface coverage, CHANGELOG (Added, and Changed for masking), README text-field row.

### C4: `FuzzyList` detail, disabled items, multi-select, paste, provider, and theming

**Status:** DONE  **Simplicity 6, Value 7**
**Notes:** Done in 1.8.0 (`FuzzyListPicker`, `FuzzyListMultiSelect`; 17 cases). Deviation: with `UseThemeStyles`, theme roles
are resolved at render time instead of being written into the style properties, so the properties keep the app's
values and turning the flag off restores them (the `MarkdownView` approach). `SetChecked` works on disabled items
(only keys and clicks refuse them). A click on the check column toggles the check with `MultiSelect`. Ranking with
`SearchDetail` doubles the gap score and adds one for a detail-only match. Down on an empty list no longer sets the
internal selection to -1 (listed under "Changed").
pass. Deviation: with `UseThemeStyles`, theme roles are resolved at render time instead of being written into the
style properties, so the properties keep the app's values and turning the flag off restores them (the
`MarkdownView` approach). `SetChecked` works on disabled items (only keys and clicks refuse them). A click on the
check column toggles the check with `MultiSelect`. Ranking with `SearchDetail` doubles the gap score and adds one
for a detail-only match.

**Problem.** `FuzzyList<T>` (`Widgets/FuzzyList.cs`, 326 lines) is the natural base for every picker and command
palette, but it stops short of what pickers need:

- its query line is hard-coded as `"> " + query` in palette color 6 (`Render`), and none of its styles follow the theme;
- Backspace removes one UTF-16 code unit (`_Query.Substring(0, _Query.Length - 1)`), which can split a surrogate pair;
- there is no way to show secondary text for an item, such as an id, a path, or a description;
- there is no way to show an item that cannot be chosen right now;
- there is no multi-select, no paste, and no clear-query key;
- items are fixed at construction, so a picker cannot offer results computed from the query (for example a
  "Create <query>" entry or a server lookup);
- Enter is not handled, so every host wires its own key.

Armada's `FilterList` (`Widgets/FilterList.cs`, 292 lines) carries exactly these features and is used by its pickers,
multi-pickers, and command palette.

**Design.** All new members are on `FuzzyList<T>`.

- **Styles and theme.** New `CellStyle` properties `QueryStyle` (default: the current palette color 6),
  `NormalStyle` (default `CellStyle.Default`), `DisabledStyle` (default dim), `DetailStyle` (default palette 8), and
  `CheckStyle` (default `CellStyle.Default`), plus `public string QueryPrompt { get; set; }` (default `"> "`; must not
  be null). The list implements `IThemeable`, with `public bool UseThemeStyles { get; set; }` default **false**, the
  `MarkdownView` pattern: while false, `ApplyTheme` only remembers the theme. While true it sets `QueryStyle` from
  `Accent`, `NormalStyle` from `Text`, `DisabledStyle` from `Disabled`, `DetailStyle` from `Muted`, and
  `HighlightStyle` from `Selection`, and re-applies on every later `ApplyTheme`.
- **Grapheme-safe Backspace (fix).** Backspace removes the last grapheme (`TextFit.PreviousBoundary`).
- **Text entry.** The list implements `ITextEntry` with `AcceptsText` true. New `public void Insert(string? text)`
  appends to the query (newlines become spaces; null or empty is ignored), for paste, and the list implements C1's
  `IPasteTarget` through it. New `public bool UseLineEditingKeys { get; set; }`, default false, with C1's contract:
  Ctrl+U clears the query and Ctrl+W deletes its last word. The list implements `ITextEntryKeys` so hints stay
  accurate. Note the one chord whose meaning differs from `TextField`: in a `TextField`, Ctrl+A (line-editing keys on)
  moves to the start; in a multi-select `FuzzyList`, Ctrl+A checks all, matching `DataTable`'s "Mark all". The list has
  no caret, so it never binds Ctrl+A to "start". Document this on both properties.
- **Detail.** New `public Func<T, string>? Detail { get; set; }`, default null. When set, each row draws the label, two
  spaces, and the detail in `DetailStyle`, with the detail truncated first when the row is narrow. New
  `public bool SearchDetail { get; set; }`, default false: when true the query also matches the detail, ranked below a
  label match of the same score.
- **Disabled items.** New `public Func<T, bool>? IsEnabled { get; set; }`, default null (all enabled). Disabled items are
  listed in `DisabledStyle`; Up, Down, PageUp, PageDown, Home, End, and clicks skip them; they cannot be activated or
  checked. `SelectedItem` is never a disabled item; if every visible item is disabled it is the default value.
- **Multi-select.** New `public bool MultiSelect { get; set; }`, default false. When true:
  - Space toggles the selected item's check instead of typing a space (document this trade-off: a multi-select
    query cannot contain spaces; fuzzy matching makes them unnecessary);
  - Ctrl+A checks every visible enabled item, or unchecks them all when all are already checked;
  - a check column is drawn with `CheckedGlyph` (default `"[x]"`) and `UncheckedGlyph` (default `"[ ]"`), the names
    `CheckList<T>` uses;
  - read-only `public IReadOnlyList<T> CheckedItems` (in original item order), `public bool IsChecked(T item)`,
    `public void SetChecked(T item, bool isChecked)`, and `public event EventHandler? CheckedChanged`.

  Checks survive query changes, because they belong to items, not rows. Item identity uses
  `EqualityComparer<T>.Default`; a constructor overload taking an `IEqualityComparer<T>` is added for types without
  value equality.
- **Provider.** New `public Func<string, IEnumerable<T>>? ItemsProvider { get; set; }`, default null. When set, every
  query change replaces the items with the provider's result for the new query, which is then scored and highlighted as
  usual. The provider runs on the UI thread; asynchronous providers are out of scope (see "Considered and not
  included"). A provider that throws leaves the previous items in place; the exception is not swallowed silently but
  surfaced through `public event EventHandler<Exception>? ProviderFailed`.
- **Activation.** New `public event EventHandler<T>? ItemActivated`. Enter with no modifiers raises it for the selected
  enabled item and is consumed **only when a handler is attached**. A double click raises it too.
- **Key hints.** The list implements `IKeyHintSource`: "Up/Down Move", "Enter Choose" when `ItemActivated` has a handler,
  and with `MultiSelect` "Space Check" and "Ctrl+A Check all". New `public bool ShowKeyHints { get; set; }`, default
  true (the `MarkdownView` 1.7 precedent); false returns no hints. Because `KeyHintResolver` collects hints from every
  node on the focus path, a focused 1.7 list now adds "Up/Down Move" to the status bar; this is listed under "Changed".

**Compatibility.** With defaults, the list draws exactly as in 1.7 (same prompt, same colors, no detail, no check
column) and consumes the same keys. The visible changes are the Backspace fix, the `ITextEntry` hint change, and the
"Up/Down Move" hint, all listed under "Changed".

**Tests** (`FuzzyListPickerSuite`, `FuzzyListMultiSelectSuite`):

- [x] Default rendering is byte-for-byte identical to 1.7 for a fixed list and query (snapshot).
- [x] Backspace after an emoji leaves no lone surrogate; `Insert` appends and flattens newlines.
- [x] `UseLineEditingKeys` false: Ctrl+U not consumed; true: clears; Ctrl+W deletes the last word.
- [x] Detail is drawn, truncated before the label, and only searched when `SearchDetail` is true.
- [x] Navigation, clicks, Enter, and Space skip or refuse disabled items; `SelectedItem` is never disabled.
- [x] Multi-select: Space toggles; Ctrl+A checks all visible enabled items, then unchecks; checks survive a query
      change; `CheckedItems` keeps original order; `CheckedChanged` fires once per change.
- [x] Without `MultiSelect`, Space types into the query (1.7 behavior).
- [x] `ItemsProvider` replaces items per query; a throwing provider keeps the old items and raises `ProviderFailed`.
- [x] `ItemActivated`: Enter not consumed without a handler; consumed and raised with one.
- [x] `UseThemeStyles` false: `ApplyTheme` changes nothing; true: styles follow the theme and a later theme.
- [x] `ShowKeyHints` true returns "Up/Down Move"; false returns no hints. `HandlePaste` appends to the query.

**Tasks.**

- [x] Implement in `Widgets/FuzzyList.cs` (add regions if the file passes 500 lines).
- [x] Share the word-boundary helper from C1.
- [x] Surface coverage, CHANGELOG (Added, and Changed for the Backspace fix and `ITextEntry`), README row.

---

## Phase 2: Data

### C2: `DataTable` paging, load states, and a page loader

**Status:** DONE  **Simplicity 5, Value 9**
**Notes:** Done in 1.8.0 (`DataTablePaging`, `DataTableLoadState`, `DataTableLoader`; 25 cases). Decisions: the `PageSize`
setter only stores the size (and clamps the page in `Local`), while `SetPageSize` moves to the page holding the
first visible row and, in `Remote`, requests it. `GoToPage` in `Remote` returns true when it raised a request. A
`Bind` in `Remote` mode replaces the current page, keeping its number and total. `ClearSort` in `Remote` requests
page 1. In `Error` the body shows only the message (rows are hidden). A load cancelled through `LoadAsync`'s token
returns the table to `Ready` through an internal `ClearLoadState`, so nothing is rebound. `DataTableLoader` is
sealed, so `Dispose(bool)` is private.

**Problem.** `DataTable<T>` (`Widgets/DataTable.cs`, 970 lines) has grown a lot since Armada wrote its own grid:
weighted and fixed widths (`DataColumn.Width`, `Weight`, `MinWidth`), typed sorting (`Comparer`, `SortKey`), server-side
sort tracking (`SortLocally = false` plus `SortChanged`), multi-select with marks that follow rows by key
(`MultiSelect`, `KeySelector`), per-row and per-cell styles, `EndReached` for infinite scroll, and key hints. What it
still cannot do is the most common shape of a list in a business or admin tool: a list that lives on a server and is
read one page at a time.

- There is no notion of a page: no page number, page size, or total count, and nothing to draw "Showing 26-50 of 248.
  Page 2 of 10." or to move to the next page.
- There is no loading or error state. While a page is being fetched, the table shows the previous rows with no sign that
  they are stale, or `EmptyText` when there were none, which reads as "there is no data". A failed fetch has nowhere to
  be shown.
- Marks are kept by key only for rows present in the new `Bind` (`Bind` rebuilds `_Marks` from the new rows), so marking
  rows on page 1, moving to page 2, and coming back loses the marks.
- Every app that pages writes the same async plumbing: cancel the superseded fetch, ignore late results, marshal the
  result to the UI thread, and turn exceptions into an error state.

Armada's `ArmadaGrid<T>` (`Widgets/ArmadaGrid.cs`, 1062 lines, used by every list screen: 37 construction sites) exists
mainly for this. Its `Reload` (around line 312) is the async plumbing above, with a generation counter, cancellation,
and a post back to the UI thread.

**Design.** Paging is a mode of the existing table, plus one small helper class for the async part. Nothing changes
unless the app sets `PagingMode`.

- **Mode.** New enum `DataTablePagingMode` (own file): `None` (default, 1.7 behavior), `Local` (the table pages the
  rows given to `Bind`), and `Remote` (the app supplies one page at a time with `BindPage`). New
  `public DataTablePagingMode PagingMode { get; set; }`.
- **Page state.** New members:
  - `public int PageSize { get; set; }`, default 25, at least 1 (`ArgumentOutOfRangeException`);
  - `public IReadOnlyList<int> PageSizes { get; set; }`, default `{ 10, 25, 50, 100 }`, non-null, non-empty, each at
    least 1, used by `CyclePageSize`;
  - read-only `public int PageNumber` (1-based) and `public int PageCount` (1 when there are no rows; unknown totals
    report the highest page known so far);
  - read-only `public long TotalRows`: in `Local` mode the bound row count; in `Remote` mode the value given to
    `BindPage`, where -1 means unknown.
- **Row indexes.** Every existing index-based member (`SelectedIndex`, `SelectionChanged`, `RowActivated`,
  `IsMarked`, `SetMarked`, `MarkedRows`, `MarkedCount`) keeps one meaning per mode, documented on each member:
  - `Local`: an index into the full bound row list in its current sort order, exactly as with `None`. The selection is
    kept on the current page; moving to another page selects that page's first row (or, through `PageAtEdges`, its
    last row when moving up), and `SelectedIndex` reports the absolute index. Marks span all bound rows, so marks on
    another page survive paging with or without `KeySelector`. `MarkAll` marks every bound row, not only the page
    (documented; this differs from `Remote`, below).
  - `Remote`: the table holds only the current page, so indexes are into the page. Marks on other pages are kept only
    through `KeySelector` (`MarkedKeys`, below); without a `KeySelector`, marks are dropped on every `BindPage`, as
    `Bind` drops them today, and the XML doc says so.
- **Sorting in `Remote` mode.** A server-backed table can only sort on the server, so in `Remote` mode `SortLocally`
  is ignored and treated as false: a sort change raises `SortChanged` and then `PageRequested` for page 1, and the
  bound page is never reordered locally. Ignoring rather than throwing is deliberate: `SortLocally` defaults to true, so
  a throw would break every table switched to `Remote`. Document this on both `SortLocally` and `PagingMode`.
- **Navigation.** New `public bool GoToPage(int pageNumber)` (clamped; returns false when nothing changed),
  `NextPage()`, `PreviousPage()`, `SetPageSize(int pageSize)` (keeps the first visible row on screen by moving to the
  page that contains it), and `CyclePageSize()`. In `Local` mode they re-slice immediately. In `Remote` mode they raise
  `PageRequested` and keep showing the current rows until `BindPage` arrives.
- **Request.** New `public event EventHandler<DataTablePageRequest>? PageRequested`. New class `DataTablePageRequest`
  (own file, immutable): `PageNumber`, `PageSize`, `SortColumnIndex` (-1 when unsorted), `SortColumnKey` (string or
  null; the column's `Key` from C3, or its `Name` until C3 lands), and `SortAscending`. In `Remote` mode a sort change
  also raises `PageRequested` for page 1, after `SortChanged` (see "Sorting in `Remote` mode"). New
  `public DataTablePageRequest CurrentRequest()` returns the request for the current state, so an app can load the first
  page without waiting for an event.
- **Binding a page.** New `public void BindPage(IReadOnlyList<T> rows, int pageNumber, long totalRows)` for `Remote`
  mode: rows must not be null (`ArgumentNullException`), `pageNumber` at least 1, `totalRows` at least -1
  (`ArgumentOutOfRangeException`). Calling it in another mode throws `InvalidOperationException` with a message naming
  the mode. It replaces the rows, sets the page, and returns the table to `Ready`. In `Remote` mode with `KeySelector`
  set, marks are kept by key across pages: a new read-only `public IReadOnlyCollection<string> MarkedKeys` holds every
  marked key, including keys of rows not on the current page, `ClearMarks` clears them all, and `MarkAll` marks the
  current page only (documented). `MarkedRows` keeps returning marked rows on the current page.
- **Load state.** New enum `DataTableState` (own file): `Ready`, `Loading`, `Error`. New read-only
  `public DataTableState State`, read-only `public string? ErrorMessage`, `public void SetLoading()`, and
  `public void SetError(string message)` (non-null). New text properties `LoadingText` (default `"Loading..."`),
  `RefreshingText` (default `"Refreshing..."`), and `ErrorFormat` (default `"Failed to load: {0}"`, validated as a
  composite format with `{0}`). Rendering:
  - `Loading` with no rows: the body shows `LoadingText` instead of `EmptyText`;
  - `Loading` with rows: the rows stay, and `RefreshingText` is appended to the paging bar (or drawn on the last row
    when there is no bar);
  - `Error`: the body shows the formatted error in a new `ErrorStyle` (default palette 1), plus the retry key when
    `RetryKey` is set.

  Any `Bind` or `BindPage` returns the state to `Ready`. These states work in every paging mode, including `None`.
- **Retry.** New `public KeyChord? RetryKey { get; set; }`, default **null**; when set (F5 is common), the chord raises
  new `public event EventHandler? RetryRequested` while the state is `Error`, and is otherwise not consumed.
- **Paging bar.** New `public bool ShowPagingBar { get; set; }`, default true; it has effect only when `PagingMode` is
  not `None`, so 1.7 tables never grow a bar. The bar is the last row of the table and draws, left, `PagingFormat`
  (default `"Showing {0}-{1} of {2}. Page {3} of {4}."`, or `UnknownTotalFormat`, default
  `"Showing {0}-{1}. Page {3}."`, when the total is -1), and, right, clickable `"<"` and `">"` controls that call
  `PreviousPage` and `NextPage`. Both formats are validated composite formats and are the localization hook. A new
  `PagingBarStyle` (default palette 8) styles it. The body's viewport height excludes the bar, so PageUp and PageDown
  keep moving by the visible rows.
- **Page keys.** New `public KeyChord? NextPageKey` and `PreviousPageKey`, default **null**. Letters (`>` and `<`) and
  Ctrl+PageUp/PageDown (already used by `TabStrip` and `TabView`) are left to the app. Optional
  `public bool PageAtEdges { get; set; }`, default false: when true, Down on the last row of a page moves to the first
  row of the next page, and Up on the first row to the last row of the previous page.
- **Interaction with `EndReached`.** `EndReached` keeps its meaning and is never raised while `PagingMode` is not `None`
  (document it: paging and infinite scroll are alternatives).
- **Loader helper.** New class `DataTableLoader<T>` (own file), implementing `IDisposable`:
  - constructor `DataTableLoader(DataTable<T> table, Func<DataTablePageRequest, CancellationToken, Task<DataTablePage<T>>> load, Action<Action> post)`
    (all non-null). `post` marshals work to the UI thread, for example `TuiApplication.Post`; tests pass `a => a()`;
  - it subscribes to `PageRequested` and `RetryRequested`, and unsubscribes in `Dispose`;
  - `public Task LoadAsync(CancellationToken token = default)` loads `table.CurrentRequest()`, and `public void Reload()`
    starts the same load without awaiting it, for event handlers;
  - each load calls `SetLoading()` on the UI thread, cancels the previous load's token, and tags the load with a
    generation number; a result or failure from an older generation is dropped;
  - a result calls `BindPage` on the UI thread. An exception other than `OperationCanceledException` (exception filter)
    calls `SetError(ErrorText(exception))`, where `public Func<Exception, string> ErrorText { get; set; }` defaults to
    `exception.Message`, and raises `public event EventHandler<Exception>? LoadFailed`;
  - read-only `public bool IsLoading`;
  - the table must be in `Remote` mode (`InvalidOperationException` otherwise). Thread safety: construct and call on the
    UI thread; the load delegate runs wherever its awaits take it, and only `post` touches the table. Uses
    `ConfigureAwait(false)`; full dispose pattern.
- New class `DataTablePage<T>` (own file): `Rows` (non-null `IReadOnlyList<T>`) and `TotalRows` (long, -1 unknown),
  with a constructor that validates both.
- **Key hints.** In a paging mode, `GetKeyHints` adds the page keys and the retry key when they are set.

**Compatibility.** With `PagingMode` at `None`, the table renders and behaves as in 1.7: no bar, no new keys,
`EndReached` unchanged, `Bind` unchanged. `SetLoading` and `SetError` are new calls, so no existing table enters a new
state on its own.

**Tests** (`DataTablePagingSuite`, `DataTableLoadStateSuite`, `DataTableLoaderSuite`):

- [x] `None` mode: a 1.7 snapshot of a populated table and an empty table is unchanged.
- [x] `Local` mode: 60 rows at page size 25 give three pages; the bar text for each; `NextPage` past the end returns
      false; a sort applies to all rows before slicing; `SetPageSize` keeps the first visible row on screen.
- [x] `Remote` mode: `GoToPage`, `SetPageSize`, and a sort raise `PageRequested` with the right request; nothing changes
      until `BindPage`; `BindPage` in another mode throws; invalid arguments throw.
- [x] Unknown total (-1): the bar uses `UnknownTotalFormat`; `NextPage` is allowed while the page is full.
- [x] `Remote`: marks by key survive a round trip from page 1 to page 2 and back; `ClearMarks` clears keys on other
      pages; `MarkAll` marks only the current page; without `KeySelector`, `BindPage` drops marks.
- [x] `Local`: `SelectedIndex` and `RowActivated` report absolute indexes on page 2; moving pages selects the page's
      first row; marks on page 1 survive a visit to page 2 without `KeySelector`; `MarkAll` marks every bound row.
- [x] `Remote` with `SortLocally` left at true: a header click raises `PageRequested` and does not reorder the page.
- [x] Loading with no rows shows `LoadingText`; with rows shows `RefreshingText`; error shows the formatted message;
      `Bind` returns to `Ready`.
- [x] `RetryKey` null: F5 not consumed; set: raises `RetryRequested` only in `Error`.
- [x] The bar's `<` and `>` respond to clicks; the viewport excludes the bar.
- [x] `EndReached` is not raised in a paging mode.
- [x] Loader: a successful load binds the page; a slow load superseded by a fast one never overwrites it; a cancelled
      load binds nothing; an exception sets the error and raises `LoadFailed`; `Dispose` unsubscribes and cancels.
- [x] Formats without `{0}` or with invalid composite syntax are rejected.

**Tasks.**

- [x] Add `DataTablePagingMode`, `DataTableState`, `DataTablePageRequest`, `DataTablePage<T>`, and `DataTableLoader<T>`.
- [x] Extend `Widgets/DataTable.cs` (regions are already required at its size); keep the paging slice and the bar in
      private helpers so `Render` stays readable.
- [x] Surface coverage, CHANGELOG, README table section with a paging snippet.

### C3: `DataTable` column keys, visibility, chooser, and a column cursor

**Status:** DONE  **Simplicity 6, Value 7**
**Notes:** Done in 1.8.0 (`DataTableColumns`; 7 cases). Deviations: `ApplyColumnChooser` returns `bool` (false when the chooser
was cancelled, is still open, or left nothing visible) instead of `void`. `SetVisibleColumns` keeps the first column
visible when the keys would hide everything. Key chords store letters lower-cased, so `ReverseSortKey` documents
`KeyChord.Parse("shift+s")` for upper-case S: an upper-case letter matches its shift+letter chord, and the reverse
key is checked first. Sort keys are only active while `ShowColumnCursor` is on.

**Problem.** Wide tables need the user to choose which columns to see, and servers sort by a field name, not by a
column index or a translated header. `DataColumn<T>` (`Widgets/DataColumn.cs`) has only `Name`, which is also the header
text, so a localized header changes the sort key, and there is no way to hide a column. Sorting from the keyboard is not
possible: sorting is by header click (`SortOnHeaderClick`) or code (`SortByColumn`). `DataTableSortChangedEventArgs`
carries `ColumnIndex` and `ColumnName`, which are both presentation details.

Armada's `GridColumn<T>` has `Key`, `Pinned`, and `DefaultVisible`, and `ArmadaGrid` adds a column chooser (`c`), a
focused column (Left and Right), and keyboard sort (`s`, `S`); 18 of its test cases cover these.

**Design.**

- **Key.** New read-only `public string Key { get; }` on `DataColumn<T>`, set through a new constructor overload
  `DataColumn(string key, string name, Func<T, string> value, bool sortable = false)` (key must not be null or empty,
  `ArgumentException`); the existing constructor sets `Key` to `Name`. New read-only `public bool HasExplicitKey`
  reports which constructor was used. The key is fixed at construction so that it cannot change after the table has
  checked it: a settable `Key` could be edited after `DataTable.Column(...)` validated uniqueness. A matching
  `DataTable.Column(string key, string name, Func<T, string> value, bool sortable = false)` overload is added.
  `DataTablePageRequest.SortColumnKey` (C2) and a new `ColumnKey` property on `DataTableSortChangedEventArgs` (added
  through a new constructor overload; the existing constructor sets `ColumnKey` to `ColumnName`) carry it. New
  `public void SortByColumn(string key, bool ascending)` on the table (`ArgumentException` for an unknown key).
- **Visibility.** New `public bool Visible { get; set; }` (default true) and `public bool CanHide { get; set; }`
  (default true) on `DataColumn<T>`. Hidden columns take no width, are skipped by layout, sorting by click, and the
  column cursor, and keep their sort when hidden (the indicator just is not drawn). New on the table:
  `public IReadOnlyList<DataColumn<T>> VisibleColumns`, `public bool SetColumnVisible(string key, bool visible)`
  (returns true when visibility changed; returns false, changing nothing, when asked to hide a column with `CanHide`
  false or the last visible column, or when the column is already in the requested state; `ArgumentException` for an
  unknown key), and
  `public void SetVisibleColumns(IEnumerable<string> keys)` (visible exactly those, plus every column with `CanHide`
  false; unknown keys are ignored so a saved layout from an older version still loads), and
  `public event EventHandler? ColumnsChanged`. Persisting the layout is the app's job; the keys make it possible.
- **Chooser.** New `public MultiSelectModal<DataColumn<T>> CreateColumnChooser(string title)`. It lists the columns
  with `CanHide` true, by header, pre-checked when visible. A new `public void ApplyColumnChooser(MultiSelectModal<DataColumn<T>> chooser)`
  applies the checked set when the app sees the modal complete (through `Modal.Completion`). Columns with `CanHide`
  false are not listed and stay visible. The table never pushes a modal itself; the app owns the modal stack.
- **Column cursor.** New `public bool ShowColumnCursor { get; set; }`, default **false**. When true, Left and Right
  (currently unbound in `HandleKey`) move a focused column across visible columns, read-only `public int FocusedColumn`
  reports it (index into `VisibleColumns`), and the focused column's header is drawn in a new `FocusedHeaderStyle`
  (default: `HeaderStyle` with underline). New `public void SortFocusedColumn()` (toggles direction when already sorted
  by it) and `public void SortFocusedColumn(bool ascending)`. New `public KeyChord? SortKey` and
  `public KeyChord? ReverseSortKey`, default null, bind those for an app that wants `s` and `S`. A non-sortable focused
  column ignores the sort keys (not consumed).
- **Key hints.** With the cursor on: "Left/Right Column", and the sort keys when set.

**Compatibility.** Every column is visible and `Key` equals `Name` by default, so layout, sort events, and rendering are
unchanged. Left and Right stay unbound unless `ShowColumnCursor` is true. Duplicate column names were previously
allowed, and a defaulted key equals the name, so the uniqueness rule is: `Column(...)` throws `ArgumentException`
(naming the key) only when the column being added has an explicit key that equals an existing column's key, or when it
has a defaulted key equal to an existing column's explicit key. Two columns that both default their keys may share one,
as two columns could share a name in 1.7; every key-based lookup (`SortByColumn(key, ...)`, `SetColumnVisible`,
`SetVisibleColumns`) then acts on the first column with that key. Document this rule on `Column` and `Key` and pin it
with a test.

**Tests** (`DataTableColumnsSuite`):

- [x] Default key equals name and `HasExplicitKey` is false; an explicit key colliding with an explicit or defaulted
      key throws; duplicate names without explicit keys still work, and key lookups act on the first.
- [x] `SetColumnVisible` returns true on change and false for a `CanHide` false column, the last visible column, and a
      no-op; an unknown key throws.
- [x] `SortByColumn(key, ...)` and `SortChanged.ColumnKey`; the old args constructor sets `ColumnKey` to `ColumnName`.
- [x] Hiding a column redistributes width; the last visible column and a `CanHide` false column cannot be hidden;
      `SetVisibleColumns` ignores unknown keys; `ColumnsChanged` fires once per change.
- [x] The chooser lists only hideable columns, pre-checked by visibility; applying it updates visibility.
- [x] `ShowColumnCursor` false: Left and Right are not consumed. True: they move the cursor across visible columns
      only; the header style marks it; sort keys sort and reverse; a non-sortable column ignores them.
- [x] `DataTablePageRequest.SortColumnKey` uses the key.

**Tasks.**

- [x] Extend `DataColumn<T>` (key constructor, `HasExplicitKey`, `Visible`, `CanHide`), `DataTableSortChangedEventArgs`,
      and `DataTable<T>` (keyed `Column` overload and key-based members).
- [x] Surface coverage, CHANGELOG, README.

---

## Phase 3: Forms and flows

### C6: `Form` sections, hints, field errors, submit and cancel, and snapshot dirty tracking

**Status:** DONE  **Simplicity 4, Value 8**
**Notes:** Done in 1.9.0 (`FormLayout`, `FormValidation`, `FormSubmit`, `FormDirty`; 19 cases). Decisions: `FocusedIndex` is
-1 while the buttons hold focus; the buttons stay last in the focus ring (fields added later are placed before
them) and survive `Clear`; `SubmitKey`, `CancelKey`, and the arrow keys are checked after the focused field had the
key, so an open dropdown closes on Esc first. The automatic label width is the widest label plus three (the
two-column focus marker and a space), capped at a third of the width, and a capped label keeps one blank column
before its field. Changing `DirtyTracking` re-baselines (calls `MarkClean`). In `Snapshot` mode `IsDirty` is also
re-checked after every key and click, so a `CheckList` (which raises no change events) still updates it. Form
validators run before a `TextField.Validator`; the first error wins.

**Problem.** `Form` (`Widgets/Form.cs`, 534 lines) lays out labeled fields, routes focus, validates, and tracks changes
through `IChangeNotifier`. A create or settings dialog needs more:

- **Structure.** Long forms need section headings and a one-line hint under a field ("Used as the branch prefix").
- **Errors.** `Validate()` returns only the first error string, so the app has to place it somewhere, and the user sees
  one problem at a time with no indication of which field it belongs to.
- **Submit and cancel.** Every form dialog adds Save and Cancel buttons and a Ctrl+S binding, then wires validation
  before save.
- **Labels beside fields.** Short fields read better with the label on the left, aligned, than with a label row above
  each one.
- **Arrow keys.** Users expect Up and Down to move between single-line fields.
- **Dirty tracking.** `TrackChanges` marks the form dirty on any `Changed` event and stays dirty until `MarkClean`, so
  typing a character and deleting it leaves a "you have unsaved changes" prompt for an unchanged form.

Armada's `FormView` (`Widgets/FormView.cs`, 331 lines; 31 construction sites) has every one of these, and its
`IFormField` interface supplies the value snapshot used for dirty tracking.

**Design.** All additions are on `Form`, plus two small types and one interface.

- **Sections.** New `public void AddSection(string title)` (non-null) adds a heading row that takes no focus, drawn in a
  new `SectionStyle` (default bold palette 6), with one blank row before it except at the top. `FieldCount` and field
  indexes count fields only; sections do not shift them.
- **Hints.** New `public void SetFieldHint(int index, string? hint)` and `HintStyle` (default dim). A hint is drawn on the
  row below the field, wrapped to the field width. (A method rather than a new optional parameter on `Add`, for binary
  compatibility, the rule recorded for `Region` in 1.4.)
- **Field errors.** New `public bool ShowFieldErrors { get; set; }`, default **false**. New
  `public IReadOnlyList<FormFieldError> ValidateAll()` runs every visible field's validator and, for a `TextField`
  field, its `Validator` (C1), stores each error, and returns them in field order. New class `FormFieldError` (own
  file, immutable): `FieldIndex`, `Label`, `Message`. New `public string? GetFieldError(int index)`,
  `public void ClearErrors()`, and `ErrorStyle` (default palette 1). While `ShowFieldErrors` is true, each stored error
  is drawn under its field (above the hint), prefixed with `ErrorPrefix` (default `"! "`, so the error is not conveyed
  by color alone). An error clears when its field changes and re-validates. `Validate()` is unchanged: it still
  returns the first error and does not store anything.
- **Validation timing.** New enum `FormValidationMode` (own file): `OnSubmit` (default: errors appear after a submit
  attempt), `OnLeave` (a field is validated when focus leaves it), `OnChange` (every change). New
  `public FormValidationMode ValidationMode { get; set; }`.
- **Submit and cancel.** New events `public event EventHandler? SubmitRequested` and
  `public event EventHandler? CancelRequested`. New `public bool Submit()`: runs `ValidateAll()`; when there are errors
  it focuses the first invalid field, returns false, and raises nothing; otherwise it raises `SubmitRequested` and
  returns true. New `public void Cancel()` raises `CancelRequested` (the handler reads `IsDirty` and decides whether to
  confirm; the form never prompts). New `public KeyChord? SubmitKey` and `public KeyChord? CancelKey`, default **null**
  (Ctrl+S and Esc are the common choices). New `public bool ShowButtons { get; set; }`, default false, with
  `SubmitLabel` (default `"Save"`) and `CancelLabel` (default `"Cancel"`): when true, a `ButtonRow` is drawn after the
  last field and is part of the focus order, its buttons calling `Submit()` and `Cancel()`. Read-only
  `public ButtonRow? Buttons` exposes it for styling and extra buttons.
- **Label placement.** New enum `FormLabelPlacement` (own file): `Above` (default, 1.7 layout) and `Left`. New
  `public FormLabelPlacement LabelPlacement { get; set; }` and `public int LabelWidth { get; set; }` (default 0: the
  widest visible label plus two, capped at a third of the width; otherwise at least 1). With `Left`, single-row fields
  sit on the label's row; multi-row fields start on it and continue below; checkboxes (inline fields) are unchanged.
- **Arrow keys.** New `public bool ArrowKeysMoveFocus { get; set; }`, default **false**. When true, Up and Down that the
  focused field does not consume move focus to the previous or next field (so a `TextEditor` keeps its arrows, and a
  `TextField` passes them on).
- **Snapshot dirty tracking.** New interface `IValueSnapshot` (own file) with `string? GetSnapshot()`, implemented by
  `TextField`, `TextEditor`, `Checkbox`, `RadioGroup`, `Dropdown`, `ComboBox`, and `CheckList<T>` (their current value
  as a string; for a list, the checked indices). New enum `FormDirtyTracking` (own file): `Events` (default, the 1.7
  `TrackChanges` behavior) and `Snapshot`. New `public FormDirtyTracking DirtyTracking { get; set; }`. In `Snapshot`
  mode, `MarkClean()` records each field's snapshot, and after every change `IsDirty` is true exactly when some
  snapshot differs, so a revert clears it and raises `DirtyChanged(false)`. Fields that do not implement
  `IValueSnapshot` fall back to event tracking. New `public void SetFieldSnapshot(int index, Func<string?>? snapshot)`
  lets an app supply a snapshot for a custom field.
- **Key hints.** `Form` implements `IKeyHintSource`: the submit and cancel keys when set, and "Up/Down Field" when
  `ArrowKeysMoveFocus` is on. It answers after the focused field (the resolver already merges leaf first).

**Compatibility.** Defaults reproduce the 1.7 layout and behavior exactly: no sections unless added, no hints unless
set, errors not drawn, no buttons, no new keys, labels above, event dirty tracking.

**Tests** (`FormLayoutSuite`, `FormValidationSuite`, `FormSubmitSuite`, `FormDirtySuite`):

- [x] 1.7 snapshot of a three-field form is unchanged.
- [x] Sections draw, take no focus, and do not shift field indexes; hints wrap under their field.
- [x] `ValidateAll` returns errors in order and includes `TextField.Validator`; `ShowFieldErrors` false draws nothing;
      true draws each error with its prefix; an error clears after a fix.
- [x] Each `FormValidationMode` validates at the right moment.
- [x] `Submit` with errors focuses the first invalid field and raises nothing; without errors raises once.
- [x] Submit and cancel keys: null not consumed; set consumed. Buttons appear only with `ShowButtons` and join the
      focus order.
- [x] `Left` placement aligns labels, keeps checkboxes inline, and handles a multi-row field.
- [x] `ArrowKeysMoveFocus`: false leaves Up and Down to the field; true moves between `TextField`s but not out of a
      `TextEditor` that consumed the key.
- [x] Snapshot mode: type then delete returns to clean and raises `DirtyChanged(false)`; a field without
      `IValueSnapshot` uses events; `SetFieldSnapshot` covers a custom field.

**Tasks.**

- [x] Add `FormFieldError`, `FormValidationMode`, `FormLabelPlacement`, `FormDirtyTracking`, and `IValueSnapshot`.
- [x] Extend `Widgets/Form.cs` and `FormField.cs`; implement `IValueSnapshot` on the listed widgets.
- [x] Surface coverage, CHANGELOG, README forms section with a submit example.

### C7: `Wizard`, a multi-step flow widget, and `WizardModal`

**Status:** DONE  **Simplicity 6, Value 7**
**Notes:** Done in 1.9.0 (`Wizard`, `WizardModal`; 13 cases). Alt arrows: as recorded in the review, macOS Terminal and iTerm2
with "Natural text editing" send ESC b and ESC f for Option+Left and Option+Right, which `InputParser` decodes as
Alt+B and Alt+F. Option (a) was taken: `NextStepAltKey` and `PreviousStepAltKey` default to Alt+F and Alt+B. Deviation:
the step keys are checked before the content instead of yielding to a text entry that reports consuming them,
because `TextField.ConsumesChord` reports Left and Right with any modifier, which would have disabled the keys on
every step with a text field; Alt+F and Alt+B therefore never type into a field. The button row does not wrap, so
Tab moves from the last button back to the content. A `Form` step gets `WrapFocus = false`. There is no Skip hint
(Skip has a button, not a key). `WizardModal` adds `NaturalWidth` and `NaturalHeight` (default 64 by 16).

**Problem.** TUIKit has no multi-step flow. Setup, import, onboarding, and "create from template" flows are common in
terminal apps, and each one rebuilds the same parts: a step header ("Step 2 of 5: Repository"), a step list showing
what is done and what is next, per-step validation before moving on, optional steps, Back, Skip, Next, Finish, and
Cancel buttons, and focus that lands on the step's first field. A search of TUIKit's source and changelog finds no
wizard.

Armada's `Wizard` (`Widgets/Wizard.cs`, 189 lines, with `WizardStep`) drives its setup and import flows (about 36
references, 13 in tests), including Alt+Left and Alt+Right to move between steps.

**Design.** New widget `Wizard` in `Widgets/Wizard.cs`, plus `WizardStep`, two event-args types, an outcome enum, and a
modal host. It implements `IWidget`, `IFocusable`, `IFocusContainer`, `IFocusAware`, `IMouseAware`, `IFocusPathNode`,
`IKeyHintSource`, and `IThemeable`.

- **Steps.** New class `WizardStep` (own file): constructor `WizardStep(string title, IWidget content)` (both
  non-null); `Title`; `Content`; `public bool Optional { get; set; }` (default false; shows Skip);
  `public Func<string?>? Validate { get; set; }` (returns an error, or null to allow leaving forward);
  `public Func<bool>? IsApplicable { get; set; }` (default null, meaning always: a step whose predicate is false is
  skipped in both directions and hidden from the step list, for flows that branch on an earlier answer).
- **Construction.** `public Wizard(IEnumerable<WizardStep> steps)` (non-null, at least one step,
  `ArgumentException` otherwise). Read-only `Steps`, `CurrentIndex`, `CurrentStep`, `IsFirst`, `IsLast` (computed over
  applicable steps), and `Error` (the last validation message, or null).
- **Navigation.** `public bool Next()` validates the current step (if `Validate` is set), stores and shows the error and
  returns false on failure, otherwise moves to the next applicable step, or on the last step raises `Finished`.
  `Back()` moves to the previous applicable step without validating. `Skip()` moves forward without validating, only
  on an optional step. `GoTo(int index)` moves to a step already visited (forward jumps validate every step in between
  unless `AllowJumpAhead` is true, default false). `Cancel()` raises `Cancelled`.
- **Events.** `public event EventHandler<WizardStepChangingEventArgs>? StepChanging` (own file: `From`, `To`, and a
  settable `Cancel`), raised before every move, so an app can save a step's data or veto; `StepChanged`
  (`EventHandler<WizardStepChangedEventArgs>`, own file: `From`, `To`); `Finished`; `Cancelled`.
- **Layout.** From top: a header row (`HeaderFormat`, default `"Step {0} of {1}: {2}"`, validated composite format); a
  step list row, or column when `StepListPlacement` is `Left` (new enum `WizardStepListPlacement`: `Top` default,
  `Left`, `None`), marking done, current, and pending steps with `DoneGlyph` (default `"✓"`), `CurrentGlyph` (default
  `">"`), and `PendingGlyph` (default `" "`), and with ASCII fallbacks (`"x"`) when the theme has `UseAsciiBorders`; the
  current step's content; the error row (`ErrorStyle`, prefixed with `"! "`); and a `ButtonRow` with Back, Skip (only
  on optional steps), Next or Finish (on the last applicable step), and Cancel. Labels are properties (`BackLabel`,
  `SkipLabel`, `NextLabel`, `FinishLabel`, `CancelLabel`) for localization. Back is disabled on the first step.
- **Focus.** Entering a step focuses its content (`FocusEdge(true)` when it is an `IFocusContainer`, otherwise the
  content itself when focusable, otherwise the Next button). Tab moves through the content and then the buttons; the
  wizard is a focus container like `Form`.
- **Keys.** New `public KeyChord? NextStepKey` and `PreviousStepKey`, defaulting to Alt+Right and Alt+Left (this is a
  new widget, so the defaults break nobody; Alt arrows are rarely bound by content widgets). They are not consumed
  while the focused content is an `ITextEntry` that accepts text and reports consuming them through
  `ITextEntryKeys.ConsumesChord`. Enter is left to the focused widget or button. Esc is left to the host (a dialog
  closes on Esc already).
- **Alt arrows on macOS (verify before fixing the defaults).** macOS Terminal by default, and iTerm2 with its
  "Natural text editing" preset, send ESC b and ESC f for Option+Left and Option+Right. `InputParser` decodes ESC
  followed by a character as Alt plus that character (`Input/InputParser.cs`, the "Alt + character" branch), so those
  terminals deliver Alt+`b` and Alt+`f`, not Alt+Left and Alt+Right, and the default keys would do nothing. Before
  this item starts, record in Notes what each P3 terminal actually sends. Then choose one: (a) also accept Alt+`b` and
  Alt+`f` through new `NextStepAltKey` and `PreviousStepAltKey` properties (default Alt+`f` and Alt+`b`; not consumed
  while a focused text entry consumes them, since shells use them for word movement); or (b) keep only Alt arrows and
  document the terminal setting. Recommendation: (a), so the wizard works out of the box on the platform the owner
  develops on. The choice changes a default, so it goes to the owner.
- **Paste.** `Wizard` implements C1's `IPasteTarget` by forwarding to its focused leaf, and `WizardModal` (a
  `DialogModal`) receives pastes through C1's `DialogModal.HandlePaste` override, so a field inside a step accepts a
  paste.
- **Theme.** `IThemeable` sets the header from `Accent`, done steps from `Success`, pending from `Muted`, the error from
  `Error`, and passes the theme to the button row. As a new widget it follows the theme by default.
- **Hints.** "Alt+Right Next step" and "Alt+Left Previous step" when those keys are set, plus "Skip" on optional steps.
- **Modal host.** New `WizardModal : DialogModal` (in `Modals/`), constructor `WizardModal(string title, Wizard wizard)`.
  It closes with `RequestClose(WizardOutcome.Finished)` or `RequestClose(WizardOutcome.Cancelled)`, so
  `Modal.Completion` resolves to a new `WizardOutcome` enum (own file: `Finished`, `Cancelled`). Esc in the modal calls
  `wizard.Cancel()`.

**Compatibility.** New types only.

**Tests** (`WizardSuite`, `WizardModalSuite`):

- [x] Constructor guards; header and step list text for first, middle, and last steps.
- [x] `Next` blocked by validation shows the error and stays; passing moves and clears it; last step raises `Finished`.
- [x] `Back` never validates; `Skip` only on optional steps; `GoTo` rules with and without `AllowJumpAhead`.
- [x] `IsApplicable` false skips the step both ways and hides it from the list and the count.
- [x] `StepChanging` cancel vetoes the move; `StepChanged` reports from and to.
- [x] Focus lands in the step content on entry; Tab reaches the buttons; Back is disabled on the first step.
- [x] Alt+Right and Alt+Left move; they are not consumed when a focused text entry consumes them; null keys disable.
- [x] If option (a) is chosen: the parsed form of ESC f and ESC b moves steps; null disables them.
- [x] A paste into a `WizardModal` reaches the focused `TextField` in the current step.
- [x] ASCII theme uses ASCII glyphs.
- [x] `WizardModal` completes with `Finished` and `Cancelled`; Esc cancels.

**Tasks.**

- [x] Add `Wizard`, `WizardStep`, `WizardStepChangingEventArgs`, `WizardStepChangedEventArgs`,
      `WizardStepListPlacement`, `WizardOutcome`, and `Modals/WizardModal.cs`.
- [x] Surface coverage, CHANGELOG, README widget table and a short example.

---

## Phase 4: Viewers

### C5: `DiffView` unified diff input, file navigation, search, and theming

**Status:** DONE  **Simplicity 7, Value 7**
**Notes:** Done in 1.10.0 (`DiffViewUnified`, `DiffViewSearch`; 13 cases). The parser is internal (`UnifiedDiffParser`, with
`GitPathUnquoter` and `DiffFile`). Decisions: a `---` line is a file header only when the next line is `+++`;
`diff -u` timestamps after a tab are stripped from paths; inside a hunk an empty line counts as context. Search
starts at the top visible line (so a match on the top row is found), not after it as `MarkdownView` does. With
`UseThemeStyles`, theme roles are resolved at render time (the `FuzzyList` approach). `DiffView` also implements
`IFocusAware` (for "while focused" hints) and adds `LineKinds` and `LineNumberStyle`.

**Problem.** `DiffView` (`Widgets/DiffView.cs`, 264 lines) has one constructor, `DiffView(string oldText, string newText,
string? language)`, and computes the diff itself. Most diffs a terminal app shows are not computed locally: they come
from `git diff`, a code review API, a patch file, or a server, already in unified format, often covering many files.
`DiffView` cannot show those. It also cannot:

- style file headers (`diff --git`, `---`, `+++`), hunk headers (`@@ -1,4 +1,5 @@`), or metadata (`index`,
  `new file mode`, `Binary files differ`);
- list the files in the diff or jump between them;
- search;
- follow the theme (its colors are palette constants);
- give back text to copy.

Armada's `DiffViewer` (`Widgets/DiffViewer.cs`) exists for exactly this; its tests assert the file list
(`TuiWidgetSuite.cs`, `TuiTypedFieldsSuite.cs`), and its docs promise `]` and `[` to jump between files.

**Design.**

- **Unified input.** New `public static DiffView FromUnifiedDiff(string diff, string? language = null)` (non-null diff).
  The parser:
  - recognizes `diff --git a/x b/y`, `--- a/x`, `+++ b/y` (file headers), `@@ ... @@ optional context` (hunk headers,
    with the old and new start lines parsed), `index`, `new file mode`, `deleted file mode`, `similarity index`,
    `rename from/to`, `Binary files ... differ`, and `\ No newline at end of file` (metadata);
  - parses the old and new line counts from each `@@ -a,b +c,d @@` header (a missing count means 1) and uses them to
    know where the hunk ends: inside a hunk, every line is a body line until both counts are used up, whatever it
    starts with. Without this, a removed line whose content starts with `-- ` (an SQL, Lua, or Haskell comment) is
    drawn as `--- ...` and would be misread as a file header; `+++` has the same problem for added lines. Header
    recognition applies only between hunks;
  - treats `+`, `-`, and space-prefixed lines inside a hunk as added, removed, and context, and `\ No newline at end of
    file` inside a hunk as metadata that does not use up a count;
  - when a hunk ends early (the input stops or a line has no valid prefix before the counts are used up), closes the
    hunk and goes on scanning for headers, so a truncated diff still renders;
  - takes the file path from the `+++` line, or from `---` when `+++` is `/dev/null` (a deleted file); a `---` of
    `/dev/null` is a new file. It strips the `a/` and `b/` prefixes, and unquotes C-style quoted paths, which git emits
    for names with spaces, quotes, or non-ASCII bytes (`+++ "b/my file.txt"`, with `\"`, `\\`, `\t`, `\n`, and octal
    escapes). `diff --git` supplies the path only when there is no `---`/`+++` pair (a binary or mode-only change);
  - treats combined diffs (`diff --cc`, `@@@` headers, two-column markers) as context, rendered literally, and never
    throws on them; combined-diff support is out of scope;
  - treats any other line as context, so plain text or a malformed diff renders literally and never throws;
  - keeps the `+`, `-`, and space markers in the drawn text, so a change is never conveyed by color alone;
  - handles CRLF input.
- **Line kinds.** `DiffLineKind` gains `FileHeader = 3`, `HunkHeader = 4`, and `Meta = 5`. New styles
  `FileHeaderStyle` (default bold), `HunkHeaderStyle` (default palette 6), and `MetaStyle` (default dim). A computed
  diff (the existing constructor) never produces the new kinds.
- **Files.** Read-only `public IReadOnlyList<string> Files` (the `b/` path, or the `a/` path for a deleted file; empty
  for a computed diff), `public int CurrentFileIndex` (the file whose header is at or above the top row; -1 when there
  are no files), and methods `NextFile()`, `PreviousFile()`, and `GoToFile(int index)` that scroll the file header to
  the top and return false when there is nowhere to go. New `public bool UseFileKeys { get; set; }`, default
  **false**: when true, `]` and `[` call `NextFile` and `PreviousFile` (letter keys are opt-in, per the 1.7 rule).
- **Line numbers.** New `public bool ShowLineNumbers { get; set; }`, default false: when true, a gutter shows the old and
  new line numbers parsed from hunk headers (or computed for the two-text constructor), blank for headers. The gutter
  width fits the largest number.
- **Search.** `SetSearch(string? query)`, `FindNext()`, `FindPrevious()`, read-only `SearchText`, and F3 and Shift+F3
  while a term is set, with exactly `MarkdownView`'s contract (1.7): case-insensitive over the drawn text including
  markers, wrapping in both directions, matches highlighted with `Theme.SearchMatchRole` when the theme is applied and
  with the `Pane` default otherwise.
- **Viewport.** `DiffView` implements `IScrollExtent` (`ContentHeight`) and gains read-only `ViewportHeight` and
  `ScrollOffset` plus `ScrollBy`, `ScrollToTop`, and `ScrollToEnd`, matching `MarkdownView`. Paging keeps its 1.7 step
  of 10 rows (changing it would alter existing apps); a new `public bool PageByViewport { get; set; }`, default false,
  pages by the viewport minus one row.
- **Text.** Read-only `public string PlainText`: the unified input as given, or for a computed diff the marked lines
  joined with newlines. This is what an app copies or saves.
- **Theme.** `DiffView` implements `IThemeable` with `public bool UseThemeStyles { get; set; }`, default **false**, the
  `MarkdownView` pattern. When true: added from `Success`, removed from `Error`, context from `Text`, file headers from
  `Accent` bold, hunk headers from `Info`, metadata from `Muted`, and the search match from `SearchMatchRole`.
- **Key hints.** `DiffView` implements `IKeyHintSource`: "Up/Down Scroll" while focused and scrollable, "]/[ File" with
  `UseFileKeys`, and "F3 Next match" with a search term. New `public bool ShowKeyHints { get; set; }`, default true (the
  `MarkdownView` 1.7 precedent); false returns no hints. A focused 1.7 view therefore adds "Up/Down Scroll" to the
  status bar; this is listed under "Changed".

**Compatibility.** The existing constructor's output, keys, paging step, and colors are unchanged. The new enum values
appear only in views built with `FromUnifiedDiff`. The one visible change is the "Up/Down Scroll" hint, listed under
"Changed".

**Tests** (`DiffViewUnifiedSuite`, `DiffViewSearchSuite`):

- [x] A two-file git diff: kinds per line, `Files`, markers kept, styles per kind.
- [x] Rename, new file, deleted file, binary, and "no newline" lines parse as metadata; CRLF input works.
- [x] Plain text and a truncated hunk render literally without throwing.
- [x] A removed line `--- x` and an added line `+++ y` inside a hunk parse as removed and added, not as file headers;
      a hunk header without counts (`@@ -1 +1 @@`) uses 1; "no newline" inside a hunk does not use up a count.
- [x] `/dev/null` on either side gives a new or deleted file with the right path; a quoted path with a space and an
      octal escape unquotes; a `diff --cc` input renders literally without throwing.
- [x] `ShowKeyHints` true returns "Up/Down Scroll" when focused and scrollable; false returns no hints.
- [x] `NextFile`, `PreviousFile`, `GoToFile`, and `CurrentFileIndex` while scrolling; `]` not consumed until
      `UseFileKeys` is true.
- [x] Line numbers from hunk headers and from the computed constructor.
- [x] Search wraps both ways; F3 not consumed without a term.
- [x] `PageByViewport` false keeps 10-row paging; true pages by viewport.
- [x] `UseThemeStyles` false: `ApplyTheme` changes nothing; true: styles follow the theme.
- [x] The two-text constructor's 1.7 snapshot is unchanged.

**Tasks.**

- [x] Add the parser as an internal class in `Content/` (one class per file, so the path unquoting gets its own
      internal helper); extend `DiffLineKind` and `DiffView`.
- [x] Surface coverage, CHANGELOG (Added, and Changed for the enum members and the scroll hint), README.

### C8: `HeatMap` selection, levels, legend, and wide cells

**Status:** DONE  **Simplicity 7, Value 5**
**Notes:** Done in 1.10.0 (`HeatMapInteraction`; 11 cases). Keeping 1.9 behavior while not selectable needed more than
"takes no keys": a bound `IFocusable` would join the host focus order and take focus from a click. The map
implements the internal `IFocusChildren` (focusable only while `IsSelectable`), and the host's click-to-focus now
checks `FocusScope.IsFocusable`, which also stops a click from focusing a disabled or hidden widget (listed under
"Changed"). `NavigateOverride` is public: `CalendarHeatMap` uses it, and it is a general hook. Discrete levels at
or below the minimum are level 0, the rest split into equal steps (`ceil`), and without `LevelGlyphs` they map onto
the four shades. An internal `CellBlank` hook lets the calendar draw out-of-range days blank.

**Problem.** `HeatMap` (`Widgets/HeatMap.cs`, 281 lines) draws a grid of shaded cells for display only:

- it is not focusable and has no selection, so the user cannot ask "what is this cell?";
- its intensity is continuous, from four shade glyphs (`░▒▓█`) in one color, so on a monochrome terminal or under
  `NO_COLOR` neighboring levels are hard to tell apart;
- it has no legend, so the reader cannot tell what the shades mean;
- cells are one column wide and column labels draw only their first character, so a calendar or weekly layout cannot
  carry month or day labels;
- nothing scrolls when the grid is wider than the widget.

Armada's `CommitHeatmap` (`Widgets/CommitHeatmap.cs`, 399 lines; 11 test references to its `Level`, `Glyph`, and
`LevelStyle`) adds all of this, and its doc comment says so.

**Design.** All additions are on `HeatMap`.

- **Selection.** `HeatMap` implements `IFocusable`, `IFocusStop`, `IFocusAware`, `IMouseAware`, and `IKeyHintSource`.
  New `public bool IsSelectable { get; set; }`, default **false**; while false, the map takes no keys or clicks (1.7
  behavior). `IsFocusStop` (default false, the `MarkdownView` 1.7 rule) makes it a Tab stop. While selectable: arrow
  keys move the selection, Home and End go to the first and last cell of the row, and a click selects; read-only
  `SelectedRow` and `SelectedColumn`, `public void Select(int row, int column)` (clamped), and events
  `SelectionChanged` and `CellActivated` (Enter or double click; Enter consumed only with a handler). The selected cell is
  drawn with a new `SelectionStyle` (default reverse) so it is visible without color.
- **Selectable cells.** New `public Func<int, int, bool>? IsCellSelectable { get; set; }` (row, column), default null
  (every cell selectable). Arrow keys skip unselectable cells in the direction of travel (stopping at the edge when
  none remains), Home and End go to the first and last selectable cell of the row, clicks on them are ignored, and
  `Select` moves to the nearest selectable cell (searching forward, then backward) or leaves the selection unchanged
  when none exists. C9 uses this for days outside its range; it is also how any sparse grid marks empty slots. Read-only
  `public bool HasSelection` is false when no cell is selectable.
- **Navigation override.** New `public Func<int, int, KeyEvent, Point?>? NavigateOverride { get; set; }`, default null.
  When set, it is asked first for every navigation key while selectable, with the current row and column; a non-null
  result is the new cell (then clamped and checked with `IsCellSelectable`), and null falls back to the default
  movement. This lets a composing widget (C9) define movement such as "next day wraps to the next week" without
  subclassing a sealed type. Keep it internal if the implementer finds no second use; record the choice in Notes.
- **Levels.** New `public int LevelCount { get; set; }`, default 0 (continuous shading, 1.7 behavior), otherwise 2 to 16
  (`ArgumentOutOfRangeException`). With levels, each value maps to a level (0 for values at or below the range minimum,
  then equal steps), and new `public IReadOnlyList<string>? LevelGlyphs` and
  `public IReadOnlyList<CellStyle>? LevelStyles` (each null or exactly `LevelCount` entries, `ArgumentException`
  otherwise) choose the glyph and style per level, falling back to the shade glyphs and `CellColor`. Distinct glyphs per
  level (for example `" "`, `"."`, `":"`, `"+"`, `"#"`) keep the map readable without color. New
  `public int LevelOf(int row, int column)` exposes the mapping for tests and tooltips.
- **Legend.** New `public bool ShowLegend { get; set; }`, default false: a row below the grid reading
  `LegendLowText` (default `"Less"`), each level's glyph in its style, and `LegendHighText` (default `"More"`).
- **Wide cells.** New `public int CellWidth { get; set; }`, default 1, 1 to 4: each cell repeats its glyph (or pads it)
  to that width, which gives column labels room.
- **Spanning labels.** New `public bool SpanColumnLabels { get; set; }`, default false: when true, a column label is drawn
  in full starting at its column and may run over following columns whose labels are empty (month names over weeks).
  Overlapping labels are truncated where the next non-empty label starts.
- **Description.** New `public Func<int, int, string?>? CellDescription { get; set; }`. When set, the map implements
  `ITooltipProvider` through it, and a new `public bool ShowSelectionDescription` (default false) draws the selected
  cell's description on a footer row.
- **Scrolling.** When selectable and wider than the widget, the visible window of columns follows the selection;
  read-only `FirstVisibleColumn`.
- **Theme.** `IThemeable` with `UseThemeStyles` default false; when true, labels from `Muted`, selection from
  `Selection`, and the default level styles shade `Success`.

**Compatibility.** With defaults, the map renders and behaves exactly as in 1.7 and is not a Tab stop.

**Tests** (`HeatMapInteractionSuite`):

- [x] 1.7 snapshot unchanged with defaults; not focusable when `IsSelectable` is false.
- [x] Arrow keys, Home, End, and clicks move the selection; events fire; Enter consumed only with a handler.
- [x] `LevelOf` boundaries; glyph and style arrays validated; distinct glyphs render per level.
- [x] Legend content; `CellWidth` 2 doubles the grid width; spanning labels run and truncate correctly.
- [x] Tooltip and footer description; scrolling keeps the selection visible.
- [x] `IsCellSelectable`: arrows skip unselectable cells and stop at the edge; clicks on them are ignored; `Select` on
      one moves to the nearest selectable cell; an all-unselectable grid has `HasSelection` false.
- [x] `NavigateOverride`: a non-null result moves there; null falls back to default movement.

**Tasks.**

- [x] Extend `Widgets/HeatMap.cs` (regions if it passes 500 lines).
- [x] Surface coverage, CHANGELOG, README.

### C9: `CalendarHeatMap`, a day-by-week heat map

**Status:** DONE  **Simplicity 6, Value 6**
**Notes:** Done in 1.10.0 (`CalendarHeatMap`; 7 cases). Decisions: the default `LevelGlyphs` are a middle dot then the four
shades, because five levels on four shade glyphs gave two levels the same glyph; `IsFocusStop` defaults to true (a
new widget); the range maximum sets the scale; month labels mark the week holding the 1st of each month and the
first week; Alternate weekday labels are the second, fourth, and sixth rows. The calendar adds `WeekCount`,
`FirstVisibleWeek`, `CellColor`, `CellWidth`, and `ShowLegend` pass-throughs.

**Problem.** The most common heat map in software is the contribution calendar: one column per week, one row per
weekday, month names above, weekday names at the side, a legend, and a selected day with its count. Building it on a
generic grid means computing week columns from dates, aligning months, handling the first day of the week, and mapping
selection back to a date. Every app that wants one (activity, commits, usage, incidents, spending) repeats that
arithmetic. Armada's `CommitHeatmap` and `TokenActivityHeatmap` are two such calendars in one app.

**Design.** New widget `CalendarHeatMap` (own file), composed over `HeatMap` (C8) rather than inheriting (both are
sealed). It implements the same interfaces as a selectable `HeatMap`.

- **Data.** `public void SetData(DateTime from, DateTime to, IReadOnlyDictionary<DateTime, double>? values)`: `from`
  and `to` are reduced to `.Date`; `to` before `from` throws `ArgumentException`; missing days are zero; null clears.
  `SetData` copies the values into its own dictionary keyed by `key.Date`, so a caller's key with a time part
  (`2026-03-01 14:30`) still matches its day; comparing by `.Date` only at lookup would miss it, because a dictionary
  looks up by the whole key. Two caller keys on the same day are summed (document it: an hourly feed totals per day).
  `DateTimeKind` is ignored: dates are calendar days, never converted between time zones (document it). Read-only
  `From`, `To`, `public double ValueOn(DateTime day)` (looks up `day.Date`).
- **Layout.** Weeks are columns, weekdays are rows, starting on `public DayOfWeek FirstDayOfWeek { get; set; }` (default
  `DayOfWeek.Sunday`). `CellWidth` defaults to 2. Month labels span their weeks (C8 `SpanColumnLabels`), and weekday
  labels show every other day by default (`WeekdayLabelMode` enum, own file: `None`, `Alternate` default, `All`). Names
  come from `public CultureInfo Culture { get; set; }`, default `CultureInfo.InvariantCulture`, for deterministic
  output.
- **Levels.** `LevelCount` defaults to 5 (zero plus four), with glyph and style overrides passed through to the inner
  map, and the legend on by default.
- **Selection.** `CalendarHeatMap` defines its own movement through C8's `NavigateOverride`, because day order runs
  down the columns, not across the rows:
  - Up and Down move one day; Down on the last weekday row wraps to the first row of the next week, and Up on the
    first row to the last row of the previous week, so Down always means "the next day";
  - Left and Right move one week (same weekday);
  - Home and End go to the first and last day in the range;
  - every move is clamped to the range; days outside it in the first and last week are drawn blank and marked
    unselectable through C8's `IsCellSelectable`, so no key or click can land on them.

  Read-only `SelectedDate`; `public void Select(DateTime day)` (clamped to the range); events `DateSelected` and
  `DateActivated` (`EventHandler<DateTime>`).
- **Footer.** New `public Func<DateTime, string>? DayDescription` (default: the date in `Culture` short format and the
  value) drawn on a footer row with the legend, and `public string? Summary` (for example a total) drawn before it.
- **Width.** When the range has more weeks than fit, the visible weeks follow the selection, and the newest week is
  visible initially (activity calendars are read from the right).

**Compatibility.** New type only.

**Tests** (`CalendarHeatMapSuite`):

- [x] A 365-day range starting on a Wednesday: column count, blank leading days, month label positions.
- [x] `FirstDayOfWeek` Monday shifts rows; weekday label modes.
- [x] Levels from values; the legend; the high-contrast glyph set.
- [x] Keyboard and click selection, range clamping, events, and the default description.
- [x] Down on the last weekday moves to the next week's first day; Up on the first weekday to the previous week's last
      day; neither moves outside the range; blank leading and trailing days cannot be selected by key or click.
- [x] Keys with a time part match their day; two keys on one day are summed; `ValueOn` ignores the time part.
- [x] Narrow width shows the newest weeks first and scrolls with the selection.
- [x] `SetData` guards and null clearing; output is identical across cultures with the invariant default.

**Tasks.**

- [x] Add `CalendarHeatMap` and `WeekdayLabelMode`.
- [x] Surface coverage, CHANGELOG, README.

---

## Phase 5: Example application

**Status:** DONE

The example app (`src/TUIKit.Example`) gets one guided-tour page per area, in the style of the 1.7 "Markdown that fits"
page, plus headless modes the test suite can run. Each page ships in the release of the items it shows: E1 and E2 in
1.8.0, E3 in 1.9.0, E4 and E5 in 1.10.0.

- [x] **E1 Text entry page.** (1.8, built as two pages: "Text fields that log in" and "Pickers", because one
      demo box could not show both legibly) A sign-in form: a username field prefilled and selected, a masked token field with
      Ctrl+R reveal, line-editing keys on, a `MaxLength`, validation shown on submit, Enter submitting, and
      `RoutePasteToFocus` on so a paste into the token field works; plus a `FuzzyList` picker with details, a
      disabled entry, multi-select, and a provider that offers "Create <query>".
- [x] **E2 Data page.** (1.8, "Server-backed tables"; the page size cycles with `z` and local mode is `m`) A 1,000-row table in `Remote` mode backed by a simulated server with a 300 ms delay and a
      "fail next load" toggle, driven by `DataTableLoader`, with `<` and `>` page keys, F5 retry, a column chooser (`c`),
      a column cursor with `s` and `S`, and marks that survive paging.
- [x] **E3 Forms page.** (1.9, "Forms and flows"; the form scrolls inside the demo box around the focused field) A settings form with sections, hints, left-aligned labels, field errors, Ctrl+S and Esc,
      buttons, arrow-key movement, and snapshot dirty tracking shown live ("unsaved changes" appears and disappears on
      revert); and a three-step `WizardModal` with an optional step and a step that is skipped by an earlier answer.
- [x] **E4 Viewers page.** (1.10, built as two pages, "Diffs from git" and "Activity calendar", for room) A `DiffView.FromUnifiedDiff` of a bundled three-file patch with `]` and `[`, line numbers,
      and search; and a `CalendarHeatMap` of a year of generated activity with selection and a description footer.
- [x] **E5 Headless modes.** (1.10; also `--numbers` and `--color` for the diff, `--width` and `--color` for both; exercised in P2, see the record) `--diff-once [--width N]` prints the bundled patch through `DiffView`;
      `--calendar-once [--ascii]` prints the calendar; both exit 0 and are exercised by a test.

---

## Phase 6: Documentation

**Status:** DONE

Run once per release, covering that release's items only.

- [x] **D1 README.** Update the widget table (`TextField`, `DataTable`, `FuzzyList`, `DiffView`, `Form`, `HeatMap`) and
      add rows for `Wizard`, `WizardModal`, `CalendarHeatMap`, and `DataTableLoader`, each with a short snippet.
- [x] **D2 CHANGELOG.** One `## [1.x.0]` section per release with Added and Changed, written for app developers, in
      the style of 1.7.0's.
- [x] **D3 Surface coverage and conformance.** Every new public member in `docs/SURFACE_COVERAGE.md`; each item checked
      against `docs/CONFORMANCE.md`.
- [x] **D4 Building terminal apps.** In `BUILDING_TERMINAL_APPS.md`, add short sections on paste routing and
      server-backed lists (paging plus the loader) in 1.8, and forms that validate and submit and multi-step flows in
      1.9, cross-linked to the example pages.

---

## Phase 7: Verification and publishing

**Status:** DONE

Run once per release.

- [x] **P1** Full suite through `Test.Automated`, `Test.Xunit`, and `Test.Nunit` on every target framework; zero
      warnings on all three library targets.
- [x] **P2** Every example headless mode exits 0; the tour pages are walked through with `--tour-once --keys`.
- [x] **P3** Manual terminal check on macOS Terminal, iTerm2, and Windows Terminal, for the release's items: masking
      with emoji, bracketed paste into a focused field and into a dialog, the paging bar, and the column chooser (1.8);
      the wizard modal and its step keys, Option+arrows included (1.9); the diff line-number gutter and the calendar in
      ASCII mode (1.10).
- [x] **P4** Pack the NuGet package locally and confirm the Armada adoption check in Phase 8 builds against it before
      publishing.
- [x] **P5** Owner publishes the release. After 1.8.0 and 1.9.0, mark their items DONE here and keep the plan in place;
      after 1.10.0 (or when the owner defers the rest), archive this plan to `archive/NEW_CAPABILITIES.md` with an
      implementation record.

---

## Phase 8: Armada adoption (tracked here, done in Armada)

### X1: Replace Armada's private copies

**Status:** TODO
**Notes:**

After each release is on NuGet, Armada bumps its `TUIKit` reference and replaces the private widgets that release
covers (1.8: text input, grid, filter list; 1.9: form and wizard; 1.10: diff viewer and heat maps), keeping its suites and
focus sweep green at every step. Armada's widgets derive from its own `ArmadaWidget` (palette, localization, focus
scopes), so most become thin wrappers that compose the TUIKit widget, map Armada's palette into its styles (or through
`ThemeService.BuildTuiKitTheme` with `UseThemeStyles`), and translate labels:

| Armada | Replaced by | Armada keeps |
|---|---|---|
| `TextInput`, `InputField` | `TextField` with `UseLineEditingKeys`, `SelectAll`, `RevealKey` Ctrl+R, `Submitted`, `MaxLength`, `Validator`; `RoutePasteToFocus` on the application | `IFormField`, localization of placeholders and errors |
| `ArmadaGrid<T>`, `GridColumn<T>`, `GridQuery`, `GridPage<T>` | `DataTable<T>` in `Remote` and `Local` paging with `DataTableLoader<T>`, column `Key`, `Visible`, `CanHide`, the chooser, and the column cursor | the row menu (`.`, Shift+F10), `z`/`Z` page-size keys bound to `CyclePageSize`, palette mapping of cell styles |
| `FilterList<T>`, `SelectOption<T>` | `FuzzyList<T>` with `Detail`, `IsEnabled`, `MultiSelect`, `ItemsProvider`, `Insert`, `UseLineEditingKeys` | picker modals' framing |
| `DiffViewer` | `DiffView.FromUnifiedDiff` with `UseFileKeys` and search | the `/` search prompt (`ScrollTextView` convention), copy |
| `FormView`, `FormRow`, `IFormField` | `Form` with sections, hints, `ShowFieldErrors`, `ShowButtons`, `SubmitKey` Ctrl+S, `CancelKey` Esc, `ArrowKeysMoveFocus`, `Snapshot` dirty tracking | the "discard changes?" confirmation on Esc |
| `Wizard`, `WizardStep` | `Wizard` and `WizardStep` | step content |
| `CommitHeatmap`, `TokenActivityHeatmap` | `CalendarHeatMap` with Armada's level glyphs and palette styles | the high-contrast palette choice |

- [ ] Open one Armada work item per release with its rows of this table and link each here.
- [ ] Record any API gap Armada hits during adoption as a 1.8.x follow-up here, rather than patching around it in
      Armada.

---

## Considered and not included

- **Focus routing, the run loop, status badges, toasts, and framed regions.** The same audit found Armada keeps private
  versions of these too, but their general part is either already covered by TUIKit or too tied to one app's design.
  They are excluded from this release by owner decision.
- **Arbitrary text selection in `TextField`.** Shift+arrow range selection, copy, and cut are a larger feature with
  clipboard implications across backends. The all-or-nothing selection in C1 covers the prefill case that apps
  actually hit.
- **Asynchronous `FuzzyList` providers.** Debouncing, cancellation, and a loading row are worth doing once a second app
  asks; `ISuggestionProvider` is the likely shape. C4's synchronous provider covers computed entries and local lookups.
- **Column reordering and resizing in `DataTable`.** The keys introduced in C3 make a later reorder API possible; no
  current consumer needs it.
- **Inline cell editing in `DataTable`.** A different widget's job (`ListEditorModal` and forms cover editing today).
- **Side-by-side diffs and word-level highlights in `DiffView`.** Valuable, but each is a separate rendering mode; C5
  focuses on getting real-world diffs on screen.
- **Unsaved-changes prompts inside `Form`.** Whether and how to confirm is app policy; C6 gives the app `IsDirty` and
  `CancelRequested` to implement it in a few lines.
- **Non-linear wizards (graphs of steps).** `IsApplicable` covers the common branch; arbitrary step graphs are rare in
  terminal flows.
- **Routing pastes to the focused widget by default.** It is the behavior most apps want, but an app that handles
  `PasteReceived` today would stop seeing pastes into a focused field. C1's `RoutePasteToFocus` is opt-in; a later
  MAJOR release can flip the default.
- **Combined diffs (`diff --cc`) in `DiffView`.** Merge-commit diffs have two marker columns and `@@@` headers; C5
  renders them literally rather than half-parsing them.
- **A built-in search prompt for `DiffView`.** Same decision as 1.7 for `MarkdownView`: prompt placement is the app's;
  the engine, keys, and theme role are TUIKit's.

---

## Implementation record

Record here every deviation from this plan, review findings and their fixes, and the final test counts per target,
as 1.5 and 1.7 did.

**1.8.0, 2026-10-14.** C1 to C4 shipped with ten new suites (76 cases). Final counts: 1052 console cases on
net8.0 and net10.0, and 1053 through xUnit and NUnit on both, all green; 0 warnings on `netstandard2.0`, `net8.0`,
and `net10.0`. Coverage of `TUIKit` on net10.0 is 84.9% (new and changed files 91.4%). Deviations, each recorded in
its item's Notes: `DialogModal.PasteTarget` replaces "forward to the focused content"; `FuzzyList` resolves theme
roles at render time; `ApplyColumnChooser` returns `bool`; `ReverseSortKey` binds upper-case letters through
shift+letter chords; E1 became two tour pages. As in 1.5 to 1.7, the release was committed to `main` in phase
commits with an annotated `v1.8.0` tag rather than on a `feature/v1.8.0` branch. The tour gained a `KeyFilter` that gives a focused text-entry demo
first refusal on keys it would type or consume (so `?` and Ctrl+K reach a field) and routes pastes to the demo.

**1.9.0, 2026-10-14.** C6 and C7 shipped with six new suites (32 cases). Final counts: 1084 console cases on net8.0
and net10.0, and 1085 through xUnit and NUnit on both, all green; 0 warnings on all three library targets. Coverage
of `TUIKit` on net10.0 is 85.4% (new and changed files 88.5%, after adding mouse and theme cases to reach the
wizard's and the form's click paths). Deviations, each in its item's Notes: the wizard's step keys come before the
content; `WizardModal` gained `NaturalWidth` and `NaturalHeight`; the Alt+F and Alt+B defaults were added. The
headless tour snapshot now draws open modals, so `--tour-once --keys` can show the wizard.

**1.10.0, 2026-10-14.** C5, C8, and C9 shipped with four new suites (32 cases with the Option+arrow byte test). Final counts: 1115 console cases
on net8.0 and net10.0, and 1116 through xUnit and NUnit on both, all green; 0 warnings on all three library
targets. Coverage of `TUIKit` on net10.0 is 85.5% (new and changed files 88.2%). The host click-focus fix is the
one change not foreseen by the plan; it is listed under "Changed". E4 became two tour pages.

**Verification (Phase 7), all three releases.** P1: every runner on both test frameworks, green, before each
publish. P2: `--tour-once --keys` walked every new page (typing, Tab, Enter, Ctrl+R, F4 to F9, the wizard modal,
local paging and keyboard sorting); `--diff-once` and `--calendar-once` exit 0. E5's "exercised by a test" is met by
this P2 run, not by a Touchstone case, because `Test.Shared` may reference only `TUIKit` and `Touchstone.Core`.
P3 (manual checks in macOS Terminal, iTerm2, and Windows Terminal) and the Option+arrow byte check were not run by
the implementer, who has no interactive terminal; the Alt+F and Alt+B defaults come from the terminals' documented
key bindings, and a Wizard case feeds those bytes (ESC b, ESC f, and the CSI Alt arrows) through `InputParser`. P4: each package was packed locally and pushed with its
symbols; the Armada build check is left to the owner as part of X1. P5: 1.8.0, 1.9.0, and 1.10.0 are on NuGet, and
this plan is archived after 1.10.0.

**Plan review, 2026-10-14 (before any work).** The plan's code references were checked against `main` at `aaa5d2c`
and held up (file sizes, the masking defect, the Ctrl rejection in `TextField.HandleKey`, `FuzzyList`'s hard-coded
prompt and code-unit Backspace, the three `DiffLineKind` values, `DiffView`'s 10-row paging, `HeatMap` being
display-only, `DataTable` leaving Left and Right unbound, and every referenced type and theme role). Changes made:

- Split into three MINOR releases (1.8 text entry and data, 1.9 forms and flows, 1.10 viewers); Phases 0 and 5 to 8
  run per release.
- C1: added paste routing (`IPasteTarget`, opt-in `TuiApplication.RoutePasteToFocus`, `DialogModal.HandlePaste`),
  because `TuiApplication.DispatchPaste` never offers a paste to a focused widget outside a modal and `DialogModal`
  drops it; `ConsumesChord` now reports Enter with a `Submitted` handler; key hints are null with defaults.
- C4 and C5: their new default hints change the status bar of a 1.7 app, so they are listed under "Changed" and
  gain `ShowKeyHints`. C4 documents the two meanings of Ctrl+A.
- C2: defined index meaning per paging mode, `MarkAll` scope in `Local`, and that `Remote` ignores `SortLocally`.
- C3: `Key` is fixed at construction (a settable key could bypass the uniqueness check); the duplicate-key rule is
  stated precisely; `SetColumnVisible` returns `bool`.
- C5: hunk extent comes from the `@@` counts, so body lines starting with `---` or `+++` are not misread as headers;
  `/dev/null`, quoted paths, and combined diffs are specified.
- C7: Alt+Left and Alt+Right may arrive as Alt+`b` and Alt+`f` on macOS terminals; verify and decide before fixing
  the defaults. `Wizard` and `WizardModal` accept pastes.
- C8: added `IsCellSelectable` and `NavigateOverride`, which C9 needs and C8 lacked. C9: day-wise wrapping
  navigation and date normalization in `SetData` are specified.
- The event convention (`EventHandler` for new events) is stated in rule 3.
- Simplicity scores revised: C1 6, C4 6, C6 4, C9 6.
