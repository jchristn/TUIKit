# TUIKit Improvements: MarkdownView for Real Apps (1.7)

TUIKit 1.6 added a width-aware Markdown engine and a `MarkdownView` widget to show its output. The engine is complete.
The widget is a display surface only:

- it cannot take focus, so a keyboard user cannot scroll it;
- it has no search;
- it ignores the application theme;
- it cannot follow a growing document.

Apps that adopt the engine end up wrapping it or writing their own viewer.

Armada (`~/Code/Armada`, the `armada tui` client) adopted 1.6.0 across its whole TUI the day it shipped. It could
render through `MarkdownRenderer.Render(markdown, width, options)` everywhere, but it could not use `MarkdownView`. It
kept its own viewer and wrote these pieces on top of TUIKit:

- keyboard scrolling, focus, and key hints (`Armada.Tui.Widgets.ScrollTextView`);
- search with next and previous match;
- styles derived from the app theme, keeping foreground colors only so a dialog's or cursor row's background shows
  through, plus the ASCII glyph preset when the theme is ASCII (`Armada.Tui.Text.ArmadaMarkdown`);
- a copy of `StreamingTranscript`'s private rule that holds back a half-received table row
  (`ArmadaMarkdown.Streaming`);
- follow-the-tail for a log rendered as Markdown (`Armada.Tui.Widgets.LogViewer.Markdown`);
- three near-identical layout caches keyed on text, width, and theme, one per transcript or list.

Every one of those is a need any TUIKit app has once Markdown is longer than a screen or arrives over time. This plan
moves the general half into TUIKit.

Three rules shape every item:

1. **Additive and incremental.** No public member is removed, renamed, or changes meaning. New behavior is opt-in
   wherever turning it on by default would change how an existing 1.6 app looks or traverses focus. Each such default
   is called out under "Compatibility" in its item.
2. **Horizontal, not vertical.** Each capability serves any app that shows Markdown: help pages, details panels,
   modal bodies, chat transcripts, release notes, and logs. No Armada concept appears in a type, member, or doc comment.
   Armada's code is cited only to show the shape that survived real use.
3. **Reuse TUIKit's own vocabulary.** Where TUIKit already solved the same problem for another widget, the new API
   copies those names and semantics: `Pane.SetSearch`/`FindNext`/`FindPrevious`, `Pane.TailFollow`, `DiffView`'s
   scrolling keys, `IFocusStop`, `IKeyHintSource`, `IThemeable`, and `DialogModal.UseThemeFocusStyles`. An app that
   knows one widget already knows the other.

Requirements in `~/Code/Agents/requirements` are authoritative, especially `CODE_STYLE.md`, `VERSIONING.md`,
`BACKEND_TEST_ARCHITECTURE.md`, `REPOSITORY_REQUIREMENTS.md`, and `WRITING_DOCUMENTS.md` (no em dashes, anywhere). The
repository's `CLAUDE.md` restates the code style rules for every `.cs` file. The "How to use this plan" rules of
`archive/IMPROVEMENTS_FOCUS_HINTS_TOASTS.md` apply here unchanged. Each item is a full product: code with XML docs,
positive and negative Touchstone coverage, `docs/SURFACE_COVERAGE.md` rows, an example, and README and CHANGELOG
updates, all in the same release.

Code references were checked against `main` at 1.6.0 (commit `d0e8cc3`). Re-check each one when its item starts.

---

## Summary

Simplicity scores ease of integration into TUIKit: 10 is small, additive, and isolated, and 1 is invasive. Value
scores how much a typical TUIKit app gains. Rows are ordered by total, highest first; ties go to value, then
simplicity.

| ID | Item | Simplicity | Value | Total | Status |
|----|------|-----------:|------:|------:|--------|
| V1 | Keyboard scrolling, opt-in focus, and key hints | 8 | 9 | 17 | DONE |
| V2 | Theme-derived Markdown styles and glyphs | 8 | 8 | 16 | DONE |
| V3 | Public streaming-safe prefix | 9 | 6 | 15 | DONE |
| V4 | Search that matches `Pane` | 7 | 7 | 14 | DONE |
| V5 | Tail-follow for growing Markdown | 7 | 6 | 13 | DONE |
| V6 | Markdown layout cache | 8 | 5 | 13 | DONE |

V4 and V5 build on V1's viewport bookkeeping (last rendered height, clamped offset), so V1 lands first. V3 and V6 are
independent and can land at any point.

### Release at a glance

| Phase | Title | Items | Status |
|---|---|---|---|
| 0 | Release setup | branch, version proposal | DONE |
| 1 | Interaction and theme | V1, V2 | DONE |
| 2 | Streaming and search | V3, V4 | DONE |
| 3 | Growing documents | V5, V6 | DONE |
| 4 | Example application and docs | E1, E2 | DONE |
| 5 | Verification and downstream check | | DONE |

---

## Phase 0: Release setup

**Status:** DONE

- [x] Start from a clean, current `main` that builds and passes the full suite at 1.6.0. Create `feature/v1.7.0`.
- [x] **Version proposal (requires owner approval, `VERSIONING.md` section 4).** Every item is additive and every
      behavior change is opt-in, so the proposed version is `1.7.0` (MINOR). This depends on V1's region auto-focus
      gate: without it, binding a `MarkdownView` region first would move initial focus in existing apps. The listed
      "Changed" entries (V1 region click, V3 header hold and four-space `|`, V4 `HighContrast` underline) are
      presented with the proposal. Record the decision and date here before
      touching any version field.
      Approved version: 1.7.0  Approved by/on: owner (Joel Christner), 2026-10-14, in the release request
- [x] Once approved, set `Version`, `AssemblyVersion`, and `FileVersion` in `src/TUIKit/TUIKit.csproj`, update
      `PackageReleaseNotes`, and open `## [1.7.0] - YYYY-MM-DD` under `## [Unreleased]` in `CHANGELOG.md`.

---

## Phase 1: Interaction and theme

### V1: Keyboard scrolling, opt-in focus, and key hints

**Status:** DONE  **Simplicity 8, Value 9**

`MarkdownView` implements `IWidget`, `IScrollExtent`, and `IMouseAware`. It scrolls only with the mouse wheel or by
setting `ScrollOffset`. A keyboard-only user cannot read past the first screen of a help page or a long modal body,
and an app has to wrap the view in its own focusable widget to fix that. `DiffView` already solves the same problem
for diffs with `IFocusable` and Up/Down, PageUp/PageDown, and Home/End.

**Design.**

- `MarkdownView` additionally implements `IFocusable`, `IFocusStop`, and `IKeyHintSource`.
- `HandleKey` binds the same keys as `DiffView`:
  - Up and Down move one row;
  - PageUp and PageDown move the last rendered height minus one row (at least one);
  - Home and End go to the top and bottom.

  The key set matches `DiffView`, but two semantics deliberately differ. `DiffView` pages by a fixed 10 rows and
  consumes every navigation key (`Widgets/DiffView.cs`). `MarkdownView` pages by its viewport, and it consumes a key
  only when the key moved the view or the content is taller than the viewport, so a short document never swallows
  keys a parent container wants. All other keys return false.
- New `public bool IsFocusStop { get; set; }`, default **false**. It is part of `IFocusStop`: false means Tab and
  Shift+Tab skip the view, as they do today, while `FocusScope.SetFocus` and a click can still focus it. An app sets it
  to true to make the view a Tab stop.
- New `public bool ShowKeyHints { get; set; }`, default true. While focused and taller than its viewport, the view
  returns hints for its keys from `GetKeyHints()`: "Up/Down Scroll", "PgUp/PgDn Page", and "Home/End Top/Bottom", built
  with `KeyHint` and the existing key labels. Otherwise it returns null. False always returns null.
- New public methods `ScrollBy(int rows)`, `ScrollToTop()`, and `ScrollToEnd()`, plus a read-only
  `int ViewportHeight` (the last rendered height, 0 before the first render). They are clamped like `Render` clamps
  `ScrollOffset`, so an app's own key bindings (for example `j`/`k`) can drive the view without reaching into layout.
- Mouse wheel scrolling and the `ScrollOffset` setter clamp immediately, against the last layout and
  `ViewportHeight`, rather than at the next render. A burst of wheel-down events or an assignment past the end then
  does not make the next wheel-up look dead. Before the first render (`ViewportHeight` is 0) the setter stores the
  value as today and `Render` clamps it.
- Region auto-focus respects `IsFocusStop`. Today `TuiApplication.SetRegionContent` adds any `IFocusable` region
  widget to the focus order and, when nothing is focused yet, focuses it, without checking `IsFocusStop`
  (`Hosting/TuiApplication.cs`, the `widget is IFocusable` block). Left as is, a 1.6 app that binds a `MarkdownView`
  region before its input region would start with focus on the Markdown. The auto-focus step changes to skip a
  widget that is an `IFocusStop` with `IsFocusStop` false. (Gating on `IsRegionTabStop` was tried first and
  rejected: it also excludes empty containers, which changed initial focus for an empty `ButtonRow` and failed the
  existing focus-audit test.) The widget still joins the focus order, so `SetFocus` and click-to-focus
  keep working. No existing widget is affected, because the only current `IFocusStop` implementer is the internal
  `FramedStackSlot`, which is never bound to a region directly.

**Compatibility.** `IsFocusStop` defaults to false, and region auto-focus skips non-stops, so focus traversal and
initial focus in an existing 1.6 layout are unchanged. The one observable change is that a click on a view bound
directly to a region can now focus it, because it is `IFocusable` and `TuiApplication` focuses an `IFocusable`
region widget on a press (`RouteMouse`). Wheel events are `MouseEventKind.Wheel`, not `Press`, so wheel scrolling
never moves focus. A view nested inside a `FocusScope` is not focused by a click, because `FocusScope` has no mouse
handling; that is unchanged and out of scope here. The region click change matches `DiffView` and is listed under
"Changed" in the CHANGELOG.

**Tasks.**

- [x] Implement the interfaces, keys, `IsFocusStop`, `ShowKeyHints`, the scroll methods, and `ViewportHeight` with
      full XML docs (defaults, ranges, thread-safety: UI thread only, as today).
- [x] Gate region auto-focus in `Bind` on an explicit `IsFocusStop` false, and document in its XML comment that a
      non-stop widget joins the focus order but never receives initial focus.
- [x] Clarify in the `ContentHeight` doc comment that it reflects the last layout and is 0 before the first render or
      `RowsAt` call, and point callers that need the height up front to `RowsAt(width).Count`.
- [x] Tests: each key moves the expected rows at a known size; keys on a document shorter than the viewport return
      false; `IsFocusStop` false is skipped by `FocusScope` Tab traversal and true is not; `SetFocus` works either way;
      hints appear only while focused and scrollable and never when `ShowKeyHints` is false; `ScrollBy` clamps at both
      ends; wheel-down past the end then one wheel-up moves the view; setting `ScrollOffset` past the end after a
      render then one wheel-up moves the view; a non-stop `MarkdownView` region bound first does not take initial
      focus and the next tab-stop region does; a press on a region-bound view focuses it and a wheel event does not.

### V2: Theme-derived Markdown styles and glyphs

**Status:** DONE  **Simplicity 8, Value 8**

`MarkdownStyles.FromTheme(Theme)` exists. It is not enough for a themed app, for three reasons:

- **Backgrounds come along.** The theme's styles carry the theme background (the built-in themes build `Text`,
  `Accent`, `Muted`, and the rest with `Styled(foreground, background)`), and `FromTheme` copies them as they are, so
  Markdown drawn inside a dialog, a selected row, or a sidebar with a different background paints stripes of the main
  background. Armada had to rebuild every style keeping only the foreground and attributes.
- **ASCII is a separate step.** The ASCII glyphs are a separate preset (`MarkdownRenderOptions.Ascii()`). A theme with
  `UseAsciiBorders` does not reach Markdown tables, bullets, quotes, or rules unless every app wires it by hand.
- **No theme hook on the view.** `MarkdownView` does not implement `IThemeable`, so `ThemeApplier` passes it by.

**Design.**

- New overload `MarkdownStyles.FromTheme(Theme theme, bool includeBackgrounds)`. `includeBackgrounds: false` keeps
  each style's foreground and attributes and leaves the background at `Color.Default`, so the surface underneath
  shows through. The existing one-argument overload is unchanged and equals `includeBackgrounds: true`.
- New `MarkdownRenderOptions WithTheme(Theme theme)`. It returns a clone of the options with `Styles` set to
  `MarkdownStyles.FromTheme(theme, includeBackgrounds: false)`. When `theme.UseAsciiBorders` is true, it also applies
  the glyph fields that `Ascii()` sets (bullets, task glyphs, quote prefix, rule glyph, table glyphs). Layout settings
  (widths, overflow, spacing, table sizing) are untouched. This is the one call a themed app needs for both the view
  and direct `MarkdownRenderer.Render` calls.
- `WithTheme` is not reversible: on a non-ASCII theme it leaves the receiver's glyphs as they are, so options that
  already carry the ASCII glyphs keep them. The XML doc says to call it on the caller's own base options each time,
  never on a previous `WithTheme` result.
- `MarkdownView` implements `IThemeable`. New `public bool UseThemeStyles { get; set; }`, default **false**, named
  after `DialogModal.UseThemeFocusStyles`. The view keeps the caller's options (the value last assigned to `Options`,
  or the constructor's) as its base. While true, `ApplyTheme` renders with `baseOptions.WithTheme(theme)` and
  invalidates the layout, so switching from an ASCII theme back to a Unicode one restores the base glyphs instead of
  stacking on the previous theme's result. While false, `ApplyTheme` only records the theme, so turning the property
  on later applies the recorded theme at once, and turning it off returns to the base options. The `Options` getter
  returns the base options, so a caller reading and reassigning it never captures themed values. `Background` stays
  caller-owned and is never changed by the theme.

**Compatibility.** Both defaults keep 1.6 output byte-for-byte: the existing `FromTheme` overload is unchanged, and a
view only follows the theme once `UseThemeStyles` is set.

**Tasks.**

- [x] Overload, `WithTheme`, `IThemeable`, and `UseThemeStyles` with XML docs, including that `WithTheme` never
      mutates the receiver and that the styles depend on the theme's roles (`Theme.MarkdownHeadingRole` and the rest).
- [x] Tests: `includeBackgrounds: false` leaves every style's background at default on `Theme.Dark`, `Theme.Light`,
      and `Theme.HighContrast`; `WithTheme` on an ASCII theme renders a table, list, quote, and rule with no character
      above 127; `WithTheme` keeps a caller's `MaxWidth` and table fallback; a view with `UseThemeStyles` false renders
      identically before and after `ThemeApplier` runs; setting it true restyles without another `ApplyTheme`;
      applying an ASCII theme and then `Theme.Dark` restores the base options' Unicode glyphs; the `Options` getter
      returns the base instance while themed; a null theme throws `ArgumentNullException`.

---

## Phase 2: Streaming and search

### V3: Public streaming-safe prefix

**Status:** DONE  **Simplicity 9, Value 6**

Any app that streams Markdown from a model or a process re-renders the text received so far on each chunk. Without
care, a table looks broken while it arrives. These two effects showed up when 1.6.0 was driven chunk by chunk at
width 50:

- **Short partial cells.** A row whose newline has not arrived renders as a short cell ("Add cac"), and the columns
  resize on every chunk until the row completes.
- **A header that flips.** A header row is laid out as a paragraph of pipe characters until its delimiter row arrives,
  then turns into a table.

`StreamingTranscript` already avoids the first effect with a private check in `StreamMarkdownBlock`. Apps that render
streamed text into their own widgets (a chat bubble, a `MarkdownView`, a details panel) cannot call it, so they copy it.

**Design.**

- New `public static string StreamingPrefix(string received)` on `MarkdownRenderer`. It returns the longest prefix of
  `received` that is safe to lay out while more text is still coming:
  - a trailing line with no newline yet, whose first character after up to three spaces is `|`, is held back;
  - a trailing complete line that could be a table header (first character after up to three spaces is `|`), and is
    not itself a delimiter row or preceded by a table line, is also held back until the next line arrives, so a header
    becomes a table in one step instead of flashing as text. When the next line is partial and also starts with `|`,
    both stay held back. When the next complete line is not a delimiter row, the header is released as text on that
    line's newline;
  - every other partial line (prose, list items, emphasis still open) is returned as it arrived, so text keeps
    streaming at the same pace.
- Indentation uses spaces only, as GFM does: a tab starts an indented code block, not a table, so a tab-indented `|`
  is not held back. The current `StreamingTranscript` check also ignores tabs (`TrimStart(' ')`).
- At most three spaces count, as GFM does: four or more spaces start an indented code block, so a `|` indented four
  spaces is not held back. This differs from the current check, which trims any number of spaces and so holds back a
  partial code line that starts with `|`.
- `StreamingTranscript.StreamMarkdownBlock` calls the new method, so the rule has one implementation. Its output
  changes in two cases: an unconfirmed header is held back for at most one line, and a partial line indented four or
  more spaces before a `|` now streams as it arrives.
- When the stream ends, the caller renders the full text, never the prefix. The XML doc says so.

**Compatibility.** A new static method. Two visible changes are in `StreamingTranscript`, both listed under
"Changed": a table header now appears together with its delimiter row instead of one line earlier as text, and a
partial indented-code line that starts with `|` after four or more spaces is no longer held back.

**Tasks.**

- [x] Method with XML docs (null throws `ArgumentNullException`; returns `string.Empty` when nothing is safe yet;
      thread-safe and allocation-free when nothing is held back).
- [x] Switch `StreamingTranscript` to it.
- [x] Tests: a half row is held back; a complete row is not; a header without a delimiter is held, then released when
      the delimiter arrives; a header followed by a partial `|` line stays held; a header followed by a complete
      non-delimiter line is released as text; a delimiter row alone, a pipe inside prose, a tab-indented pipe, a
      partial list item, and unclosed emphasis are all returned unchanged; four-space-indented `|` is not held back; feeding a seeded document
      one character at a time never yields a render where a table row has fewer cells than its header (reuse the 1.6
      seeded-document generator).

### V4: Search that matches `Pane`

**Status:** DONE  **Simplicity 7, Value 7**

Long Markdown (a changelog, a runbook, a generated report) needs find. `Pane` already has it:

- `SetSearch(string?)` sets or clears the term;
- `FindNext()` and `FindPrevious()` move between matching rows, wrapping around;
- matches are highlighted on render with a private `HighlightMatches` helper and a hard-coded `_SearchStyle`.

`MarkdownView` has nothing, so an app copies the rows out and searches them itself.

**Design.**

- `MarkdownView` gains `SetSearch(string? query)`, `FindNext()`, and `FindPrevious()`, with the same names,
  matching, and return values as `Pane`'s:
  - matching is case-insensitive over the plain text of rendered rows (`StyledText.ToPlainString()`), so it finds what
    the user sees, not Markdown syntax;
  - the search starts from the row after (or before) the top visible row, `ScrollOffset`, and wraps around, as
    `Pane.FindDirectional` starts from `_ViewTop`;
  - before the first render, `Pane` searches at a fallback width of 80. `MarkdownView` does the same, using the last
    layout width when there is one, and the XML doc says the result may move once the real width is known;
  - a found row is scrolled into view.

  Thread-safety differs, as the two widgets already do. `Pane`'s search members are thread-safe under its `_Sync`
  lock. `MarkdownView`'s are UI thread only, like the rest of the view. "Same semantics" in the docs covers matching
  and return values, not threading.

  New read-only `string? SearchText`, on both `Pane` and `MarkdownView`, so a status line can show the active term.
  `Pane.SearchText` reads `_Search` under `_Sync`.
- New theme role `Theme.SearchMatchRole`. `MarkdownView` resolves it in `ApplyTheme` (V2's `IThemeable`), with
  today's `Pane` style (black on palette yellow) as the fallback.
- `Pane` is `IWidget, IMouseAware` only today and never sees a theme. It additionally implements `IThemeable`.
  `ApplyTheme` sets only the search highlight style, under `_Sync`, from `theme.Resolve(Theme.SearchMatchRole,
  fallback)` with the same fallback; nothing else about the pane follows the theme. Because the fallback and the
  built-in registrations equal today's hard-coded `_SearchStyle`, `Pane` renders identically after `ThemeApplier`
  runs on `Theme.Dark`, `Theme.Light`, or a custom theme that does not register the role.
- The built-in themes register the current values. `Theme.HighContrast` adds `Underline`, so a match differs by
  attribute as well as color, per the theming rule. That makes a `Pane` search highlight underlined under
  `Theme.HighContrast` once the theme is applied, which is the one visible change to `Pane`.
- `Pane`'s private `HighlightMatches` moves to an internal shared helper. No public surface.
- Keys, when V1's focus is in use: F3 and Shift+F3 call `FindNext` and `FindPrevious` while a search term is set.
  Printable keys such as `/`, `n`, and `N` are deliberately not bound, because many apps use them as global
  shortcuts. The search prompt is the app's own (a `TextField` in a status line or overlay), shown in the example.

**Compatibility.** Additive, with one listed change. `Pane` renders identically under `Theme.Dark`, `Theme.Light`,
no theme, and any custom theme that does not register the role, because the role's fallback and those built-in
registrations equal today's hard-coded style. Under `Theme.HighContrast`, `Pane` search matches gain an underline.
That is an accessibility fix and is listed under "Changed". If the owner prefers zero visual change, drop the
underline from the `HighContrast` registration and record the decision here.

**Tasks.**

- [x] Members, role, shared helper, `Pane`'s `IThemeable`, and F3 handling with XML docs, including each widget's
      thread-safety and the width-80 fallback before the first render.
- [x] Tests: a match in a table cell, list item, and code block is found; matching ignores case and Markdown markup
      (searching `**bold**` does not match rendered "bold" text, searching `bold` does); wrap-around in both
      directions; clearing the search removes highlights and makes `FindNext` return false; F3 is not consumed when no
      search is set; `FindNext` from a scrolled view finds the first match below the top row, not from the
      document start; `Pane` output is cell-for-cell identical to 1.6 with no theme, after `Theme.Dark` and
      `Theme.Light`, and with a custom theme without the role; under `Theme.HighContrast` the only difference is
      the underline on matched cells; `Pane.SearchText` is safe to read from another thread while the pane renders.

---

## Phase 3: Growing documents

### V5: Tail-follow for growing Markdown

**Status:** DONE  **Simplicity 7, Value 6**

A document that grows while it is shown needs the view to stay at the bottom, and to say how much arrived if the user
scrolled up. Examples are a build or agent log written in Markdown, a live report, or a streamed reply shown in a
`MarkdownView` instead of a `Pane`. `Pane` does this with a public `TailFollow` (`Pane.TailFollow`).
`MarkdownView` keeps its offset when `Markdown` changes, so new rows land out of sight.

**Design.**

- New `public TailFollow TailFollow { get; }` on `MarkdownView`, never null, the same type and contract as
  `Pane.TailFollow`. A new view sets its mode to `TailFollowMode.Never`.
- Rows are only known at a width, so `Render` compares the new row count with the previous layout's. When following,
  it scrolls to the end. When not following, it reports growth through `OnContentAppended(delta)` and shrinkage
  through `OnContentRemoved(removedBelow)`. Scrolling (V1 keys, wheel, `ScrollOffset`, `ScrollBy`) reports through
  `OnViewportMoved`, and a search jump (V4) through `OnJumpedAway`.
- While `TailFollow.ShowIndicator` is set and `IndicatorText` is non-null, the indicator is drawn on the last row,
  right-aligned, as `Pane` draws it.
- When not following and a resize changes the row count, the view keeps the same `ScrollOffset` (row number),
  clamped to the new end, and the content under it may shift. Anchoring to a source position would need a
  row-to-source map the engine does not expose, so it is out of scope. The XML doc says so.

**Compatibility.** Mode `Never` by default, so a 1.6 view keeps its offset as before. Setting
`FollowAtBottom` or `AlwaysFollow` opts in.

**Tasks.**

- [x] Property, render-time bookkeeping, and indicator with XML docs.
- [x] Tests: `AlwaysFollow` stays at the end across growth; `FollowAtBottom` detaches on scroll-up, counts new rows,
      and reattaches at the end; a document that shrinks lowers the count; a resize that changes the row count without
      new content does not count as new content and keeps the offset clamped to the new end; `Never` keeps the
      offset; the indicator renders only when enabled.

### V6: Markdown layout cache

**Status:** DONE  **Simplicity 8, Value 5**

Apps that draw many Markdown blocks inside their own widgets (a chat transcript of message bubbles, a list with
rendered descriptions, a details panel with several Markdown fields) lay the same text out again on every frame,
unless each one writes a cache keyed on text, width, and options. `MarkdownView` caches internally, but only for its
own single document.

**Design.**

- New `MarkdownLayoutCache` (sealed, `Content/MarkdownLayoutCache.cs`):
  - `IReadOnlyList<StyledText> GetRows(string markdown, int width, MarkdownRenderOptions options)` returns the cached
    layout or renders and stores it;
  - `Capacity` (int, default 256, minimum 1, maximum 1,000,000; least recently used entries are evicted);
  - `Count`, and `Clear()`.
- The key is the Markdown text (ordinal equality), the width, and the options instance (reference equality). As with
  `MarkdownView.Invalidate`, a caller that mutates an options instance in place calls `Clear()`. A caller that swaps in
  a new instance, as `WithTheme` (V2) produces, gets fresh entries automatically.
- Reference equality means a caller must keep and reuse one options instance. `WithTheme` returns a new clone on
  every call, so calling it per frame would make every lookup miss and fill the cache with duplicates. The XML docs
  on both `GetRows` and `WithTheme` say to call `WithTheme` once per theme change and reuse the result.
- Fixed behavior, with no setting: when `GetRows` is called with a width that has no entries, all entries at other
  widths are dropped first. A transcript laid out at one width at a time then never holds a stale copy per width
  during resize churn, which is what Armada's three caches did by hand. An app that lays text out at two widths at
  once (for example a list and a preview) uses one cache per surface; the XML doc says so. A setting to keep other
  widths is added only if a real app needs it.
- Thread-safe, guarded by one lock, because renders may be prepared off the UI thread.

**Compatibility.** A new type. Nothing existing changes.

**Tasks.**

- [x] Type with XML docs (null arguments throw `ArgumentNullException`, width below 1 throws
      `ArgumentOutOfRangeException`, capacity outside its range throws `ArgumentOutOfRangeException`).
- [x] Tests: a hit returns the same instance; a different width, text, or options instance misses; eviction follows
      least-recent use at capacity; `Clear` empties it; two `WithTheme` results for the same theme miss each other;
      a request at a new width drops every entry at other widths; concurrent
      `GetRows` from several threads returns consistent rows.

---

## Phase 4: Example application and docs

- [x] **E1.** Extend the "Markdown that fits" tour page:
  - make the view a Tab stop with themed styles (V1, V2);
  - add a `/` prompt built from a `TextField` that drives `SetSearch`, with F3 for next and previous (V4);
  - add a toggle that appends a section every second with follow on (V5).
- [x] **E2.** Change the harness transcript's streamed reply to show the prefix rule (V3), with a short note on what
      is held back. Add a message list drawn through `MarkdownLayoutCache` (V6).
- [x] README: a "Markdown" section covering the interactive view, theming, streaming, and the cache. CHANGELOG entries
      under Added and Changed for each item. `docs/SURFACE_COVERAGE.md` rows for every new member.

---

## Phase 5: Verification and downstream check

- [x] Full suite through `Test.Automated`, `Test.Xunit`, and `Test.Nunit` on every target framework, with zero
      warnings.
- [x] Check each item against `docs/CONFORMANCE.md`.
- [x] Downstream check, without changing Armada in this plan: confirm from a local package build that Armada could
      replace its private Markdown viewer, theme mapping, streaming check, and layout caches with V1 to V6. Record any
      gap here as a follow-up instead of adding Armada-specific API.

---

## Considered and not included

- **Unsealing `MarkdownView`.** Inheritance would widen the public contract for every future change. Composition
  through the members above covers the cases Armada needed.
- **A built-in search prompt.** Prompt placement and styling vary by app (status line, overlay, inline). V4 supplies
  the engine and a theme role; the example shows a prompt in a few lines.
- **Binding `/`, `n`, and `N` by default.** These collide with common global shortcuts. F3 and Shift+F3 are safe,
  and apps can bind letters through their own key bindings and the V1 and V4 methods.
- **`PlainTextAt(width)` for copying what is shown.** `RowsAt(width)` plus `StyledText.ToPlainString()` already does
  it in one line. The raw source is `Markdown`.
- **Markdown values inside `DefinitionList` or `Table` cells.** Useful, but it is a layout feature for those widgets,
  not for `MarkdownView`, and it can follow once V6 makes per-cell layout cheap.

---

## Implementation notes (1.7.0)

The work was done on `main` rather than a `feature/v1.7.0` branch, at the owner's request, and committed in
phases with an annotated `v1.7.0` tag. What shipped differs from the text above in these places. Each was decided during implementation and is
reflected in the code, tests, README, and CHANGELOG.

- **V1.** The view also implements `IFocusAware` and exposes a read-only `IsFocused`, so hints know whether it has
  focus. While a search term is set, `GetKeyHints` adds "F3 Next match". The auto-focus gate lives in
  `TuiApplication.Bind` (there is no `SetRegionContent`) and checks `IsFocusStop` directly, as described above.
- **V2.** `WithTheme` keeps the receiver's styles for the elements a theme does not define (`Strong`, `Emphasis`,
  `Strikethrough`, `TableFirstColumn`, `TableCell`). Replacing every style dropped a caller's key-column style,
  which the themed snapshot of the tour sample showed. The search highlight follows `Theme.SearchMatchRole` on
  every `ApplyTheme`, whether or not `UseThemeStyles` is set, since search is new in 1.7 and has no 1.6 look to
  keep.
- **V3.** A partial line after a header candidate keeps the header held while it could still become a delimiter
  row (only `|`, `-`, `:`, and spaces so far), not only while it starts with `|`. The seeded test uses its own
  table-focused generator (prose, lists, indented code, and tables of 1 to 4 columns) rather than the 1.6 token
  generator, because the invariant needs known column counts.
- **V4.** `FindNext` starts after the current match when it is still visible, otherwise after the top visible row,
  so two matches on the last page are both reachable. `Pane` keeps its 1.6 search start.
- **V5.** Turning follow on (from `Never`) attaches at the next render if the view is already at its end. The
  indicator click is handled by `HandleMouse`, as on `Pane`.
- **V6.** Added a read-only `Width` (the cached width, 0 when empty) and `GetRowsAsync`, the async variant
  `CODE_STYLE.md` asks for. The width eviction is fixed behavior with no setting.
- **E1.** The tour draws its own frame and owns Tab and PageUp/PageDown, so the page forwards keys to the view
  (Up/Down, Home/End, F3) instead of making it a Tab stop, applies the app theme itself, and paints the theme
  background behind themed styles. `--tour-once --keys` now feeds one key per frame, because one pump drained every
  buffered key before the page had rendered, which made search land at the 80-column fallback.
- **E2.** The harness streams its reply six characters at a time through `StreamingPrefix` and shows the held-back
  count in the telemetry `reply` row. The `MarkdownLayoutCache` message list is a new tour page, "Markdown in your
  own widgets", rather than part of the already full harness layout. `--markdown-once --theme` prints the sample
  through `WithTheme`.
- **Verification.** 976 console cases and 977 xUnit/NUnit cases pass on net8.0 and net10.0 with 0 warnings. Line
  coverage is 84.2% for the assembly and 96.7% for the new and changed files.

