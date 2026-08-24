# ListEditorModal + Hierarchical Selection — Implementation Plan & Gap Analysis

**Status:** Proposal / implementation plan
**Targets:** TUIKit `0.8.3` (local source in `src/TUIKit`)
**Driver:** Armor (paranoid-grade backup tool)

Armor ships two hand-written `Modal` subclasses today — an exclude-rule editor and a
cascading file/folder selector — for the plain reason that no single TUIKit type covers
either. Both are compositions of primitives TUIKit already owns (`DialogModal`,
`ActionListView<T>`, `ReorderableList<T>`, `TextField`, `Tree<T>`, `CheckList<T>`), so the
right fix is not "keep re-implementing them per app" but "land the missing reusable pieces in
TUIKit and delete the bespoke code." This document is the build order for that.

It specifies three things and treats each as shippable work, not sketch:

1. **`ListEditorModal<T>`** — a validated, single-screen list editor for `TUIKit.Modals`.
2. **A gap analysis of `FileBrowser` and `Tree<T>`**, with a new **`CheckTree<T>`** widget and
   **`FileSelectModal`** so Armor's cascading selector can be retired.
3. **Tests and example coverage** for all of the above, written to the repository's Touchstone
   and guided-tour conventions.

Every "Armor requirement" (`Rn`) is a hard capability Armor needs before it will adopt the
TUIKit type. They are written as acceptance criteria on TUIKit so nothing gets lost in the
handoff.

---

## Conventions and coding standards

Everything below must land under `C:\Code\agents\requirements\CODE_STYLE.md`. The rules that
shape these specific APIs, called out so the implementer does not have to rediscover them:

- **One type per file.** No grouping. Each class, struct, and enum named here gets its own
  `.cs` file — the file lists in §1.2, §2.4, and §2.5 are exact, not illustrative.
- **No tuples.** The author's words are "I hate tuples." Results that would tempt a tuple —
  parse outcomes, a file exclusion (path + is-directory), a selection (includes + holes) —
  are named types (`ParseResult<T>`, `FileExclusion`, `FileSelection`). Do not regress these
  into `(bool, string)`.
- **Namespaces and usings.** Namespace first; `using` directives inside the namespace block;
  `System.*` alphabetical, then the rest alphabetical.
- **Docs.** XML doc comments on every public type, member, constructor, and method — including
  `<exception>` tags for what public methods throw and explicit notes on default / min / max
  values. No doc comments on private members.
- **Private fields** are `_PascalCase` with a leading underscore.
- **Configurable, not const.** Values a consumer may want to change (glyphs, key chords,
  min/max sizes) are public properties backed by private fields with sensible defaults, never
  `const`.
- **Nullability, guards, exceptions.** `<Nullable>enable</Nullable>`; guard-clause every
  public entry point; throw specific exception types (`ArgumentNullException`,
  `ArgumentException`) with messages that name the offending argument. Never `catch (Exception)`
  to swallow — the filesystem provider catches `UnauthorizedAccessException`/`IOException`
  specifically.
- **Async shape.** Any new async method takes a `CancellationToken` (unless the type already
  holds a `CancellationTokenSource`), uses `.ConfigureAwait(false)`, and checks cancellation at
  the obvious points. The lazy child provider is deliberately synchronous (directory
  enumeration is synchronous); an async provider overload is offered in §2.4 for callers that
  need it and follows the same token rule.
- **No `Console.*` in library code**, no `var`, prefer LINQ and `.Any()` over `.Count() > 0`,
  and use `.FirstOrDefault()` with a null check rather than `.First()`.

Widgets already in the tree (`ActionListView<T>`, `ReorderableList<T>`, `TextField`,
`DialogModal`) are treated as opaque and used through their documented surface only.

---

## Table of contents

- [Part 1 — `ListEditorModal<T>`](#part-1--listeditormodalt)
- [Part 2 — Hierarchical selection: `FileBrowser` & `Tree<T>` gaps](#part-2--hierarchical-selection-filebrowser--treet-gaps)
- [Part 3 — Cross-cutting TUIKit requirements](#part-3--cross-cutting-tuikit-requirements)
- [Part 4 — TUIKit.Example coverage](#part-4--tuikitexample-coverage)
- [Part 5 — Armor migration plan](#part-5--armor-migration-plan)
- [Part 6 — Delivery, phasing, versioning](#part-6--delivery-phasing-versioning)
- [Appendix — grounding notes](#appendix--grounding-notes-verified-against-srctuikit--083)

---

## Part 1 — `ListEditorModal<T>`

### 1.1 Motivation

Armor lets the user maintain a list of **exclude rules** — `*.docx`, `.git/`, `report.pdf`,
`re:^.*/cache/`. With no list-editor primitive, Armor drives a loop of `SelectModal` (the rule
list plus pseudo-rows for "Add", "Remove", "Done") and `PromptModal` (typing one rule). It
works, but it hops between modals and gives no feedback while you type. The experience the user
actually asked for is one screen: a legend of examples, each rule shown with a plain-English
description, and an inline field that validates and previews as you type.

The shape is general — tag editors, allow/deny lists, header lists, env-var lists, glob lists
all want the same thing — which is exactly why it belongs in TUIKit rather than in Armor.

### 1.2 Files

One type per file, all under `src/TUIKit/Modals/`:

| File | Type |
|---|---|
| `ListEditorModal.cs` | `ListEditorModal<T> : DialogModal` |
| `ListEditorOptions.cs` | `ListEditorOptions<T>` (behavior + key bindings) |
| `ParseResult.cs` | `ParseResult<T>` (readonly struct: parse outcome) |

### 1.3 Public API

```csharp
namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Widgets;

    /// A single-screen editor for an ordered list of T. Items are added through an inline
    /// text field (parsed, validated, and previewed live), the selected item can be removed,
    /// and — when enabled — items can be reordered. Enter finishes; Escape cancels. The result
    /// is delivered through Completion / ShowAsync&lt;IReadOnlyList&lt;T&gt;&gt;: the item list on
    /// finish, or null on cancel. This type is not thread-safe; drive it from the UI loop.
    public sealed class ListEditorModal<T> : DialogModal
    {
        /// Initializes a new editor.
        /// <param name="initialItems">Items to start from; copied, never mutated. Cannot be null.</param>
        /// <param name="display">Renders an item's primary text. Cannot be null.</param>
        /// <param name="options">Behavior and key bindings; defaults applied when null.</param>
        /// <exception cref="ArgumentNullException">initialItems or display is null.</exception>
        public ListEditorModal(
            IEnumerable<T> initialItems,
            Func<T, string> display,
            ListEditorOptions<T>? options = null);
    }
}
```

`ListEditorOptions<T>` carries the knobs (all public properties, backed by private fields, so
consumers override only what they need):

- `Func<string, ParseResult<T>>? Parse` — turns typed text into an item; **required to enable
  Add**. When null the editor is remove/reorder-only.
- `Func<T, string>? Describe` — secondary text shown after each item and as the live `→`
  preview while typing.
- `IReadOnlyList<string>? Help` — legend lines rendered above the list.
- `bool AllowReorder` (default `false`), `bool AllowDuplicates` (default `false`),
  `bool AllowEmpty` (default `true`).
- `IEqualityComparer<T>? DedupeComparer` — defaults to `EqualityComparer<T>.Default`.
- `string AddPrompt` (default `"New item"`), `string EmptyText` (default `"No items yet."`).
- `KeyChord AddKey` / `AddKeyAlt` / `RemoveKey` / `RemoveKeyAlt` — defaults `a` / `Insert` /
  `d` / `Delete`, parsed with `KeyChord.Parse`.

`ParseResult<T>` is a readonly struct with `bool Ok`, `T Value`, `string? Error`, and two
factory methods, `Success(T value)` and `Failure(string error)`. It exists precisely so the
add path can reject bad input with a message instead of returning a tuple.

### 1.4 Composition

`ListEditorModal<T>` subclasses `DialogModal` — it does not hand-draw a border or reimplement
Escape. It leans on what the widget tree already provides:

| Concern | Existing type | Hook used |
|---|---|---|
| Border, title, footer, sizing, Esc→cancel | `DialogModal` | override `RenderContent`, `MeasureContentWidth/Height`; call `HandleDismiss`, `Truncate` |
| Item list, selection, paging | `ActionListView<T>` (or `ReorderableList<T>` when `AllowReorder`) | `RegisterAction(chord, id, isEnabled)`, `Activated`; `MoveUp/MoveDown/RemoveSelected` |
| Inline text entry | `TextField` | `Value`, `HandleKey`, `OnFocusChanged` |
| Key parsing / labels | `KeyChord` | `Parse`, `FromKeyEvent`, `ToLabel` |

The modal owns a two-widget focus flip (list ⇄ field). If [R-X1](#part-3--cross-cutting-tuikit-requirements)
lands, even that glue moves into `DialogModal`.

### 1.5 Interaction, layout, validation

Two modes, matching Armor's proven flow. In **Browse** the list has focus: arrows move the
selection, the add chord opens the field, the remove chord drops the selected row, `Alt+Up`/
`Alt+Down` reorder when enabled, `Enter` finishes, `Escape` cancels the whole modal. In
**Edit(add)** the field has focus: printable keys and Backspace edit the buffer while a live
`→ describe(parse(buffer))` line (or a red error) tracks it, `Enter` commits, and `Escape`
returns to Browse without closing.

`RenderContent` fills the content rect top-down — legend, a blank line, `Items (N):`, the
scrolling list with the selected row highlighted, then (only while adding) the `Add:` field and
its preview/error line. The footer hint is mode-dependent. Commit runs `Parse`; on `Failure`
the buffer is kept so a bad regex like `re:[` can be fixed in place, and on `Success` the
dedupe policy is applied before the item is appended and selected.

The one rule worth stating plainly: **commit failure never discards the buffer.** Armor's users
lean on that to correct a mistyped rule without starting over.

### 1.6 Result and edge cases

Finish calls `Close` with a fresh `List<T>`; cancel calls `Close(null)` via `HandleDismiss`, so
`await app.ShowAsync<IReadOnlyList<T>>(modal)` yields the edited list or `null`. A `null` means
"leave the caller's data alone" — distinct from an empty list, which is a real "no items." The
input collection is copied and never mutated.

Edge behavior: an empty start shows `EmptyText` and still allows Add; finishing with zero items
is permitted only when `AllowEmpty`; `Parse == null` disables and hides the Add affordance;
values may contain spaces (single-field entry, no tokenizing); a too-small terminal is clamped
by `DialogModal` and the list still scrolls.

### 1.7 Work breakdown

1. `ParseResult.cs`, `ListEditorOptions.cs` — structs/options with full XML docs.
2. `ListEditorModal.cs` — fields (`_List`, `_Field`, `_Mode`, `_Error`, `_Items`, `_Options`,
   `_Display`); `HandleKey` routing by mode falling through to `HandleDismiss`; `RenderContent`
   + measure overrides.
3. Guided-tour + example-modal coverage (Part 4).
4. Touchstone suite + updates (§1.8).
5. `CHANGELOG.md` entry; minor version bump; a short section in `BUILDING_TERMINAL_APPS.md`.

### 1.8 Tests

New suite `src/Test.Shared/Suites/ListEditorModalSuite.cs`, registered in
`TUIKitSuites.All`, following the `ListEditingSuite` pattern (construct, drive with
`KeyEvent.Special`/`KeyEvent.Char`, assert with `Check.*`; render into a `BufferSurface` /
`CellBuffer` when asserting on-screen text). Positive and negative cases both:

| Case id | Direction | Asserts |
|---|---|---|
| `AddCommitsParsedItem` | + | type + `Enter` appends the parsed value and selects it |
| `AddMultiple` | + | two adds yield both, in entry order |
| `DescribePreviewShown` | + | while editing, the `→ describe(...)` preview renders |
| `RemoveSelected` | + | remove chord drops the selected row; selection clamps |
| `ReorderWhenEnabled` | + | `Alt+Up`/`Alt+Down` change order when `AllowReorder` |
| `FinishReturnsList` | + | `Enter` in Browse closes with the items in order |
| `CancelReturnsNull` | + | `Escape` in Browse closes with `null` (not an empty list) |
| `InvalidRejectedBufferKept` | − | `Parse` `Failure` shows the error, does **not** add, buffer retained |
| `DuplicateRejected` | − | a dup (per `DedupeComparer`) is refused when `AllowDuplicates=false` |
| `EscInEditReturnsToBrowse` | − | `Escape` while adding cancels the add only, modal stays open |
| `AddDisabledWhenNoParser` | − | with `Parse == null`, the add chord is a no-op / hidden |
| `EmptyDisallowedBlocksFinish` | − | with `AllowEmpty=false` and 0 items, `Enter` does not close |
| `NullArgsThrow` | − | null `initialItems` or `display` throws `ArgumentNullException` |

**Updated tests:** extend `ListEditingSuite` only if `ActionListView<T>`/`ReorderableList<T>`
gain members for this work; otherwise no edits there. Add a modal-level `HeadlessBackend` +
`FeedInput` script under `BackendModalValidationSuite` that opens the editor, types a value,
finishes, and asserts `Completion`.

### 1.9 Armor requirements

| # | Requirement | Why |
|---|---|---|
| R1 | Generic `T`; `display`, `Parse → ParseResult<T>`, optional `Describe`. | Item is `ExcludePattern`; parse is the token grammar; describe is the plain-English line. |
| R2 | Seed with `initialItems`. | Editing a policy pre-loads its manual rules. |
| R3 | Inline add with **live preview** and **inline error**; buffer kept on failure. | Regex feedback without leaving the screen. |
| R4 | Remove selected. | Prune a rule. |
| R5 | Values may contain spaces; no tokenizing. | File names with spaces are valid rules. |
| R6 | Finish → `IReadOnlyList<T>`; cancel → `null` (≠ empty). | Cancel must leave rules untouched; empty is a real state. |
| R7 | `AllowEmpty = true`. | "No excludes" is valid. |
| R8 | Dedupe (`AllowDuplicates=false` default). | Avoid duplicate rules. |
| R9 | Legend/help lines. | The user asked for built-in guidance. |
| R10 | Optional reorder, off by default. | Not needed for excludes; must not be forced. |
| R11 | Deterministic under `HeadlessBackend`/`FeedInput`. | Armor verifies flows headlessly in CI. |

---

## Part 2 — Hierarchical selection: `FileBrowser` & `Tree<T>` gaps

### 2.1 What Armor needs

"Select folders and files to back up" has to root at **every ready drive** (a forest), expand
**lazily** so a huge tree stays responsive, and shrug off unreadable directories. It offers
**cascading tri-state selection**: checking a folder pulls in its whole subtree; the user drills
in and unchecks specific descendants; ancestors of an unchecked hole render **partial**; a
node's effective state is inherited from its nearest explicit ancestor. The result is the set of
**top-most included paths** plus the **excluded holes**, which is what maps cleanly onto a backup
policy (include the folder, exclude the holes) and stays correct as files are added later. On
reopen it must **pre-seed** those includes and holes and **reveal** (expand to) each so the tree
looks exactly as the policy left it.

Neither shipping type does this. The reasons are specific, below.

### 2.2 `FileBrowser` gap analysis

`FileBrowser` (`src/TUIKit/Widgets/FileBrowser.cs`) is a single-directory, single-file
navigator — a `cd`-style pane. It lists one directory (parent link, folders, files), descends
on `Enter`, activates one file through `FileActivated`, and records the last `SelectedPath`.
Good at what it is; wrong shape for Armor:

- **G-FB1** No multi-select — one activated path, no selection set.
- **G-FB2** No checkboxes, no tri-state, no cascade.
- **G-FB3** Not a tree — one directory at a time, no indented hierarchy, no forest of roots.
- **G-FB4** No pre-seed and no reveal.
- **G-FB5** Result is a single path; no includes/excludes decomposition.
- **G-FB6** Only a `FileActivated(string)` event.
- **G-FB7** Listing controls (hidden files, dirs-only, ordering) are fixed.

The verdict is to leave `FileBrowser` as the flat picker it is — optionally growing a
`SelectionMode` flag, see [2.7](#27-design-decision-cardinality-vs-interaction-model) — and build
the hierarchical case separately.

### 2.3 `Tree<T>` gap analysis

`Tree<T>` (`src/TUIKit/Widgets/Tree.cs`) is closer — an expandable hierarchy with a single
selection — but has blocking gaps and two real performance bugs:

- **G-T1** Single root. The constructor is `Tree(T root, …)`; Armor needs a forest. A hidden
  super-root is a hack that still renders and participates.
- **G-T2** No selection/check state beyond `SelectedNode`. No multi-select, no tri-state, no
  partial, no inheritance.
- **G-T3** The `children` delegate is re-invoked constantly and never cached. `Rebuild()` walks
  every expanded node calling `_Children`, and `HasChildren` calls `_Children` **again, once per
  visible row per render**, just to choose the disclosure glyph. Backed by a filesystem that is
  a directory enumeration per row per frame — unusable at Armor's scale.
- **G-T4** Expansion state lives in a `HashSet<T>`, keyed by `T` identity. Filesystem nodes
  recreated on each enumeration lose their expanded state unless `T` has value equality.
- **G-T5** `Enter` is bound to expand (aliases `Right`), so a host cannot use `Enter` for
  confirm or `Space` for toggle without fighting the widget.
- **G-T6** No reveal-to-node — `Expand(node)` exists, but nothing expands the ancestor chain
  down to a node, which is exactly what pre-seeding needs.
- **G-T7** No per-row decoration — rendering is `indent + disclosure + label`; a check glyph can
  only be smuggled into the label, and rows cannot be styled by state.
- **G-T8** No `Expanded`, `SelectionChanged`, or `CheckChanged` events.
- **G-T9** No result derivation (top-most checked, top-most unchecked-under-checked).

G-T3 and G-T4 are correctness/performance defects for *any* large or generated tree, not just
Armor's — they should be fixed on `Tree<T>` itself even if `CheckTree<T>` becomes the selection
widget. The rest are best served by a sibling type rather than by overloading `Tree<T>` with a
selection model it was not built for.

### 2.4 Proposal: `CheckTree<T>` widget

A forest tree with lazy, cached children and cascading tri-state selection. One type per file
under `src/TUIKit/Widgets/`:

| File | Type |
|---|---|
| `CheckTree.cs` | `CheckTree<T> : IWidget, IFocusable, IFocusAware` |
| `CheckState.cs` | `CheckState` enum (`Unchecked`, `Checked`, `Partial`) |

```csharp
namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// A forest of expandable nodes with cascading tri-state checkboxes. Checking a node makes
    /// its subtree effectively checked; unchecking a descendant carves a hole and marks
    /// ancestors Partial. Effective state is inherited from the nearest explicit ancestor,
    /// defaulting to unchecked at a root. Children are loaded lazily and cached (see the
    /// performance contract). State is keyed through the supplied comparer, so regenerated
    /// nodes keep their expansion and checks. Not thread-safe.
    public sealed class CheckTree<T> : IWidget, IFocusable, IFocusAware
        where T : notnull
    {
        /// <param name="roots">The forest roots (e.g. drive roots). Cannot be null or empty.</param>
        /// <param name="children">Lazy child provider; invoked at most once per node and cached. Cannot be null.</param>
        /// <param name="label">Renders a node's text. Cannot be null.</param>
        /// <param name="hasChildren">Cheap "can expand?" test (e.g. is-a-directory) avoiding enumeration; when null, falls back to a single cached probe of children.</param>
        /// <param name="comparer">Equality for keyed state; defaults to EqualityComparer&lt;T&gt;.Default.</param>
        /// <exception cref="ArgumentNullException">roots, children, or label is null.</exception>
        /// <exception cref="ArgumentException">roots is empty.</exception>
        public CheckTree(
            IReadOnlyList<T> roots,
            Func<T, IEnumerable<T>> children,
            Func<T, string> label,
            Func<T, bool>? hasChildren = null,
            IEqualityComparer<T>? comparer = null);

        // Tri-state (G-T2)
        public void SetExplicit(T node, bool? state);        // true/false override; null = inherit
        public bool EffectiveChecked(T node);                // nearest explicit ancestor; default false
        public CheckState State(T node);                     // Checked / Unchecked / Partial (loaded descendants)
        public void ToggleAt(T node);                        // flip effective; clear loaded descendant overrides

        // Result derivation (G-T9)
        public IReadOnlyList<T> IncludedRoots();             // top-most effectively-checked
        public IReadOnlyList<T> ExcludedHoles();             // top-most unchecked under a checked ancestor

        // Navigation & reveal (G-T6)
        public T SelectedNode { get; }
        public void Expand(T node);
        public void Collapse(T node);
        public bool IsExpanded(T node);
        public void RevealTo(T node);                        // load + expand the ancestor chain to node

        // Decoration & theming (G-T7) — configurable, not const
        public string CheckedGlyph { get; set; }             // default "[x] "
        public string UncheckedGlyph { get; set; }           // default "[ ] "
        public string PartialGlyph { get; set; }             // default "[~] "
        public Func<T, CellStyle>? RowStyle { get; set; }

        // Events (G-T8)
        public event Action<T>? Expanded;                    // fires once when a node is first expanded/loaded
        public event Action? CheckChanged;
        public event Action? SelectionChanged;

        // IWidget / IFocusable / IFocusAware
        public bool HandleKey(KeyEvent key);                 // Space toggles; Enter is NOT consumed (G-T5)
        public Size Measure(Size available);
        public void Render(ISurface surface);
        public void OnFocusChanged(bool focused);
    }
}
```

`ExcludedHoles()` returns the hole nodes as plain `T`. "Is it a directory?" is domain knowledge
a generic tree cannot answer, so that flag is added downstream: for the filesystem case `T` is
`string` and `FileSelectModal` maps each hole to a `FileExclusion` (path + `IsDirectory`, §2.5)
using its provider — no tuples anywhere in the chain.

Semantics reproduce Armor's proven model exactly. `SetExplicit` sets an override; a node with
no override inherits its nearest explicit ancestor; roots default unchecked. `ToggleAt` sets the
node's explicit state to `!EffectiveChecked`, clears explicit overrides on its **loaded**
descendants so the subtree inherits cleanly, and auto-expands one level when newly checked so
the now-checked children are visible. A folder is **Partial** when its loaded descendants differ
in effective state from its own — only loaded nodes are inspected, which keeps the check cheap
and correct because an unopened branch inherits uniformly. `IncludedRoots`/`ExcludedHoles` are a
single depth-first pass collecting the top of each checked subtree and the top of each hole
beneath it.

**Performance contract (also the fix for `Tree<T>` G-T3):**

- **P1** `children(node)` is invoked at most once per node and memoized until an explicit
  `Invalidate(node)` / `Refresh()`. No per-render enumeration.
- **P2** The disclosure glyph uses `hasChildren` (O(1) "is a directory"); it never enumerates.
  When `hasChildren` is null, probe `children` once and cache the boolean.
- **P3** Every keyed structure (expansion set, explicit overrides, child cache) goes through
  `comparer`.

An **async child provider** overload is offered for callers whose backing store is async I/O:
`Func<T, CancellationToken, Task<IReadOnlyList<T>>>`, awaited on first expansion, cached
identically. The filesystem provider stays synchronous because directory enumeration is; the
async overload exists to satisfy CODE_STYLE's async-variant rule for providers that need it, not
because the filesystem case does.

### 2.5 Proposal: `FileSelectModal`

A `DialogModal` wrapping `CheckTree<string>` (nodes are absolute paths; comparer is the
platform's path comparer) with a filesystem provider and result mapping. One type per file under
`src/TUIKit/Modals/`:

| File | Type |
|---|---|
| `FileSelectModal.cs` | `FileSelectModal : DialogModal` |
| `FileSelectOptions.cs` | `FileSelectOptions` (roots, pre-checks, show-files/hidden, title) |
| `FileSelection.cs` | `FileSelection` (includes + excludes) |
| `FileExclusion.cs` | `FileExclusion` (readonly struct: path + `IsDirectory`) |

`FileSelectModal`'s `Completion` / `ShowAsync<FileSelection>` yields a `FileSelection` on finish
or `null` on cancel. `FileSelection` exposes `IReadOnlyList<string> Includes` and
`IReadOnlyList<FileExclusion> Excludes`. `FileSelectOptions` carries `Roots` (default: ready
drives on Windows, `/` on Unix), `PreCheckedIncludes`, `PreCheckedExcludes`, `ShowFiles`
(default true), `ShowHidden` (default true), and `Title`.

TUIKit owns the behavior every consumer would otherwise re-derive: roots default to the machine's
ready drives with volume labels when available; the lazy child set is sorted `GetDirectories`
then `GetFiles`, each wrapped so `UnauthorizedAccessException` / `IOException` yields an empty
child set and raises the `Expanded` event with a non-fatal note rather than throwing;
`hasChildren` is "is a directory" with no enumeration; the path comparer is `OrdinalIgnoreCase`
on Windows and `Ordinal` elsewhere, with `GetFullPath` and trailing-separator normalization so
pre-seed keys match enumerated nodes; each pre-checked include and hole is revealed on open and
the cursor lands on the first include; the result is `IncludedRoots()` + `ExcludedHoles()` mapped
into `FileSelection`.

### 2.6 Armor requirements

| # | Requirement | Closes |
|---|---|---|
| R12 | Forest roots = ready drives (or caller-supplied). | G-T1 / G-FB3 |
| R13 | Lazy, cached children; unreadable dirs tolerated. | P1/P2, G-T3 / G-FB7 |
| R14 | Cascading tri-state with Partial and nearest-ancestor inheritance. | G-T2 |
| R15 | Result = top-most includes + excluded holes (dir/file flag). | G-T9 / G-FB5 |
| R16 | Pre-seed includes **and** holes, and reveal each. | G-T6 / G-FB4 |
| R17 | Path-keyed state via comparer; regenerated nodes keep state. | G-T4 |
| R18 | `Space` toggles; `Enter` free for the host to confirm. | G-T5 |
| R19 | Per-row check glyph column + per-node style + Partial glyph. | G-T7 |
| R20 | `Expanded` / `CheckChanged` events. | G-T8 |
| R21 | Multi-select across the whole tree. | G-FB1 |
| R22 | Deterministic under `HeadlessBackend`/`FeedInput` with an in-memory provider. | testing |

### 2.7 Design decision: cardinality vs. interaction model

Should file selection split into `SingleFileBrowser` / `MultiFileBrowser`, or be one component
with a multi-select flag? Neither, quite. Single-versus-multi is not the axis that actually
varies; interaction model is. Two orthogonal axes exist:

|              | Flat navigator (cd-style)                | Hierarchical tree                          |
|--------------|------------------------------------------|--------------------------------------------|
| **Single**   | today's `FileBrowser`                    | "pick one node in a tree"                  |
| **Multiple** | check several items in the *current dir* | Armor: cascading subtree include/exclude   |

A `Multiple` flag on `FileBrowser` only buys flat multi-select *within one directory at a time* —
not cross-directory subtree selection. The cascade is a different interaction model and a
different result shape, so it is a different widget regardless of any flag.

`SingleFileBrowser`/`MultiFileBrowser` twins are the wrong move: cardinality is a poor seam for a
*type* split, forcing two near-identical types that duplicate all the directory/roots/error logic
for one boolean. Cardinality *is* the right axis for a flag, but only inside the flat picker,
where single and multi share one interaction model and one result type:

```csharp
namespace TUIKit.Widgets
{
    public enum FileSelectionMode { None, Single, Multiple }   // its own file
}

// FileBrowser gains (uniform result, one interaction model):
//   FileSelectionMode SelectionMode { get; set; }            // default None
//   IReadOnlyList<string> SelectedPaths { get; }             // 0..1 Single, 0..n Multiple
//   string? SelectedPath => SelectedPaths.Count > 0 ? SelectedPaths[0] : null;
//   event Action<IReadOnlyList<string>>? Confirmed;
```

The hierarchical selector stays its own type (`CheckTree<T>` + `FileSelectModal`) because its
result type (`FileSelection`) and interaction model differ; bolting it onto `FileBrowser` behind
a flag would give it a result property whose type changes by mode. Shared OS logic (list a
directory, enumerate ready drives, hidden-file policy, `UnauthorizedAccess`/`IOException`
tolerance) belongs in one internal helper — `FileSystemProvider` — consumed by both `FileBrowser`
and `FileSelectModal`'s `CheckTree` provider, so the split costs no duplication.

To summarize the seam: cardinality → a `SelectionMode` flag on the flat `FileBrowser`;
flat-versus-tree → distinct types. Armor uses `FileSelectModal`; the `FileBrowser` flag is a nice
independent win but not a substitute for it. None of this changes R12–R22.

### 2.8 Tests

Two new suites plus targeted updates to the suites that already touch these widgets. Filesystem
determinism comes from a tiny **in-memory provider** used only in tests — a `children` function
over a `Dictionary<string, string[]>` — so the cases run identically on any OS and never touch
disk. That provider is test-local (in `Test.Shared`), not shipped.

**New — `src/Test.Shared/Suites/CheckTreeSuite.cs`** (registered in `TUIKitSuites.All`):

| Case id | Direction | Asserts |
|---|---|---|
| `CheckFolderCascades` | + | checking a folder makes children `EffectiveChecked` |
| `UncheckDescendantMakesHole` | + | unchecking a child → child unchecked, parent `Partial`, sibling checked |
| `IncludedRootsTopMost` | + | `IncludedRoots()` returns the folder, not each child |
| `ExcludedHolesReported` | + | `ExcludedHoles()` returns the hole with the right `IsDirectory` |
| `RevealToExpandsChain` | + | `RevealTo(node)` loads and expands every ancestor |
| `StateSurvivesRegeneratedNodes` | + | with a value comparer, re-created nodes keep checks/expansion (G-T4) |
| `PartialOnlyFromLoaded` | + | an unopened branch never forces `Partial` |
| `SpaceTogglesEnterIgnored` | + | `Space` toggles; `HandleKey(Enter)` returns false (G-T5) |
| `LazyChildrenCachedOncePerNode` | − | a counting provider is invoked once per node across many renders (P1/P2) |
| `EmptyRootsThrows` | − | empty `roots` throws `ArgumentException` |
| `NullArgsThrow` | − | null `roots`/`children`/`label` throw `ArgumentNullException` |
| `UnreadableChildrenTolerated` | − | a provider that throws `IOException` yields an empty child set, no crash |

**New — `src/Test.Shared/Suites/FileSelectModalSuite.cs`** (in-memory provider; registered):

| Case id | Direction | Asserts |
|---|---|---|
| `PreSeedRevealsIncludesAndHoles` | + | opens with includes checked, holes `[ ]`, ancestors `Partial` |
| `ResultMapsToFileSelection` | + | finish → `Includes` + `Excludes` (with `IsDirectory`) match the tree |
| `CancelReturnsNull` | + | `Escape` closes with `null` |
| `ShowFilesFalseHidesFiles` | + | folders-only listing when `ShowFiles=false` |
| `RootsDefaultToDrives` | + | default roots come from the provider's root set |
| `PreSeedMissingPathIgnored` | − | a pre-checked path that no longer exists is skipped, no throw |
| `NoSelectionFinishEmpty` | − | finishing with nothing checked yields empty `Includes` |

**Updated:**

- `src/Test.Shared/Suites/NewWidgetsSuite.cs` and `NavigationKeysSuite.cs` — add cases for the
  `Tree<T>` fixes: `HasChildrenUsesCheapProbe` (children delegate not called per render once a
  cheap probe is supplied), and `ExpansionStateByComparer` (expansion keyed by comparer, not
  identity). These pin G-T3/G-T4 on the base widget.
- `src/Test.Shared/Suites/SplitMenuFileSuite.cs` — add `SelectionModeSingle`,
  `SelectionModeMultiple` (Space checks several paths; `SelectedPaths` reflects them;
  `Confirmed` fires on Enter) and `SelectionModeNoneKeepsActivate` (default behavior unchanged),
  covering the `FileBrowser` flag from §2.7 without breaking existing cases.

Both new suites use `Check.True`/`Check.Equal`/`Check.Throws`/`Check.ThrowsAsync` and drive
widgets with `KeyEvent.Special`/`KeyEvent.Char`; the modal suites additionally use
`HeadlessBackend.FeedInput` and read back through `BufferSurface`/`CellBuffer.Get`, matching the
existing `BackendModalValidationSuite` style.

---

## Part 3 — Cross-cutting TUIKit requirements

A few things make both new types robust and let Armor drop its remaining glue. **R-X1** is a
lightweight way for a `DialogModal` to host one or more focusable child widgets (a list plus a
text field) and route keys and focus, so `ListEditorModal` and `FileSelectModal` do not each
re-implement a two-widget focus ring; if it does not land, they manage it internally, which is
acceptable but repeated. **R-X2** requires an `IEqualityComparer<T>` on `Tree<T>`/`CheckTree<T>`
for all keyed state, closing the identity trap for generated graphs. **R-X3** is the widget
contract that no user delegate is called work-proportional-to-the-tree on every render (the G-T3
fix), documented so future widgets do not reintroduce it. **R-X4** keeps the generic
`ShowAsync<TResult>(Modal)` path so both modals return strongly-typed results and `null` on
cancel. **R-X5** is theming — honor `DialogModal.BorderStyleColor`/`BackgroundStyle` and expose
the glyph/style knobs so a host matches its palette. **R-X6** is parity with the repo's own norm:
full XML docs and a working example page for each new type.

---

## Part 4 — TUIKit.Example coverage

The guided tour (`src/TUIKit.Example/GuidedTour.cs`) hosts an `IWidget` per `TourPage`
(title, description, live demo, code snippet), so it fits widgets directly and modals through a
launcher. Concretely:

- **`CheckTree<T>` — a guided-tour page.** Add a `TourPage` in `GuidedTour.BuildPages()` whose
  `Demo` is a `CheckTree<string>` over a small in-memory forest (`root → { docs, src → { a, b },
  bin }`), with a code snippet showing construction, `hasChildren`, `Space` to toggle, and
  reading `IncludedRoots()`. Because the tour already forwards keys to a focusable demo, checking
  and expanding work live.
- **`ListEditorModal<T>` — an example modal.** Follow the existing `SettingsModal` / `ChoiceModal`
  pattern (a standalone example file plus a launcher entry in the harness): a `string` tag editor
  with a legend and a validating parser, launched with `ShowAsync<IReadOnlyList<string>>`, and the
  result echoed into the harness log. New file `src/TUIKit.Example/ListEditorExample.cs`.
- **`FileSelectModal` — an example modal.** Same launcher pattern, backed by the in-memory
  provider so the example is deterministic and does not depend on the host filesystem; show the
  returned `FileSelection` (includes + holes) in the log. New file
  `src/TUIKit.Example/FileSelectExample.cs`.
- **`FileBrowser.SelectionMode` — extend the existing FileBrowser tour/demo** (in
  `SplitMenuFileSuite`'s companion demo, or the file page of the tour) to toggle
  `Single`/`Multiple` and display `SelectedPaths`.

Each example carries the same doc-comment discipline as the rest of `TUIKit.Example` and appears
in its `README.md` feature list.

---

## Part 5 — Armor migration plan

With the above in place, Armor deletes both bespoke modals and keeps only its domain logic.

| Armor today | Replaced by | Adapter Armor supplies |
|---|---|---|
| exclude editor loop (`SelectModal`+`PromptModal`) | `ListEditorModal<ExcludePattern>` | `display` = token form; `Parse` = token→`ExcludePattern` + regex validation → `ParseResult`; `Describe` = the plain-English line; `Help` = the examples block. |
| `FileBrowserModal` (custom `Modal`, ~500 lines) | `FileSelectModal` | map `FileSelection.Includes` → `Policy.IncludePaths`, `Excludes` → Armor's `(?#armor)` exclude encoding; on edit pass `PreCheckedIncludes`/`PreCheckedExcludes` from the policy. |

Armor keeps the pattern grammar and the `(?#armor)` exclude encoding; everything about drawing,
scrolling, revealing, and focus moves into TUIKit. Adoption is accepted when R1–R22 are green
under Armor's existing headless end-to-end tests, which already drive the app through
`HeadlessBackend.FeedInput` and assert on the database.

## Part 6 — Delivery, phasing, versioning

Ship in an order that unblocks Armor early and keeps each step independently releasable. Phase 1
is `ListEditorModal<T>` with its suite and example — small, and it retires Armor's exclude loop
on its own. Phase 2 is the `Tree<T>` correctness and performance fixes (`hasChildren`, child
caching, `IEqualityComparer<T>`), which stand alone as bug fixes for any large or dynamic tree.
Phase 3 is `CheckTree<T>`, and Phase 4 is `FileSelectModal` on top of it plus the
`FileBrowser.SelectionMode` flag. Each phase adds its suites to `TUIKitSuites.All`, its example,
a `CHANGELOG.md` entry, and a minor version bump; all additions are source-compatible
(`Tree<T>`/`FileBrowser` gain optional constructor parameters and members, nothing is removed).

The through-line is smaller than it looks on the page: two reusable modals and one selection
widget, each already 80% present in TUIKit's primitives, replacing several hundred lines of
per-app modal code — and pinning two `Tree<T>` performance bugs that would eventually bite any
consumer with a large tree, Armor or not.

---

## Appendix — grounding notes (verified against `src/TUIKit` @ 0.8.3)

- `DialogModal` exposes `RenderContent(ISurface)`, `MeasureContentWidth/Height`,
  `HandleDismiss(KeyEvent, object cancelResult)`, `Truncate`, plus Title/FooterHint/size/style
  properties — the subclassing surface for both new modals.
- `ActionListView<T>` has `RegisterAction(KeyChord, string, Func<T,bool>)`, `Activated`, and a
  static `ActivateActionId`; `ReorderableList<T>` has `MoveUp/MoveDown/RemoveSelected` with
  `Reordered`/`Removed`; `TextField` has `Value`/`HandleKey`/`OnFocusChanged`; `CheckList<T>`
  shows a flat checkbox list (`SetChecked`, `CheckedItems`) — a useful reference for glyph/state
  handling, but flat, not hierarchical.
- `Tree<T>` confirmed: single `_Root`; `_Expanded` is a `HashSet<T>`; `Rebuild()` and
  `HasChildren()` both call the `children` delegate on every render; `Enter` aliases `Right`.
  These are the concrete sources of G-T1/G-T3/G-T4/G-T5.
- `FileBrowser` confirmed: single-directory listing, one activated `SelectedPath`, `Left`/
  `Backspace` go up, no tree, no multi-select — the sources of G-FB1–G-FB7.
- Test conventions: `TUIKitSuites.All` is the suite registry; suites are
  `TestSuiteDescriptor`/`TestCaseDescriptor` with `Check.True/False/Equal/Throws/ThrowsAsync`,
  driven by `KeyEvent.Special`/`KeyEvent.Char` and, for modals, `HeadlessBackend.FeedInput` with
  `BufferSurface`/`CellBuffer.Get`. Existing `ListEditingSuite`, `NewWidgetsSuite`,
  `NavigationKeysSuite`, and `SplitMenuFileSuite` are the ones this plan extends.
- Example conventions: `GuidedTour.BuildPages()` registers `TourPage(title, description, IWidget
  demo, code[])`; example modals follow `SettingsModal`/`ChoiceModal`.
