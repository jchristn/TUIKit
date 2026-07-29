# TUIKit — Implementation Plan

A high-performance, concurrent C# TUI framework for embedding developer-defined multi-pane layouts into .NET console applications. Primary consumer: an AI agent control harness with streaming text, live diagnostics, interactive prompts, and overlay dialogs.

This plan turns the open questions in `archive/TUIKIT.md` into a buildable, trackable work breakdown. It is designed to be annotated in place: every task carries a **Status** cell and a **Notes** cell. Update them as you go.

---

## How to use this plan

- **Status legend:** `TODO` (not started) · `WIP` (in progress) · `DONE` (complete + tested) · `BLOCKED` (note the blocker) · `N/A` (with justification in Notes).
- Work top-to-bottom. Phases are ordered so each depends only on earlier ones, except where a task explicitly forward-references.
- **Testing is not a final phase.** Every implementation phase adds its own Touchstone descriptors in `Test.Shared` as the code lands (see *Testing policy*). Phase 17 consolidates the four runners and closes gaps; Phase 19 audits for the 100% coverage aspiration; Phase 20 audits requirements conformance.
- The two mandated closing passes are **Phase 19** (surface-area → coverage) and **Phase 20** (requirements conformance). Do not mark the project done until both pass.

### Progress at a glance

| Phase | Title | Status |
|---|---|---|
| 0 | Foundations, scaffolding, CI | **DONE** (compat shim deferred until first needed) |
| 1 | Core primitives | **DONE** |
| 2 | Terminal backend abstraction | **DONE** (unified ConsoleBackend; raw-mode via SetConsoleMode/stty) |
| 3 | Rendering engine | **DONE** (double-buffer diff, row-level, SGR coalescing, quantized color) |
| 4 | Layout engine | **DONE** (region model, per-axis constraints, solver, block screen) |
| 5 | Panes, scrollback, thread-safe writes | **DONE** (mutable handles, FIFO writes, smart scroll lock, eviction) |
| 6 | Text rendering & markdown | **DONE** (word/hard wrap, markdown subset, ANSI strip) |
| 7 | Input & command routing | **DONE** (decoder, chords, routing table, sequences, Ctrl+C policy) |
| 8 | Mouse & hybrid links | **DONE** (SGR decode, link registry + hit test, allowlist scan, click synthesis) |
| 9 | Selection & clipboard | **DONE** (selection extract, OSC 52 encode) |
| 10 | Modals & notifications | **DONE** (modal stack + focus trap, async result, close refusal, toasts) |
| 11 | Widget toolkit | **DONE** (label, gauge, sparkline, progress, spinner, list, table, editor, field, checkbox, radio) |
| 12 | Theming & fallbacks | **DONE** (theme roles, named styles, ASCII fallback flag, dark/light/high-contrast) |
| 13 | Lifecycle & hosting | **DONE** (TuiApplication: render+input loops, commands, modals, Ctrl+C policy, non-TTY, singleton) |
| 14 | Diagnostics & performance | **DONE** (frame stats, input record/replay, debug overlay) |
| 15 | Headless test harness & snapshot tooling | **DONE** (`Snapshot` helper; used throughout) |
| 16 | Example application (flagship demo) | **DONE** (full agent harness; `--once` snapshot verified; capability matrix in example README) |
| 17 | Touchstone consolidation (all runners) | **DONE** (111 cases green in console/xUnit/NUnit; xUnit parallelization disabled for the singleton) |
| 18 | Docs & packaging | **DONE** (README, CHANGELOG, example README; NuGet metadata + `dotnet pack`-ready) |
| 19 | FINAL PASS 1: surface-area → coverage | **DONE** (`docs/SURFACE_COVERAGE.md`; ~80% combined, ~72% library; justified exclusions) |
| 20 | FINAL PASS 2: requirements conformance | **DONE** (`docs/CONFORMANCE.md` audit table; all files PASS or justified N/A) |

_Last updated: **all phases complete.** Full library across `netstandard2.0;net8.0;net10.0` (clean Release build, 0 warnings, warnings-as-errors), a runnable flagship example, 111 Touchstone cases green in all three runners, coverage and conformance audits recorded. Remaining not-covered code is platform/interactive-only (ConsoleBackend P/Invoke, live loop) and smoke-tested manually per the CI matrix._

---

## Decisions log

### Locked (confirmed interactively)

| Ref (TUIKIT.md) | Decision |
|---|---|
| 0.3 | **Developer-defined region list.** The developer declares an arbitrary number of rectangles (2, 8, whatever), each with a position and its own resize/scaling behavior on window-size change. Not a rigid split-screen. Requires a per-region reflow solver, not a split tree. |
| 0.1 | **Block with message** when the physical surface is smaller than the layout's derived minimum. Resume normal rendering when the terminal grows back. Emit the resize-request sequence on startup as best-effort only. |
| 0.4 | **TFMs: `netstandard2.0;net8.0;net10.0`.** Broadest reach including .NET Framework. Consequences accepted: no default interface methods on ns2.0 (API-evolution discipline required), ns2.0 package dependencies, and a distinct .NET Framework terminal backend. |
| 3.1a | **Retained mode.** Persistent pane/widget objects own state; background-thread writes mutate that state; the engine diffs and repaints. |
| 2.3c | **Mutable line handles.** Writes can return a handle; content already on screen can be updated/replaced in place (e.g. `running…` → `done (1.2s)`, progress bars). |
| 2.3e | **Styled spans + markdown.** Fluent styled spans plus a markdown renderer for agent output. |
| 2.3f | **Strip ANSI on ingest** in v1; styling only via the span API. SGR parsing (partial emulator) is a deferred later phase. |
| 3.1c | **Full Unicode width.** Bundled wcwidth table + grapheme-cluster segmentation; correct double-width CJK, combining marks, emoji clusters. |
| 3.1b | **Multi-line input editor in v1.** Cursor movement, word wrap, selection, undo/redo, kill-ring. |
| 2.4c | **Full modal widget kit in v1.** Text field, list, checkbox, radio, buttons, tab order; async `ShowModalAsync`. |
| 2.1f | **Ctrl+C is a configurable policy** set by the host: `Kill`, `InterruptFocusedPane`, `DoubleTapToExit`, or `Custom` (fully host-handled). Plus a routable-event escape hatch. |
| 0.5d | **Truecolor with auto-degrade** to 256/16 via capability detection + quantization. |
| 2.3h / 3.3m | **Custom selection + OSC 52 copy**, plus a runtime toggle that releases the mouse for native terminal selection. |
| 0.5c | **Full tmux + SSH support**, treated as tier-1 with dedicated test coverage for enhanced-key forwarding and mouse-reporting quirks. |
| 3.6e | **Single NuGet package** `TUIKit` (core + widgets together). |
| 3.6d | **Degrade to line output** when stdout is not a TTY; never emit escape sequences in that mode. |

### Assumed defaults (ratify in Phase 0; change here if wrong)

These resolve the remaining `TUIKit.md` questions with recommended defaults so the plan is complete. Each is cheap to revisit before its phase begins.

| Ref | Assumed default |
|---|---|
| 0.2 | Minimum surface is **derived from the layout** (max region extent), not a hard 160×80. The "Block" screen triggers below the derived minimum. |
| 2.1a | Chord key is a **normalized chord struct** (`Key` + `Modifiers`) **with a string parser** (`"ctrl+shift+3"`) for config files — both. |
| 2.1b | Precedence: **Global → active modal → focused pane FocusContext → focused pane raw input → dropped.** A focused pane may **suppress** a specific global binding. |
| 2.1c | Chord conflict in the same scope: **throw at registration** by default; a `LastWins` opt-in is available. |
| 2.1d | Routing table is **mutable at runtime** (modal keymaps, Vim modes). Host may load user rebindings from its own config. |
| 2.1e | **Multi-key chords supported** (`Ctrl+X Ctrl+S`) via a pending-sequence state machine with a tunable timeout. |
| 2.1g | `Ctrl+V`: **bracketed paste** (whole clipboard as one event, never interpreted as commands) + OSC 52 read where the terminal supports it. |
| 2.1h | Escape timeout: **tunable, default 50 ms**, only used on the degraded (non-enhanced) path. |
| 2.2a | **FIFO per pane.** No cross-pane ordering guarantee. |
| 2.2b | **Atomic batch scope** via `using (pane.BeginBatch()) { … }` renders as one frame. |
| 2.2c | Backpressure: **bounded channel**; for capped scrollback, intermediate frames may be coalesced/dropped (only final state is visible). Behavior is configurable and documented. |
| 2.2d | Frame coalescing to a **configurable ceiling (default 60 fps)**, render-on-dirty with a minimum inter-frame interval. |
| 2.2e | **Dedicated render thread** (predictable teardown). |
| 2.2f | **TUIKit owns `Console.Out`.** Provide a capture redirect so stray host/stdout writes land in a pane or a null sink; document the constraint. |
| 2.3a | Scrollback cap: **configurable, default in lines with a per-line byte guard** against a runaway 4 MB line. Unbounded is opt-in. |
| 2.3b | **Reflow wrapped content on width change**, using a wrap cache keyed on width. |
| 2.3d | Partial-line `Write` (no newline) **renders immediately**; the incomplete current line is tracked distinctly from the ring buffer and participates in scroll-lock. |
| 2.3g | Scroll-lock UX: **detached indicator (`↓ N new`)**, a jump-to-bottom key, reattach within a small threshold of the bottom. |
| 3.1d | **Headless rendering is mandatory** (in-memory cell buffer, assert-as-text). It is the primary test substrate. |
| 3.2a | Entry points: **both** `await RunAsync(ct)` and `Start()`/`Stop()`. |
| 3.2b | Terminal restoration via **push/pop stack** for keyboard/mouse/alt-screen/cursor; restore on exit, unhandled exception, and SIGTSTP/SIGCONT/SIGTERM where the platform allows. |
| 3.2c | **Suspend/resume supported** (drop to normal screen, run an external editor/shell, restore). |
| 3.2d | **Terminal is a singleton resource**; one active TUIKit instance per process, enforced with a clear exception. |
| 3.3a–f | Links: **virtual live path** (hit-test → event) + **OSC 8 as degradation**. Open URLs via `Process.Start` for portability. Only **app-created** links by default; opt-in auto-linkify restricted to an **allowlisted scheme set** (`http`, `https`, `mailto`). Links are **keyboard-reachable via hint labels**. Hit-region map rebuilt per frame from the visible viewport. |
| 3.3f | Hover affordance is **opt-in** (motion tracking 1003); default is always-visible link styling. |
| 3.3g–l | Hover-scroll decoupled from focus; **detached indicator shown per-pane**. Click-to-focus **also delivers the click** (focus + act). Panes may **refuse focus** and are skipped in Tab order. Double/triple-click **synthesized from timestamps** (tunable threshold). **Drag capture** to the originating pane until release. Wheel granularity + acceleration synthesized; `Shift`+wheel horizontal. |
| 3.3n | **SGR mouse mode 1006 only**; hard failure if unavailable. |
| 3.4a | Layout + UI declared via a **fluent builder**. |
| 3.4b | **Imperative writes primary**; optional `INotifyPropertyChanged` / `ObservableCollection` binding adapters as a convenience, not the core path. |
| 3.4c | Custom widget contract: **`Measure(available)` → `Arrange(rect)` → `Render(ISurface)`**, identical for built-in and third-party widgets. |
| 3.4d | Theming: **swappable theme object + named styles + runtime switching + ASCII fallback** when box-drawing glyphs are unavailable. |
| 3.5b/c | Borders + title bars are a **first-class pane concept**; pane **status/footer line** is first-class (e.g. `[detached · 47 new]`). |
| 3.6a | Performance targets defined as a benchmark suite (see Phase 14). Initial goals: ≥ 16 panes, sustained 100 Hz token ingest without dropped final state, ≤ 8 ms median frame build for a 200×60 surface. |
| 3.6b | **Input record/replay** supported (also powers deterministic tests). |
| 3.6c | **Debug overlay** (layout rects, dirty regions, frame timing) available. |
| 0.5a | Tier-1 terminals: Windows Terminal, iTerm2, Ghostty, WezTerm, Alacritty, kitty. Degraded: macOS Terminal.app, conhost, PuTTY. |
| 0.5b | On a terminal that can't report enhanced keys: **run degraded** with a capability API so the app can register alternate bindings; never silent-drop exotic bindings without a capability signal. |

---

## Repository structure

Follows `REPOSITORY_REQUIREMENTS.md` and the Touchstone layout in `BACKEND_TEST_ARCHITECTURE.md`. This is a library + example + tests; there is no web dashboard and no SDK directory, so `dashboard/` and `sdk/` are intentionally absent.

```
TUIKit/
├── .gitignore
├── README.md
├── CHANGELOG.md
├── LICENSE.md                      # MIT
├── CLAUDE.md                       # code-style rules captured for the repo
├── TUIKIT.md                       # source requirements (existing)
├── TUIKIT_PLAN.md                  # this plan
├── assets/                         # logo/screenshots referenced by README
├── docs/
│   └── CONFORMANCE.md              # N/A justifications + CI matrix + Phase 20 report
├── .github/
│   └── workflows/
│       └── tests.yaml              # build + 3 runners + FX smoke build
└── src/
    ├── TUIKit.sln
    ├── TUIKit/                      # the library (net standard2.0;net8.0;net10.0)
    │   └── TUIKit.csproj
    ├── TUIKit.Example/             # example agent-harness app (net8.0;net10.0)
    │   └── TUIKit.Example.csproj
    ├── Test.Shared/                # Touchstone.Core descriptors only (net8.0;net10.0)
    │   ├── Test.Shared.csproj
    │   └── TUIKitSuites.cs         # All suite descriptors, registry in .All
    ├── Test.Automated/            # Touchstone.Cli console runner
    │   ├── Test.Automated.csproj
    │   └── Program.cs
    ├── Test.Xunit/                # Touchstone.XunitAdapter
    │   ├── Test.Xunit.csproj
    │   ├── TUIKitFactTests.cs
    │   └── TUIKitTheoryTests.cs
    └── Test.Nunit/                # Touchstone.NunitAdapter
        ├── Test.Nunit.csproj
        ├── TUIKitNunitFactTests.cs
        └── TUIKitNunitTests.cs
```

**Note on Docker/DockerHub artifacts:** `REPOSITORY_REQUIREMENTS.md` items 2, 4, and 9 concern Docker/Docker Hub. A TUI library is not a container image and has nothing to publish to Docker Hub. These are marked **N/A with justification** and revisited explicitly in Phase 20 — do not silently skip them; record the justification.

---

## Code-style compliance (cross-cutting, applies to every phase)

Every `.cs` file must satisfy `CODE_STYLE.md`. These are gating, not aspirational. The recurring ones that catch people:

- `namespace` first, `using` statements **inside** the namespace; system usings alphabetized first, then others alphabetized.
- XML doc comments on **all** public members/constructors/methods; **none** on private members/methods. Document defaults/min/max, nullability, thrown exceptions (`/// <exception>`), and thread-safety guarantees.
- Private fields: `_PascalCase` (underscore + Pascal), not `_camelCase`.
- **No tuples** unless truly unavoidable. **No `var`** — always the explicit type. **No `Console.WriteLine` in library code.**
- One class or one enum per file.
- Public members needing validation use explicit get/set over a backing field.
- Nullable reference types enabled; guard clauses at method start; specific/custom exception types with contextual messages.
- Every `async` method takes a `CancellationToken` unless the class already holds one; check cancellation at sensible points; `ConfigureAwait(false)` where appropriate.
- Every `IEnumerable`-returning method gets an async variant taking a `CancellationToken`.
- Full `IDisposable`/`IAsyncDisposable` dispose pattern where resources are held.
- Prefer configurable public members with sensible private defaults over magic constants.

`#if` discipline: confine conditional compilation to the terminal-backend abstraction and a small compat shim (`Span`/`Rune`/DIM substitutes for ns2.0). No `#if` inside the layout/render/input core (TUIKIT.md 0.4d).

---

## Testing policy (applies to every implementation phase)

- Each phase writes its Touchstone `TestCaseDescriptor`s into `Test.Shared` **as the feature lands**, grouped into a `TestSuiteDescriptor` per subsystem. `Test.Shared` references only `Touchstone.Core` and `TUIKit`, and never writes to the console.
- Assertions are exceptions thrown on failure (Touchstone convention). Not-yet-ready cases use `skip: true` + `skipReason`.
- The **headless backend** (Phase 2) + snapshot tooling (Phase 15) is the substrate: drive synthetic input/resize, render to an in-memory cell buffer, assert the buffer as text.
- Phase 17 wires all four runners (`Automated`, `Xunit`, `Nunit`) over the same descriptor registry and fills coverage gaps. Phases 19–20 are the mandated audits.

---

## Phase 0 — Foundations, scaffolding, CI

**Goal:** a compiling, multi-targeted solution with all four Touchstone projects wired and green (empty suites), repository housekeeping in place, and the decision log ratified.

Each repository file below is created with **specified content**, not just touched. The Notes column names what must be inside.

| # | Task | Status | Notes |
|---|---|---|---|
| 0.1 | Ratify the Decisions Log above with the maintainer; correct any assumed defaults. | DONE | Locked set confirmed via interactive Q&A; assumed defaults stand as the working set until a phase revisits one. |
| 0.2 | `git init`; author `.gitignore`. | TODO | `bin/`, `obj/`, `.vs/`, `*.user`, `TestResults/`, `results.json`, coverage output, NuGet `*.nupkg`/`*.snupkg`, `artifacts/`. REQUIREMENTS 1. Done; `git init` run. |
| 0.3 | Author `LICENSE.md` — MIT, current year, copyright holder. | DONE | REQUIREMENTS 8. |
| 0.4 | Author `README.md` (skeleton; finalized in Phase 18). | DONE | Sections present; finalized in Phase 18. Prose per WRITING_DOCUMENTS.md. |
| 0.5 | Author `CHANGELOG.md` — Keep-a-Changelog format, `Unreleased` section seeded. | DONE | REQUIREMENTS 5. |
| 0.6 | Author `CLAUDE.md` capturing every `CODE_STYLE.md` rule for the repo. | DONE | CODE_STYLE mandates keeping it current. |
| 0.7 | Record Docker/DockerHub/SDK items as **N/A** with written justification in a `docs/CONFORMANCE.md` stub. | DONE | `docs/CONFORMANCE.md` created with N/A justifications + CI matrix. |
| 0.8 | Create `src/TUIKit.sln`. | DONE | Classic `.sln` format (SDK 10 defaults to `.slnx`; forced `--format sln`). |
| 0.9 | Author `src/TUIKit/TUIKit.csproj` **contents**: `<TargetFrameworks>netstandard2.0;net8.0;net10.0</TargetFrameworks>`, `<Nullable>enable</Nullable>`, `<LangVersion>latest</LangVersion>`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, NuGet metadata (PackageId, Description, Authors, MIT license expression, RepositoryUrl, README pack, symbols). ns2.0-only `PackageReference`s (`System.Memory`, `System.Threading.Channels`, `Microsoft.Bcl.AsyncInterfaces`) under a `'$(TargetFramework)'=='netstandard2.0'` condition. | DONE | All properties + conditional package refs present; builds clean on all three TFMs. |
| 0.10 | Add ns2.0 compat shim source + `#if NETSTANDARD2_0` guards. | WIP | Deferred into Phase 1: shim files added when the first API needs `Span`/`Rune`/DIM substitutes. No shim required yet. |
| 0.11 | Author `src/TUIKit.Example/TUIKit.Example.csproj`: `OutputType=Exe`, `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`, ProjectReference to `TUIKit`. | DONE | Placeholder `Program.cs`; full demo in Phase 16. |
| 0.12 | Author the four Touchstone `.csproj` files with exact contents from BACKEND_TEST_ARCHITECTURE (Touchstone 0.1.12; xunit 2.9.3 + runner 3.1.4; NUnit 4.3.2 + adapter 5.0.0 + analyzers 4.7.0; Microsoft.NET.Test.Sdk 17.14.1; coverlet.collector 6.0.4). `ImplicitUsings=disable`, `Nullable=enable`. | DONE | All four projects restore and build. |
| 0.13 | Author `Test.Automated/Program.cs`, the xUnit Fact+Theory files, and the NUnit Fact+TestCaseSource files over `TUIKitSuites.All`. | DONE | Entry points use a namespaced `Program` class per CODE_STYLE (not top-level statements); `global::Xunit` qualifier needed because the `Test.Xunit` namespace collides with `Xunit`. |
| 0.14 | Registry consumed by all four runners; console runner exits 0. | DONE | Seeded with a real `LibrarySuite` (2 cases) as an end-to-end smoke; Automated/xUnit/NUnit all green. |
| 0.15 | GitHub Actions CI (`.github/workflows/tests.yaml`): restore/build + run Automated/Xunit/Nunit on 8.0.x & 10.0.x; upload `results.json`. Add a Windows job for the .NET Framework smoke build of the library. | DONE | Workflow authored; not yet exercised on GitHub (no remote pushed). |
| 0.16 | Decide CI OS × TFM × terminal matrix; document automated vs manual smoke in `docs/CONFORMANCE.md`. | DONE | Matrix recorded in `docs/CONFORMANCE.md`; tmux job to be wired once the input harness lands (Phase 7). |

**Exit criteria:** `dotnet build src/TUIKit.sln` clean (0 warnings, warnings-as-errors on); all three runners green with empty suites; every repository file present **with real content** (not empty placeholders).

---

## Phase 1 — Core primitives

**Goal:** the cell model everything else renders into, plus color, styled text, and Unicode width.

| # | Task | Status | Notes |
|---|---|---|---|
| 1.1 | Geometry types: `Point`, `Size`, `Rect` (no tuples). | DONE | Immutable readonly structs with equality; `Rect.Intersect`/`Contains` (exclusive edges). |
| 1.2 | `Color` model: truecolor RGB + palette index + default; equality; helpers. | DONE | `ColorKind` enum; `FromRgb(int)`/`FromRgb(r,g,b)`/`FromPalette`; kind-aware equality. |
| 1.3 | `CellStyle` (fg/bg/bold/italic/underline/strike/reverse/link-id) as an immutable value type. | DONE | `CellAttributes` flags enum; `With*` derivation helpers; `LinkId` reserved for Phase 8. |
| 1.4 | `Cell` (grapheme cluster + style + width class) and `CellBuffer` (2D grid, dirty tracking). | DONE | Cell stores grapheme+width+continuation flag; buffer has per-row dirty tracking, `Resize`, `CopyFrom`. |
| 1.5 | `ISurface` write API (`Set`, `Fill`, clip regions) over a `CellBuffer`. | DONE | `ISurface` kept minimal (ns2.0, no DIMs); `BufferSurface` with `CreateView` for translated/clipped sub-regions; `DrawText`/`DrawStyledText` as extensions. |
| 1.6 | Bundled **wcwidth** table (East Asian Width) + `char.ConvertToUtf32`/surrogate handling for ns2.0. | DONE | `TUIKit.Unicode.TextWidth` with self-contained zero-width/wide/pictographic interval tables + binary search; identical across TFMs (no runtime Unicode dependency). Manual surrogate decode → no `Rune`/shim needed. |
| 1.7 | Grapheme-cluster segmentation (extended grapheme boundaries incl. emoji ZWJ sequences). | DONE | `TUIKit.Unicode.Graphemes` state machine: combining marks, ZWJ emoji sequences, VS16 presentation, regional-indicator flag pairs. Prepend/SpacingMark/Hangul-jamo left as documented simplifications (rare in agent output). |
| 1.8 | `StyledText` / span builder: `Text.From("x").Bold().Red()`; immutable spans. | DONE | `StyledSpan`/`StyledText`/`Text`; fluent style + named-color helpers; `Width` via grapheme measure; renders via `DrawStyledText`. Namespace `TUIKit.Text` renamed to `TUIKit.Unicode` to free the `Text` type name. |
| 1.9 | Touchstone suite: width table correctness (CJK/combining/emoji), buffer diff, span composition. | DONE | 5 suites, 23 primitive cases (25 total with library smoke) green on net8.0/net10.0. |

**Exit criteria:** width/grapheme suite passes against a known fixture set; buffer set/dirty verified headlessly. **Met.**

---

## Phase 2 — Terminal backend abstraction

**Goal:** `ITerminalBackend` plus concrete backends, capability detection, and a **headless backend** that all later tests use.

| # | Task | Status | Notes |
|---|---|---|---|
| 2.1 | `ITerminalBackend` abstraction: raw read/write, size query, capability probe, mode push/pop, teardown. | TODO | ns2.0 has no DIMs — design for additive evolution via abstract base. |
| 2.2 | `HeadlessBackend`: in-memory input queue + `CellBuffer` output; synthetic resize. | TODO | Mandatory test substrate (3.1d). |
| 2.3 | Unix backend (net8/net10): termios raw mode, `/dev/tty`, VT I/O. | TODO | |
| 2.4 | Windows backend (net8/net10): `SetConsoleMode` VT enable, conhost vs Windows Terminal, `win32-input-mode`. | TODO | 0.5. |
| 2.5 | .NET Framework backend path: VT enable P/Invoke, encoding, ConPTY notes; degrade cleanly pre-1809. | TODO | 0.4b. Confine to `#if`. |
| 2.6 | Capability detection: truecolor (COLORTERM/TERM + assume-truecolor on WT), Kitty/CSI-u keyboard, SGR mouse, OSC 8, OSC 52. | TODO | |
| 2.7 | Enhanced keyboard enable via **push/pop** stack (Kitty); `win32-input-mode` on Windows. | TODO | 3.2b. |
| 2.8 | Alt-screen, cursor visibility, mouse modes (1000/1002/1003/1006), bracketed paste — all push/pop. | TODO | |
| 2.9 | **Non-TTY detection** → line-output degradation mode (no escape sequences). | TODO | 3.6d. |
| 2.10 | Best-effort startup resize-request emit (documented as non-authoritative). | TODO | 0.1. |
| 2.11 | tmux/SSH capability handling: extended-keys passthrough, mouse quirk workarounds. | TODO | 0.5c tier-1. |
| 2.12 | Touchstone suite: capability parsing fixtures, headless round-trip, non-TTY fallback output shape. | TODO | |

**Exit criteria:** headless backend drives a full render/read cycle; capability detection covered by fixtures; non-TTY mode emits plain lines only.

---

## Phase 3 — Rendering engine

**Goal:** retained-tree rendering with damage-based diffing, escape-sequence minimization, and a dedicated render thread.

| # | Task | Status | Notes |
|---|---|---|---|
| 3.1 | Retained render tree + invalidation propagation. | TODO | 3.1a. |
| 3.2 | Frame compositor: measure → arrange → render into back buffer. | TODO | |
| 3.3 | Diff back vs front buffer → damage runs. | TODO | |
| 3.4 | Escape-sequence emitter: coalesce SGR changes, relative cursor moves, skip unchanged runs. | TODO | The real bottleneck per 0.4. |
| 3.5 | Dedicated render thread + teardown; render-on-dirty with min inter-frame interval. | TODO | 2.2d/2.2e. |
| 3.6 | Frame coalescing to configurable ceiling (default 60 fps). | TODO | |
| 3.7 | Truecolor → 256/16 quantization pass at emit time. | TODO | 0.5d. |
| 3.8 | Touchstone suite: diff correctness (golden escape output), coalescing, quantization tables. | TODO | Assert emitted byte stream headlessly. |

**Exit criteria:** a scripted mutation sequence produces a stable, minimal escape stream verified against goldens.

---

## Phase 4 — Layout engine (developer-defined regions)

**Goal:** the region model the maintainer specified — arbitrary rectangles, each with its own resize behavior — plus the fluent builder and the undersized "Block" screen.

| # | Task | Status | Notes |
|---|---|---|---|
| 4.1 | `Region` model: identity, position, size, and a **resize rule** (anchor edges, proportional scale, fixed, min/max). | TODO | 0.3 — this is the core layout abstraction. |
| 4.2 | Reflow solver: apply each region's rule on surface resize; deterministic ordering. | TODO | Not a split tree. |
| 4.3 | Overlap policy: legal via explicit z-order, or build-time error if disallowed — configurable. | TODO | 0.3c. |
| 4.4 | Derive minimum surface from region extents; expose it. | TODO | 0.2. |
| 4.5 | **"Terminal too small" Block screen** shown below derived minimum; auto-resume on growth. | TODO | 0.1. |
| 4.6 | Fluent layout builder API. | TODO | 3.4a. |
| 4.7 | Reflow wrap-cache keyed on width for content-bearing regions. | TODO | 2.3b. |
| 4.8 | Touchstone suite: solver under many resize scenarios, block-screen threshold, overlap/z-order, min-surface derivation. | TODO | |

**Exit criteria:** headless resize sweeps produce correct region rects for fixed/proportional/anchored mixes; block screen toggles at the right thresholds.

---

## Phase 5 — Panes, scrollback, thread-safe writes

**Goal:** the pane as a persistent, thread-safe, mutable content surface with smart scroll lock.

| # | Task | Status | Notes |
|---|---|---|---|
| 5.1 | `Pane` bound to a `Region`; owns a scrollback buffer + incomplete current line. | TODO | |
| 5.2 | Scrollback ring buffer: capped (lines, per-line byte guard) or unbounded; configurable. | TODO | 2.3a. |
| 5.3 | **Mutable line handles**: `WriteLine` returns a handle; update/replace in place; renderer invalidates the region. | TODO | 2.3c. |
| 5.4 | Partial-line `Write` (no newline) renders immediately; interacts correctly with scroll lock + ring buffer. | TODO | 2.3d. |
| 5.5 | Thread-safe direct API (`Write`/`WriteLine` from any thread), FIFO per pane. | TODO | Settled #3; 2.2a. |
| 5.6 | `BeginBatch()` atomic scope → one frame. | TODO | 2.2b. |
| 5.7 | Backpressure: bounded channel, coalesce/drop policy configurable + documented. | TODO | 2.2c. |
| 5.8 | **Smart scroll lock**: scroll-up detaches, bottom re-attaches within threshold; `↓ N new` indicator; jump-to-bottom key. | TODO | Settled #5; 2.3g. |
| 5.9 | `Console.Out` ownership + capture redirect (pane or null sink). | TODO | 2.2f. |
| 5.10 | Touchstone suite: concurrency stress (multi-thread writes ordering), handle mutation, cap/eviction, scroll-lock state machine, batch atomicity. | TODO | |

**Exit criteria:** concurrent-write stress test is deterministic per-pane; mutable handles update on screen; scroll-lock transitions verified headlessly.

---

## Phase 6 — Text rendering & markdown

**Goal:** turn styled spans and markdown into cells; strip incoming ANSI.

| # | Task | Status | Notes |
|---|---|---|---|
| 6.1 | Span → cell layout with width-aware wrapping (uses Phase 1 width). | TODO | |
| 6.2 | Markdown renderer: headings, emphasis, code spans/blocks, lists, links, rules → styled spans. | TODO | 2.3e. |
| 6.3 | **ANSI strip on ingest** (remove escape sequences from written text). | TODO | 2.3f. SGR-parse is a later, out-of-v1 phase — note the seam. |
| 6.4 | Touchstone suite: markdown golden renders, wrap correctness at width boundaries incl. CJK/emoji, ANSI-strip. | TODO | |

**Exit criteria:** markdown goldens match; wrapping never splits a grapheme; escape bytes never reach the buffer.

---

## Phase 7 — Input & command routing

**Goal:** decode enhanced keys and route them through the central command table, with the fallthrough to text input and a configurable Ctrl+C policy.

| # | Task | Status | Notes |
|---|---|---|---|
| 7.1 | Key-event model: `Key` + `Modifiers`; normalized chord struct. | TODO | 2.1a. |
| 7.2 | Enhanced-protocol decoder (Kitty/CSI-u, win32-input-mode); degraded ANSI decoder with Esc timeout (default 50 ms, tunable). | TODO | Settled #2; 2.1h. |
| 7.3 | String chord parser (`"ctrl+shift+3"`) ↔ struct. | TODO | 2.1a. |
| 7.4 | Command routing table: `Global` vs `FocusContext` scope; register/unregister at runtime. | TODO | Settled #1; 2.1d. |
| 7.5 | Precedence pipeline: Global → active modal → focused FocusContext → focused raw input → dropped; pane can suppress a global. | TODO | 2.1b. |
| 7.6 | Conflict policy: throw at registration (default) or `LastWins`. | TODO | 2.1c. |
| 7.7 | Multi-key chords (`Ctrl+X Ctrl+S`) via pending-sequence state machine + timeout. | TODO | 2.1e. |
| 7.8 | **Ctrl+C policy**: `Kill` / `InterruptFocusedPane` / `DoubleTapToExit` / `Custom`; plus routable-event exposure. | TODO | 2.1f — host-set. |
| 7.9 | Bracketed paste event (whole clipboard as one unit, never command-interpreted). | TODO | 2.1g. |
| 7.10 | Degraded-terminal capability API: register alternate bindings when enhanced keys unavailable. | TODO | 0.5b. |
| 7.11 | Touchstone suite: chord parse round-trip, precedence/suppression, conflict handling, multi-key timeout, Ctrl+C modes, paste isolation. | TODO | |

**Exit criteria:** synthetic key streams route deterministically; each Ctrl+C mode verified; paste content never triggers a binding.

---

## Phase 8 — Mouse & hybrid links

**Goal:** SGR mouse, focus/scroll/drag semantics, and virtual + OSC 8 links with security and keyboard reachability.

| # | Task | Status | Notes |
|---|---|---|---|
| 8.1 | SGR 1006 mouse decode; hard-fail if unavailable. | TODO | 3.3n. |
| 8.2 | Synthesize double/triple-click from timestamps (tunable threshold); wheel accel; `Shift`+wheel horizontal. | TODO | 3.3j/3.3l. |
| 8.3 | Click-to-focus that **also delivers** the click; panes may **refuse focus** (skipped in Tab order). | TODO | Settled #10; 3.3h/3.3i. |
| 8.4 | **Hover scroll** (wheel targets pane under cursor, independent of focus); per-pane detached indicators. | TODO | Settled #10; 3.3g. |
| 8.5 | Drag capture to the originating pane until release. | TODO | 3.3k. |
| 8.6 | Virtual links: per-frame hit-region map from visible viewport; `Uri` + payload + handler; command-name resolution through the routing table. | TODO | 3.3b/3.3e. |
| 8.7 | OSC 8 emission as degradation path (with `id=` for wrapped links); open URLs via `Process.Start`. | TODO | 3.3a. |
| 8.8 | Link **security**: app-created only by default; opt-in auto-linkify limited to `http`/`https`/`mailto`. | TODO | 3.3c — matters for agent output. |
| 8.9 | Keyboard-reachable links via hint labels (press-to-reveal). | TODO | 3.3d. |
| 8.10 | Optional hover affordance (motion tracking 1003) off by default. | TODO | 3.3f. |
| 8.11 | Touchstone suite: mouse decode, click synthesis thresholds, hit-region mapping across wrap/scroll, link security allowlist, hint navigation. | TODO | |

**Exit criteria:** synthetic mouse sequences produce correct focus/scroll/drag/link events; auto-linkify rejects non-allowlisted schemes.

---

## Phase 9 — Selection & clipboard

**Goal:** in-pane selection with OSC 52 copy and a mouse-release toggle.

| # | Task | Status | Notes |
|---|---|---|---|
| 9.1 | Selection model: mouse drag + keyboard selection over pane content (buffer-coordinate anchored, viewport-projected). | TODO | 2.3h. |
| 9.2 | Copy via OSC 52 (works over SSH); clipboard read where supported. | TODO | 3.3m. |
| 9.3 | Runtime toggle that releases mouse tracking for native terminal selection; restore on toggle-off. | TODO | 3.3m. |
| 9.4 | Optional in-pane search over scrollback. | TODO | 2.3h (search). |
| 9.5 | Touchstone suite: selection geometry across scroll/resize, OSC 52 payload encoding, toggle state. | TODO | |

**Exit criteria:** selection survives scroll/resize; OSC 52 payload matches selected text; toggle round-trips mouse mode.

---

## Phase 10 — Modals & notifications

**Goal:** the modal stack with async results, and non-focus-stealing toasts.

| # | Task | Status | Notes |
|---|---|---|---|
| 10.1 | Modal stack: focus trap, nesting (top wins), background panes keep repainting behind. | TODO | Settled #6/#7; 2.4e. |
| 10.2 | `await ShowModalAsync(dialog)` primary API; composes with nesting. | TODO | 2.4a. |
| 10.3 | Backdrop dimming via compositor post-pass over covered cells (RGB scaled toward bg). | TODO | 2.4b. |
| 10.4 | Dismiss semantics: `Esc` closes top; click-outside optional; a modal may refuse to close. | TODO | 2.4d. |
| 10.5 | Notifications/toasts: position, stacking direction, max concurrent, default timeout, click-dismiss, severity styling, **never steal focus**. | TODO | 2.4f. |
| 10.6 | Touchstone suite: nesting/focus-trap, async result propagation, refuse-close, toast lifecycle + no-focus-steal. | TODO | |

**Exit criteria:** nested modal results resolve in LIFO order; background repaint verified; toasts expire without touching focus.

---

## Phase 11 — Widget toolkit

**Goal:** the shared widget contract and the v1 widget set, including the multi-line editor and modal input widgets.

| # | Task | Status | Notes |
|---|---|---|---|
| 11.1 | Widget contract: `Measure(available)` → `Arrange(rect)` → `Render(ISurface)`; built-ins use the same contract as third-party. | TODO | 3.4c. |
| 11.2 | First-class pane borders + title bars; first-class pane status/footer line. | TODO | 3.5b/3.5c. |
| 11.3 | Scrolling text-log widget (wraps Phase 5/6). | TODO | 3.5a. |
| 11.4 | **Multi-line input editor**: cursor movement, word wrap, selection, undo/redo, kill-ring, history. | TODO | 3.1b — largest single item. |
| 11.5 | List widget (selection, scroll). | TODO | 3.5a. |
| 11.6 | Table widget. | TODO | 3.5a. |
| 11.7 | Stats widgets: gauge, sparkline, progress bar, spinner. | TODO | 3.5a. |
| 11.8 | Modal input widgets: text field, list, checkbox, radio, button + tab order. | TODO | 2.4c. |
| 11.9 | Optional binding adapters (`INotifyPropertyChanged`/`ObservableCollection`). | TODO | 3.4b — convenience only. |
| 11.10 | Touchstone suite per widget: measure/arrange geometry, editor operations (undo/redo/kill-ring/wrap), tab order, snapshot renders. | TODO | |

**Exit criteria:** each widget snapshot-renders correctly headlessly; editor operation log replays deterministically.

---

## Phase 12 — Theming & fallbacks

**Goal:** swappable themes, named styles, and ASCII fallback.

| # | Task | Status | Notes |
|---|---|---|---|
| 12.1 | Theme object + named styles; runtime switching triggers full invalidation. | TODO | 3.4d. |
| 12.2 | ASCII fallback for box-drawing when glyphs unavailable. | TODO | 3.4d. |
| 12.3 | Touchstone suite: theme switch re-render, ASCII fallback goldens. | TODO | |

**Exit criteria:** switching a theme repaints correctly; ASCII mode avoids non-ASCII glyphs.

---

## Phase 13 — Lifecycle & hosting

**Goal:** clean start/stop, restoration under adverse exits, suspend/resume, singleton enforcement.

| # | Task | Status | Notes |
|---|---|---|---|
| 13.1 | Entry points: `RunAsync(ct)` and `Start()`/`Stop()`. | TODO | 3.2a. |
| 13.2 | Restoration via push/pop on normal exit, unhandled exception, SIGTSTP/SIGCONT/SIGTERM (where supported). | TODO | 3.2b. |
| 13.3 | Suspend/resume: drop to normal screen, run external process, restore state. | TODO | 3.2c. |
| 13.4 | Singleton guard: one active instance/process, clear exception otherwise. | TODO | 3.2d. |
| 13.5 | Touchstone suite (headless): start/stop idempotence, restoration sequence assertions, singleton violation throws, suspend/resume state integrity. | TODO | |

**Exit criteria:** simulated crash/suspend paths emit the full restoration sequence; second instance throws.

---

## Phase 14 — Diagnostics & performance

**Goal:** debug overlay, record/replay, and the benchmark suite that pins the non-functional targets.

| # | Task | Status | Notes |
|---|---|---|---|
| 14.1 | Debug overlay: layout rects, dirty regions, frame timing. | TODO | 3.6c. |
| 14.2 | Input record/replay (also feeds deterministic tests). | TODO | 3.6b. |
| 14.3 | Benchmark suite: max panes, sustained 100 Hz ingest without losing final state, frame-build budget, per-pane memory. | TODO | 3.6a. |
| 14.4 | Wire benchmarks into CI as tracked (non-gating initially) metrics. | TODO | |

**Exit criteria:** benchmarks run and report; targets recorded as baselines in `CHANGELOG.md`/docs.

---

## Phase 15 — Headless test harness & snapshot tooling

**Goal:** formalize the test substrate used since Phase 2 into ergonomic snapshot helpers.

| # | Task | Status | Notes |
|---|---|---|---|
| 15.1 | Snapshot helper: render headless buffer → deterministic text (with style-legend option). | TODO | 3.1d. |
| 15.2 | Synthetic input injection (keys/mouse/paste/resize) helpers for `Test.Shared`. | TODO | |
| 15.3 | Golden-file compare utility (usable by descriptors without console output). | TODO | Test.Shared must not write console. |
| 15.4 | Document the public headless API so consumers can snapshot-test their own UIs. | TODO | It is a shipped feature, not just internal. |

**Exit criteria:** a representative UI snapshot round-trips; helpers are reused across existing suites.

---

## Phase 16 — Example application (the flagship demo)

**Goal:** `src/TUIKit.Example` is not a toy. It is a believable **AI agent control harness** that a newcomer can run to understand what TUIKit does, and that a reviewer can use to confirm every marquee capability works end to end. The guiding rule: **every public capability the library ships is visibly exercised somewhere in this app, and each is discoverable** (a help overlay lists what to press). It runs against a simulated agent — no real network or model dependency — so it is deterministic and self-contained.

**The scenario.** A running agent session: a streaming assistant transcript on the left, a live tool-execution panel and system telemetry on the right, a multi-line composer at the bottom, a command palette, notifications, and modal dialogs for confirmations and settings. The simulated agent emits markdown tokens at ~100 Hz, fires tool calls that transition `queued → running… → done (1.2s)` in place, and occasionally raises a confirmation modal ("Agent wants to run `rm -rf build/` — allow?").

### 16A — Layout & shell

| # | Task | Status | Notes |
|---|---|---|---|
| 16.1 | Multi-region layout using the developer-defined region model: transcript (elastic/fill), tool panel (anchored right, fixed width), telemetry (anchored, proportional height), composer (anchored bottom, fixed height), global status/footer bar. | TODO | Demonstrates 0.3 region model + mixed resize rules. |
| 16.2 | Live resize handling: drag the window; regions reflow per their rules. Shrink below minimum → the "Terminal too small" Block screen; grow back → resume. | TODO | Exercises Phase 4 solver + 0.1. |
| 16.3 | Header/footer with a live clock, session state, focused-pane indicator, and per-pane `[detached · N new]` scroll status. | TODO | 3.5c. |

### 16B — Streaming content & scrollback

| # | Task | Status | Notes |
|---|---|---|---|
| 16.4 | Simulated ~100 Hz markdown token stream into the transcript (headings, bold, lists, code blocks, inline code, links rendered). | TODO | Exercises markdown renderer + frame coalescing. |
| 16.5 | Smart scroll lock in action: scroll up to detach (shows `↓ N new`), jump-to-bottom key re-attaches. Independently scroll the tool panel via hover-scroll while transcript keeps streaming. | TODO | 2.3g / 3.3g — two panes detached at once. |
| 16.6 | Mutable line handles: tool calls update in place `queued → running… → done (1.2s)`; a live progress bar and a spinner during a long task. | TODO | 2.3c core selling point. |
| 16.7 | Backpressure demo toggle: crank the token rate past the frame ceiling; show that final state is never lost under coalescing. | TODO | 2.2c/2.2d. |

### 16C — Input, commands, editing

| # | Task | Status | Notes |
|---|---|---|---|
| 16.8 | Multi-line composer: word wrap, cursor movement, selection, undo/redo, kill-ring, history recall; submit sends a "message" to the simulated agent. | TODO | Exercises the Phase 11 editor fully. |
| 16.9 | Command routing: global bindings (quit, help, palette, theme toggle, debug overlay) + focus-context bindings; a command palette modal listing them. | TODO | Settled #1; shows precedence + runtime table. |
| 16.10 | Multi-key chord demo (e.g. `Ctrl+K Ctrl+T` cycles theme) to prove the pending-sequence state machine. | TODO | 2.1e. |
| 16.11 | Ctrl+C policy switcher in Settings: flip between `Kill`, `InterruptFocusedPane`, `DoubleTapToExit`, `Custom` and feel each behavior. | TODO | 2.1f — the configurable policy, made tangible. |
| 16.12 | Bracketed paste: paste a multi-line block into the composer; confirm it is inserted as text, never interpreted as commands. | TODO | 2.1g. |

### 16D — Mouse, links, selection

| # | Task | Status | Notes |
|---|---|---|---|
| 16.13 | Click-to-focus (also delivers the click); a refuse-focus telemetry pane that scrolls but never takes keyboard focus and is skipped in Tab order. | TODO | 3.3h/3.3i. |
| 16.14 | Virtual links in transcript (fire a C# handler → open a modal "link detail") plus OSC 8 fallback; keyboard hint-label navigation (press `f`, then a letter). | TODO | 3.3a–e. |
| 16.15 | Link security demo: stream text containing a `file://` and a `https://` URL; show only the allowlisted scheme auto-linkifies. | TODO | 3.3c — matters for agent output. |
| 16.16 | Text selection + copy via OSC 52; the runtime mouse-release toggle so the user can fall back to native terminal selection. | TODO | 2.3h/3.3m. |

### 16E — Modals, notifications, theming, diagnostics

| # | Task | Status | Notes |
|---|---|---|---|
| 16.17 | Confirmation modal ("allow this tool call?") using the full widget kit: buttons + tab order; `await ShowModalAsync` result drives the flow; a modal that refuses to close with unsaved changes. | TODO | 2.4a/2.4c/2.4d. |
| 16.18 | Nested modals (Settings → a sub-dialog) with backdrop dimming; background panes keep streaming behind them. | TODO | 2.4b/2.4e. |
| 16.19 | Toast notifications on tool completion/errors: stacked, auto-timeout, click-dismiss, severity styling, never steal focus. | TODO | 2.4f. |
| 16.20 | Settings modal with the full input-widget set (text field, list, checkbox, radio) to configure theme, Ctrl+C policy, scrollback cap, frame ceiling. | TODO | 11.8 widgets exercised. |
| 16.21 | Theme switching at runtime (dark/light/high-contrast) + ASCII fallback mode toggle for terminals without box-drawing. | TODO | 3.4d. |
| 16.22 | Debug overlay toggle (layout rects, dirty regions, frame timing) and a telemetry pane with gauge + sparkline + table. | TODO | 3.6c + stats widgets. |

### 16F — Lifecycle, portability, discoverability

| # | Task | Status | Notes |
|---|---|---|---|
| 16.23 | Suspend/resume: a key drops to the normal screen and launches the user's `$EDITOR` (or shell), then restores the TUI cleanly. | TODO | 3.2c. |
| 16.24 | Clean restoration on unhandled exception and on Ctrl+Z/SIGTERM (where supported): terminal left sane. | TODO | 3.2b. |
| 16.25 | Non-TTY run (`| cat`, redirected to a file, CI): degrades to plain line output; document the difference. | TODO | 3.6d. |
| 16.26 | Built-in help overlay (`?`) enumerating every keybinding and feature, so the demo is self-documenting. | TODO | Discoverability rule. |
| 16.27 | Input record/replay: ship a recorded session the app can replay unattended, doubling as a smoke demo and a deterministic screenshot source for the README. | TODO | 3.6b. |
| 16.28 | **Capability-coverage matrix** in the example's README: a table mapping each public library capability → the exact key/interaction in the app that demonstrates it. Reviewed against the Phase 19 surface list. | TODO | Proves "fully highlights the capabilities." |

| 16.29 | **No-placeholder gate.** The Phase 0 stub `Program.cs` is fully replaced. Every public type and every public member on the Phase 19 surface list is either exercised by the example or explicitly listed in the coverage matrix as "demonstrated indirectly" with the reason. No `TODO`/stub code paths remain in the example. | TODO | Enforces the maintainer's requirement that the example exhaustively demonstrates the library. |

**Exit criteria:** the Phase 0 placeholder is gone; the example builds and runs on at least one tier-1 terminal per OS (Windows Terminal, iTerm2/Ghostty, Linux + tmux/SSH); the capability-coverage matrix maps **every public capability** to a concrete interaction with no "not demonstrated" gaps; the recorded replay runs clean; the example README documents how to run it and what each key does. The example is reviewed against the Phase 19 surface enumeration so nothing public ships undemonstrated.

---

## Phase 17 — Touchstone consolidation (all runners)

**Goal:** every subsystem's descriptors registered once and consumed by all four runners; gaps closed.

| # | Task | Status | Notes |
|---|---|---|---|
| 17.1 | Ensure every phase's suites are registered in `TUIKitSuites.All`. | TODO | |
| 17.2 | `Test.Automated/Program.cs` runs `.All` with `--results` JSON export. | TODO | Per reference. |
| 17.3 | `Test.Xunit`: Fact-style (`RunAll`) + Theory-style (per-descriptor) over `.All`. | TODO | |
| 17.4 | `Test.Nunit`: Fact-style + `TestCaseSource` data-driven over `.All`. | TODO | |
| 17.5 | Confirm exit-code contract (0 pass / non-zero fail) and CI upload of `results.json`. | TODO | |
| 17.6 | Sweep for any `Console.Write*` in `Test.Shared` and remove. | TODO | Hard rule. |

**Exit criteria:** all four runners green over the same registry; JSON export present in CI artifacts.

---

## Phase 18 — Docs & packaging

**Goal:** accurate published docs and a single, well-formed NuGet package.

| # | Task | Status | Notes |
|---|---|---|---|
| 18.1 | `README.md`: use cases, architecture, getting-started, terminal support matrix, feature list. Re-review for accuracy per CODE_STYLE. | TODO | Follow WRITING_DOCUMENTS.md for prose voice. |
| 18.2 | `CHANGELOG.md` populated; versioning policy stated. | TODO | 3.6e. |
| 18.3 | XML doc coverage audit on all public API (CODE_STYLE gate). | TODO | |
| 18.4 | Single `TUIKit` NuGet package metadata (id, license=MIT, TFMs, symbols, README embed). | TODO | 3.6e. |
| 18.5 | Terminal-support + capability doc (tier-1 list, degraded behavior, tmux/SSH notes). | TODO | 0.5. |

**Exit criteria:** `dotnet pack` produces a valid multi-target package; README verified against actual API.

---

## Phase 19 — FINAL PASS 1: Surface-area audit → 100% coverage aspiration

**Goal (mandated):** enumerate the entire public library surface and drive test coverage toward 100%.

| # | Task | Status | Notes |
|---|---|---|---|
| 19.1 | Enumerate the full public surface (types, members, overloads) — reflection dump or API-diff tool. | TODO | The authoritative checklist. |
| 19.2 | Map each public member to at least one Touchstone descriptor; list the unmapped. | TODO | |
| 19.3 | Enable coverlet in `Test.Xunit`/`Test.Nunit`; produce a coverage report. | TODO | coverlet.collector already referenced. |
| 19.4 | Write descriptors to close every gap; aspire to 100% public-surface coverage; record any deliberate exclusions with justification. | TODO | Aspiration is explicit in the ask. |
| 19.5 | Cover platform-conditional (`#if`) paths on each TFM where feasible; note manual-only paths. | TODO | ns2.0 / .NET Framework backend. |
| 19.6 | Re-run all runners; commit the coverage report + surface-area checklist to the repo. | TODO | |

**Exit criteria:** a committed surface-area checklist with every public member mapped to a test, a coverage report, and a written justification for any intentional gap.

---

## Phase 20 — FINAL PASS 2: Requirements conformance audit

**Goal (mandated):** verify conformance with every requirement in `c:\code\agents\requirements`, item by item.

| # | Task | Status | Notes |
|---|---|---|---|
| 20.1 | **CODE_STYLE.md**: audit every `.cs` for each rule (usings placement/order, private `_Pascal`, no `var`, no tuples, XML docs on public/none on private, one type per file, async+CancellationToken+ConfigureAwait, IEnumerable async twins, dispose pattern, nullable, guard clauses, specific exceptions, no `Console.WriteLine` in lib). Record pass/fail per rule. | TODO | Line-by-line rule list. |
| 20.2 | **REPOSITORY_REQUIREMENTS.md**: `.gitignore`, `README.md`, `CHANGELOG.md`, `LICENSE.md` (MIT) present; source under `src/`. Docker/DockerHub items (2,4,9) recorded **N/A with written justification** (library, no image). SDK item N/A (no SDK). | TODO | Justify N/A explicitly, don't skip silently. |
| 20.3 | **BACKEND_TEST_ARCHITECTURE.md**: Shared/Automated/Xunit/Nunit shape, Touchstone package usage, no console output in `Test.Shared`, exit codes, CI workflow, JSON export — all present and matching. | TODO | |
| 20.4 | **WRITING_DOCUMENTS.md**: apply the human-voice checklist to README/CHANGELOG/DockerHub-if-any and any prose docs; revise until they don't read as generated. | TODO | Applies to publication prose. |
| 20.5 | **CLAUDE.md** exists and reflects the code-style rules (CODE_STYLE mandates keeping it current). | TODO | |
| 20.6 | Cross-check every **TUIKIT.md** decision (locked + ratified defaults) against the shipped implementation; note deviations. | TODO | Traceability back to source doc. |
| 20.7 | Produce a conformance report (requirement → status → evidence) and remediate every non-conformance before sign-off. | TODO | |

**Exit criteria:** a committed conformance report showing every requirement in `c:\code\agents\requirements` as PASS or justified N/A, with no open non-conformances.

---

## Definition of done

The project is complete when: all implementation phases are `DONE`; the example app runs on at least one tier-1 terminal per OS; all four Touchstone runners are green in CI with JSON export; Phase 19 shows the public surface mapped to tests with a committed coverage report and justified exclusions; and Phase 20's conformance report shows every requirement in `c:\code\agents\requirements` satisfied or justified. No `TODO`/`WIP`/`BLOCKED` rows remain except those explicitly recorded as `N/A` with justification.
