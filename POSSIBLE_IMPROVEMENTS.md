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

## 8. Appendix — everything evaluated

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
