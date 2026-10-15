# TUIKit.Example — Guided Tour & Agent Harness

Running the example launches a **guided tour** of TUIKit: a header names the feature being
shown, the left "Live demo" pane renders the widget itself, and the right "Code" pane shows the
code that builds it. **Tab** cycles focus between the **Live demo** and a full-width **Interactive**
box that echoes whatever you type. While the Live demo is focused, browse features with
**PageUp/PageDown** (or `[` / `]`); the **arrow keys** and **Enter** interact with the focused box,
and **Ctrl+Q** quits. The tour covers markup, banners, charts, progress, tables, trees, tabs, fuzzy
finding, menus, nested splits, the color picker, diffs, the key-binding editor, and images
(half-block / sixel / kitty). Pages 2 to 4 and 6 cover the 1.4 usability features: **Focus you can see**
(pane frames and the tab-strip marker; Tab moves inside the demo), **Keys follow focus** (the bar
hides keys that would type once you Tab into the search field), **Streaming without losing your
place** (a feed that follows unless you scroll away; End returns), and **Clickable rows** (page 6:
inline buttons with `o`/`x` keys; repeats coalesce even though each raise builds a fresh lambda, toasts carry
severity labels, and **Ctrl+O** runs the newest toast's action). Page 5 covers 1.6: **Markdown that
fits** (a model-style reply with a table that squeezes, truncates, and stacks as you change the border,
width, cell overflow, and fallback with `b`, `w`, `o`, and `f`). Pages 7 and 8 cover 1.5: **Framed
screens** (a `FramedStack` of channels, transcript, and composer with whole focused frames, left titles,
a composer whose hints keep `/` and show `F1` for help while typing, a status bar with a reserved hint
and right text, and an **F2** drawer that takes focus) and **Tab strip** (a `TabStrip` with bracket
markers above content the app routes itself). The last three pages cover 1.8: **Text fields that log in** (a sign-in form whose
remembered user arrives selected, a masked token with **Ctrl+R** reveal, validation shown on **Enter**, and
**F5** to **F9** switching line-editing keys, the reveal key, the length limit, and the validation mode),
**Pickers** (a `FuzzyList` with detail text, a disabled package, checkboxes on **F7**, and a "Create <query>"
provider on **F8**), and **Server-backed tables** (a `DataTable` over a simulated 1,000-row server with a 300 ms
delay: `<` and `>` page, `f` fails the next load, **F5** retries, Space marks rows across pages, `c` opens the
column chooser, `k` turns on the column cursor with `s` and `S` to sort, `z` cycles the page size, and `m`
switches to local paging). While a text-entry page is focused it gets first refusal on the keys it would type,
so `?` or Ctrl+K reach the field, and a paste goes to it. The last page covers 1.9: **Forms and flows** (a settings `Form` with sections, a
hint, labels beside the fields, errors under each field, **Ctrl+S** and **Esc**, Save and Cancel buttons, Up/Down
between fields, and snapshot dirty tracking, so `* unsaved` goes away when you put a value back; **F5** to **F9**
switch label placement, error display, validation mode, dirty tracking, and arrow keys, and **F4** opens a
three-step `WizardModal` whose Repository step appears only when "Import an existing repository" is checked). The last two pages cover 1.10: **Diffs from git** (a bundled three-file patch through
`DiffView.FromUnifiedDiff`, with `]` and `[` between files, `/` to search and F3 for the next match, `n` for line
numbers, `p` for viewport paging, and `t` for theme styles) and **Activity calendar** (a `CalendarHeatMap` over a
year of generated commits: arrows move by day and week, Enter opens a day, `m` starts weeks on Monday, `w` cycles
weekday labels, `g` switches to ASCII glyphs, and `t` follows the theme). `--diff-once` and `--calendar-once`
print the same views without a terminal.

Global keys open live UI on top of the tour:

- **F1** — help overlay
- **Ctrl+G** — settings & actions menu, including **Edit tags** (a `ListEditorModal<string>` tag editor
  with a legend and validating parser, [`ListEditorExample.cs`](ListEditorExample.cs)) and **Select
  files** (a `FileSelectModal` cascading folder/file selector, [`FileSelectExample.cs`](FileSelectExample.cs))
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

A fourth demo, the **focus showcase** (`--focus`, [`FocusShowcase.cs`](FocusShowcase.cs)), is a
small app built from real regions: the focused region draws a heavy frame, the Details tab view's
strip is its own focus stop, the footer lists the keys of whatever has focus, the Log tab follows new
output, the Activity rows have clickable inline buttons, and **Ctrl+S** shows a "Saved" toast that
coalesces with a count when repeated. `--focus-audit-once` runs `FocusAudit` over it on a backend that
does not claim the terminal (`ClaimsTerminal = false`, so it could run beside another app) and exits 1
if it finds a problem. Frame titles are left-aligned and toasts show severity labels below a one-row
top offset.

## Running it

```bash
# Guided tour, live and interactive (needs a real terminal)
dotnet run --project src/TUIKit.Example

# The v0.4.0 interaction-contract demo
dotnet run --project src/TUIKit.Example -- --contract

# The agent-control harness instead of the tour
dotnet run --project src/TUIKit.Example -- --harness

# The focus showcase (visible focus, key hints, tail-follow, inline buttons)
dotnet run --project src/TUIKit.Example -- --focus

# Headless one-frame snapshots printed to stdout (great for CI and screenshots)
dotnet run --project src/TUIKit.Example -- --tour-once --page 3   # a tour page
dotnet run --project src/TUIKit.Example -- --once                 # the harness
dotnet run --project src/TUIKit.Example -- --contract-once        # the interaction-contract demo
dotnet run --project src/TUIKit.Example -- --focus-once           # the focus showcase
dotnet run --project src/TUIKit.Example -- --focus-audit-once     # FocusAudit over the showcase (exit 1 on problems)
dotnet run --project src/TUIKit.Example -- --markdown-once --width 60   # the Markdown sample at a width
dotnet run --project src/TUIKit.Example -- --markdown-once --width 40 --color | less -R  # keep colors when piped
dotnet run --project src/TUIKit.Example -- --markdown-once --width 70 --color --theme light    # through WithTheme
dotnet run --project src/TUIKit.Example -- --tour-once --page 4   # the "Markdown that fits" page
dotnet run --project src/TUIKit.Example -- --tour-once --page 5   # the "Markdown in your own widgets" page
dotnet run --project src/TUIKit.Example -- --tour-once --keys "$(printf '\033[6~\033[6~\033[6~\033[6~/redis\r')"  # keys, one per frame
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
| Streaming markdown | The transcript streams each reply a few characters at a time into one `Pane.WriteMarkdown` block (headings, bold, bullets, a table, a blockquote, a code fence, and a URL) that reflows when the window is resized; each frame shows `MarkdownRenderer.StreamingPrefix` of what arrived, and the telemetry `reply` row says how many characters are held back |
| Width-aware Markdown (`MarkdownRenderOptions`, `MarkdownTableOptions`, `MarkdownTableGlyphs`, `MarkdownView`) | Tour page "Markdown that fits": `b` cycles border glyphs, `w` the maximum width, `o` the cell overflow, `f` the table fallback, `k` a key-column style; `--markdown-once --width N` prints the sample |
| Interactive `MarkdownView` (keys, `UseThemeStyles`, search, `TailFollow`) | Tour page "Markdown that fits": Up/Down and Home/End scroll, `t` toggles theme styles (cycle themes with `Ctrl+T`; High Contrast turns the table ASCII), `/` opens a search prompt, `F3`/`Shift+F3` step through matches, `Esc` clears, `l` appends a section every second with tail-follow on |
| `MarkdownRenderOptions.WithTheme` | `--markdown-once --theme dark\|light\|contrast` lays the sample out over the theme background with no stripes |
| `MarkdownLayoutCache` | Tour page "Markdown in your own widgets": a message list draws every bubble from one cache; `a` adds a message, and the status line shows the layouts held and lookups answered |
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
| Visible focus (`HighlightFocusedRegion`, `FocusFrame`, `SplitView.ShowPaneFrames`) | `--focus` regions; tour page "Focus you can see" |
| TabView focused tab (`StripFocusStop`) | Details tab view in `--focus`; tour page "Focus you can see" |
| Focus path (`CurrentFocusPath`, `FocusPath`) | Tour page "Focus you can see" prints the live path |
| Focus-aware key hints (`BindKeyHints`, `KeyHintResolver`, `ITextEntry`) | `--focus` footer; tour page "Keys follow focus" |
| Tail-follow (`TailFollow`, `ListView.Append`) | Tour page "Streaming without losing your place"; the Log tab in `--focus` |
| Inline click regions (`ClickRegionMap`, `InlineButton`) | Activity rows in `--focus`; tour page "Clickable rows" |
| Notification coalescing | Repeat **Ctrl+S** in `--focus`, or an action on "Clickable rows" |
| Headless mouse, frame capture, focus audit | `--focus-audit-once`; the test suites drive `FeedClick` and `CaptureFrame` |

If you add a public capability to the library, add a row here and wire it into the app.
