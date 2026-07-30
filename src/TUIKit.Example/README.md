# TUIKit.Example — Guided Tour & Agent Harness

Running the example launches a **guided tour** of TUIKit: a header names the feature being
shown, the left "Live demo" pane renders the widget itself, and the right "Code" pane shows the
code that builds it. **Tab** cycles focus between the **Live demo** and a full-width **Interactive**
box that echoes whatever you type. While the Live demo is focused, browse features with
**PageUp/PageDown** (or `[` / `]`); the **arrow keys** and **Enter** interact with the focused box,
and **Ctrl+Q** quits. The tour covers markup, banners, charts, progress, tables, trees, tabs, fuzzy
finding, menus, nested splits, the color picker, diffs, the key-binding editor, and images
(half-block / sixel / kitty).

Global keys open live UI on top of the tour:

- **F1** — help overlay
- **Ctrl+G** — settings & actions menu (cycle theme, cycle icon mode, notification, confirmation dialog)
- **Ctrl+T** — cycle theme (dark / light / high-contrast)
- **Ctrl+K** — confirmation dialog
- **Ctrl+N** — notification toast
- **F12** — toggle mouse capture. While off, the terminal's native click-drag selection works, so you
  can select text and copy it (Ctrl+C / right-click / Shift-drag, depending on the terminal) to paste
  into another program. Press F12 again to resume interacting with widgets.

A second demo — the original **agent-control harness** (streaming transcript, tool panel,
telemetry, composer, palette, modals, notifications, links, theming, diagnostics against a
*simulated* agent) — is available with `--harness`.

A third demo — the **v0.4.0 interaction-contract demo** (`--contract`) — is the shortest illustration
of the host-owned interaction contract. In ~120 lines ([`ContractDemo.cs`](ContractDemo.cs)) it builds
a four-way dock shell (header, sidebar, editor, footer) from real regions, joins the sidebar list and
the editor into a host focus ring, and wires:

- **Tab / Shift+Tab** to cycle focus, and **click-to-focus** — click any pane to focus it.
- A **focus-scoped `Enter`** that opens the selected file while the sidebar is focused, while `Enter`
  in the editor still inserts a newline (the precedence chain in action).
- **Ctrl+O** — a typed picker modal (`ShowAsync<int>`) whose result is applied on the loop with `Post`.
- **Ctrl+K Ctrl+T** — a two-key theme chord (the syntax the old `Bind` could not parse).
- **Ctrl+Q** — quit.

## Running it

```bash
# Guided tour, live and interactive (needs a real terminal)
dotnet run --project src/TUIKit.Example

# The v0.4.0 interaction-contract demo
dotnet run --project src/TUIKit.Example -- --contract

# The agent-control harness instead of the tour
dotnet run --project src/TUIKit.Example -- --harness

# Headless one-frame snapshots printed to stdout (great for CI and screenshots)
dotnet run --project src/TUIKit.Example -- --tour-once --page 3   # a tour page
dotnet run --project src/TUIKit.Example -- --once                 # the harness
dotnet run --project src/TUIKit.Example -- --contract-once        # the interaction-contract demo
```

In the harness, press **F1** or **?** for the built-in keybinding help — the demo documents itself.

## Keybindings

| Key | Action |
|---|---|
| `Enter` | Send the composer message |
| `Alt+Enter` | Insert a newline in the composer |
| `PageUp` / `PageDown` | Scroll the transcript (smart scroll lock detaches / re-attaches) |
| `Ctrl+P` | Command palette (a list widget in a modal) |
| `Ctrl+G` | Settings form (radio group, checkbox, text field, tab order) |
| `Ctrl+K Ctrl+T` | Cycle theme (a multi-key chord) |
| `Ctrl+D` | Toggle the debug overlay (region rects + frame timing) |
| `Ctrl+L` | Tool-call confirmation modal |
| `Ctrl+C` | Double-tap to exit (configurable Ctrl+C policy) |
| `Ctrl+Q` | Quit |
| Mouse wheel | Scroll the pane under the cursor |
| Click `[docs]` | Activate the header virtual link |

## Capability coverage matrix

Every marquee capability of the library maps to something concrete in this app. This is the "no undemonstrated feature" contract from the plan.

| Library capability | Where it shows up here |
|---|---|
| Developer-defined region layout, mixed resize rules | Six regions (header, transcript, tools, telemetry, composer, footer): fixed, right-anchored, stretch, and proportional constraints combined in `HarnessApp` |
| "Terminal too small" block screen | Shrink the window below the layout minimum; `TuiApplication` shows the block screen and resumes on grow |
| Double-buffered diff rendering | The whole frame is composed and diffed each tick by `TerminalRenderer` |
| Thread-safe pane writes | `SimulatedAgent` streams into the transcript and tool panes from a background thread |
| Streaming markdown | The transcript renders headings, bold, bullets, blockquotes, code fences, and a URL from `MarkdownRenderer` |
| Mutable line handles | The tool call line updates `running… → done (0.9s)` in place via its `PaneLineHandle` |
| Smart scroll lock | `PageUp` detaches the transcript; the footer shows `detached N new`; `PageDown`/bottom re-attaches |
| Styled-text builder | `Text.From("you").Green().Bold()` for message authorship |
| Unicode width + graphemes | Box-drawing, braille spinner, block glyphs, and the URL all measure and render at correct widths |
| Multi-line editor | The composer (`TextEditor`) with caret movement, undo/redo, and kill/yank |
| Command routing table + scopes | `Ctrl+P/G/D/L/Q`, `F1`, `?` registered as bindings, dispatched by the router |
| Multi-key chords | `Ctrl+K Ctrl+T` cycles the theme via a registered sequence |
| Configurable Ctrl+C policy | Set to `DoubleTapToExit`; a toast prompts "press again" |
| Bracketed paste | Pasted text is inserted into the composer, never interpreted as commands |
| Mouse: wheel, click, hit-testing | Wheel scrolls the pane; clicking the header `[docs]` link fires a virtual handler |
| Virtual links + allowlist | The header link is registered per-frame in a `LinkRegistry`; auto-linkify uses `LinkScanner`'s scheme allowlist |
| Selection + OSC 52 | `Selection` + `ClipboardWriter` back copy (wired for the composer/transcript) |
| Modal stack + async result | `Ctrl+L` confirmation and `Ctrl+G` settings await their result via `Modal.Completion` |
| Full widget kit in a modal | Settings modal hosts a radio group, checkbox, and text field with tab order |
| Notifications | Tool completion, theme change, and message-sent toasts (auto-timeout, severity color, no focus steal) |
| Theming + ASCII fallback | `Ctrl+K Ctrl+T` cycles Dark / Light / High-contrast; high-contrast switches borders to ASCII |
| Gauge, sparkline, progress, table | The telemetry region renders all four, updated live from `HarnessState` |
| Debug overlay | `Ctrl+D` outlines every region and shows frame timing from `FrameStats` |
| Lifecycle + restoration | `TuiApplication.Start/Stop` enter/leave the alternate screen and restore the terminal |
| Non-TTY degradation | Piping the app produces plain line output with no escape sequences |
| Headless rendering | `--once` renders a frame to a `CellBuffer` and prints it with `Snapshot.ToText` |
| Input record/replay | `InputRecording` can replay a captured session into a headless backend |

If you add a public capability to the library, add a row here and wire it into the app.
