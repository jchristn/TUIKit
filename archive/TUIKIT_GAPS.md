# TUIKit Gaps — Enabling Spectre.Console Removal in Consumers

> **✅ COMPLETE — shipped in TUIKit v0.2.0.** G1–G6 are implemented, documented, and covered by
> Touchstone tests (264 console / 265 xUnit / 265 NUnit cases, green on net8.0/net10.0; the library
> builds warning-clean on `netstandard2.0;net8.0;net10.0`). Delivered public API:
> `TUIKit.Markup.Escape` (G1), `TUIKit.Terminal.AnsiText.Render` (G2),
> `TUIKit.Rendering.InlineRenderer.ToAnsiLines` (G3), `NO_COLOR` handling +
> `CapabilityDetector.ResolveOutputColorDepth` (G4), `TUIKit.StyledConsole` (G5), and the extended
> `TUIKit.Widgets.Table` with `TableBorder`/`ColumnSizing`/`CellAlignment` + styled/markup rows (G6).
> **G7 = Option A** (mux rewrites `grey15` mux-side; no TUIKit change). Remaining are consumer/release
> actions, not TUIKit gaps: **publish** the 0.2.0 package to NuGet, then bump mux's pinned `TUIKit`
> reference and do the mux-side Spectre swap. This document is archived; see `TUIKIT_MIGRATION.md` §17.

**Status:** ✅ Complete (shipped in TUIKit v0.2.0). **Owner of the motivating need:** the `mux` CLI
(`C:\Code\Mux`), which wants to drop its dependency on **Spectre.Console** and use TUIKit for all
styled terminal output. This document enumerates the concrete capability gaps in TUIKit that block
that, with proposed public APIs, acceptance criteria, and tests, so a developer can implement them
and annotate progress.

> These gaps are about **styled one-shot / inline output to stdout** — printing styled lines and
> tables from a normal CLI command that is *not* a full-screen `TuiApplication`. TUIKit already has
> everything needed for full-screen interactive rendering.

---

## 0. How to use this checklist

Annotate each task's box as its state changes; add a short `— note: …` for decisions, PR links, or
blockers.

- `[ ]` not started · `[~]` in progress · `[x]` done (code + tests + docs) · `[!]` blocked · `[-]` dropped/superseded

A gap is **done** only when its code, XML docs, and Touchstone tests are in, the library builds
warning-clean on all target frameworks (`netstandard2.0;net8.0;net10.0`), and its **Acceptance
criteria** all hold. The whole effort is validated end-to-end by §9 (Definition of done), which
rebuilds `mux` against the new TUIKit with Spectre.Console's rendering usage removed.

## 0.1 Conventions for all new code

Match TUIKit's existing house style (see `CLAUDE.md` / existing source):

- `netstandard2.0;net8.0;net10.0` multi-target; `Nullable` enabled; `ImplicitUsings` disabled;
  `TreatWarningsAsErrors` — **no new warnings**.
- Namespace first; **`using` directives inside the namespace block**; System/Microsoft usings first
  (alphabetical), then others (alphabetical).
- **One public class/enum per file.** XML docs on every public member; document thread-safety and
  which exceptions are thrown (`<exception>`), plus default/min/max for configurable values.
- No `Console.Write*` inside library code except through the new writer's explicitly-provided
  `TextWriter` (see G5). Guard clauses + specific exception types.
- **Tests are Touchstone descriptors** in the existing shared test project, runnable through the
  console runner, xUnit, and NUnit — matching TUIKit's current test setup. Assert via headless output
  (`HeadlessBackend`, `Snapshot`) and string inspection; no reliance on a real TTY.

---

## 1. Why (motivation)

`mux` uses Spectre.Console purely as a **styled `println`**, never as a UI framework. Verified usage
across its 6 Spectre-touching files:

| Spectre API | Calls | Purpose |
|---|---:|---|
| `Markup.Escape(text)` | 122 | escape arbitrary text before interpolating into markup |
| `AnsiConsole.MarkupLine` / `Markup` | 63 | write styled lines to stdout |
| `new Table()` + `AnsiConsole.Write(table)` | ~18 | render tables to stdout (`TableBorder.Rounded`) |
| `AnsiConsole.WriteLine` | 4 | plain lines |

- **Styles used:** `dim`, `bold`, `italic`, `underline`; foreground `cyan green red yellow grey blue`
  and `grey15`; background `on grey15`. No `Live`/`Status`/`Progress`/prompt/full-screen usage at all.
- **Critical behavior:** output targets `Console.Out`; when stdout is redirected/piped or not a TTY,
  Spectre automatically emits **plain text (no ANSI)**. `mux`'s own tests capture stdout/stderr and
  assert on plain content, so any TUIKit-based replacement **must** degrade to plain identically.

## 2. Scope & non-goals

**In scope (this document):** TUIKit capabilities to render styled text and tables to a `TextWriter`
(stdout) inline, without a `TuiApplication`, with correct capability-based color degradation.

**Out of scope / not a TUIKit gap:**
- **CLI argument parsing.** `mux` keeps `Spectre.Console.Cli` (the command framework). TUIKit is not
  an argument parser. **Important consequence:** `Spectre.Console.Cli` depends on `Spectre.Console`
  *transitively*, so even after all rendering is migrated, the `Spectre.Console` package remains in
  the restore graph until `mux` also replaces `Spectre.Console.Cli`. Fully deleting the
  `Spectre.Console` package is therefore gated on a **separate mux-side** decision (replace the
  command host), tracked in the mux plan — not here. The gaps below let mux remove all *direct*
  Spectre.Console *usage*; they are necessary but, on their own, not sufficient to drop the package.
- Interactive/full-screen rendering (already supported).

## 3. What TUIKit already provides (build on these — no work needed)

Confirmed public and usable standalone:

- **Color/style model:** `TUIKit.Color` (`FromRgb`, `FromPalette`, `Default`), `TUIKit.CellStyle`
  (`With*` builders), `TUIKit.CellAttributes` (`Bold, Dim, Italic, Underline, Strikethrough, Reverse,
  …`).
- **Styled text builder:** `TUIKit.Text` (`Text.From`), `TUIKit.StyledText` fluent
  (`.Bold().Dim().Cyan().Foreground(Color).Background(Color).Append(...)`), with `.Spans`,
  `.ToPlainString()`.
- **Markup parser:** `TUIKit.Markup.Parse(string)` / `Parse(string, CellStyle)`. Syntax
  `[bold red]x[/]`; literal brackets via `[[`/`]]`. Named colors currently limited to
  `black red green yellow blue magenta cyan white gray/grey` (+ `#RRGGBB` + palette index 0–255).
- **SGR generation:** `TUIKit.Terminal.Ansi.Sgr(CellStyle, TerminalColorDepth)`, `ResetAttributes`, …
- **Capabilities:** `TUIKit.Terminal.CapabilityDetector.Detect(getEnv, interactive)`,
  `TerminalCapabilities`, `TerminalColorDepth { None, Ansi16, Palette256, TrueColor }`.
- **Color/escape utilities:** `TUIKit.Terminal.ColorQuantizer.ToPalette256/ToAnsi16`,
  `TUIKit.Content.AnsiStripper.Strip`.
- **Headless render path:** `TUIKit.Rendering.TerminalRenderer` + `TUIKit.Terminal.HeadlessBackend`
  (produces ANSI, but grid/absolute-cursor oriented), `TUIKit.Testing.Snapshot.ToText` /
  `RenderWidget` (plain only), `CellBuffer`, `BufferSurface`, `ISurface` (public).

The gaps below are the missing "last mile" that turns these primitives into a styled-stdout writer.

---

## 4. Gaps

Each gap lists the problem, the current state (with real type names), a proposed public API
(signatures are proposals — refine to fit TUIKit conventions), acceptance criteria, and tests.

### G1 — `Markup.Escape` helper
**Problem:** mux calls `Markup.Escape` 122×. TUIKit supports literal brackets only via in-parser
`[[`/`]]` doubling; there is **no public escape method**.

**Proposed API** (`src/TUIKit/Content/Markup.cs`, extend `TUIKit.Markup`):
```csharp
/// <summary>Escapes markup control characters so the text renders literally: '[' → '[[', ']' → ']]'.</summary>
public static string Escape(string text);
```

- [x] Implement `Markup.Escape`.
- [x] Touchstone tests.

**Acceptance:**
- `Markup.Escape("[dim]x[/]")` returns a string that, passed to `Markup.Parse`, yields the literal
  text `[dim]x[/]` with no styling.
- `Markup.Escape(null)` throws `ArgumentNullException`; empty string returns empty.
- Round-trip: `Parse(Escape(s)).ToPlainString() == s` for arbitrary `s`.

---

### G2 — Render `StyledText`/markup to an ANSI string (no cursor moves)
**Problem:** There is no one-call "styled text → ANSI-colored string." `StyledText.ToString()` /
`ToPlainString()` strip styling; the only colored path (`TerminalRenderer`) emits absolute cursor
moves. mux needs a flowing colored string for a single line/segment.

**Proposed API** (`src/TUIKit/Terminal/AnsiText.cs`, new `public static class TUIKit.Terminal.AnsiText`):
```csharp
/// <summary>Renders styled text to a string of SGR escape sequences + text, with a trailing reset,
/// quantized to <paramref name="depth"/>. Emits no cursor movement. When depth is None, returns plain text.</summary>
public static string Render(StyledText text, TerminalColorDepth depth);

/// <summary>Convenience: parses markup then renders. Equivalent to Render(Markup.Parse(markup), depth).</summary>
public static string Render(string markup, TerminalColorDepth depth);
```

Implementation walks `StyledText.Spans`, emits `Ansi.Sgr(span.Style, depth)` + span text, quantizing
via `ColorQuantizer` per `depth`, and a final `Ansi.ResetAttributes`. No `MoveTo`.

- [x] Implement `AnsiText.Render(StyledText, depth)` and the markup overload.
- [x] Touchstone tests.

**Acceptance:**
- `AnsiStripper.Strip(AnsiText.Render(st, TrueColor)) == st.ToPlainString()` (color is additive only).
- `AnsiText.Render(st, None) == st.ToPlainString()` (no escape sequences; verify no `\e`).
- Output contains an SGR sequence for a bold/red span at `TrueColor`, `Palette256`, and `Ansi16`
  (quantized), and none at `None`.
- Output contains **no** cursor-move sequences (`\e[…H`, `\e[…;…H`).

---

### G3 — Render a `CellBuffer` to colored, inline ANSI lines
**Problem:** To print a **widget** (e.g. a Table) one-shot in color, you render it to a `CellBuffer`
(`Snapshot.RenderWidget` does this but returns plain text). There is **no public `CellBuffer` → colored
lines** helper; `TerminalRenderer` only emits an absolute-cursor diff and `Snapshot.ToText` is plain.

**Proposed API** (`src/TUIKit/Rendering/InlineRenderer.cs`, new `public static class TUIKit.Rendering.InlineRenderer`):
```csharp
/// <summary>Converts each row of a cell buffer to an ANSI-styled string (SGR runs, trailing reset),
/// with trailing blank cells trimmed and no cursor movement. Rows are returned top-to-bottom.
/// When depth is None, rows are plain text (matching Snapshot.ToText semantics).</summary>
public static IReadOnlyList<string> ToAnsiLines(CellBuffer buffer, TerminalColorDepth depth);
```

(Adjacent cells with equal `CellStyle` should share one SGR run; continuation cells of wide glyphs are
skipped, mirroring `Snapshot.ToText`.)

- [x] Implement `InlineRenderer.ToAnsiLines`.
- [x] Touchstone tests.

**Acceptance:**
- For a buffer filled by `Snapshot.RenderWidget(widget, w, h)` semantics, `ToAnsiLines(buffer, None)`
  equals the lines of `Snapshot.ToText(buffer)`.
- `AnsiStripper.Strip(string.Join("\n", ToAnsiLines(buffer, TrueColor)))` equals the plain
  `Snapshot.ToText(buffer)` (ignoring trailing-space normalization).
- No line contains a cursor-move sequence.

---

### G4 — Capability resolution for output (`NO_COLOR` + redirection)
**Problem:** The writer (G5) must pick a color depth from the *environment*: honor `NO_COLOR`, drop to
plain when stdout is redirected/not a TTY, and otherwise use the detected depth. Today: `NO_COLOR` is
**not honored anywhere** (grep-confirmed), and TTY/redirection is only observable by constructing a
heavyweight `ConsoleBackend` (`IsInteractive`), not via a standalone helper.

**Proposed API:**
```csharp
// Extend src/TUIKit/Terminal/CapabilityDetector.cs
/// <summary>Honors the NO_COLOR convention: when the NO_COLOR environment variable is present and
/// non-empty, returns capabilities with ColorDepth = None regardless of other signals.</summary>
// (Fold NO_COLOR handling into the existing Detect(getEnv, interactive) overload.)

/// <summary>Resolves the color depth appropriate for writing to <paramref name="output"/>:
/// None when the writer is redirected/not a TTY, when NO_COLOR is set, or when TERM=dumb; otherwise
/// the detected depth. Intended for one-shot styled output.</summary>
public static TerminalColorDepth ResolveOutputColorDepth(TextWriter output);
```

Redirection check: for `Console.Out`/`Console.Error`, use `Console.IsOutputRedirected` /
`IsErrorRedirected`; for any other `TextWriter`, treat as non-interactive (plain) unless the caller
overrides depth explicitly (G5 ctor).

- [x] Add `NO_COLOR` handling to `CapabilityDetector.Detect`.
- [x] Add `ResolveOutputColorDepth(TextWriter)`.
- [x] Touchstone tests (inject env via the `getEnv` delegate; simulate redirected via a non-console `TextWriter`).

**Acceptance:**
- `Detect` returns `ColorDepth == None` when `getEnv("NO_COLOR")` is non-empty, even with
  `COLORTERM=truecolor`.
- `ResolveOutputColorDepth` returns `None` for a plain `StringWriter` and for `TERM=dumb`.
- With `NO_COLOR` unset and `COLORTERM=truecolor` on an interactive writer, returns `TrueColor`.

---

### G5 — `StyledConsole`: the inline styled writer (the core capability)
**Problem:** This is the direct replacement for `AnsiConsole.Markup/MarkupLine/WriteLine/Write(table)`.
Nothing public writes styled, flowing output to a `TextWriter` at the current cursor position. The
existing colored renderer is full-screen/absolute-cursor; the built-in "line mode" is `private`,
`Pane`-only, and colorless.

**Proposed API** (`src/TUIKit/StyledConsole.cs`, new `public sealed class TUIKit.StyledConsole`):
```csharp
public sealed class StyledConsole
{
    /// <summary>Creates a writer over an explicit output and color depth (no auto-detection).</summary>
    public StyledConsole(TextWriter output, TerminalColorDepth colorDepth);

    /// <summary>Writer over Console.Out with depth resolved via CapabilityDetector.ResolveOutputColorDepth
    /// (plain when redirected / NO_COLOR / dumb).</summary>
    public static StyledConsole ForStandardOutput();
    /// <summary>As above, over Console.Error.</summary>
    public static StyledConsole ForStandardError();

    /// <summary>The resolved color depth; None means output is plain.</summary>
    public TerminalColorDepth ColorDepth { get; }
    /// <summary>Default width used when rendering widgets and the terminal width is unknown. Default 80, min 1.</summary>
    public int DefaultWidth { get; set; }

    public void Write(StyledText text);
    public void WriteLine(StyledText text);
    public void WriteLine();
    public void Markup(string markup);        // Write(Markup.Parse(markup))
    public void MarkupLine(string markup);    // WriteLine(Markup.Parse(markup))
    public void Write(string text);           // literal, no markup parsing
    public void WriteLine(string text);

    /// <summary>Renders a widget to its own lines and writes them (each followed by a newline).
    /// Width defaults to the current terminal width, else DefaultWidth. Height from Measure.</summary>
    public void Write(IWidget widget, int? width = null);
    public void WriteLine(IWidget widget, int? width = null);
}
```

Behavior: text paths use `AnsiText.Render` (G2); widget paths render into a `CellBuffer`
(`Snapshot.RenderWidget` semantics) then `InlineRenderer.ToAnsiLines` (G3); when `ColorDepth == None`
everything is plain. **Never** enters alt-screen, **never** emits cursor moves — output flows at the
current position. Writes go only to the injected `TextWriter`.

- [x] Implement `StyledConsole` (ctor + factories + all Write/Markup overloads + widget rendering).
- [x] Touchstone tests (write to a `StringWriter`; assert plain vs ANSI by depth; assert widget lines).
- [x] Doc example in `BUILDING_TERMINAL_APPS.md` ("Styled one-shot output without a full-screen app").

**Acceptance:**
- `new StyledConsole(sw, None)` + `MarkupLine("[bold red]hi[/]")` writes `hi\n` (no escapes) to the
  `StringWriter`.
- Same with `TrueColor` writes SGR + `hi` + reset + `\n`; `AnsiStripper.Strip(result).TrimEnd() == "hi"`.
- `ForStandardOutput()` yields `ColorDepth == None` when `Console.IsOutputRedirected` is true (the case
  in mux's in-process tests) — i.e. captured output is plain.
- `Write(table)` emits the table's rows as flowing lines with no cursor-move sequences.
- Writing multiple `MarkupLine`s produces output whose stripped form equals the concatenated plain
  lines (proves inline flow, not overwrite).

---

### G6 — `Table` parity (borders, styled cells, auto-width, alignment)
**Problem:** `TUIKit.Widgets.Table` is far below Spectre's: headers-only ctor, **evenly-split columns**
(no auto-size/alignment), **no borders**, **plain `string` cells only** (no markup/styling), hard
substring truncation. mux tables use `TableBorder.Rounded`, markup in headers and cells, and rely on
content-fit column widths.

**Proposed API** (extend `src/TUIKit/Widgets/Table.cs`; keep existing members for back-compat):
```csharp
public enum TableBorder { None, Square, Rounded }   // new file src/TUIKit/Widgets/TableBorder.cs

// additive on Table:
public Table(string[] headers, TableBorder border);
public TableBorder Border { get; set; }             // default None (back-compat)
public void AddRow(params StyledText[] cells);       // styled/markup-capable row
public void AddMarkupRow(params string[] cells);     // convenience: parses each cell as markup
public ColumnSizing Sizing { get; set; }             // Even (current default) | FitContent
// optional: per-column alignment
public enum CellAlignment { Left, Center, Right }
public void SetAlignment(int columnIndex, CellAlignment alignment);
```

Rendering: draw box-drawing borders per `Border` (Rounded = `╭─╮ │ ╰─╯ ├┼┤` set), size columns to
content when `Sizing == FitContent` (clip with `…` when constrained), render styled cells via the
cell style pipeline so it works both into a live `ISurface` and via `StyledConsole` (G5). Header
styling stays bold+accent but configurable.

- [x] Add `TableBorder` (+ `CellAlignment`/`ColumnSizing`) and additive `Table` API; keep old API working.
- [x] Implement border drawing, content-fit sizing, styled/markup cells, alignment.
- [ ] Touchstone snapshot tests (plain via `Snapshot.ToText`, colored via `InlineRenderer.ToAnsiLines`).

**Acceptance:**
- A `Rounded` table with markup cells renders box-drawing borders; `Snapshot.ToText` shows the border
  glyphs and aligned columns sized to content.
- Styled cell content survives to ANSI output (a `[green]ok[/]` cell yields an SGR run under `TrueColor`).
- Existing `new Table(headers)` + `AddRow(string[])` behavior is unchanged (no border, even columns) —
  back-compat verified by keeping/adjusting current tests.

---

### G7 — (Optional) Expanded named colors for markup compatibility
**Problem:** mux markup uses `grey15` and `on grey15`; TUIKit markup knows only `gray/grey` (one name).
Two ways to resolve:

- **Option A (recommended, mux-side, no TUIKit change):** mux rewrites `grey15` → a TUIKit-supported
  form (`#262626` or palette index `235`) and `on grey15` → `on #262626`. Zero TUIKit work.
- **Option B (TUIKit):** extend `Markup` color parsing with the Spectre-style grey scale
  (`grey0`–`grey100`) and bright names.

- [-] Default to Option A (tracked in the mux prerequisites). Implement Option B only if broad
  Spectre-markup compatibility is desired across consumers. — note the decision here.

**Acceptance (if Option B taken):** `Markup.Parse("[grey15]x[/]")` produces the expected palette/RGB
color; unknown names throw or fall back per existing policy.

---

## 5. Suggested build order

Dependency order (each builds on the prior):

1. **G1** (escape) and **G4** (capabilities) — independent, small, unblock everything.
2. **G2** (StyledText → ANSI).
3. **G3** (CellBuffer → ANSI lines).
4. **G6** (Table) — needs nothing new but pairs with G3 for colored table tests.
5. **G5** (StyledConsole) — ties G2/G3/G4 together; the consumer-facing surface.
6. **G7** — optional/deferred.

---

## 6. Testing requirements

- Every gap ships Touchstone descriptors in the shared test project, green through the console runner,
  xUnit, and NUnit on `net8.0` and `net10.0` (and `netstandard2.0` where the library targets it).
- Assertions use `HeadlessBackend`, `Snapshot`, `AnsiStripper`, and `StringWriter` capture — **no real
  TTY**. Inject environment via the `CapabilityDetector` `getEnv` delegate; simulate redirection with a
  non-console `TextWriter`.
- Keep/repair existing `Table` tests to prove back-compat (G6).

---

## 7. Release / coordination

- [x] Land G1–G6 (G7 per decision) behind a **minor TUIKit version bump** (e.g. `0.2.0`), with
  `CHANGELOG.md` entries and `BUILDING_TERMINAL_APPS.md` coverage of `StyledConsole`.
- [ ] Publish the package. `mux` then bumps its pinned `TUIKit` reference and does the consumer-side
  swap (see the mux plan's "Spectre.Console removal — prerequisites").
- [ ] Reminder: the TUIKit source is developed here but consumed by mux **as a NuGet package** — mux
  never project-references it. Cutting a package version is the hand-off.

---

## 8. Non-gaps (explicitly out of scope)

- CLI argument parsing / command dispatch (mux's `Spectre.Console.Cli`) — not a TUIKit concern.
- Interactive full-screen rendering — already supported; mux's interactive UI rewrite consumes the
  existing TUIKit surface API and, with **G6** (Table) and **G1** (escape), needs nothing new here.
- Spinners/progress/status/prompts — mux does not use Spectre for these; add only if a future consumer
  needs one-shot equivalents.

---

## 9. Definition of done (validated against a real consumer)

- [x] **G1–G6 complete** (G7 decided), library warning-clean on all TFMs, all Touchstone tests green.
- [x] `StyledConsole` can express **every** Spectre usage mux has: escaped interpolation (G1), styled
  lines in the used styles `dim/bold/italic/underline` + `cyan/green/red/yellow/grey/blue` fg and
  bg (G2/G5), and `Rounded`-bordered tables with styled cells (G5/G6).
- [ ] **Redirection parity:** writing through `StyledConsole.ForStandardOutput()` while
  `Console.Out` is redirected produces **plain** text (so mux's in-process CLI tests that capture and
  assert on stdout/stderr keep passing).
- [ ] **Proof:** a throwaway sample (or the mux migration branch) replaces mux's
  `AnsiConsole.Markup*`/`Markup.Escape`/`new Table()`+`AnsiConsole.Write` with the new TUIKit APIs and
  the mux test suite stays green — confirming behavioral parity before mux deletes its Spectre.Console
  *usage*.
- [x] Documented clearly that removing the `Spectre.Console` **package** from mux additionally requires
  replacing `Spectre.Console.Cli` (transitive owner) — a mux-side task, not a TUIKit gap.
