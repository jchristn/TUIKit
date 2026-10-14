# TUIKit Improvements: Width-Aware Markdown Rendering (1.6)

TUIKit has rendered Markdown since 1.0, but only in the narrowest sense. `MarkdownRenderer` reads one source line at a
time and styles it without knowing how wide the output will be. The `Pane` wraps each line later, generically. That
was enough for a demo transcript. It falls apart on the output that language models actually produce:

- A GFM table comes out as one ` │ `-joined string per row, so the columns never line up.
- A bullet or quote that wraps loses its hanging indent and quote bar on every continuation row.
- Rules and table separators are 24 columns wide whatever the pane width.
- Fenced code ignores its language tag, although `SyntaxHighlighter` ships in the same assembly.
- Nested emphasis, links, and escapes are not parsed at all.
- Content styled at write time never reflows when the terminal is resized.

Claude Code and Codex both lay Markdown out at a known width. Columns are sized from their content, list items keep
their indent when they wrap, and a table that cannot fit drops to a stacked layout instead of breaking. This release
gives TUIKit the same thing: a block parser, a layout engine that takes the width as an input, and a table layout that
sizes columns from cell content. The caller controls every visual decision at render time through one options object:
colors, border glyphs, header and first-column styles, column minimums and maximums, wrap versus truncate, maximum
width, and the rest.

Requirements in `~/Code/Agents/requirements` are authoritative, especially `CODE_STYLE.md`, `VERSIONING.md`,
`BACKEND_TEST_ARCHITECTURE.md`, `REPOSITORY_REQUIREMENTS.md`, and `WRITING_DOCUMENTS.md` (no em dashes, anywhere). The
repository's `CLAUDE.md` restates the code style rules for every `.cs` file. The previous plan is
`archive/IMPROVEMENTS_FOCUS_HINTS_TOASTS.md`; its "How to use this plan" rules apply here unchanged.

Code references were checked against `main` at 1.5.0 (commit `0732618`). Re-check each one when its item starts.

---

## Summary

Simplicity scores ease of integration into TUIKit: 10 is small, additive, and isolated, and 1 is invasive. Value
scores how much a typical TUIKit app gains. Rows are in implementation order.

| ID | Item | Simplicity | Value | Status |
|----|------|-----------:|------:|--------|
| M1 | Block parser and internal document model | 6 | 9 | DONE |
| M2 | Inline parser: nesting, links, escapes, code spans | 6 | 7 | DONE |
| M3 | Render options, table options, glyph sets, new styles | 8 | 9 | DONE |
| M4 | Width-aware layout: paragraphs, headings, lists, quotes, code, rules | 5 | 10 | DONE |
| M5 | Table layout: content-sized columns, borders, alignment, overflow, fallback | 4 | 10 | DONE |
| M6 | Public render API: width overloads, async variant, natural width | 9 | 8 | DONE |
| M7 | `Pane.WriteMarkdown`: blocks that reflow on resize | 6 | 9 | DONE |
| M8 | `MarkdownView` widget | 8 | 7 | DONE |
| M9 | `StreamingTranscript` width-aware mode | 6 | 8 | DONE |

### Release at a glance

| Phase | Title | Items | Status |
|---|---|---|---|
| 0 | Release setup | version | DONE |
| 1 | Parsing | M1, M2 | DONE |
| 2 | Options and layout | M3, M4 | DONE |
| 3 | Tables | M5 | DONE |
| 4 | API and integration | M6, M7, M8, M9 | DONE |
| 5 | Example application | E1, E2 | DONE |
| 6 | Documentation | D1 to D4 | DONE |
| 7 | Verification and publishing | P1 to P5 | DONE |

_Last updated: 2026-10-14, all phases done; 1.6.0 published._

### Implementation record

The work followed this plan with a few deliberate changes, each made where the plan met real output. They are
recorded here once instead of being scattered through the items.

- **Column sharing (M5).** Proportional sharing by shortfall, as designed, wrapped short headers such as
  `p99 (ms)` at 60 columns while a long prose column took the space. Spare columns now go to the smallest
  shortfalls first, each in full, with ties going to the narrower column; the widest prose column absorbs the
  squeeze. `ShortColumnsStayWhole` pins both the 60- and 46-column results.
- **`MaxRowLines` (M3, M5).** At 40 columns the minimum widths of the demo table fit, so the grid won, with
  the Notes column one word wide and rows seven lines tall. `MarkdownTableOptions.MaxRowLines` (default 4)
  stacks such a table under the `Stack` fallback. It is ignored for truncated or clipped cells and for the
  other fallbacks, and 0 turns it off.
- **Stacked records (M5).** A record's first row wraps at the full width and its continuation rows hang by
  two columns, so `Status: operational` stays on one row at width 20.
- **Code wrapping (M4).** A wrapped code line continues at its own leading whitespace (capped at half the
  width) instead of at the block indent.
- **Trailing backslash (M1).** A backslash or two trailing spaces at the end of a paragraph's last line is
  literal, not a hard break, matching CommonMark.
- **`MaxLineLength` (M7).** Applied to each source line rather than the whole source, so a long reply is
  never cut.
- **Extra public members.** `PaneLineHandle.UpdateMarkdown(string, MarkdownRenderOptions)` (the transcript
  needs it to turn a collapsed live line into a block with the right options), `MarkdownView.RowsAt`,
  `MarkdownView.WheelStep`, and `IMouseAware` on `MarkdownView`.
- **Example.** Besides the tour page and harness transcript, `--markdown-once [--width N] [--ascii]
  [--color]` prints the sample at any width; `--color` keeps ANSI when piped.
- **Visual check (P1).** Screen capture was not available to the agent, so the colored output of
  `--markdown-once --color` was rendered to images at 100, 60, and 40 columns and inspected, and the tour page
  and harness were checked through their headless snapshots. The live interactive harness and the manual
  terminal matrix were not run by the agent and remain for the owner.
- **Results.** 934 console cases (net8.0 and net10.0) and 935 xUnit and NUnit cases pass; the 845 cases from
  1.5.0 pass unchanged. Line coverage of `TUIKit` is 83.7%, and 98.5% for the new Markdown files. The Release
  build has zero warnings on all three targets. A 36 KB document lays out in about 16 ms, and streaming costs
  about 0.5 ms per frame.

---

## How to use this plan

Every item has an ID, a status line to edit in place, and checklists. Tick boxes as work lands. Flip an item to
`DONE` only when its code, tests, and docs are complete and the solution builds clean with `TreatWarningsAsErrors` on
`netstandard2.0`, `net8.0`, and `net10.0`. Record design changes under the item's **Notes** line instead of rewriting
the design text. Status values: `TODO`, `WIP`, `DONE`, `BLOCKED` (blocker in Notes), and `N/A` (reason in Notes).

### Compatibility contract

Everything in 1.6.0 is additive, and nothing that renders today renders differently by default. The existing
line-oriented entry points keep their exact output:

- `MarkdownRenderer.Render(string)` and `Render(string, MarkdownStyles)`;
- `MarkdownRenderer.RenderLine` and `RenderInline`;
- `StreamingTranscript` with its new `RenderOptions` left at null.

The new engine is reached only through new overloads that take a width or a `MarkdownRenderOptions`, through
`Pane.WriteMarkdown`, through `MarkdownView`, or by setting `StreamingTranscript.RenderOptions`. The cost is that the
legacy and new engines share no parser code. That duplication is deliberate: rewriting the legacy path on top of the
new parser would change what `RenderInline` returns for nested emphasis, and existing callers would see different
output after a minor-version upgrade.

`MarkdownStyles` gains properties. Their defaults leave legacy output untouched because the legacy renderer never
reads them.

### Test rules

Every item gets positive tests (it works), negative tests (bad input throws the documented exception, malformed
Markdown renders literally instead of throwing), and boundary tests (width 1, width at the exact fit, empty input).
Tests are Touchstone descriptors in `src/Test.Shared/Suites`, registered in `TUIKitSuites.All`, and run through all
three runners. They assert on plain text and on span styles, never on console output.

One invariant runs across the whole engine and gets its own property-style suite: **no row is ever wider than the
layout width**, for every option combination, for widths from 1 to 120, over a fixed corpus and over seeded random
Markdown. The same suite asserts that no input throws.

---

## Public surface

The whole configurable surface lives in three classes and two enums, all in `TUIKit.Content`, plus additions to
`MarkdownStyles`. Defaults are chosen so `new MarkdownRenderOptions()` looks like Claude Code's output on a dark
terminal.

### `MarkdownRenderOptions`

| Member | Type | Default | Range and meaning |
|---|---|---|---|
| `Styles` | `MarkdownStyles` | `MarkdownStyles.Default` | Colors and attributes of every element. Not null. |
| `Table` | `MarkdownTableOptions` | new instance | Table layout and glyphs. Not null. |
| `MaxWidth` | `int` | `0` | Caps the layout width. `0` uses the full available width. Must be 0 or more. |
| `MinWidth` | `int` | `10` | When the available width is smaller, layout happens at `MinWidth` and the host clips. 1 to 1000. |
| `Overflow` | `MarkdownOverflow` | `Wrap` | Paragraphs, headings, list items, and quotes: wrap, truncate with `Ellipsis`, or clip. |
| `CodeOverflow` | `MarkdownOverflow` | `Wrap` | The same choice for fenced code lines. |
| `Ellipsis` | `string` | `"…"` | Appended by `Truncate`. Not null; may be empty. |
| `BlockSpacing` | `int` | `1` | Blank rows between blocks. 0 to 4. |
| `ShowHeadingMarkers` | `bool` | `false` | Keeps the `#` markers in headings. |
| `BulletGlyphs` | `IReadOnlyList<string>` | `•`, `◦`, `▪` | One per nesting depth, cycling. Not null or empty; no null or empty entries. |
| `ListIndent` | `int` | `0` | Spaces before every list marker. 0 to 8. |
| `TaskOpenGlyph`, `TaskDoneGlyph` | `string` | `☐`, `☑` | Task list markers. Not null or empty. |
| `QuotePrefix` | `string` | `"│ "` | Drawn before every quoted row, once per nesting level. Not null or empty. |
| `RuleGlyph` | `string` | `"─"` | Repeated to draw a thematic break. Not null or empty. |
| `RuleWidth` | `int` | `0` | `0` spans the layout width; otherwise the rule is capped at this width. Must be 0 or more. |
| `CodeIndent` | `int` | `2` | Spaces before every code line. 0 to 16. |
| `ShowCodeLanguage` | `bool` | `true` | Shows the fence's language tag above the block. |
| `SyntaxHighlighting` | `bool` | `true` | Runs `SyntaxHighlighter` when the fence names a language. |
| `FillCodeBackground` | `bool` | `false` | Pads code rows to the layout width so a `CodeBlock` background reads as a block. |
| `ShowLinkUrls` | `bool` | `true` | Renders `[text](url)` as `text (url)`; when false only the text shows. |

`Clone()` returns a deep copy. `MarkdownRenderOptions.Ascii()` returns a fresh instance with ASCII glyphs everywhere
(`-`/`*`/`+` bullets, `| ` quotes, `-` rules, `[ ]`/`[x]` tasks, `...` ellipsis, `MarkdownTableGlyphs.Ascii`).

### `MarkdownTableOptions`

| Member | Type | Default | Range and meaning |
|---|---|---|---|
| `Glyphs` | `MarkdownTableGlyphs` | `MarkdownTableGlyphs.Light` | Border characters. Not null. |
| `ShowOuterBorder` | `bool` | `true` | Draws the top, bottom, left, and right edges. |
| `ShowHeaderSeparator` | `bool` | `true` | Draws the line under the header row. |
| `ShowRowSeparators` | `bool` | `false` | Draws a line between body rows. |
| `CellPadding` | `int` | `1` | Spaces on each side of a cell's content. 0 to 4. |
| `MinColumnWidth` | `int` | `3` | No column is narrower, unless the fallback is `Shrink`. 1 to 1000. |
| `MaxColumnWidth` | `int` | `0` | `0` means no cap. Otherwise caps natural width; values below `MinColumnWidth` act as `MinColumnWidth`. Must be 0 or more. |
| `MaxRowLines` | `int` | `4` | With the `Stack` fallback, a grid whose wrapped rows would be taller than this stacks instead. `0` means no limit. 0 to 1000. Added during implementation. |
| `CellOverflow` | `MarkdownOverflow` | `Wrap` | What a cell does when its content is wider than its column. |
| `Fallback` | `MarkdownTableFallback` | `Stack` | What happens when even the minimum widths do not fit. |
| `Expand` | `bool` | `false` | Stretches the table to the layout width, sharing the extra space by natural width. |

### `MarkdownTableGlyphs`

Eleven glyph strings (`Horizontal`, `Vertical`, the four corners, the four tees, `Cross`), each one column wide.
Presets: `Light` (`┌─┬┐│├┼┤└┴┘`), `Rounded`, `Heavy`, `Double`, `Ascii` (`+`, `-`, `|`), and `None` (spaces, so a
borderless table still aligns). `FromBorderStyle(BorderStyle)` maps the existing enum. The constructor takes all
eleven and rejects null, empty, or wider-than-one-column glyphs with `ArgumentException`.

### Enums

`MarkdownOverflow`: `Wrap` (break at spaces, hard-break long words), `Truncate` (cut and append `Ellipsis`), `Clip`
(cut with no marker). `MarkdownTableFallback`: `Stack` (each row becomes a block of `Header: value` lines), `Shrink`
(columns share the width below their minimums and cells hard-wrap), `Clip` (columns keep their minimums and the table
is cut at the layout width).

### `MarkdownStyles` additions

`Strong`, `Emphasis`, `Strikethrough` (attributes the new engine overlays; defaults bold, italic, strikethrough),
`Link` (underlined blue), `LinkUrl` (grey), `CodeLanguage` (grey), `TableHeader` (bold cyan, matching the `Table`
widget), `TableFirstColumn` (default: no change), `TableCell` (default: no change), and `TableBorder` (grey).
`FromTheme` derives them from theme roles. `Clone()` returns a copy.

### Entry points

| Member | Purpose |
|---|---|
| `MarkdownRenderer.Render(string markdown, int width)` | Width-aware render with default options. |
| `MarkdownRenderer.Render(string markdown, int width, MarkdownRenderOptions options)` | Width-aware render. |
| `MarkdownRenderer.RenderAsync(string markdown, int width, MarkdownRenderOptions options, CancellationToken token)` | Async variant required by `CODE_STYLE.md`; checks cancellation between blocks. |
| `MarkdownRenderer.MeasureWidth(string markdown, MarkdownRenderOptions options)` | The natural width: the widest row when nothing wraps, capped by `MaxWidth`. Lets callers size a modal or column from content. |
| `Pane.WriteMarkdown(string markdown)` and `(string, MarkdownRenderOptions)` | Commits one block that reflows on resize. Returns a `PaneLineHandle`. |
| `PaneLineHandle.UpdateMarkdown(string markdown)` | Replaces a block's source and keeps its options. |
| `MarkdownView` (in `TUIKit.Widgets`) | A widget that lays its Markdown out at its own width; implements `IWidget` and `IScrollExtent`. |
| `StreamingTranscript.RenderOptions` | `null` (default) keeps 1.5.0 behavior; set, each streamed block is a reflowing Markdown block. |

No custom exception type is added. The renderer never fails on malformed Markdown: anything it cannot parse renders
as literal text. Invalid arguments throw `ArgumentNullException`, `ArgumentOutOfRangeException`, or
`ArgumentException`, documented on each member.

---

## Implementation order

1. Phase 0.
2. M1 and M2 (the layout needs a model to walk).
3. M3, then M4 (layout reads the options).
4. M5 (uses M4's wrapping helpers).
5. M6, M7, M8, M9 (each a thin shell over the engine).
6. Phases 5, 6, and 7.

---

## Phase 0: Release setup

**Status:** DONE
**Notes:**

- [x] Confirm `main` is clean and green at 1.5.0 (845 console cases, zero warnings).
- [x] **Version (owner approval required, `VERSIONING.md` section 4).** Every item is additive and nothing changes by
      default, so the version is `1.6.0` (MINOR).
      Approved version: 1.6.0  Approved by/on: owner (Joel Christner), 2026-10-14, in the request to plan, implement,
      and publish ("prefer a minor version increment since this is an incremental non-breaking capability").
- [x] Set `Version`, `AssemblyVersion` (`1.6.0.0`), and `FileVersion` (`1.6.0.0`) in `src/TUIKit/TUIKit.csproj`,
      update `PackageReleaseNotes`, and open `## [1.6.0] - 2026-10-14` in `CHANGELOG.md`.

---

## Phase 1: Parsing

### M1: Block parser and internal document model

**Status:** DONE
**Notes:**

The legacy renderer has no model at all, which is why a table row cannot know the width of the row below it. M1 adds
an internal tree and a parser that builds it. The model stays internal so it can change without a major version. The
public contract is the rendered output.

The parser covers the subset that model output uses, following CommonMark and GFM where they agree:

| Block | Rules |
|---|---|
| ATX heading | One to six `#`, then a space or end of line. Trailing `#` run removed. |
| Setext heading | A paragraph line followed by `===` (level 1) or `---` (level 2). |
| Paragraph | Consecutive non-blank lines joined with a space. A line ending in two spaces or `\` is a hard break. |
| Fenced code | ```` ``` ```` or `~~~`, three or more; closed by a fence of the same character at least as long. The info string's first word is the language. An unclosed fence runs to the end. |
| Block quote | Lines starting `>`; the stripped content is parsed recursively, so quotes nest and hold any block. A non-blank line without `>` continues the quote's last paragraph (lazy continuation). |
| List | `-`, `*`, `+`, `1.`, `1)`. Lines indented to the item's content column belong to it and are parsed recursively, so lists nest. A blank line between items makes the list loose. Ordered lists keep their start number. `[ ]` and `[x]` after the marker make a task item. |
| Table | A pipe row, then a delimiter row (`---`, `:--`, `--:`, `:-:`) with the same cell count. Body rows run to a blank line or a line without a pipe. `\|` is a literal pipe. Short rows are padded and long rows trimmed to the header's count. |
| Thematic break | Three or more `-`, `*`, or `_`, optionally separated by spaces. |

Indented code blocks are left out on purpose: models rarely emit them, and the four-space rule misreads list
continuations. HTML blocks render as text.

Files (internal, one class or enum per file): `MarkdownDocument`, `MarkdownBlock` (abstract), `MarkdownHeadingBlock`,
`MarkdownParagraphBlock`, `MarkdownCodeBlock`, `MarkdownQuoteBlock`, `MarkdownListBlock`, `MarkdownListItem`,
`MarkdownTableBlock`, `MarkdownRuleBlock`, `MarkdownColumnAlignment`, and `MarkdownParser`.

- [x] Parser and model.
- [x] Positive tests: each block kind, nesting (list in quote, quote in list, three-level lists), loose versus tight
      lists, ordered start numbers, setext headings, `~~~` fences, unclosed fences, table alignment parsing, escaped
      pipes, ragged table rows.
- [x] Negative tests: null source throws `ArgumentNullException`; a delimiter row with the wrong cell count is not a
      table; `#hashtag` is not a heading; `**` alone is not a rule; CRLF and CR line endings parse like LF.
- [x] Boundary tests: empty string, whitespace only, a single `>`, a single list marker with no text.

### M2: Inline parser

**Status:** DONE
**Notes:**

The legacy `RenderInline` cannot nest (`**bold *and italic***` breaks) and does not know links or escapes. M2 is a new
internal `MarkdownInlineParser` used only by the new engine.

- Emphasis with `*` and `_`, strong with `**` and `__`, both with `***`. `_` inside a word is literal
  (`snake_case_name`). An opener followed by whitespace is literal.
- `~~strike~~`.
- Code spans with any backtick run length; contents are literal, one leading and trailing space trimmed when both are
  present.
- `[text](url)` and `[text](url "title")`, with nested brackets in the text. Rendered as the text in `Link` style,
  then ` (url)` in `LinkUrl` style when `ShowLinkUrls` is on and the text differs from the URL.
- `![alt](url)` renders as `[image: alt]`.
- Autolinks `<https://...>` and `<mailto:...>`.
- Backslash escapes for ASCII punctuation.
- Hard breaks from M1 split the paragraph into separate rows.
- Unmatched delimiters render literally.

- [x] Parser.
- [x] Positive tests: each construct, nesting two and three deep, `***`, code spans holding `*`, links holding
      emphasis, escaped delimiters, intraword underscores.
- [x] Negative tests: unclosed `**`, `[text](` with no close, `[]()`, a lone backtick, a backslash at end of input.

---

## Phase 2: Options and layout

### M3: Render options, table options, glyph sets, new styles

**Status:** DONE
**Notes:**

Implements the [public surface](#public-surface) tables exactly: `MarkdownRenderOptions`, `MarkdownTableOptions`,
`MarkdownTableGlyphs`, `MarkdownOverflow`, `MarkdownTableFallback`, and the `MarkdownStyles` additions. Every
validated member uses an explicit getter and setter over a private field. Every member's XML doc states its default,
its range, and what the values mean. Instances are mutable and not thread-safe to mutate during a render; each class
says so. `Pane.WriteMarkdown` takes a deep copy at write time so later edits to the caller's instance never change
committed blocks.

- [x] Types and `Clone()` methods; `MarkdownRenderOptions.Ascii()`; `MarkdownTableGlyphs` presets and
      `FromBorderStyle`; `MarkdownStyles.FromTheme` coverage of the new styles.
- [x] Positive tests: defaults match the tables above; `Clone()` is deep (mutating the clone's `Styles` and `Table`
      leaves the original alone); every `BorderStyle` maps; `Ascii()` contains no character above U+007F.
- [x] Negative tests: every range boundary just outside throws `ArgumentOutOfRangeException`; every null throws
      `ArgumentNullException`; `BulletGlyphs` empty or holding null throws `ArgumentException`; a two-column glyph
      throws `ArgumentException`.

### M4: Width-aware layout

**Status:** DONE
**Notes:**

The layout engine (internal `MarkdownLayout`) walks the document at a width `W`, computed as `MaxWidth` (or the
available width when `MaxWidth` is 0) clamped to the available width, then raised to `MinWidth` if smaller. Every
row it returns is at most `W` columns. The one exception is the `Clip` table fallback, which also cuts at `W`, so the
invariant holds there too.

Wrapping uses a new internal prefix-aware wrapper beside `TextWrapper`: content wraps at `W` minus the prefix width,
and each row gets the first-row prefix or the continuation prefix. That is how a wrapped list item keeps its hanging
indent and a wrapped quote keeps its bar at every depth.

| Block | Layout |
|---|---|
| Paragraph | Inline text laid out by `Overflow`. Hard breaks start a new row. |
| Heading | `Heading1` or `Heading` style, optional `#` markers, laid out by `Overflow`. |
| List | Marker from `BulletGlyphs[depth % count]`, the ordered number (`12.`), or a task glyph. Continuation rows indent to the content column. Ordered markers in one list are right-aligned to the widest number. Children lay out at the remaining width. Tight lists have no blank rows between items; loose lists use `BlockSpacing`. |
| Quote | Children lay out at `W` minus the prefix width; every row gets `QuotePrefix` in `Quote` style. |
| Code | Optional language row in `CodeLanguage` style; lines highlighted when a language is named and `SyntaxHighlighting` is on, overlaid on `CodeBlock`; `CodeIndent`; `CodeOverflow`; optional background fill to `W`. Tabs expand to four spaces. |
| Rule | `RuleGlyph` repeated to `W` or `RuleWidth`, whichever is smaller. |

If the prefixes alone leave less than one column (deep nesting at width 3, for example), content lays out at one
column and rows are cut to `W`, so the invariant still holds.

- [x] Prefix-aware wrapper, truncate, and clip helpers (grapheme-safe; a wide character never splits).
- [x] Layout of every block kind.
- [x] Positive tests: wrapped bullet continuation rows start at the content column; wrapped quote rows all carry the
      bar, nested quotes carry two; ordered numbers align; code language label shows and hides; highlighting applies
      the keyword color; `FillCodeBackground` pads to `W`; `BlockSpacing` 0 and 4; `MaxWidth` narrower than the
      available width; `Truncate` ends with the ellipsis; `Clip` does not.
- [x] Negative and boundary tests: width 0 and negative throw `ArgumentOutOfRangeException`; width 1; width below
      `MinWidth`; CJK and emoji text never split and never exceed `W`.

---

## Phase 3: Tables

### M5: Table layout

**Status:** DONE
**Notes:**

Tables are where the current renderer is weakest and where model output most often lands. Column widths come from
content, in four steps:

1. **Measure.** Each cell is inline-rendered. A column's natural width is its widest cell. Its minimum width is its
   longest unbreakable word when `CellOverflow` is `Wrap`, or `MinColumnWidth` when cells truncate or clip. Both are
   raised to `MinColumnWidth`, and both are capped by `MaxColumnWidth` when one is set.
2. **Budget.** The chrome is the outer border (two columns when shown), the inner separators (one per gap), and
   `CellPadding` on both sides of every column. The budget is `W` minus the chrome.
3. **Fit.** If the natural widths fit the budget, use them, or share the spare columns by natural width when
   `Expand` is on. Otherwise, if the minimums fit, start every column at its minimum and share what is left in
   proportion to each column's shortfall (natural minus minimum), so wide columns give up space first and short
   columns stay whole. Rounding remainders go to the columns with the largest fractional share.
4. **Fall back.** If the minimums do not fit, apply `Fallback`. `Stack` renders each body row as a block of
   `Header: value` rows (header in `TableHeader` style, value wrapped with a hanging indent), with a rule between
   records. `Shrink` shares the budget evenly with a floor of one column and hard-wraps cells. `Clip` keeps the
   minimums and cuts each row at `W`.

Cells wrap, truncate, or clip by `CellOverflow`, align by the delimiter row (`:--`, `:-:`, `--:`), and are top-aligned
when a row spans several lines. The header uses `TableHeader`; the first column overlays `TableFirstColumn` on
`TableCell`; borders use `TableBorder`. A header with no body rows still renders. The column solver is an internal
class (`MarkdownColumnSolver`) so the `Table` widget can adopt it in a later release; 1.6.0 does not change `Table`.

- [x] Column solver, row rendering, borders for every glyph preset and every separator flag combination.
- [x] Positive tests: a table that fits renders with aligned columns and the exact natural widths; a squeezed table
      keeps every column at or above its minimum and the longest word unbroken; proportional sharing gives the wide
      column the cut; right and center alignment; `Expand`; `ShowOuterBorder` off; `ShowRowSeparators` on; `None`
      glyphs; `MaxColumnWidth` caps a long column and wraps it; first-column and header styles land on the right
      spans; `Stack`, `Shrink`, and `Clip` fallbacks each at a width where minimums do not fit.
- [x] Negative and boundary tests: a one-column table; a header-only table; empty cells; a cell holding only inline
      code; a table at exactly the natural width and at one column less; width 1 with each fallback.

---

## Phase 4: API and integration

### M6: Public render API

**Status:** DONE
**Notes:**

The [entry points](#entry-points) on `MarkdownRenderer`. `Render` parses once and lays out once. `RenderAsync` does
the same work and checks the token between top-level blocks; it uses `ConfigureAwait(false)` and does not wrap the
work in `Task.Run`. `MeasureWidth` lays out at an effectively unbounded width (or `MaxWidth`) with every overflow
treated as `Clip`, and returns the widest row.

- [x] Overloads with full XML docs, including thread safety (stateless; safe from any thread as long as the options
      are not mutated during the call).
- [x] Tests: each overload; a cancelled token throws `OperationCanceledException`; `MeasureWidth` of a table equals
      the rendered width at that width; `Render(md, MeasureWidth(md))` never wraps.

### M7: `Pane.WriteMarkdown`

**Status:** DONE
**Notes:**

`PaneLine.Wrapped(width)` (`src/TUIKit/Content/PaneLine.cs:33`) already caches visual rows per width, and every pane
path (`CountRows`, `VisibleRows`, `BuildRows`, search) goes through it. A Markdown line keeps its source and a private
options snapshot, and its `Wrapped(width)` returns the layout at that width. A resize changes the width, which misses
the cache and reflows the block. Nothing else in the pane changes.

A Markdown block counts as one committed line for `ScrollbackCapacityLines` and for the "N new below" indicator.
`MaxLineLength` applies to the source. `SnapshotPlainLines` expands a block into its rows at the last rendered width
(80 before the first render). `PaneLineHandle.Update` on a Markdown line turns it into a plain line;
`UpdateMarkdown` on a plain line turns it into a Markdown line with default options.

- [x] `PaneLine` support, `Pane.WriteMarkdown` overloads, `PaneLineHandle.UpdateMarkdown`, snapshot expansion.
- [x] Positive tests: a table reflows when the pane renders at 80 and then at 40; `UpdateMarkdown` replaces content;
      options mutated after the write do not change the block; search finds text inside a block; capacity eviction
      removes a whole block.
- [x] Negative tests: null source and null options throw; `UpdateMarkdown` after `Remove` returns false.

### M8: `MarkdownView`

**Status:** DONE
**Notes:**

A widget for Markdown that is not part of a scrolling log: a help page, a details panel, a modal body. It holds
`Markdown` (string, not null) and `Options` (`MarkdownRenderOptions`, not null, copied on set), plus `Background`
and `ScrollOffset` (0 or more, clamped at render). `Measure` returns the available width and the row count at that
width. `Render` lays out at the surface width, caches by width, and draws from `ScrollOffset`. `ContentHeight`
reports rows at the last width; `TryGetFocusRect` returns false. Setters invalidate the cache; `Invalidate()` covers
in-place edits to the options.

- [x] Widget.
- [x] Tests: renders into a `BufferSurface`; `Measure` height matches the row count; scroll offset shows later rows and
      clamps; null setters throw; a negative offset throws.

### M9: `StreamingTranscript` width-aware mode

**Status:** DONE
**Notes:**

With `RenderOptions` set, a streamed block is a single Markdown pane line, updated with the source received so far.
A trailing partial line that starts with `|` is held back until its newline arrives, so a half-received table row
never flashes as a paragraph. `FinalizeBlock` commits the whole source. Re-laying out the block on each append is
linear in the block size, which is fine for model responses (a few kilobytes). The XML doc says so.
`RenderMarkdownWhileStreaming` off still collapses to one live line until `FinalizeBlock`, which then writes one
Markdown block.

- [x] Property and streaming path.
- [x] Tests: streaming a table in small chunks ends identical to `WriteMarkdown` of the whole text; a partial table row
      is held back; `FinalizeBlock` with nothing buffered is a no-op; null `RenderOptions` keeps the 1.5.0 output
      (an existing streaming suite stays green unchanged).

---

## Phase 5: Example application

### E1: Markdown tour page

**Status:** DONE
**Notes:**

- [x] A "Markdown that fits" tour page after "Streaming without losing your place". A `MarkdownView` shows a heading,
      nested lists with wrapping items, a quote, highlighted C#, and a four-column table. Keys cycle the border preset
      (Light, Rounded, Heavy, Double, Ascii, None), the overflow mode, the table fallback, and `MaxWidth`, and the page
      description names the current settings.
- [x] `--tour-once --page N` renders the page headlessly; the README's page number list is updated.

### E2: Harness transcript

**Status:** DONE
**Notes:**

- [x] `SimulatedAgent` writes its replies with `Pane.WriteMarkdown`, and one reply includes a comparison table, so
      the main harness shows a table reflowing when the window is resized.
- [x] The capability matrix row for streaming Markdown in `src/TUIKit.Example/README.md` describes the new behavior.

---

## Phase 6: Documentation

### D1: README

**Status:** DONE
**Notes:**

- [x] A "Markdown" section: one `WriteMarkdown` call, one fully configured `MarkdownRenderOptions`, and a picture of
      the fit and stacked table layouts. Compile-check every sample.

### D2: CHANGELOG

**Status:** DONE
**Notes:**

- [x] `## [1.6.0] - 2026-10-14` with an "Added" section naming every public type and member. No "Changed" section is
      needed: nothing changes by default.

### D3: Coverage and conformance

**Status:** DONE
**Notes:**

- [x] `docs/SURFACE_COVERAGE.md` rows for every new public type, and a `docs/CONFORMANCE.md` 1.6.0 addendum.

### D4: BUILDING_TERMINAL_APPS.md

**Status:** DONE
**Notes:**

- [x] "Rendering model output": when to use `WriteMarkdown`, `MarkdownView`, or `StreamingTranscript.RenderOptions`,
      and why committed blocks reflow.

---

## Phase 7: Verification and publishing

### P1: Build and test

**Status:** DONE
**Notes:**

- [x] `dotnet build src/TUIKit.sln -c Release` with zero warnings on all three targets.
- [x] `Test.Automated`, `Test.Xunit`, and `Test.Nunit` on `net8.0` and `net10.0`, all green. Record totals here.
- [x] The example's `--once`, `--tour-once` (every page), `--contract-once`, `--focus-once`, and `--focus-audit-once`
      modes exit 0.
- [x] Visual check of the Markdown page and the harness at three widths (wide, medium, narrow enough to stack).

### P2: Requirements audit

**Status:** DONE
**Notes:**

- [x] `CODE_STYLE.md` walk over every new or changed file: usings inside the namespace, no `var`, no tuples, one type
      per file, docs on every public member and none on private ones, `_PascalCase` fields, no `#if` outside the
      backend and shim, no `Console.Write*` in the library.
- [x] `grep -nP '\x{2014}'` over every changed file returns nothing. Box-drawing and bullet glyphs in code are
      expected.
- [x] Source compatibility: the 1.5.0 test sources compile unchanged. Behavioral compatibility: the 845 cases that
      existed at 1.5.0 pass unchanged.

### P3: Package and publish to NuGet

**Status:** DONE
**Notes:**

- [x] `dotnet pack src/TUIKit/TUIKit.csproj -c Release -o artifacts/1.6.0` produces `TUIKit.1.6.0.nupkg` and
      `TUIKit.1.6.0.snupkg`; inspect both (README, logo, font licenses, XML docs for all targets, portable PDBs).
- [x] Push both with the API key from an environment variable only, never written to a file or committed.
- [x] Verify `https://api.nuget.org/v3-flatcontainer/tuikit/index.json` lists 1.6.0.

### P4: Template

**Status:** DONE
**Notes:**

- [x] Update `templates/tuikit-app` to reference 1.6.0.

### P5: Commit and push

**Status:** DONE
**Notes:**

- [x] Commit to `main` in phase-sized commits, tag `v1.6.0`, and push the branch and the tag.
- [x] Implementation record filled in. The plan stays at the repository root, where the owner asked for it,
      until it has been reviewed; move it to `archive/` afterwards.

---

## Open work after 1.6.0

The `Table` widget still sizes columns with `Even` or `FitContent` and never wraps a cell. Moving it onto
`MarkdownColumnSolver` would give data tables the same fit-then-squeeze-then-stack behavior, but it changes `Table`
output, so it belongs in a release that can list it under "Changed". OSC 8 hyperlinks for Markdown links are also
left out: `LinkId` is registered per frame against hit rectangles, and a reflowing block would need to re-register
its link targets at every width. Both are worth doing. Neither should hold this release.
