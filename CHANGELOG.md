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

[Unreleased]: https://github.com/jchristn/TUIKit/commits/main
