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
- Regions can declare a border via `WithBorder(BorderStyle, title)`: `None`, `Ascii`, `Line`, `Rounded`, `Double`, or `Thick`. The host draws the border, honors the theme's ASCII fallback, insets content for it, and folds it into the region's minimum size. `DrawBox` gained a `BorderStyle` overload. The example's tool, telemetry, and composer panels now use declarative rounded borders.
- `POSSIBLE_IMPROVEMENTS.md`: a capability analysis versus the agent CLIs, the awesome-tuis catalog, and the C#/.NET frameworks (Spectre.Console, Terminal.Gui, Consolonia, Terminaux), with a tiered improvement roadmap.

### Changed
- Layout regions default to one cell of interior padding on every side, and `Padding` supports vertical as well as horizontal insets. Content and debug outlines no longer touch region edges.
- Themes now set an explicit background, so switching between dark, light, and high-contrast reverses the whole palette. Pane content composes over the theme background via `CellStyle.Over`.

### Fixed
- Modals (`Modal.ContentPadding`) inset their content from the border; the example's confirmation, palette, settings, and help dialogs now keep a cell of padding on every side.
- `DrawBox` clips an over-long title, and the example help overlay renders into a clipped, sized box so its text no longer spills outside the frame.
- The example transcript no longer stacks blank lines between streamed paragraphs and list items.

[Unreleased]: https://github.com/jchristn/TUIKit/commits/main
