# TUIKit — Capability Analysis and Possible Improvements

This report looks at what the best terminal UIs actually do, then measures TUIKit against them. The sources are the agent CLIs this framework was built for (Claude Code and OpenAI's Codex CLI), the full project list at [awesome-tuis](https://github.com/rothgar/awesome-tuis), the framework peers that show up in that list — Textual and Rich (Python), Bubble Tea/Lip Gloss and tview (Go), Ratatui (Rust), FTXUI (C++) — and, examined in depth because TUIKit competes with them directly, the C#/.NET TUI frameworks: **Spectre.Console, Terminal.Gui (gui.cs), Consolonia, and Terminaux**, plus the adjacent .NET libraries that solve pieces of the problem. Section 4 is a head-to-head for the .NET field; the full inventory of everything evaluated is in the appendix.

The goal is not a wish list. It is a ranked set of capabilities that decide whether someone can build a terminal interface in TUIKit that is genuinely beautiful and genuinely useful, and a candid account of where the library stands today.

## How to read this

Each capability is tagged against the current codebase:

- **Have** — implemented and tested.
- **Partial** — a foundation exists but it is not enough to build the real thing.
- **Missing** — not present.

The final sections turn that into a tiered roadmap. If you only read one thing, read [Tier 1](#tier-1--table-stakes).

---

## 1. What the agent CLIs do well

Claude Code and Codex are, underneath the model plumbing, terminal applications with a specific and demanding UI. Watching them closely is the fastest way to see which rendering problems are worth solving once in a library instead of forever in every app.

They stream. Tokens arrive continuously and the transcript grows without the operator losing their place — scroll up to read history, and new output keeps landing at the bottom until you return there. They render **Markdown as it arrives**, not after, including fenced code blocks with **syntax highlighting**. They show **diffs** — often the single most important surface in the whole session — with per-line add/remove coloring and, ideally, syntax highlighting inside the diff. They collapse detail: a tool call renders as a one-line summary (`Read src/foo.cs`) that can expand into its full output, and "thinking" folds away. They interrupt cleanly — `Esc` stops generation without killing the app. They ask for permission through **modal approvals** that trap focus. They complete input as you type: `/` opens a slash-command menu, `@` opens a file picker, both filtered as you keep typing. They keep a **status line** with the model, context usage, and elapsed time, and a **spinner with a live timer** while working. They handle paste of large blocks and, increasingly, images.

The dividing line for a library is clean. The agent logic — deciding what to run, talking to the model, executing tools — stays in the app. But every one of the rendering behaviors above is generic: a streaming transcript with scroll lock, a Markdown renderer, a syntax highlighter, a diff view, collapsible sections, an autocomplete popup, an approval modal, a spinner-with-timer, and a status/hint bar. **A framework that ships those turns "build an agent TUI" from a month into a weekend.** TUIKit already owns the streaming transcript, scroll lock, Markdown, and approval modals. The conspicuous gaps are syntax highlighting, diff rendering, collapsible sections, and autocomplete — and those are exactly the surfaces that make an agent CLI feel finished.

| Agent-CLI capability | TUIKit today |
|---|---|
| Streaming transcript + smart scroll lock | **Have** |
| Markdown rendering | **Have** (subset — no tables, ordered/nested lists, task lists) |
| Syntax-highlighted code blocks | **Missing** |
| Diff view (add/remove coloring, hunks) | **Missing** |
| Collapsible / expandable sections | **Missing** |
| Autocomplete / typeahead popup (`/`, `@`) | **Missing** |
| Modal approval / confirmation | **Have** |
| Spinner with live elapsed timer | **Partial** (spinner exists; no timer/ETA) |
| Status line / contextual key hints | **Partial** (the example draws one; not a widget) |
| Interrupt (Esc) without exit | **Have** (configurable Ctrl+C policy + key routing) |
| Copy code block to clipboard | **Have** (selection + OSC 52) |
| Image paste / inline image display | **Missing** |

---

## 2. Capability patterns across awesome-tuis

The awesome-tuis catalog spans dashboards, developer tools, container/k8s consoles, editors, file managers, games, messaging, multimedia, productivity, and web clients. The applications differ wildly; the **primitives they lean on do not**. A handful of building blocks appear again and again, and they are the real specification for a general-purpose framework.

**Tables that mean it.** htop, btop, k9s, lazydocker, ctop, harlequin, and every database and process browser live or die by the table: sortable columns, resizable columns, a selected/highlighted row, live-updating cells, and — for k9s-sized data — virtualized rendering so a hundred thousand rows don't cost a hundred thousand cells. TUIKit has a table, but it is a static header-plus-rows renderer with none of that.

**Trees and miller columns.** File managers (yazi, ranger, nnn, lf, broot) and anything with hierarchy (k9s resources, a JSON viewer) need an expand/collapse tree and, in the ranger lineage, the three-column "miller" preview. TUIKit has neither.

**A fuzzy finder, everywhere.** fzf is its own entry, but the *pattern* is embedded in helix's pickers, k9s's `/` filter, telescope, broot, and half the productivity apps. Type to filter a list, with fuzzy matching and match highlighting. This is arguably the single most-reimplemented widget in the whole ecosystem, and TUIKit doesn't have it.

**Tabs and resizable splits.** tmux, zellij, and most multi-panel apps (lazygit's panes, k9s) give the user tabs and draggable split borders. TUIKit's layout is a fixed set of regions per frame with no tab concept and no interactive resize.

**Scrollbars and scrollable containers.** Almost every list, log, and preview pane shows a scrollbar. TUIKit scrolls content but never draws a scrollbar, and there is no generic "scrollable viewport" you can drop arbitrary content into.

**Charts.** btop, bottom, zenith, gping, and bandwhich render braille line plots, bar charts, and gauges. TUIKit has a sparkline, a gauge, and a progress bar — a good start, but no line/bar chart and no braille canvas to draw them on.

**Images and graphics.** yazi, chafa, ncspot, and image viewers render pictures with sixel, the kitty graphics protocol, iTerm2 inline images, or a half-block/braille fallback. This is a major differentiator for file managers and media tools, and TUIKit has nothing here.

**Rich inline markup and border styles.** Rich, Lip Gloss, and Spectre.Console popularized inline markup (`[bold red]text[/]`) and a palette of border styles — rounded, double, thick — plus alignment, padding, and even shadows. This is most of what makes output *look* designed. TUIKit has a fluent style builder and a single box style; it has padding but no rounded/double/thick borders, no inline markup parser, and no shadow.

**Modal editing and pickers.** helix, vim, and kakoune show that a modal keybinding layer (normal/insert/visual) and command mode (`:`) are worth having as first-class concepts. TUIKit's command routing table can express this, but there's no modal-mode helper.

The following table collapses the catalog into reusable capabilities and TUIKit's status.

| Capability (seen across awesome-tuis) | Representative apps | TUIKit today |
|---|---|---|
| Sortable/resizable/virtualized table | htop, k9s, harlequin, lazydocker | **Partial** (static table only) |
| Expandable tree / miller columns | yazi, ranger, broot, k9s | **Missing** |
| Fuzzy finder / filter with match highlight | fzf, helix, k9s, telescope | **Missing** |
| Tabs | zellij, tmux, k9s | **Missing** |
| Interactive split resize (drag borders) | tmux, zellij, lazygit | **Missing** |
| Scrollbar + generic scrollable viewport | nearly all | **Missing** |
| Line/bar charts, braille canvas | btop, bottom, gping | **Partial** (sparkline/gauge/bar only) |
| Image rendering (sixel/kitty/iTerm2/half-block) | yazi, chafa, viewers | **Missing** |
| Inline rich-text markup (`[bold]…[/]`) | Rich, Lip Gloss, Spectre | **Missing** (builder API only) |
| Border styles (rounded/double/thick) + shadow | Rich, Lip Gloss, tview | **Partial** (single + ASCII only) |
| Modal editing layer / command mode | helix, vim, k9s | **Partial** (routing table can, no helper) |
| Menus / menu bar | tview, Terminal.Gui, Turbo Vision ports | **Missing** |
| Forms with validation + tab order | tview, Terminal.Gui, posting/ATAC | **Partial** (widgets + example form; no framework) |
| Syntax highlighting | editors, lazygit, agent CLIs | **Missing** |
| Animation / transitions / timers | Textual, games, screensavers | **Missing** |

---

## 3. Where TUIKit already stands

It is worth being clear about the base, because it is strong and it shapes what's cheap to add. TUIKit already ships the hard, load-bearing parts that many frameworks get wrong:

- A correct cell model with **truecolor and automatic 256/16 degradation**, and **real Unicode width** (CJK double-width, combining marks, emoji grapheme clusters) — the thing that quietly breaks most homegrown TUIs.
- A **double-buffered diff renderer** that emits only changed cells with coalesced styling, so high-frequency updates stay cheap.
- **Thread-safe panes** with mutable line handles, a smart scroll lock, and a capped scrollback — the concurrency model an agent harness actually needs.
- An **enhanced input decoder** (UTF-8, CSI/SS3, Kitty/CSI-u, SGR mouse, bracketed paste) feeding a **command routing table** with scopes, multi-key chords, and a configurable Ctrl+C policy.
- **Mouse, virtual links with a security allowlist, selection with OSC 52 copy, a focus-trapping modal stack, toasts, theming with an ASCII fallback, and a debug overlay.**
- A **headless renderer** and snapshot helper, so any UI built on it is testable — a capability most of the peer frameworks bolt on later, if at all.

That foundation means most of the gaps below are *widgets and renderers on top of a solid engine*, not rewrites. The engine is the expensive part, and it's done.

---

## 4. The C# / .NET TUI landscape — head-to-head

This is the field TUIKit actually competes in, so it deserves detail. There are four serious frameworks and a long tail of adjacent libraries. For each of the four, what follows is what it is, what's unique or important about it, its advantages, its disadvantages, where TUIKit is already ahead, and what TUIKit should learn or lift.

The one-paragraph orientation: **Spectre.Console** owns rich *output* and prompts and is the aesthetic benchmark. **Terminal.Gui** owns the full retained-mode *widget toolkit* and is the breadth benchmark. **Consolonia** brings *Avalonia XAML/MVVM* to the terminal. **Terminaux** is a broad *console-manipulation* toolkit. TUIKit's distinct territory — the thing none of them centers on — is a **concurrency-first, streaming, agent-shaped** framework with a correct Unicode/diff engine and first-class headless testing.

### 4.1 Spectre.Console

**What it is.** The most popular .NET console library, inspired by Python's Rich. It makes ordinary console output beautiful: an inline markup language (`[bold red]text[/]`), tables, grids, panels, trees, bar/breakdown charts, rules, progress bars with multiple concurrent tasks, status spinners, a `Live` region that re-renders a widget in place, `Canvas` pixel drawing, FIGlet banner text, image rendering (via ImageSharp), and a full suite of interactive prompts (text, confirm, single-select, multi-select, secrets). 3/4/8/24-bit color with automatic capability detection. MIT. Explicitly built to be unit-testable.

**Unique / important.** The **markup language** and the **prompt suite** are the two things developers reach for it for, and both are best-in-class in .NET. Its **default aesthetic** is the bar everyone measures against — the tables and progress displays look designed out of the box. Automatic color degradation is mature.

**Advantages.** Gorgeous defaults; enormous adoption and documentation; markup; prompts; `Live` and multi-task progress; canvas, figlet, and images; MIT; testable output.

**Disadvantages.** It is **not a full-screen, retained-mode, keyboard-driven application framework**. There is no persistent widget tree with focus traversal, no alternate-screen multi-pane app model, no general mouse-driven navigation, and no concurrency story for several producers streaming into different regions at once. `Live` is a single re-rendered widget, not a compositor. You cannot build k9s or lazygit with it; you build beautiful *output* and *prompts*.

**Where TUIKit already wins.** The entire interactive-application layer: a retained multi-region layout, **thread-safe panes that any producer can stream into**, a diff-based compositor, an input decoder + command routing table with scopes and chords, a focus-trapping modal stack, and full-frame headless snapshot testing (Spectre tests *output fragments*; TUIKit tests *whole interactive frames* with synthetic input). TUIKit's smart scroll lock, mutable line handles, and links have no Spectre equivalent.

**Opportunities for TUIKit (learn / implement).** Lift the **inline markup parser** (Tier 1), the **prompt suite** (confirm/select/multiselect/text/password as one-call helpers), **multi-task progress**, **FIGlet banner text**, a **`Canvas`/pixel-drawing primitive**, and **image rendering**. Match its **default aesthetic** — rounded borders, tasteful spacing, well-styled tables. Its `Live` ergonomics are worth copying as a convenience wrapper over the render loop.

### 4.2 Terminal.Gui (gui.cs), v2

**What it is.** The heavyweight retained-mode toolkit for .NET: **50+ built-in views** — windows, dialogs, wizards, menus and menu bars, status bars, buttons, checkboxes, radio groups, text fields and a full `TextView` editor (clipboard, undo/redo, Unicode), `TableView`, `TreeView`, `TabView`, `ScrollView`, `ListView`, charts/graphs, a color picker, a file dialog with search/filter, progress, and Markdown support. A computed layout system (**Pos/Dim**) gives responsive, relative positioning ("as responsive as a web page"). Mouse, TrueColor, Unicode/wide characters, double-buffering, and multiple console drivers (Windows, curses, net). v2 modernized it with an instance-based application model.

**Unique / important.** The **Pos/Dim layout system** (position/dimension relative to other views: `Pos.Right(x) + 1`, `Dim.Fill()`, `Dim.Percent(50)`) is genuinely powerful and is the model TUIKit's region layout is a simpler cousin of. The **sheer breadth of views** — menus, dialogs, table/tree/tab, color picker, file dialog — is the widget catalog TUIKit lacks. Data views are virtualized ("infinite elements").

**Advantages.** Widget breadth; the Pos/Dim layout; menus/dialogs/file-picker that TUIKit has none of; virtualized table/tree; mature; cross-platform drivers; keyboard-and-mouse throughout.

**Disadvantages.** A **single-threaded UI model** — background work must marshal onto the UI thread (`Application.Invoke`), which is exactly the friction TUIKit was built to remove for streaming/agent workloads. API ergonomics carry v1 legacy and are heavier and less discoverable than Spectre's. Default aesthetics are functional, not beautiful. No built-in headless snapshot testing of the kind TUIKit ships. Historically churny across v1→v2.

**Where TUIKit already wins.** **Concurrency**: any thread writes to any pane, FIFO per pane, no marshaling — Terminal.Gui's biggest structural weakness for agent/dashboard use. **Testability**: headless full-frame snapshots with injected input are first-class. **Footprint and reach**: TUIKit multi-targets down to `netstandard2.0` and stays dependency-free on modern TFMs. **Agent-shaped features**: streaming transcript, smart scroll lock, mutable lines, Markdown, links, configurable Ctrl+C policy.

**Opportunities for TUIKit (learn / implement).** The **widget catalog is the roadmap**: menus/menu bar, dialogs and a file browser, virtualized `TableView`/`TreeView`, `TabView`, `ScrollView` with scrollbars, and a color picker. Study **Pos/Dim** for TUIKit's nested-layout story (Tier 3 #25) — relative positioning inside a region is the natural next layout step. Its **global focus traversal** across a view tree is the model for TUIKit's focus manager (Tier 2 #17).

### 4.3 Consolonia

**What it is.** A terminal UI framework that renders **Avalonia** (the cross-platform XAML/MVVM desktop framework) to the console. You write XAML, use data binding and `ObservableCollection`, style with resources and control templates, and reuse desktop controls (buttons, sliders, tree views, tab controls) — no manual coordinate math. Cross-platform. Beta, ~800+ GitHub stars.

**Unique / important.** The pitch is **skill and code reuse between desktop and terminal**: the same MVVM patterns, XAML, and bindings that an Avalonia developer already knows. For teams already on Avalonia, that is a real superpower.

**Advantages.** Declarative XAML; full data binding; MVVM; control templating and style resources; leverages Avalonia's mature layout/styling engine; desktop-skill reuse.

**Disadvantages.** A **heavy dependency** (the entire Avalonia stack) for a terminal app; beta maturity with limited production history; the abstraction distance from the terminal makes low-level control (raw escape sequences, precise cell control, streaming performance) harder; unclear/underdocumented mouse, truecolor, and graphics support; not aimed at streaming/agent workloads.

**Where TUIKit already wins.** **Weight and directness**: TUIKit is a focused, dependency-free-on-modern-TFMs engine with precise cell control and a diff renderer, versus dragging Avalonia into a console app. **Concurrency and streaming**; **headless cell-level testing**; **`netstandard2.0` reach**.

**Opportunities for TUIKit (learn / implement).** The **declarative + data-binding ergonomics** are the strongest argument for TUIKit's Tier 3 reactive/data-binding layer (#26): offer optional `INotifyPropertyChanged`/`ObservableCollection` binding and a lightweight declarative builder so users who want MVVM aren't forced into imperative rendering — without taking on an Avalonia-sized dependency. Its **style-resource/theming model** is worth studying for TUIKit's theme role vocabulary.

### 4.4 Terminaux

**What it is.** A broad .NET console-manipulation toolkit (from the Aptivi/Nitrocid ecosystem): 256-color and truecolor tooling, VT-sequence manipulation, console **mouse** support, an input/reader (readline-style) layer, interactive TUIs and widgets, writers, **FIGlet**, **image rendering**, notifications, and color selection. Very wide target coverage — .NET Framework, .NET Standard, and .NET 6/8/9. Actively developed.

**Unique / important.** Breadth of **low-level console tooling** in one place — VT sequences, color, mouse, figlet, images, notifications — and unusually wide framework support including .NET Framework.

**Advantages.** Comprehensive console primitives; figlet and image rendering built in; notifications; broad TFM support; active development.

**Disadvantages.** **GPL-3.0 licensing** — a viral copyleft license that is a hard blocker for many commercial and MIT-ecosystem consumers (TUIKit is MIT, a decisive difference). Historically coupled to the Nitrocid/KS ecosystem, and the API breadth lacks a single unifying application/compositor model the way TUIKit or Terminal.Gui present one. Aesthetics and cohesion are secondary to coverage.

**Where TUIKit already wins.** **License** (MIT vs GPL-3.0) is the headline — for most libraries choosing a TUI dependency, this alone decides it. Also a **cohesive application model** (regions, panes, host loop), **concurrency**, and **headless testing**.

**Opportunities for TUIKit (learn / implement).** **Image rendering** (sixel/kitty/iTerm2/half-block — Tier 2 #13), **FIGlet banner text**, a **notification** vocabulary (TUIKit has toasts already; compare severity/positioning options), and thorough **VT-sequence coverage** for capability detection.

### 4.5 The .NET field at a glance

| Dimension | Spectre.Console | Terminal.Gui v2 | Consolonia | Terminaux | **TUIKit** |
|---|---|---|---|---|---|
| Primary purpose | Rich output + prompts | Retained widget toolkit | Avalonia XAML on terminal | Console-manipulation toolkit | Concurrency-first app framework |
| Full-screen interactive app model | No | Yes | Yes | Partial | **Yes** |
| Retained widget tree + focus traversal | No | Yes | Yes (Avalonia) | Partial | Partial (modal focus; no global tree) |
| Layout system | Grid/panel flow | **Pos/Dim relative** | Avalonia layout | Ad hoc | Region constraints + padding |
| Thread-safe concurrent streaming | No | No (marshal to UI thread) | No | No | **Yes** |
| Diff-based compositor | Live (single widget) | Double-buffer | Avalonia | Manual | **Yes (cell diff)** |
| Widget breadth | Medium (output) | **Very high (50+)** | High (Avalonia) | Medium | Medium |
| Inline markup language | **Yes** | Partial | XAML | Partial | No (fluent builder) |
| Tables / trees / tabs | Tables/trees (static) | **Virtualized all three** | Avalonia controls | Some | Static table only |
| Charts / canvas | Charts + Canvas | Charts/graphs | Avalonia | Some | Sparkline/gauge/bar |
| Images (sixel/kitty) | Via ImageSharp | Limited | Unclear | **Yes** | No |
| FIGlet / banner text | **Yes** | No | No | **Yes** | No |
| Prompts (confirm/select/…) | **Best-in-class** | Via dialogs | Via controls | Reader | No (modal kit only) |
| Data binding / MVVM | No | Partial | **Yes (full)** | No | No (optional, planned) |
| Headless snapshot testing | Output only | Limited | No | No | **Yes (whole frames)** |
| Unicode width / grapheme correctness | Good | Good | Avalonia | Good | **Yes (bundled tables)** |
| Multi-target incl. netstandard2.0 | Yes | Modern .NET | Modern .NET | **Wide incl. .NET Fx** | **Yes** |
| License | MIT | MIT | MIT | **GPL-3.0** | **MIT** |

### 4.6 What to lift from the .NET peers, concretely

Reading the field, the borrow list is unambiguous and mostly reinforces the awesome-tuis findings:

- From **Spectre.Console**: the inline **markup parser**, the **prompt suite**, **multi-task progress**, **FIGlet**, a **`Canvas`**, **image rendering**, and — most of all — its **default aesthetic bar**.
- From **Terminal.Gui**: the **widget catalog** (menus, dialogs, file browser, virtualized table/tree, tabs, scrollview + scrollbars, color picker), **Pos/Dim-style relative layout** for nesting inside regions, and **global focus traversal**.
- From **Consolonia**: an optional **data-binding / declarative** ergonomics layer, without the Avalonia-sized dependency.
- From **Terminaux**: **image rendering**, **FIGlet**, and broad **VT/capability** coverage — while keeping TUIKit's **MIT** license as a deliberate competitive advantage.

None of the four combines TUIKit's concurrency model, diff engine, headless testability, and MIT license in one focused package. That is the gap TUIKit fills — provided it closes the widget and aesthetic distance identified above.

---

## 5. Roadmap

### Tier 1 — Table stakes

Without these, a user cannot build the interfaces they came for. Each maps cleanly onto the existing engine.

1. **Syntax highlighting** for code (a tokenizer + theme, applied to `StyledText`). Unlocks agent CLIs, editors, diffs, and any dev tool. Highest leverage single item.
2. **Diff renderer** — unified and side-by-side, add/remove/context coloring, hunk headers, optional intra-line highlighting, and syntax highlighting inside. The defining surface for agent and git TUIs.
3. **A real data table** — sortable columns, resizable/auto-sized columns, row selection and highlight, per-cell styling, and virtualized rendering for large sources. Rework the current `Table` into this.
4. **Tree / hierarchical list** with expand/collapse, indentation guides, and selection. Powers file trees, JSON/YAML viewers, and resource browsers.
5. **Fuzzy finder / filterable list** — type-to-filter with fuzzy matching and match highlighting, reusable as a picker and inline. The most-reused widget in the ecosystem.
6. **Scrollable viewport + scrollbars** — a generic container that scrolls arbitrary child content and draws a scrollbar; wire the scrollbar into panes and lists too.
7. **Autocomplete / typeahead popup** anchored to the input caret, filtered live. Needed for slash commands, `@` mentions, and command palettes.
8. **Border styles and box polish** — rounded, double, thick, and no-border variants; title alignment; and a shadow option for modals. Cheap, high aesthetic payoff.
9. **Inline rich-text markup parser** (`[bold red]…[/]` or similar) that produces `StyledText`, so users aren't forced through the fluent builder for everything.
10. **Tabs widget** and a **status/hint bar widget** (the contextual footer every good TUI has), promoted out of the example into the library.
11. **Collapsible section widget** — a header line that expands/collapses child content, for tool calls, log groups, and details.

### Tier 2 — High-value differentiators

These are what make a TUI feel beautiful and alive rather than merely functional.

12. **Charts** — a braille/quadrant **canvas** primitive, then line and bar charts and a labeled gauge on top. btop-quality telemetry.
13. **Image rendering** — detect and use sixel, the kitty graphics protocol, and iTerm2 inline images, with a half-block/braille fallback. Transformative for file managers and media tools.
14. **Animation and timers** — a frame-tick/easing helper so spinners, transitions, and progress can move smoothly; add elapsed-time/ETA to the spinner and progress bar.
15. **Interactive split resize** — draggable region borders and keyboard resize, building on the region model.
16. **A forms/dialog framework** — compose inputs with validation, a shared focus/tab manager across widgets, and standard prompts (confirm, select, multi-select, text). Today the pieces exist; the orchestration doesn't.
17. **Global focus manager** — Tab/Shift-Tab across a declared focus ring, with visible focus rings, independent of the modal stack.
18. **Menus / menu bar and context menus.**
19. **Markdown completeness** — tables, ordered and nested lists, task lists (`[ ]`/`[x]`), nested block quotes, and inline links, plus syntax highlighting in fenced blocks (shared with #1).

### Tier 3 — Polish, platform, and paradigm

20. **In-pane search** (`/`) with match navigation and highlighting.
21. **Modal backdrop dimming** and drop shadows (needs a compositor read-back pass over covered cells).
22. **Suspend/resume** (drop to the shell/editor and restore) and **POSIX signal restoration** (SIGTSTP/SIGCONT/SIGTERM) — already flagged as unfinished in the plan.
23. **OSC 8 hyperlink emission**, **clipboard read**, and **keyboard link-hint labels** (Vimium-style) — finish the links/clipboard story.
24. **A modal-editing helper** (normal/insert/visual modes, command mode) layered on the routing table, for editor-style apps.
25. **Nested layouts inside a region** — a mini fl/grid layout so a region can host sub-layouts without manual math.
26. **An optional reactive/declarative layer** — data binding or an MVU-style update loop, so users who prefer Textual/Bubble Tea ergonomics aren't forced into imperative rendering. Keep the retained core; offer this as a convenience on top.
27. **Accessibility and richer non-TTY output** — structured line output and screen-reader-friendly fallbacks beyond the current plain-text mode.

---

## 6. The aesthetics checklist

"Beautiful" is not vague. Across the apps that people call beautiful — Textual showcases, btop, lazygit, yazi — the same concrete choices recur. A framework should make all of these easy and, ideally, correct by default:

- Consistent **padding and breathing room** (TUIKit has region and modal padding — keep leaning on it), and content **alignment** (left/center/right, top/middle/bottom).
- **Border variety** (rounded feels modern; double/thick for emphasis) with aligned, styled titles.
- **Truecolor with graceful degradation**, semantic **theme roles**, and runtime theme switching (TUIKit has these — extend the role vocabulary: success/warning/error/info, selection, disabled, accent).
- **Gradients and subtle color** for headers and gauges, and **dim** for secondary text.
- **Nerd Font / icon glyph** support with an ASCII fallback, so status columns and file trees can use icons.
- **Motion**: smooth spinners, progress that animates, and transitions that aren't jarring.
- **Scrollbars and focus rings** so the interface communicates state without the user guessing.
- **Whitespace over chrome** — the frameworks that look best under-decorate.

---

## 7. Recommended next three

If the point is to move the needle fastest toward "users can build beautiful, useful TUIs," start here, in order:

1. **Syntax highlighting + the diff renderer.** They share a tokenizer, they unblock the agent-CLI and developer-tool audiences TUIKit was designed for, and nothing else in the library is a prerequisite.
2. **The real data table, the tree, and the fuzzy filterable list.** This trio covers the overwhelming majority of awesome-tuis applications — dashboards, file managers, k8s/docker consoles, database browsers — and each sits directly on the existing pane/scroll engine.
3. **Border styles, inline markup, scrollbars, and the status/tabs widgets.** The polish pass that makes everything built on the above actually look designed rather than merely rendered.

Everything in Tier 1 is achievable on the current engine without touching the renderer, the input pipeline, or the layout solver. The hard, correctness-critical work — Unicode, diffing, concurrency, headless testing — is already done. What remains is largely widgets and taste.

---

## 8. Developer experience — reducing friction

Capability is only half the battle. The frameworks people love — Spectre.Console, Textual, Bubble Tea — win as much on *ergonomics* as on features. Having built the example harness and the test suite against TUIKit's own API, the friction is visible and specific. This section is an honest critique of the current surface and a concrete plan to make it easier, more approachable, and more consistent, **without weakening the powerful low-level engine underneath.** The guiding principle is additive: keep the precise API, add a batteries-included layer on top.

### 8.1 Where it fights you today

A handful of rough edges show up immediately when you write a real app:

- **Ceremony to start.** The smallest useful app creates a backend, creates an application, builds a layout, creates a pane, binds the pane, registers a chord, maps the chord to a command name, maps the command name to an action, and then runs. That is a lot of moving parts before anything appears on screen.
- **Namespace sprawl.** A basic app pulls in `TUIKit`, `TUIKit.Content`, `TUIKit.Hosting`, `TUIKit.Input`, `TUIKit.Layout`, and `TUIKit.Terminal` — six `using`s before the first line of logic. A widget-heavy app adds three or four more.
- **Only panes can bind to regions.** `BindPane` exists, but widgets (a gauge, a table, an editor) have no equivalent. In the example, every telemetry widget and the composer are positioned by hand in a `RenderOverlay` callback with manual `CreateView` and `ContentRect` math. That hand-layout is the single biggest source of code in the example, and it's exactly what a layout engine should remove.
- **Two-step keybinding.** Binding a key is `Commands.Register(chord, "id")` *and* `RegisterCommand("id", action)`. The indirection is valuable for config-file rebinding, but it's overkill for "Ctrl+Q quits."
- **Modal boilerplate.** A custom dialog means deriving from `Modal`, implementing `Render` and `HandleKey`, computing a centered box, pushing it, and — because there's no first-class await on the app — observing `Completion` with a `ContinueWith` on the thread pool. A confirm prompt should be one line.
- **Verbose styling for the common case.** `Text.From("done").Green().Bold().Append(Text.From("  1.2s").Dim())` is precise but heavy for what Rich/Spectre express as `[green bold]done[/] [dim] 1.2s[/]`.
- **Two vocabularies for the same idea.** `AxisConstraint.FromEnd` and `RightAnchored`, `Stretch` and `FillWidth`, `RequestStop` and (mentally) "quit" — the same concept has more than one name, which slows discovery.

### 8.2 Approachability — a batteries-included entry point

The fastest win is a one-call host that supplies the obvious defaults (a `ConsoleBackend`, a full-screen single region, Ctrl+C to quit, the dark theme) so a first program is tiny.

```csharp
// Today
using System.Threading;
using System.Threading.Tasks;
using TUIKit;
using TUIKit.Content;
using TUIKit.Hosting;
using TUIKit.Input;
using TUIKit.Layout;
using TUIKit.Terminal;

using ConsoleBackend backend = new ConsoleBackend();
using TuiApplication app = new TuiApplication(backend);
app.Layout = Layout.Create().Add("log", r => r.FillWidth().FillHeight()).Build();
Pane log = new Pane("log");
app.BindPane("log", log);
app.Commands.Register(KeyChord.Parse("ctrl+q"), "quit");
app.RegisterCommand("quit", () => app.RequestStop());
await app.RunAsync(CancellationToken.None);
```

```csharp
// Proposed — same behavior
using TUIKit;

await TuiApp.RunAsync(app =>
{
    Pane log = app.AddPane("log");          // creates + binds to a full-screen region
    app.Bind("ctrl+q", app.Quit);           // one-step keybinding
    log.WriteLine("ready.");
});
```

Concrete proposals:

1. **`TuiApp.RunAsync(Action<TuiApplication> configure, CancellationToken)`** and a `TuiApp.Create()` builder that default the backend, a single full-screen region, and Ctrl+Q-to-quit.
2. **A facade namespace or a shipped `GlobalUsings`** so `using TUIKit;` is enough for the common types. Re-export `Pane`, `Text`, `KeyChord`, the widgets, and `Layout` from the root, or provide `TUIKit.All` global usings.
3. **`dotnet new` templates and a copy-pasteable "hello, panes" in the README** — the first five minutes decide adoption.
4. **Sensible defaults everywhere**: a default theme, a default layout when none is set, and Ctrl+C-quits out of the box (already the policy default) so nothing is required to get a runnable screen.

### 8.3 Syntax — say more with less

The library should offer a short path for the 90% case and keep the explicit path for the 10%.

**Bind a widget to a region** (removes the example's hand-layout entirely):

```csharp
// Today: manual placement in a RenderOverlay callback
Rect rect = RegionFor("telemetry").ContentRect(buffer.Size);
gauge.Render(buffer.CreateView(new Rect(0, 1, rect.Width, 1)));

// Proposed
app.AddWidget("telemetry", gauge);   // host measures, arranges, and renders it
```

**Inline markup** (a Tier-1 capability that doubles as a DX win):

```csharp
// Today
pane.WriteLine(Text.From("done").Green().Bold().Append(Text.From("  1.2s").Dim()));

// Proposed (markup parser produces StyledText; the fluent builder stays for programmatic styling)
pane.WriteLine(Markup.Parse("[green bold]done[/] [dim] 1.2s[/]"));
// or an overload: pane.WriteMarkup("[green bold]done[/] [dim] 1.2s[/]");
```

**Await a dialog** instead of deriving a type and wiring a continuation:

```csharp
// Today: derive from Modal, implement Render/HandleKey, push, observe Completion via ContinueWith

// Proposed
bool ok = await app.ConfirmAsync("Allow tool call?", "Allow", "Deny");
string name = await app.PromptAsync("Project name?");
int pick = await app.SelectAsync("Theme", "Dark", "Light", "High-contrast");
object? result = await app.ShowAsync(myCustomModal);   // for the custom case
```

**One-step keybindings** for the simple case, keeping the command-id route for rebinding:

```csharp
// Today
app.Commands.Register(KeyChord.Parse("ctrl+p"), "palette");
app.RegisterCommand("palette", OpenPalette);

// Proposed (convenience overload; the two-step API remains for config-driven bindings)
app.Bind("ctrl+p", OpenPalette);
```

**Layout that reads like the picture.** Keep `AxisConstraint` for full control, but offer a split/grid DSL for the common shapes so users rarely touch the raw constraint:

```csharp
// Today
.Add("main",  r => r.Horizontal(AxisConstraint.Stretch(0, 34)).Vertical(AxisConstraint.Stretch(1, 6)))

// Proposed helpers
.Row(top => top.Left("main").Right("sidebar", width: 34))   // or a Column/Grid equivalent
```

### 8.4 Semantics — one obvious way

Consistency lowers the cost of learning the whole surface.

1. **Unify "things that live in a region."** Make `Pane` implement `IWidget` (it already has a `Render`), and let `BindPane`/`AddWidget` collapse into a single `Bind(regionId, IWidget)`. One concept — "content bound to a region" — instead of two parallel ones.
2. **Pick one name per idea.** Prefer the intent-revealing convenience names (`FillWidth`, `RightAnchored`, `Quit`) in the docs and examples, and document the primitive (`AxisConstraint`, `RequestStop`) as the advanced escape hatch. Don't present both as equals.
3. **State thread-safety at the call site.** Panes are thread-safe; most widgets are not. A short, consistent XML-doc convention ("thread-safe" / "render-thread only") on every public type removes guesswork; a `[ThreadSafe]`-style marker or a naming convention would make it scannable.
4. **Make the async result API the primary modal path**, as the plan intended (`ShowModalAsync`), rather than the push-and-observe pattern the example currently uses.
5. **Name for discovery.** `TuiApplication` is fine, but the common verbs (`Quit`, `Bind`, `AddPane`, `AddWidget`, `Log`, `Notify`) should exist on the app so IntelliSense on `app.` reveals the whole beginner surface.

### 8.5 A prioritized DX backlog

- **DX-1 (highest leverage): `Bind(regionId, IWidget)` with `Pane : IWidget`.** Removes the biggest chunk of hand-layout and unifies the content model. Everything in the example's `RenderOverlay` collapses.
- **DX-2: `TuiApp.RunAsync`/`Create` bootstrap + sensible defaults + a single `using TUIKit;`.** The "first five minutes" fix.
- **DX-3: `ConfirmAsync`/`PromptAsync`/`SelectAsync`/`ShowAsync` on the app.** Deletes modal boilerplate; pairs with the Spectre-style prompt suite from Section 4.
- **DX-4: inline markup parser + `WriteMarkup`.** Shared with Tier-1 capability #9; the single biggest readability win for styled output.
- **DX-5: `app.Bind(chord, action)` and app-level verbs (`Quit`, `Log`, `Notify`).** Collapses the two-step ceremony for the common case.
- **DX-6: a split/grid layout DSL** over the constraint solver, so `AxisConstraint` becomes the advanced path, not the default one.
- **DX-7: a `TuiTest`/driver harness** — feed keys, tick a frame, assert a `Snapshot` — so testing an app is as ergonomic as testing a widget already is.
- **DX-8: documentation and templates** — a quick-start, a cookbook of recipes (streaming log, dashboard, form, wizard), and `dotnet new tuikit` templates.

None of these removes anything. The retained engine, the region constraints, the command routing table, and the raw modal API all stay for the apps that need them. The point is that a newcomer should reach a beautiful, working screen in a dozen lines, and only meet `AxisConstraint`, `CommandRoutingTable`, and a hand-written `Modal` when they actually need that control.

---

## 9. Appendix — everything evaluated

For transparency, this is the full inventory of what was examined for this report. Individual applications were assessed as representatives of their category rather than exhaustively; frameworks were examined for their model and capabilities.

**Agent CLIs.** Claude Code, OpenAI Codex CLI.

**awesome-tuis applications, by category (representative sample):**

- *Dashboards / system:* htop, btop++, Glances, bottom, zenith, WTF, bandwhich, gtop, gping.
- *Development:* lazygit, gitui, ATAC, posting, euporie, pudb, harlequin, rainfrog.
- *Containers / k8s:* k9s, lazydocker, ctop, dive, podman-tui, lazytrivy.
- *Editors:* helix, vim/neovim, micro, kakoune, vis, zee, slap.
- *File managers:* lf, ranger, nnn, yazi, broot, vifm.
- *Games / screensavers:* chess-tui, tetris, 2048, NetHack, pokete.
- *Messaging:* aerc, irssi, matterhorn, weechat, discordo.
- *Multimedia:* ncspot, cmus, termusic, mpv, chafa, spotify-player.
- *Productivity:* tmux, zellij, taskwarrior-tui, calcurse, slides, patat.
- *Web:* lynx, w3m, browsh, carbonyl, newsboat.

**Non-.NET frameworks / libraries (model and capability review):**

- *Python:* Textual, Rich, Urwid, PyTermGUI, Python Prompt Toolkit, py_cui, Blessed, PyTermTk, Vindauga.
- *Go:* Bubble Tea, Lip Gloss, Bubbles, tview, gocui, tcell, termui, termdash, pterm.
- *Rust:* Ratatui (and its predecessor tui-rs), Cursive, Iocraft, crossterm.
- *C / C++:* ncurses, notcurses, FINAL CUT, FTXUI, imtui, tvision (Turbo Vision port), tuibox.
- *JavaScript / Node:* Ink, blessed / neo-blessed, terminal-kit, Melker.
- *Other:* Ashen (Swift), Lanterna (Java), brick/vty (Haskell).

**.NET / C# frameworks (deep comparison, Section 4):** Spectre.Console, Terminal.Gui (gui.cs) v2, Consolonia, Terminaux.

**.NET / C# adjacent libraries (evaluated for specific capabilities):**

- *Interactive UI / animation:* klooie (MIT; animation- and physics-driven console UI with forms and focus), PowerArgs (its console-app/observability UI layer).
- *Prompts:* Sharprompt, Spectre.Console prompts.
- *Color / styling output:* Pastel, Crayon, Colorful.Console.
- *Banner text:* Figgle (FIGlet), Spectre FIGlet.
- *Tables:* ConsoleTables, BetterConsoleTables.
- *Text / rune handling:* NStack (the string/rune layer under Terminal.Gui).
- *Line editing:* RadLine.
- *ncurses bindings:* Mindmagma.Curses / dotnet-curses / CursesSharp.

**Method note.** The .NET frameworks were reviewed against their repositories and documentation for feature set, layout model, concurrency model, input/mouse support, styling, licensing, and target frameworks. Capabilities were then cross-checked against TUIKit's implemented surface (the same Have / Partial / Missing tags used throughout this report).

---

## 10. Prioritized improvement scorecard

Every improvement identified in this report, scored and rank-ordered. Each dimension is **1–5, higher is more favorable**:

- **Ease** — ease of implementation on the current engine (5 = trivial widget/sugar, 1 = major subsystem).
- **Flex** — flexibility it gives consumers (5 = unlocks many use cases and consumption patterns).
- **Simpl** — simplicity it brings the app developer (5 = removes real friction, 1 = neutral).
- **Perf** — runtime performance impact when used (5 = negligible or positive, 1 = heavy).
- **Stakes** — how table-stakes it is (5 = baseline everyone expects, 1 = niche polish).
- **Total** — the sum (max 25). The table is sorted by Total, highest first.

The rankings are deliberately opinionated; the scores are a starting point for discussion, not a verdict. Ties are broken toward higher table-stakes.

| # | Improvement | Description | Ease | Flex | Simpl | Perf | Stakes | Total | Does it best |
|---|---|---|:--:|:--:|:--:|:--:|:--:|:--:|---|
| 1 | Bind any widget to a region (`Pane : IWidget`, one `Bind`) | Let the host place and render any widget in a region, not just panes; unify the content model | 4 | 5 | 5 | 4 | 4 | **22** | Terminal.Gui |
| 2 | Scrollable viewport + scrollbars | Generic scroll container with a visible scrollbar, reused by panes and lists | 4 | 4 | 4 | 4 | 5 | **21** | Textual / Terminal.Gui |
| 3 | Fuzzy finder / filterable list | Type-to-filter with fuzzy matching and match highlighting; reusable picker | 3 | 5 | 4 | 4 | 5 | **21** | fzf |
| 4 | Inline markup parser | `[bold red]…[/]` → `StyledText`; a `WriteMarkup` overload | 4 | 4 | 5 | 4 | 4 | **21** | Spectre.Console / Rich |
| 5 | Async prompts on the app | `ConfirmAsync`/`PromptAsync`/`SelectAsync`/`ShowAsync` — one-line dialogs | 4 | 4 | 5 | 4 | 4 | **21** | Spectre.Console |
| 6 | Split / grid layout DSL | `Row`/`Column`/`Grid` sugar over the constraint solver | 4 | 4 | 5 | 4 | 4 | **21** | Lip Gloss / FTXUI |
| 7 | Status / hint bar widget | Contextual footer of keybinding hints, promoted from the example | 5 | 3 | 4 | 5 | 4 | **21** | helix / lazygit |
| 8 | Docs, cookbook & `dotnet new` templates | Quick-start, recipe cookbook, and project starters | 4 | 3 | 5 | 5 | 4 | **21** | Textual / Spectre |
| 9 | One-step `Bind(chord, action)` + app verbs | Collapse the two-step keybinding; add `Quit`/`Log`/`Notify` on the app | 5 | 3 | 5 | 5 | 3 | **21** | Bubble Tea |
| 10 | One-call bootstrap + defaults + single `using` | `TuiApp.RunAsync`/`Create` with a default backend, layout, and quit key | 5 | 3 | 5 | 4 | 3 | **20** | Bubble Tea / Spectre |
| 11 | Tree / hierarchical list | Expand/collapse nodes with indent guides and selection | 3 | 4 | 4 | 4 | 5 | **20** | Terminal.Gui / yazi |
| 12 | Diff renderer | Unified/side-by-side, add/remove/context coloring, hunk headers | 3 | 4 | 4 | 4 | 5 | **20** | delta / lazygit |
| 13 | Collapsible section widget | Expandable header for tool calls, log groups, and details | 4 | 4 | 4 | 4 | 4 | **20** | Claude Code / Textual |
| 14 | Tabs widget | Tabbed views with keyboard/mouse switching | 4 | 3 | 4 | 5 | 4 | **20** | zellij / Terminal.Gui |
| 15 | Forms / dialog framework | Inputs + validation + shared tab order | 3 | 4 | 5 | 4 | 4 | **20** | Terminal.Gui |
| 16 | Expanded theme role vocabulary | success/warning/error/info/selection/disabled semantic roles | 5 | 3 | 4 | 5 | 3 | **20** | Textual / Spectre |
| 17 | Real data table (sortable/resizable/virtualized) | Replace the static table with selection, column sizing, and virtualization | 2 | 5 | 4 | 3 | 5 | **19** | Terminal.Gui / k9s |
| 18 | Syntax highlighting | A tokenizer + theme producing styled code | 2 | 5 | 4 | 3 | 5 | **19** | helix / bat |
| 19 | Autocomplete / typeahead popup | Caret-anchored, live-filtered completion (`/`, `@`) | 3 | 4 | 4 | 4 | 4 | **19** | prompt_toolkit |
| 20 | Global focus manager | Tab/Shift-Tab focus ring with visible focus rings | 3 | 4 | 4 | 4 | 4 | **19** | Terminal.Gui / Textual |
| 21 | In-pane search (`/`) | Match navigation and highlighting within a pane | 4 | 3 | 4 | 4 | 3 | **18** | less / k9s |
| 22 | Nested layouts inside a region | Sub-layouts (mini flex/grid) without manual coordinate math | 3 | 4 | 4 | 4 | 3 | **18** | Terminal.Gui (Pos/Dim) |
| 23 | `TuiTest` / app-driver harness | Feed keys, tick a frame, assert a `Snapshot` | 4 | 3 | 4 | 5 | 2 | **18** | Textual (snapshot testing) |
| 24 | Multi-task progress / `Live` wrapper | Concurrent progress display over the render loop | 4 | 3 | 4 | 4 | 3 | **18** | Spectre.Console |
| 25 | Menus / menu bar / context menus | Drop-down and context menus | 3 | 3 | 4 | 4 | 3 | **17** | Terminal.Gui / Turbo Vision |
| 26 | Nerd Font / icon glyphs + ASCII fallback | Icon glyphs for status columns and trees | 4 | 3 | 3 | 5 | 2 | **17** | yazi / lsd |
| 27 | Charts (braille canvas + line/bar) | A drawing canvas, then line and bar charts | 3 | 4 | 3 | 3 | 3 | **16** | btop / Ratatui |
| 28 | Interactive split resize | Draggable/keyboard-resizable region borders | 3 | 3 | 3 | 4 | 3 | **16** | tmux / zellij |
| 29 | Markdown completeness | Tables, ordered/nested lists, task lists, nested quotes | 3 | 3 | 3 | 4 | 3 | **16** | glow / Rich |
| 30 | Data binding / reactive layer | `INotifyPropertyChanged`/`ObservableCollection` + a declarative option | 2 | 5 | 4 | 3 | 2 | **16** | Consolonia / Textual |
| 31 | Suspend/resume + signal restoration | Drop to shell/editor and restore; SIGTSTP/SIGCONT/SIGTERM | 3 | 3 | 3 | 4 | 3 | **16** | vim / tmux |
| 32 | Modal-editing helper | normal/insert/visual modes and command mode over the routing table | 3 | 3 | 3 | 5 | 2 | **16** | helix / vim |
| 33 | Animation / transitions / timers | Frame-tick + easing for smooth spinners and transitions | 3 | 4 | 3 | 3 | 2 | **15** | Textual / klooie |
| 34 | OSC 8 emission + clipboard read + link hints | Finish the link/clipboard story; Vimium-style link labels | 3 | 3 | 3 | 4 | 2 | **15** | WezTerm / kitty |
| 35 | File browser / open dialog widget | A file picker with search and filtering | 2 | 3 | 4 | 3 | 3 | **15** | ranger / Terminal.Gui |
| 36 | FIGlet / banner text | Large ASCII-art headings | 4 | 2 | 3 | 4 | 1 | **14** | Spectre / Figgle |
| 37 | Color picker widget | Interactive color selection | 3 | 2 | 3 | 4 | 2 | **14** | Terminal.Gui |
| 38 | Box shadows / modal drop shadows | Shadow under boxes and modals (region borders already shipped) | 4 | 2 | 3 | 4 | 1 | **14** | Textual / Spectre |
| 39 | Backdrop dimming behind modals | Dim covered cells via a compositor read-back pass | 3 | 2 | 3 | 3 | 2 | **13** | Textual |
| 40 | Image rendering (sixel/kitty/iTerm2/half-block) | Inline pictures with a graphics-protocol path and a cell fallback | 1 | 4 | 3 | 2 | 2 | **12** | yazi / chafa |

**Reading the scorecard.** The top of the list is dominated by *ergonomics and reuse* — binding widgets to regions, scroll containers, markup, prompts, layout sugar, and docs — because they are cheap on the current engine, remove real developer friction, and are broadly expected. The heavy, high-value subsystems (real table, syntax highlighting, diff) score slightly lower only because they are harder to build, not less important — their table-stakes score is maxed. The bottom of the list is genuine polish and platform work (images, dimming, shadows) that is either expensive or niche. A reasonable execution order is: clear the 20+ ergonomics band first, interleave the table/tree/diff/syntax subsystems (they unblock the agent-and-dashboard audiences), and treat everything below ~16 as opportunistic.

### Detailed catalog

Each entry below is numbered to match the scorecard. Code snippets illustrate *proposed* API — they show the shape of the improvement, not shipped signatures.

#### 10.1 — Bind any widget to a region (`Pane : IWidget`)

**What it is.** Make `Pane` implement `IWidget` and add `app.Bind(regionId, IWidget)` so any widget — a gauge, table, or editor — can occupy a layout region, not just panes.

**What it does.** The host measures, arranges, and renders the widget into the region's content rectangle every frame, exactly as it already does for bound panes.

**Benefit.** Deletes the hand-written coordinate math the example currently needs for every non-pane widget, and gives one mental model — "content bound to a region" — for everything.

```csharp
app.Bind("telemetry", new Gauge { Value = 0.62 });
app.Bind("log", new Pane("log"));   // panes bind the same way
```

#### 10.2 — Scrollable viewport + scrollbars

**What it is.** A generic `ScrollView` container that hosts content larger than its region, plus a visible scrollbar for panes and lists.

**What it does.** Clips and scrolls the child, tracks position, and draws a scrollbar thumb; the wheel and PageUp/PageDown move it.

**Benefit.** Any content becomes scrollable without bespoke logic, and users can see how much is off-screen.

```csharp
var view = new ScrollView(child) { ShowScrollbar = true };
view.ScrollToLine(120);
app.Bind("body", view);
```

#### 10.3 — Fuzzy finder / filterable list

**What it is.** A list widget that filters as the user types, using fuzzy matching with highlighted match runs.

**What it does.** Maintains a query, ranks items by fuzzy score, and emphasizes the matched characters.

**Benefit.** The most-reused TUI interaction — palettes, file jumps, pickers — becomes one widget instead of a per-app reimplementation.

```csharp
var picker = new FuzzyList(files) { Query = "recmd" };  // matches "RecallDB/commands.md"
string? chosen = picker.SelectedItem;
```

#### 10.4 — Inline markup parser

**What it is.** A parser that turns Rich/Spectre-style markup into `StyledText`.

**What it does.** Interprets `[bold red]…[/]` tags into styled spans; a `WriteMarkup` overload writes them straight to a pane.

**Benefit.** Concise, readable styled output without chaining builder calls, and styling can live in strings or config.

```csharp
pane.WriteMarkup("[green bold]done[/] [dim]1.2s[/]");
StyledText t = Markup.Parse("[yellow]warning:[/] disk low");
```

#### 10.5 — Async prompts on the app

**What it is.** One-call awaitable dialogs — confirm, text, select, multi-select, and a generic show.

**What it does.** Pushes a prebuilt modal, traps focus, and returns the result via `await`.

**Benefit.** Removes modal boilerplate for the common cases; a dialog reads like a normal async call.

```csharp
if (await app.ConfirmAsync("Delete build/?", "Delete", "Cancel")) { /* ... */ }
string name = await app.PromptAsync("Project name?");
int theme = await app.SelectAsync("Theme", "Dark", "Light");
```

#### 10.6 — Split / grid layout DSL

**What it is.** `Row`, `Column`, and `Grid` helpers layered over the constraint solver.

**What it does.** Express common layouts declaratively; the helpers generate the underlying `AxisConstraint`s.

**Benefit.** Layout reads like the picture, and `AxisConstraint` becomes the advanced escape hatch rather than the default path.

```csharp
app.Layout = Layout.Column(
    Layout.Row(("main", Size.Fill()), ("sidebar", Size.Cells(34))),
    ("footer", Size.Cells(1)));
```

#### 10.7 — Status / hint bar widget

**What it is.** A one-line footer widget that renders contextual keybinding hints, optionally sourced from the routing table.

**What it does.** Lays out `key → label` pairs across the width, truncating gracefully.

**Benefit.** Every good TUI has one; shipping it means apps get discoverable shortcuts for free.

```csharp
var hints = new StatusBar().Add("^P", "palette").Add("^Q", "quit");
app.Bind("footer", hints);
```

#### 10.8 — Docs, cookbook & templates

**What it is.** A documentation site with a quick-start, a recipe cookbook (log, dashboard, form, wizard), and `dotnet new tuikit` templates.

**What it does.** Gives newcomers a working starting point and copy-pasteable patterns.

**Benefit.** The first five minutes decide adoption; templates remove the blank-page problem.

```bash
dotnet new tuikit -o MyApp   # scaffolds a runnable multi-pane app
```

#### 10.9 — One-step `Bind(chord, action)` + app verbs

**What it is.** A convenience `app.Bind("ctrl+q", action)` plus intent-revealing verbs on the app (`Quit`, `Log`, `Notify`).

**What it does.** Registers the chord and its handler in one call; verbs wrap common operations.

**Benefit.** Removes the two-step chord→id→action ceremony for simple bindings, and `app.` IntelliSense reveals the beginner surface.

```csharp
app.Bind("ctrl+q", app.Quit);
app.Bind("ctrl+l", () => app.Notify("cleared", Severity.Info));
```

#### 10.10 — One-call bootstrap + defaults + single `using`

**What it is.** `TuiApp.RunAsync(configure)` / `TuiApp.Create()` that default a `ConsoleBackend`, a full-screen region, the dark theme, and Ctrl+Q-to-quit.

**What it does.** Wraps the boilerplate so a first program is a few lines under a single `using TUIKit;`.

**Benefit.** Dramatically lowers the barrier to a runnable screen.

```csharp
using TUIKit;
await TuiApp.RunAsync(app => app.AddPane("log").WriteLine("hello"));
```

#### 10.11 — Tree / hierarchical list

**What it is.** A widget rendering expandable/collapsible hierarchical nodes with indentation guides.

**What it does.** Manages expand state, selection, and keyboard navigation over a node tree.

**Benefit.** Powers file trees, JSON/YAML viewers, and resource browsers without custom recursion and rendering.

```csharp
var tree = new Tree<FileNode>(root, n => n.Children) { Label = n => n.Name };
tree.Expand(root);
app.Bind("files", tree);
```

#### 10.12 — Diff renderer

**What it is.** A renderer for unified and side-by-side diffs with add/remove/context coloring and hunk headers, optionally syntax-highlighted.

**What it does.** Takes two texts (or a patch) and renders the changes.

**Benefit.** The defining surface for agent CLIs and git tools; ship it once instead of per app.

```csharp
var diff = DiffView.Unified(oldText, newText) { SyntaxLanguage = "csharp" };
app.Bind("diff", diff);
```

#### 10.13 — Collapsible section widget

**What it is.** A header line that expands or collapses a block of child content.

**What it does.** Toggles child visibility on a key or click and shows a disclosure indicator.

**Benefit.** Keeps dense output — tool calls, log groups, stack traces — scannable.

```csharp
var section = new Collapsible("Read src/foo.cs", body) { Expanded = false };
app.Bind("detail", section);
```

#### 10.14 — Tabs widget

**What it is.** A tabbed container showing one of several child views with a tab strip.

**What it does.** Tracks the active tab and switches content on click or shortcut.

**Benefit.** Standard multi-view navigation (k9s/zellij style) as a drop-in.

```csharp
var tabs = new TabView().Add("Logs", logsPane).Add("Stats", statsWidget);
app.Bind("main", tabs);
```

#### 10.15 — Forms / dialog framework

**What it is.** A `Form` that composes input widgets with validation and a shared tab order.

**What it does.** Manages focus across fields, runs validators, and collects results.

**Benefit.** Turns "assemble fields by hand" into a declarative form with built-in navigation.

```csharp
var form = new Form()
    .Text("name", required: true)
    .Checkbox("verbose")
    .Select("theme", "Dark", "Light");
var result = await app.ShowAsync(form);
```

#### 10.16 — Expanded theme role vocabulary

**What it is.** More semantic roles on `Theme` — success, warning, error, info, selection, disabled, accent — beyond today's text/accent/border/muted.

**What it does.** Gives widgets named styles to pull from so color stays consistent and swappable.

**Benefit.** Apps look coherent and re-theme cleanly, with no hard-coded palette indices.

```csharp
pane.WriteLine(Text.From("ok").Style(theme.Success));
pane.WriteLine(Text.From("failed").Style(theme.Error));
```

#### 10.17 — Real data table

**What it is.** A table with sortable and resizable columns, row selection, per-cell styling, and virtualized rendering for large sources.

**What it does.** Renders only visible rows, handles sort and selection, and sizes columns to content or weights.

**Benefit.** Dashboards, k8s/docker consoles, and DB browsers need exactly this; the current static table cannot do them.

```csharp
var table = new DataTable<Process>()
    .Column("PID", p => p.Id)
    .Column("CPU%", p => p.Cpu, sortable: true);
table.Bind(processes);   // virtualized
app.Bind("procs", table);
```

#### 10.18 — Syntax highlighting

**What it is.** A tokenizer plus a highlight theme that turns source code into styled spans.

**What it does.** Lexes a language and maps token types to theme colors; used in code blocks and diffs.

**Benefit.** Makes code readable in transcripts, editors, and diffs — the top agent- and dev-tool ask.

```csharp
StyledText code = Syntax.Highlight(source, "csharp", theme);
pane.WriteLine(code);
```

#### 10.19 — Autocomplete / typeahead popup

**What it is.** A popup anchored at the input caret offering live-filtered completions.

**What it does.** Shows candidates for a trigger (`/`, `@`), filters as the user types, and inserts on accept.

**Benefit.** Slash-command and file-mention UX like the agent CLIs, reusable for any input.

```csharp
editor.Autocomplete = new Autocomplete('/', () => commandNames);
```

#### 10.20 — Global focus manager

**What it is.** A focus ring across declared focusable widgets, with Tab/Shift-Tab traversal and visible focus rings.

**What it does.** Tracks the focused widget, routes input to it, and moves focus on Tab.

**Benefit.** Multi-widget keyboard navigation without per-app focus plumbing.

```csharp
app.Focus.Register(nameField, themeRadio, okButton);
app.Bind("tab", app.Focus.Next);
```

#### 10.21 — In-pane search (`/`)

**What it is.** A `/`-triggered search over a pane's content with match navigation and highlighting.

**What it does.** Finds matches, highlights them, and jumps between them with n/N.

**Benefit.** Long logs and transcripts become navigable — a universal expectation.

```csharp
pane.Search("error");   // highlights and scrolls to the first match
pane.SearchNext();
```

#### 10.22 — Nested layouts inside a region

**What it is.** The ability to place a sub-layout (a mini flex/grid) inside a region.

**What it does.** Resolves child rectangles relative to the parent region, recursively.

**Benefit.** Complex panels compose without manual coordinate math; layouts nest like the UI does.

```csharp
app.Bind("sidebar", Layout.Column(("filters", Size.Cells(6)), ("results", Size.Fill())));
```

#### 10.23 — `TuiTest` / app-driver harness

**What it is.** A test helper that drives a whole app against a headless backend — feed keys, tick a frame, assert a snapshot.

**What it does.** Wraps `HeadlessBackend` + `PumpInputOnce`/`RenderOnce` + `Snapshot.ToText` in a fluent API.

**Benefit.** Testing an app becomes as easy as testing a widget already is.

```csharp
await TuiTest.For(app).Type("hi").Press("enter").Render()
    .AssertContains("you  hi");
```

#### 10.24 — Multi-task progress / `Live` wrapper

**What it is.** A progress display managing several concurrent tasks, and a `Live` convenience that re-renders a widget over the loop.

**What it does.** Tracks per-task progress and paints them together; `Live` updates one region without a full app.

**Benefit.** Common "downloading five things" and "watch this value" displays without wiring the loop.

```csharp
await app.Live(progress, ctx => {
    var t = ctx.AddTask("build", total: 100);
    while (!t.IsFinished) t.Increment(10);
});
```

#### 10.25 — Menus / menu bar / context menus

**What it is.** Drop-down menus, a top menu bar, and right-click context menus.

**What it does.** Renders items with shortcuts and submenus, dispatching commands on selection.

**Benefit.** Desktop-style discoverability for feature-rich apps.

```csharp
var bar = new MenuBar()
    .Menu("File", m => m.Item("Open", "ctrl+o", Open).Item("Quit", "ctrl+q", app.Quit));
app.Bind("menu", bar);
```

#### 10.26 — Nerd Font / icon glyphs + ASCII fallback

**What it is.** A helper set of icon glyphs (file types, status) with an automatic ASCII fallback.

**What it does.** Emits Nerd Font glyphs when available, plain characters otherwise.

**Benefit.** File trees and status columns look modern where supported and stay legible where not.

```csharp
string icon = Icons.ForFile("foo.cs");   // a Nerd Font glyph, or a fallback
```

#### 10.27 — Charts (braille canvas + line/bar)

**What it is.** A braille/quadrant drawing `Canvas`, plus line and bar chart widgets built on it.

**What it does.** Plots series at sub-cell resolution using braille dots.

**Benefit.** btop-quality telemetry graphs, not just sparklines.

```csharp
var chart = new LineChart(series) { Height = 8 };
app.Bind("cpu", chart);
```

#### 10.28 — Interactive split resize

**What it is.** Draggable and keyboard-resizable borders between regions.

**What it does.** Adjusts the constraints of adjacent regions as the border moves.

**Benefit.** Users tune their layout live, like tmux/zellij panes.

```csharp
app.Layout = Layout.Row(("a", Size.Fill()), ("b", Size.Cells(40))).Resizable();
```

#### 10.29 — Markdown completeness

**What it is.** Extend the Markdown renderer to tables, ordered and nested lists, task lists, and nested block quotes, with syntax-highlighted fenced code.

**What it does.** Parses the fuller Markdown grammar into styled lines.

**Benefit.** Agent output and docs render faithfully instead of dropping structure.

```csharp
foreach (StyledText line in Markdown.Render(md, theme))  // now handles | tables | and 1. lists
    pane.WriteLine(line);
```

#### 10.30 — Data binding / reactive layer

**What it is.** Optional `INotifyPropertyChanged`/`ObservableCollection` binding and a small declarative builder.

**What it does.** Re-renders bound widgets when the source changes, MVVM-style.

**Benefit.** Users who prefer Textual/Consolonia ergonomics aren't forced into imperative rendering.

```csharp
list.BindItems(viewModel.Items);          // ObservableCollection
label.Bind(() => viewModel.Status);
```

#### 10.31 — Suspend/resume + signal restoration

**What it is.** Drop to the normal screen to run an external editor or shell and restore, plus SIGTSTP/SIGCONT/SIGTERM handling.

**What it does.** Tears down and rebuilds terminal modes cleanly around external processes and signals.

**Benefit.** Agent harnesses and editors can shell out without corrupting the terminal.

```csharp
await app.SuspendAsync(() => Process.Start("vim", file).WaitForExitAsync());
```

#### 10.32 — Modal-editing helper

**What it is.** A helper layering normal/insert/visual modes and a command mode over the routing table.

**What it does.** Switches active keymaps by mode and parses `:` commands.

**Benefit.** Editor-style apps get Vim-like modes without building the state machine.

```csharp
var modes = new ModalKeymap(app.Commands);
modes.Normal.Bind("i", () => modes.Enter(EditorMode.Insert));
```

#### 10.33 — Animation / transitions / timers

**What it is.** A frame-tick and easing helper for smooth motion, plus elapsed/ETA on spinners and progress.

**What it does.** Schedules value changes over time and interpolates them each frame.

**Benefit.** Interfaces feel alive — smooth spinners, animated progress, gentle transitions.

```csharp
app.Animate(gauge, g => g.Value, from: 0, to: 1, duration: TimeSpan.FromSeconds(1));
```

#### 10.34 — OSC 8 emission + clipboard read + link hints

**What it is.** Emit OSC 8 hyperlinks for copy and degradation, read the clipboard, and show Vimium-style keyboard link hints.

**What it does.** Wraps link runs in OSC 8, exposes clipboard read, and overlays letter labels to activate links by keyboard.

**Benefit.** Finishes the links/clipboard story; links survive copy-paste and are reachable without a mouse.

```csharp
pane.WriteLine(Text.Link("docs", "https://example.com"));  // virtual + OSC 8
app.ShowLinkHints();   // press a letter to open
```

#### 10.35 — File browser / open dialog widget

**What it is.** A file picker widget and dialog with search and filtering.

**What it does.** Navigates the filesystem, filters entries, and returns a selection.

**Benefit.** "Open a file" is a common need; ship it instead of rebuilding it.

```csharp
string? path = await app.OpenFileAsync(startDir: ".", filter: "*.cs");
```

#### 10.36 — FIGlet / banner text

**What it is.** Large ASCII-art text from FIGlet fonts.

**What it does.** Renders a string as multi-row banner glyphs.

**Benefit.** Splash screens and section headers with personality.

```csharp
pane.WriteLine(Figlet.Render("TUIKit", font: "standard"));
```

#### 10.37 — Color picker widget

**What it is.** An interactive color-selection widget (palette and/or RGB).

**What it does.** Lets the user pick a color and returns it.

**Benefit.** Theme editors and drawing tools need it; a ready widget saves the effort.

```csharp
Color chosen = await app.PickColorAsync(initial: theme.Accent);
```

#### 10.38 — Box shadows / modal drop shadows

**What it is.** An optional drop shadow under boxes and modals (region borders already ship).

**What it does.** Draws a dimmed, offset shadow behind a framed region.

**Benefit.** Modals lift off the background — a cheap, high-impact aesthetic touch.

```csharp
region.WithBorder(BorderStyle.Rounded).WithShadow();
modal.Shadow = true;
```

#### 10.39 — Backdrop dimming behind modals

**What it is.** Dim the cells behind an open modal via a compositor read-back pass.

**What it does.** Scales the RGB of covered cells toward the background before drawing the modal.

**Benefit.** Focus is drawn to the dialog and the app feels polished.

```csharp
app.Modals.BackdropDim = 0.5;   // 0 = none, 1 = fully darkened
```

#### 10.40 — Image rendering (sixel/kitty/iTerm2/half-block)

**What it is.** Inline images via sixel, the kitty graphics protocol, or iTerm2, with a half-block/braille cell fallback.

**What it does.** Detects the terminal's graphics capability and encodes the image accordingly.

**Benefit.** File managers, media tools, and previews can show real pictures.

```csharp
var img = new ImageView("logo.png") { Fallback = ImageFallback.HalfBlock };
app.Bind("preview", img);
```
