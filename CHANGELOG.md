# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Core primitives**: `Point`/`Size`/`Rect` geometry, `Color` (truecolor/palette/default), `CellStyle`/`CellAttributes`, `Cell`, `CellBuffer` with per-row dirty tracking, `ISurface`/`BufferSurface` with clipped views, and text-drawing extensions.
- **Unicode**: self-contained `TextWidth` (wcwidth interval tables) and `Graphemes` segmentation (combining marks, emoji ZWJ, regional-indicator flags), identical across target frameworks.
- **Styled text**: fluent `Text.From("x").Bold().Red()` builder with per-span styles.
- **Terminal backend layer**: `ITerminalBackend`, in-memory `HeadlessBackend`, real `ConsoleBackend` (Windows `SetConsoleMode` / Unix `stty` raw mode), capability detection, ANSI/SGR sequence builders, and truecolor→256/16 quantization.
- **Rendering**: double-buffered `TerminalRenderer` with row-level diffing and SGR coalescing.
- **Layout**: developer-defined region model with per-axis constraints (fixed, end-anchored, stretch, proportional), a solver, minimum-size derivation, and a "terminal too small" block screen.
- **Content**: thread-safe `Pane` with mutable line handles, smart scroll lock, capacity eviction, atomic batches; word/hard text wrapping; a Markdown renderer; ANSI stripping; text selection with OSC 52 clipboard.
- **Input**: byte decoder (UTF-8, control keys, CSI/SS3, CSI u, SGR mouse, bracketed paste), key chords with a string parser, a command routing table with scopes/conflict policy/multi-key sequences, and a configurable Ctrl+C policy.
- **Mouse and links**: link registry with per-frame hit-testing, security-aware auto-linkify allowlist, and click-count synthesis.
- **Modals and notifications**: focus-trapping modal stack with async results and close refusal, plus non-focus-stealing toasts.
- **Widgets**: label, gauge, sparkline, progress bar, spinner, list, table, multi-line editor (undo/redo, kill/yank), text field, checkbox, and radio group.
- **Theming**: dark/light/high-contrast themes with named styles and an ASCII-border fallback.
- **Hosting**: `TuiApplication` with render and input loops, command dispatch, modal integration, non-TTY line degradation, terminal restoration, and a single-instance guard.
- **Diagnostics and testing**: frame statistics, input record/replay, a debug overlay, and a `Snapshot` helper for headless snapshot testing.
- **Example**: a full simulated agent-control harness demonstrating every capability, with a capability-coverage matrix.
- Repository scaffolding, four Touchstone test projects, CI workflow, and documentation (README, LICENSE, CLAUDE, conformance and coverage reports).

### Added
- **Ergonomics layer** (from `POSSIBLE_IMPROVEMENTS.md`): `Pane` now implements `IWidget`, and `TuiApplication.Bind(regionId, IWidget)` binds any widget to a region (10.1). App verbs `Bind(chord, action)`, `Quit`, `Notify`, and the `AddPane`/`AddWidget`/`AddRegion` helpers (10.9); the `TuiApp.RunAsync` one-call bootstrap (10.10). Inline markup: `Markup.Parse` and `Pane.WriteMarkup` for `[bold red]…[/]` styling (10.4). Expanded theme roles — success/warning/error/info/selection/disabled — and `StyledText.Style` (10.16). A `Layout.Row`/`Layout.Column` split DSL with `LayoutSlot` (10.6).
- **New widgets**: `StatusBar` (10.7), `ScrollView` with vertical and horizontal scrollbars (10.2), `Collapsible` (10.13), `TabView` (10.14), `Tree<T>` (10.11), and `FuzzyList` (10.3).
- **Focus, forms, prompts, find** (tranche 2): a global `FocusManager` (10.20); `Form`/`FormField` with tab navigation and validation (10.15); async `ConfirmAsync`/`PromptAsync`/`SelectAsync` prompts (10.5); `Pane` incremental find with match highlighting and `TextEditor.Find`/`ReplaceAll`, surfaced as `Ctrl+F`/`Ctrl+H` bindings (10.21).
- **Guided-tour example**: running `TUIKit.Example` now launches a self-describing guided tour — a header names each feature, the left pane renders the live widget, and the right pane shows the code that builds it (PageUp/PageDown to browse 15 feature pages, arrows/Enter to interact, Ctrl+Q to quit). The original agent-control harness moved behind `--harness`, with a headless `--tour-once --page N` snapshot for CI/screenshots.
- **Key-binding editor & templates** (tranche 9): `KeyBindingSet`/`KeyBinding` model an editable, conflict-checked keymap, and `KeyBindingEditor` is an interactive settings widget that captures a key press to rebind a command (the user-configurable key bindings). Added a `tuikit-app` `dotnet new` template under `templates/` for scaffolding new apps (10.8).
- **Modal editing, links, lifecycle** (tranche 8): `ModalDispatcher`/`EditMode` route keys through vi-style modes with per-mode bindings and transitions (10.32); `LinkHints` assigns keyboard labels to links and `SystemClipboard` adds best-effort native clipboard read alongside the existing OSC 8 hyperlink and OSC 52 write support (10.34); `AppLifecycle` surfaces suspend/resume/resize/interrupt as events and wires POSIX signals on .NET 8+ Unix (10.31).
- **Banners and visual effects** (tranche 7): `Banner`/`BannerText` render large block-letter text from a built-in 5×5 font (10.36); `ISurface.DrawShadow` casts a drop shadow under a box (10.38); `Backdrop.Dim` dims a rendered buffer behind a modal (10.39); and `HalfBlockImage` renders truecolor pixel images at double vertical resolution with the ▀ glyph (10.40).
- **Splits, menus, files** (tranche 6): `SurfaceView` maps a sub-region of any surface into local coordinates, and `SplitView` uses it to divide an area between two children with an arrow-key-resizable divider that nests arbitrarily (10.22, 10.28); `MenuBar`/`Menu`/`MenuItem` provide a keyboard-driven menu bar with drop-downs (10.25); `FileBrowser` lists and navigates the file system with selection and activation events (10.35).
- **Reactive, animation, testing** (tranche 5): `Observable<T>` gives thread-safe one-way data binding with `Subscribe`/`Bind` (10.30); `Easing`, `Tween`, and a tick-driven `FrameTimer` provide deterministic animation and periodic callbacks (10.33); `MultiProgress`/`ProgressTask` render concurrent progress bars (10.24); and `WidgetTester` is a fluent headless harness for driving keys and asserting rendered output (10.23).
- **Charts, icons, color** (tranche 4): `BrailleCanvas` packs a 2×4 dot grid per cell for high-resolution drawing, with `LineChart` and `BarChart` built on it (10.27); an `Icons` catalog exposes common glyphs as Nerd Font, portable Unicode, and ASCII renderings selectable via `IconMode` (10.26); `ColorPicker` is an interactive RGB picker with channel sliders and a hex preview (10.37).
- **Diff, table, syntax, Markdown** (tranche 3): `DiffView` renders an LCS-based unified line diff with colored add/remove/context lines and optional syntax-highlighted context (10.12); `DataTable<T>` gives a columnar, sortable, virtualized table over any typed source (10.17); `SyntaxHighlighter` colors keywords, strings, numbers, and comments for C#/JS/TS/Python/JSON (10.18); the Markdown renderer now handles task lists, ordered lists, nested bullets, and tables (10.29).
- **User documentation**: `BUILDING_TERMINAL_APPS.md`, an exhaustive guide to building apps with the library, including a worked live-dashboard example and cross-platform/SSH/tmux guidance.
- Regions can declare a border via `WithBorder(BorderStyle, title)`: `None`, `Ascii`, `Line`, `Rounded`, `Double`, or `Thick`. The host draws the border, honors the theme's ASCII fallback, insets content for it, and folds it into the region's minimum size. `DrawBox` gained a `BorderStyle` overload. The example's tool, telemetry, and composer panels now use declarative rounded borders.
- `POSSIBLE_IMPROVEMENTS.md`: a capability analysis versus the agent CLIs, the awesome-tuis catalog, and the C#/.NET frameworks (Spectre.Console, Terminal.Gui, Consolonia, Terminaux), with a tiered improvement roadmap.

### Changed
- `SplitView.MinRatio`/`MaxRatio` now clamp assignments to `[0.0, 1.0]`, `ResizeStep` rejects values outside `(0.0, 1.0]` with `ArgumentOutOfRangeException`, and the divider clamp is robust to an inverted min/max. Added a dedicated value-validation test suite asserting that every range-constrained input across the widgets (gauges, progress, sparkline, scroll view, split, color picker, braille canvas, tween, timer, cell buffer) either clamps or throws.
- Layout regions default to one cell of interior padding on every side, and `Padding` supports vertical as well as horizontal insets. Content and debug outlines no longer touch region edges.
- Themes now set an explicit background, so switching between dark, light, and high-contrast reverses the whole palette. Pane content composes over the theme background via `CellStyle.Over`.

### Fixed
- Modals (`Modal.ContentPadding`) inset their content from the border; the example's confirmation, palette, settings, and help dialogs now keep a cell of padding on every side.
- `DrawBox` clips an over-long title, and the example help overlay renders into a clipped, sized box so its text no longer spills outside the frame.
- The example transcript no longer stacks blank lines between streamed paragraphs and list items.

[Unreleased]: https://github.com/jchristn/TUIKit/commits/main
