# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.0] - 2026-08-13

Horizontal component expansion: a set of general-purpose components, drawn from patterns downstream
consumers had to build by hand, plus per-region background colors.

### Added
- **Per-region background colors.** A layout `Region` can now carry a background painted across its
  whole resolved rectangle, behind the border and any bound widget. Set an explicit color with
  `RegionBuilder.Background(Color)`, or bind to a named theme style with
  `RegionBuilder.BackgroundRole(string)` so a theme switch restyles the region without a code change;
  `NoBackground()` clears it. Regions with no background stay transparent and inherit the theme text
  background, so the change is backward compatible. The built-in themes register conventional
  `Theme.SidebarRole` and `Theme.StatusBarRole` styles for panels and status strips.
- **`DialogModal` base class.** A reusable base for centered, bordered dialogs that owns box
  measurement, min/max content clamping, centering, background fill, border, an optional title and a
  dim footer hint, and hands subclasses a clipped inner surface through `RenderContent`. Subclasses
  report their natural content size and draw content; they no longer compute box geometry by hand.
- **`CheckList<T>` widget and `MultiSelectModal<T>` dialog.** A vertical list whose items can each be
  checked independently (Space toggles, a configurable key toggles all), of any item type with a
  display selector, plus a dialog wrapper that completes with the checked indices (Enter) or null
  (Escape).
- **`DefinitionList` widget.** A thread-safe list of labeled values with optional section headers for
  status panels and telemetry sidebars; setting the same label updates its value in place, and values
  truncate (never the label) when space is tight.
- **`ActivityIndicator` widget.** A spinner-plus-rotating-phrase "working…" line advanced by explicit
  ticks (deterministic and testable), exposing its current line so it can also be pushed into a pane.
- **`StreamingTranscript` helper.** Projects a stream of text and keyed status lines onto a `Pane`:
  buffer streaming text into a block shown on a live line, re-render the finished block as Markdown,
  and update named lines in place (for example flipping a task line from "running…" to "done").
- **`ActionListView<T>` widget.** A list whose rows expose keyboard actions (Enter to activate, plus
  consumer-registered chords like "e"/Delete) with an optional per-row enabled predicate; firing an
  action raises a typed `ListAction<T>` carrying the row index, item, and action id.
- **`ReorderableList<T>` widget.** A list that moves the selected item up/down (Alt+Up/Down or "["/"]")
  and removes it (Delete/"d"), exposing the current order and raising `Reordered`/`Removed` events.
- **`Command` and `CommandRegistry`.** One command descriptor (id, title, category, optional chord,
  slash aliases, handler, enabled predicate) projected onto every surface: `ApplyTo` binds chords and
  handlers into the host, `BuildMenuBar` groups enabled commands by category, `BuildPalette` returns a
  `FuzzyList<Command>` for a command palette, and `ResolveSlash` routes `/name args` input.
- **Autocomplete / typeahead.** An `ISuggestionProvider` contract (with a built-in
  `PrefixSuggestionProvider`) and an `AutocompleteOverlay` that shows ranked, caret-anchored
  suggestions for a text input — Up/Down to move, Tab/Enter to accept, Escape to dismiss — flipping
  above the caret when there is no room below. This is the one capability the original build plan had
  deliberately excluded.
- **Text and input utilities.** `HintText.Wrap` wraps a separator-delimited hint footer to a width
  without splitting a segment; `ColumnFormatter.Format` aligns rows into columns sized to the widest
  cell; the `Rule` widget draws a horizontal or vertical divider with an optional centered caption;
  and `SubmitKeyResolver` resolves a key to submit / insert-newline / ignore, encoding the
  cross-terminal Enter-vs-Ctrl+J-vs-Shift+Enter reality so multi-line editors stop reinventing it.

### Changed
- **`ScrollView` follows focus.** When its child implements the new `IScrollExtent` contract,
  `ScrollView` scrolls the child's focused region into view automatically (`AutoScrollToFocus`, on by
  default), with a public `EnsureVisible(top, height)` that clamps rather than throwing. `Form`
  implements `IScrollExtent`, so a form taller than its viewport keeps the focused field visible.
- **`Form` field sets can be rebuilt at runtime.** `Form.Clear()` empties the fields and resets the
  focus ring (paired with `FocusManager.Clear()`), and `Form.SetFocusedField(index)` restores focus
  after a rebuild — the basis for dependent forms that swap fields when a selection changes.
- **`KeyLabel` audit.** Confirmed OS-adaptive modifier labels are complete: `KeyChord.ToLabel` renders
  `⌃/⌥/⇧/⌘` in symbol style and `Ctrl+/Alt+/Shift+/Super+` in ASCII, and `KeyLabel.Recommended`
  follows the platform. No code change was needed; existing coverage stands.

### Breaking
- **Generic list widgets.** `ListView`, `FuzzyList`, and (internally) the list picker are now generic
  over the item type: `ListView<T>` and `FuzzyList<T>` take an optional `Func<T,string>` display
  selector (the identity when `T` is `string`) and expose `SelectedItem` as `T`, so the selection is
  the original object rather than a string that had to be mapped back. Migration: `new ListView()`
  becomes `new ListView<string>()` and `new FuzzyList(items)` becomes `new FuzzyList<string>(items)`;
  a non-string type supplies a selector, e.g. `new ListView<FileInfo>(f => f.Name)`. `SelectModal`
  keeps its string+index API (it is backed by `ListView<string>`), so its call sites are unchanged.

## [0.5.1] - 2026-08-04

Documentation fixes. No code changes from 0.5.0.

### Fixed
- **README version references.** The header banner and the `<PackageReference>` install snippet now
  reflect the 0.5 line (masked text input) instead of 0.4.1.

### Changed
- **Archived internal notes.** `USABILITY_IMPROVEMENTS.md` moved into `archive/`.

## [0.5.0] - 2026-08-04

Masked text input for secret entry.

### Added
- **`TextField` value masking.** `TextField` gains a `MaskChar` property and an `IsMasked`
  convenience flag so a field can obscure its rendered value — for passwords, API keys, bearer
  tokens, and other secrets. When `MaskChar` is `'\0'` (the default) the field renders as typed, so
  the change is additive and backward compatible. Set it to a visible character (for example `'•'`)
  and every value character, including the glyph shown under the caret, renders as the mask while
  `Value` and all editing and caret behavior stay unchanged.

## [0.4.1] - 2026-07-30

Input decoding fix so applications can offer a terminal-independent "insert newline" chord.

### Fixed
- **Carriage return and line feed are decoded distinctly.** `InputParser` previously folded both
  `0x0D` (CR) and `0x0A` (LF) into `Enter`. Now CR decodes as `Enter` while LF decodes as `Ctrl+J`
  (`Char` `'j'` + `Ctrl`). In raw mode Enter always transmits as CR, so this is lossless — LF arrives
  only from Ctrl+J. Because no terminal reports `Shift+Enter` or `Ctrl+Enter` without the enhanced
  keyboard protocol (Windows Terminal, macOS Terminal.app, and legacy xterm all send a bare CR), an
  application can now bind `Ctrl+J` as a newline chord that works on every platform. Bracketed paste
  is unaffected: pasted newlines are still captured as literal paste text, not key events.

## [0.4.0] - 2026-07-29

The interaction-contract release. The host now assembles the interactive skeleton — focus, key
precedence, mouse hit-testing, and modal marshalling — on the consumer's behalf, turning "read the
example and replicate the wiring" into "bind widgets, set focus, run." Everything is additive: the
raw escape hatches (`KeyReceived`, `MouseReceived`, `RenderOverlay`) still work unchanged.

### Added
- **Host-owned focus ring.** Focusable widgets bound with `Bind`/`AddWidget` join a focus ring in
  bind order; the first is focused automatically. `TuiApplication.Focus(regionId)`, `FocusNext()`,
  `FocusPrevious()`, the `FocusedRegion`/`FocusOrder` properties, and the `FocusChanged` event drive
  and observe focus. `FocusContext` now follows focus automatically, so focus-scoped commands apply
  to whatever is focused. `Tab`/`Shift+Tab` traverse the ring when the focused widget declines them.
- **Focus contract (`IFocusAware`).** A new optional companion to `IFocusable`: the host and
  `FocusManager` call `OnFocusChanged(bool)` on every focus transition so a widget's rendered focus
  state never diverges from routing. `TextField`, `TextEditor`, and `ListView` implement it;
  `FocusManager` now drives visual focus.
- **Explicit input-precedence chain.** Keys route in a defined order: modal trap → optional
  `KeyFilter` pre-filter → focus-scoped commands → **focused widget (first refusal)** → host focus
  traversal → global commands → `KeyReceived`. Giving the focused widget first refusal fixes the
  global-chord-vs-widget-key collision (e.g. a global `Ctrl+K` sequence no longer steals the editor's
  kill-to-end-of-line).
- **Sequence timeout is wired.** A dangling two-key sequence prefix is cleared after
  `SequenceTimeoutMilliseconds` (default 800), and an abandoned prefix no longer swallows the
  following keystroke — the next key falls through and is processed normally.
- **Host-owned mouse routing.** A per-frame, host-owned hit-test map (rebuilt every draw pass, never
  stored on widgets) powers click-to-focus for `IFocusable` widgets and wheel/click forwarding to the
  new optional `IMouseAware` interface, with coordinates translated into each widget's own rectangle.
  `Pane` and `ScrollView` scroll on the wheel out of the box. Toggle with `EnableMouseRouting`.
- **Typed modals and a loop scheduler.** `ShowAsync<T>(Modal)` returns the modal result as `T` with no
  cast, and `Post(Action)` queues work onto the loop thread (drained each frame) so a modal
  continuation or a background task can safely mutate UI state.
- **Application-shell layout helpers.** `LayoutBuilder.DockTop`/`DockBottom`/`DockLeft`/`DockRight`/
  `Fill` build a four-way shell (header, footer, sidebar, main) as real, non-overlapping regions you
  bind `StatusBar`/`MenuBar`/content into — no hand-computed rectangles or overlay math.
- **Interaction-contract example.** A new `--contract` demo (and `--contract-once` snapshot) stands up
  a full interactive app in ~120 lines using the dock shell, the focus ring, click-to-focus, a
  focus-scoped `Enter`, a two-key theme chord, and a typed picker modal marshalled back with `Post`.
- Touchstone `Usability` suite covering the focus ring, precedence chain, sequence timeout,
  click-to-focus, wheel routing, typed modals, `Post`, multi-key `Bind`, the layout guard, and the
  dock helpers.

### Fixed
- **`Bind` now parses the documented multi-key syntax.** `app.Bind("ctrl+k ctrl+t", …)` registers a
  two-key sequence instead of throwing; a single chord still binds a single command. Rebinding a
  sequence is idempotent (`CommandRoutingTable.UnregisterSequence`).
- **Assigning `Layout` after incremental construction is now rejected** with a clear
  `InvalidOperationException` instead of silently discarding regions added via
  `AddRegion`/`AddPane`/`AddWidget`. Appending with `AddRegion` after assigning a layout still works.
- **Version/documentation drift** corrected: the README and package metadata now agree on the current
  version.

### Changed
- `CommandRoutingTable` gained `ResolveFocusScoped` and `ResolveGlobalSingle` (scope-specific
  resolution) and `CommandRouter` gained `BeginPending`/`TryCompletePending` so a host can own the
  precedence chain. The existing `Process`/`ResolveSingle` methods are unchanged and still available.

## [0.3.1] - 2026-07-29

Unix keyboard input follow-up to v0.3.0.

### Fixed
- **macOS/Linux: keystrokes no longer echo and the screen no longer scrolls.** The Unix `ConsoleBackend` now performs all terminal I/O directly on the standard file descriptors (libc `read`/`write`) instead of through `System.Console`. `System.Console`'s Unix implementation echoed input and re-cooked the terminal (re-enabling `ISIG`/`OPOST`/echo) *behind* the `termios` raw mode we set, which is why typing, Enter, and Tab echoed and scrolled the view even after raw mode was applied. Window size still comes from `Console.WindowWidth`/`Height`, which queries the tty without re-cooking it.
- **Function keys and help are reliable.** F1–F4 are now decoded in their CSI form (`ESC [ P`, `ESC [ 1;5 P`) in addition to SS3 (`ESC O P`), so F1 opens help under the enhanced-keyboard protocol iTerm2 and others use. Kitty key-release events and modifier sub-parameters (`5:3`) are parsed, so a key release no longer double-fires a command or dismisses the modal its press just opened — the cause of the phantom command and the settings dialog not appearing.

### Added
- The example binds `?` as a help alias (macOS frequently reserves F1–F12 for system functions) and renders footer/help key hints via `KeyChord.ToLabel(KeyLabel.Recommended)`.
- Input-decoder tests for F1–F4 via CSI, a Cursor Position Report not being mistaken for F3, and Kitty release-event suppression.

### Validated
- Interactive keyboard input, rendering, and terminal restoration confirmed working on **Windows** (Windows Terminal), **macOS** (iTerm2), and **Linux**, including over an **SSH** session.

## [0.3.0] - 2026-07-29

Native cross-platform keyboard input.

### Fixed
- **Unix raw mode now works natively.** `ConsoleBackend` previously shelled out to `stty` in a child process to enter raw mode on Linux and macOS; the .NET runtime's terminal-state save/restore around child processes silently reverted the change, leaving the terminal in cooked mode with echo on. The result was that on macOS (iTerm2, Terminal.app) and Linux, typing echoed and scrolled the screen, Tab and Page Up/Down were consumed by the terminal, and function keys, `Ctrl` combinations, and the help/settings shortcuts never reached the app. Raw mode is now set **in-process** via the libc `termios` API (`tcgetattr`/`cfmakeraw`/`tcsetattr`), so keyboard handling matches the Windows console path across Windows Terminal/Command Prompt/PowerShell, iTerm2/Terminal.app, Linux terminals, and SSH/tmux sessions. When standard input is not a terminal, raw mode is skipped gracefully.

### Added
- **`KeyChord.ToLabel(KeyLabelStyle)`** plus the `KeyLabelStyle` enum (`Ascii`, `Symbols`) and `KeyLabel.Recommended`, for rendering key hints with the conventions of the host OS — `Ctrl+G` / `PgUp` on Windows and Linux, `⌃G` / `⇞` on macOS. The example app's footer and help now use it.
- **Process-exit safety net**: on Unix, `ConsoleBackend` restores cooked mode, shows the cursor, and leaves the alternate screen if the process exits without calling `Stop`, so a crash no longer leaves a broken terminal.
- Input-decoder coverage for the full non-text key set (Tab, Shift+Tab, F1–F12 via SS3 and CSI, arrows via CSI and SS3, Page Up/Down, Home/End, Insert/Delete, `Ctrl` letters, split escape sequences) and for the new key-label formatting, plus a documented manual interactive test matrix in the Building Terminal Apps guide.

## [0.2.0] - 2026-07-29

Styled one-shot output, plus a hardening and documentation pass.

### Added
- **Styled one-shot output (no full-screen app).** A new surface for printing styled text and tables to a `TextWriter` inline — the building blocks for a CLI to render color without a `TuiApplication`:
  - `Markup.Escape(text)` — escapes `[`/`]` so arbitrary text renders literally.
  - `TUIKit.Terminal.AnsiText.Render(StyledText | markup, TerminalColorDepth)` — styled text → a flowing SGR string (no cursor moves); plain when depth is `None`.
  - `TUIKit.Rendering.InlineRenderer.ToAnsiLines(CellBuffer, TerminalColorDepth)` — a cell buffer → colored inline lines (coalesced SGR runs), matching `Snapshot.ToText` when plain.
  - `CapabilityDetector` now honors `NO_COLOR`, and `ResolveOutputColorDepth(TextWriter)` picks the depth for a writer (plain when redirected / `NO_COLOR` / `TERM=dumb`).
  - `TUIKit.StyledConsole` — the writer itself: `Write`/`WriteLine`/`Markup`/`MarkupLine` and `Write(IWidget)`, over an explicit `TextWriter` or `ForStandardOutput()`/`ForStandardError()`.
  - `Table` gained borders (`TableBorder` None/Square/Rounded), styled/markup cells (`AddRow(params StyledText[])`, `AddMarkupRow`), content-fit sizing (`ColumnSizing`), and per-column `CellAlignment` — the original even-column, borderless behavior is unchanged.
- **Comprehensive validation coverage**: eight suites asserting that every documented argument guard and range bound across the host (`TuiApplication`/`TuiApp`), core primitives (geometry, buffers, styled text, surface drawing), layout, theming, input/routing, links, backends (including `ConsoleBackend`), modals, content, diagnostics, and widgets either throws the declared exception or clamps. Added `Check.ThrowsAsync`, behavioral tests for `PromptModal`/`SelectModal` Enter/Escape results, and the styled-output suite above — **264 Touchstone cases** total, green across the console, xUnit, and NUnit runners on net8.0 and net10.0.

### Changed
- Documentation overhaul: README feature catalog refreshed to the full widget set, added quick links, a "How it compares" section, an ergonomic Quick Start, and an accurate "Project status"; the Building Terminal Apps guide gained a "Data, navigation, and visuals" widget gallery and a corrected diagnostics snippet; stale coverage/conformance reports annotated with the current state; internal design docs (`POSSIBLE_IMPROVEMENTS.md`, `TUIKIT.md`, `TUIKIT_PLAN.md`) moved to `archive/`.

## [0.1.0] - 2026-07-29

First public preview.

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
- **Ergonomics layer** (from `archive/POSSIBLE_IMPROVEMENTS.md`): `Pane` now implements `IWidget`, and `TuiApplication.Bind(regionId, IWidget)` binds any widget to a region (10.1). App verbs `Bind(chord, action)`, `Quit`, `Notify`, and the `AddPane`/`AddWidget`/`AddRegion` helpers (10.9); the `TuiApp.RunAsync` one-call bootstrap (10.10). Inline markup: `Markup.Parse` and `Pane.WriteMarkup` for `[bold red]…[/]` styling (10.4). Expanded theme roles — success/warning/error/info/selection/disabled — and `StyledText.Style` (10.16). A `Layout.Row`/`Layout.Column` split DSL with `LayoutSlot` (10.6).
- **New widgets**: `StatusBar` (10.7), `ScrollView` with vertical and horizontal scrollbars (10.2), `Collapsible` (10.13), `TabView` (10.14), `Tree<T>` (10.11), and `FuzzyList` (10.3).
- **Focus, forms, prompts, find** (tranche 2): a global `FocusManager` (10.20); `Form`/`FormField` with tab navigation and validation (10.15); async `ConfirmAsync`/`PromptAsync`/`SelectAsync` prompts (10.5); `Pane` incremental find with match highlighting and `TextEditor.Find`/`ReplaceAll`, surfaced as `Ctrl+F`/`Ctrl+H` bindings (10.21).
- **Mouse-capture toggle for native text selection**: `TuiApplication.MouseCaptureEnabled` / `ToggleMouseCapture()` hand the mouse back to the terminal at runtime, so users can drag-select and copy text with their terminal (to paste into another program) and then resume. The sample app binds it to **F12** and documents it in the help overlay; `Start()` honors the flag and setting it after start emits the enable/disable escape immediately.
- **Terminal image protocols (10.40)**: `SixelEncoder` and `KittyImageEncoder` turn a truecolor pixel grid into raw sixel / kitty escape sequences (palette-quantized + run-length-encoded for sixel; chunked base64 RGB for kitty) for terminals that support graphics, complementing the everywhere-portable `HalfBlockImage`.
- **POSIX signals on `netstandard2.0` (10.31)**: `AppLifecycle.HookPosixSignals` now falls back to a libc `signal()` compatibility shim on `netstandard2.0` (SIGINT/SIGWINCH/SIGTSTP/SIGCONT with per-OS numbers), where `PosixSignalRegistration` is unavailable; .NET 8+ continues to use the safer registration API.
- **Guided-tour live UI**: the example now surfaces the modal system directly — **F1**/**?** help overlay, **Ctrl+G** settings & actions menu (theme, icon mode, notifications, confirmation dialog), **Ctrl+T** theme cycle, **Ctrl+K** confirmation dialog, **Ctrl+N** notification toast — plus a landing page describing them.
- **Test suites renamed** from `TrancheNSuite` to descriptive names (e.g. `RichContentSuite`, `ChartsIconsColorSuite`, `ValueValidationSuite`, `ImageProtocolSuite`).
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
- `archive/POSSIBLE_IMPROVEMENTS.md`: a capability analysis versus the agent CLIs, the awesome-tuis catalog, and the C#/.NET frameworks (Spectre.Console, Terminal.Gui, Consolonia, Terminaux), with a tiered improvement roadmap.

### Changed
- `SplitView.MinRatio`/`MaxRatio` now clamp assignments to `[0.0, 1.0]`, `ResizeStep` rejects values outside `(0.0, 1.0]` with `ArgumentOutOfRangeException`, and the divider clamp is robust to an inverted min/max. Added dedicated value-validation and layout-DSL test suites asserting that every range-constrained input across the widgets (gauges, progress, sparkline, scroll view, split, color picker, braille canvas, tween, timer, cell buffer) and layout primitives (`AxisConstraint`, `LayoutSlot`, `Layout.Column`/`Row`) either clamps or throws, plus more markup, tab, tree, fuzzy-list, and form-input (text field, checkbox, radio group, editor/pane search) edge cases.
- Layout regions default to one cell of interior padding on every side, and `Padding` supports vertical as well as horizontal insets. Content and debug outlines no longer touch region edges.
- Themes now set an explicit background, so switching between dark, light, and high-contrast reverses the whole palette. Pane content composes over the theme background via `CellStyle.Over`.

### Fixed
- Modals (`Modal.ContentPadding`) inset their content from the border; the example's confirmation, palette, settings, and help dialogs now keep a cell of padding on every side.
- `DrawBox` clips an over-long title, and the example help overlay renders into a clipped, sized box so its text no longer spills outside the frame.
- The example transcript no longer stacks blank lines between streamed paragraphs and list items.

[Unreleased]: https://github.com/jchristn/TUIKit/commits/main
