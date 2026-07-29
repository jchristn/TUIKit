<div align="center">
  <img src="assets/logo.png" alt="TUIKit" width="120" height="120" />
</div>

# Building Terminal Apps with TUIKit

This is the top-to-bottom guide to building terminal user interfaces with TUIKit. It covers every capability in the library, with runnable examples, and finishes with a worked real-world app. If you have used Spectre.Console or Terminal.Gui, the mental model here is different in one important way: **TUIKit is concurrency-first** — any thread can write to any pane while a background render loop paints — so it fits streaming, agent, and dashboard apps naturally.

> TUIKit is v0.1.0 (alpha). The API is stabilizing; pin your version.

## Contents

1. [Install and hello world](#1-install-and-hello-world)
2. [The mental model](#2-the-mental-model)
3. [Layout: regions, constraints, padding, borders](#3-layout)
4. [Panes: streaming text, styling, markdown, mutable lines](#4-panes)
5. [Styled text and inline markup](#5-styled-text-and-inline-markup)
6. [Input: keys, chords, commands, mouse, paste](#6-input)
7. [Widgets](#7-widgets)
8. [Modals and notifications](#8-modals-and-notifications)
9. [Theming](#9-theming)
10. [Diagnostics](#10-diagnostics)
11. [Testing your UI headlessly](#11-testing-your-ui-headlessly)
12. [Cross-platform, SSH, and tmux](#12-cross-platform-ssh-and-tmux)
13. [Worked example: a live log dashboard](#13-worked-example-a-live-log-dashboard)

---

## 1. Install and hello world

```bash
dotnet add package TUIKit
```

The shortest useful program uses the one-call host. `TuiApp.RunAsync` creates a console backend, applies sensible defaults (Ctrl+C quits), runs your configuration, and cleans up on exit.

```csharp
using TUIKit.Hosting;

await TuiApp.RunAsync(app =>
{
    var log = app.AddPane("log");     // a full-screen pane
    app.Bind("ctrl+q", app.Quit);     // one-step keybinding
    log.WriteLine("Hello, terminal. Press Ctrl+Q to quit.");
});
```

`app.AddPane` creates a pane, adds a region for it, and binds it in one call. `app.Bind` maps a chord straight to an action. That is the entire ceremony.

---

## 2. The mental model

TUIKit is a stack of layers, each usable on its own:

- **Backend** (`ITerminalBackend`) — the raw byte sink/source. `ConsoleBackend` drives a real terminal; `HeadlessBackend` captures everything in memory for tests.
- **Renderer** — a double-buffered diff that emits only the cells that changed.
- **Layout** — a set of named *regions*, each a rectangle with its own resize rule, padding, and optional border.
- **Content** — *panes* (thread-safe scrolling text) and *widgets* (gauges, tables, trees, …), each bound to a region.
- **Host** (`TuiApplication`) — owns the render and input loops, command routing, modals, notifications, and terminal restoration.

You describe a layout, bind content to its regions, register keybindings, and run. Content updates from any thread; the host repaints.

`TuiApplication` is the full API; `TuiApp.RunAsync` is a convenience over it. Use the class directly when you need a custom backend or your own loop:

```csharp
using ConsoleBackend backend = new ConsoleBackend();
using TuiApplication app = new TuiApplication(backend);
// ... configure ...
await app.RunAsync(CancellationToken.None);
```

The terminal is a singleton resource — only one `TuiApplication` runs at a time.

---

## 3. Layout

A layout is any number of **regions**. Each region resolves to a rectangle from its horizontal and vertical constraints for the current terminal size, so the UI reflows when the window changes.

### The split/grid DSL (easiest)

For the common shapes, use `Layout.Row` and `Layout.Column` with fixed and fill slots:

```csharp
using TUIKit.Layout;

// A header and footer bar around a filling body:
app.Layout = Layout.Column(
    LayoutSlot.Fixed("header", 1),
    LayoutSlot.Fill("body"),
    LayoutSlot.Fixed("footer", 1));

// A fixed sidebar beside a filling main area:
app.Layout = Layout.Row(LayoutSlot.Fixed("sidebar", 30), LayoutSlot.Fill("main"));
```

Fill slots share the remaining space equally. Fixed slots take their exact cell count.

### The constraint builder (full control)

For anything the DSL doesn't express, build regions directly. Each region has an independent horizontal and vertical `AxisConstraint`:

```csharp
app.Layout = Layout.Create()
    .Add("transcript", r => r.Horizontal(AxisConstraint.Stretch(0, 34))   // fill, leaving 34 on the right
                             .Vertical(AxisConstraint.Stretch(1, 4)))       // fill, leaving 1 top / 4 bottom
    .Add("tools", r => r.RightAnchored(0, 33).TopAnchored(1, 12))
    .Add("composer", r => r.FillWidth().BottomAnchored(0, 4))
    .Build();
```

Convenience methods cover the common anchors: `FillWidth`, `FillHeight`, `LeftAnchored(offset, width)`, `RightAnchored(offset, width)`, `TopAnchored`, `BottomAnchored`, `ProportionalWidth`, `ProportionalHeight`.

### Padding and borders

Every region keeps one cell of interior padding by default, and can declare a border. Content is inset for both automatically:

```csharp
.Add("tools", r => r.RightAnchored(0, 33).TopAnchored(1, 12)
                    .WithBorder(BorderStyle.Rounded, "Tools")   // titled rounded border
                    .WithPadding(1))                            // one cell inside the border
```

`BorderStyle` is `None`, `Ascii`, `Line`, `Rounded`, `Double`, or `Thick`. The host draws it, and honors the theme's ASCII fallback on terminals without box-drawing glyphs.

### Too-small terminals

When the terminal is smaller than the layout's derived minimum, the host shows a "terminal too small" screen and resumes automatically when the window grows.

---

## 4. Panes

A **pane** is a persistent, thread-safe, scrolling region of text. Any thread may write to it; writes are ordered first-in-first-out per pane.

```csharp
Pane log = app.AddPane("log");

// From a background thread — no marshaling needed:
_ = Task.Run(async () =>
{
    for (int i = 0; i < 1000; i++)
    {
        log.WriteLine($"event {i}");
        await Task.Delay(20);
    }
});
```

### Mutable lines

`WriteLine` returns a handle you can update in place — a running task becomes done, a progress note advances:

```csharp
PaneLineHandle line = log.WriteLine("running…");
// later, from any thread:
line.Update("done (1.2s)");
```

### Partial-line streaming

`Write` (no newline) appends to the current line, so token streams render as they arrive:

```csharp
log.Write("The ");
log.Write("answer ");
log.WriteLine("is 42.");
```

### Smart scroll lock

Scrolling up detaches the viewport from the live tail; returning to the bottom re-attaches. The pane tracks this for you:

```csharp
log.ScrollUp(5);
bool live = log.IsAtBottom;         // false while detached
int missed = log.NewSinceDetached;  // "↓ N new" indicator
log.ScrollToBottom();               // re-attach
```

### Scrollback and batching

```csharp
log.ScrollbackCapacityLines = 10_000;   // 0 = unbounded
using (log.BeginBatch())                // renders as one frame
{
    log.WriteLine("part 1");
    log.WriteLine("part 2");
}
```

---

## 5. Styled text and inline markup

Two ways to produce styled text. The fluent builder is precise:

```csharp
using TUIKit;

log.WriteLine(Text.From("done").Green().Bold().Append(Text.From("  1.2s").Dim()));
log.WriteLine(Text.From("critical").Style(app.Theme.Error));   // apply a semantic theme role
```

Inline markup is terser and can live in strings or config:

```csharp
log.WriteMarkup("[green bold]done[/] [dim]1.2s[/]");
log.WriteMarkup("[yellow]warning:[/] disk at [#ff8800]90%[/]");
StyledText t = Markup.Parse("[red on white] ERROR [/]");
```

Tags are attributes (`bold`, `dim`, `italic`, `underline`, `strike`, `reverse`), a foreground color, and `on <color>` for background. Colors are named (`red`…`white`, `gray`), `#RRGGBB` hex, or a palette index. Tags nest and close with `[/]`. Write a literal bracket as `[[` or `]]`.

Unicode is handled correctly throughout — CJK double-width, combining marks, and emoji grapheme clusters measure and render properly.

### Markdown

Agent and doc output often arrives as Markdown. Render it to styled lines:

```csharp
using TUIKit.Content;

foreach (StyledText line in MarkdownRenderer.Render("# Title\n\n- **bold** item\n\n> a quote"))
    log.WriteLine(line);
```

Headings, emphasis, inline code, lists, block quotes, fenced code, and rules are supported. Incoming ANSI escape sequences from subprocesses are stripped on ingest via `AnsiStripper.Strip`.

---

## 6. Input

### Keybindings and commands

The simplest binding maps a chord to an action:

```csharp
app.Bind("ctrl+p", OpenPalette);
app.Bind("ctrl+k ctrl+t", CycleTheme);   // multi-key chord
```

For config-driven rebinding, use the two-step command route: register a chord against a command *id*, and the id against a handler. Bindings have scope (global or focus-context) and a conflict policy:

```csharp
app.Commands.Register(KeyChord.Parse("ctrl+s"), "file.save");
app.Commands.Register(KeyChord.Parse("ctrl+s"), "editor.snippet", CommandScope.FocusContext, "editor");
app.RegisterCommand("file.save", Save);
```

A focus-context binding overrides a global one for the same chord, which is how a focused editor claims a shortcut.

### Ctrl+C policy

`Ctrl+C` behavior is your choice:

```csharp
app.CtrlCPolicy = CtrlCPolicy.Kill;                 // quit (default)
app.CtrlCPolicy = CtrlCPolicy.DoubleTapToExit;      // press twice
app.CtrlCPolicy = CtrlCPolicy.InterruptFocusedPane; // raise Interrupted, don't quit
app.CtrlCPolicy = CtrlCPolicy.Custom;               // deliver as an ordinary key
```

### Text input, paste, and mouse

Keys not consumed by a modal or a command fall through so you can drive a focused input:

```csharp
app.KeyReceived  += key   => editor.HandleKey(key);
app.PasteReceived += text => editor.InsertText(text);   // bracketed paste, never interpreted as commands
app.MouseReceived += m =>
{
    if (m.Button == MouseButton.WheelUp)   log.ScrollUp(3);
    if (m.Kind == MouseEventKind.Press)    Focus(m.X, m.Y);
};
```

Mouse decoding is SGR (1006), so coordinates beyond column 223 work. Double/triple clicks are synthesized from timing via `ClickSynthesizer`.

### Links, selection, clipboard

```csharp
var links = new LinkRegistry();
Link link = links.Add("docs", new Rect(10, 0, 6, 1), "https://example.com");
Link? hit = links.HitTest(mouse.X, mouse.Y);   // fire on click

var scanner = new LinkScanner();                 // auto-linkify only http/https/mailto
IReadOnlyList<LinkMatch> urls = scanner.Scan(streamedText);

string osc52 = ClipboardWriter.BuildSequence("copied");  // clipboard over SSH
backend.Write(osc52);
```

---

## 7. Widgets

Every widget implements `IWidget` and binds to a region with `app.Bind(regionId, widget)` (or `app.AddWidget`). Panes are widgets too.

### Text and status

```csharp
app.Bind("title", new Label(Text.From("Dashboard").Bold()));
app.Bind("footer", new StatusBar().Add("^P", "palette").Add("^Q", "quit"));
```

### Meters and charts

```csharp
var cpu = new Gauge { Value = 0.62 };                       // horizontal bar
var spark = new Sparkline(); spark.Push(0.6, capacity: 60); // live series
var progress = new ProgressBar { Value = 0.4 };             // percent bar
var spinner = new Spinner();                                // animate: spinner.Advance()
```

### Lists, tables, trees

```csharp
var list = new ListView(); list.SetItems(new[] { "one", "two", "three" });

var table = new Table(new[] { "Metric", "Value" });
table.AddRow(new[] { "cpu", "62%" });

var tree = new Tree<DirectoryInfo>(root, d => d.GetDirectories(), d => d.Name);
tree.Expand(root);

var picker = new FuzzyList(fileNames);   // type to filter with fuzzy matching
picker.Query = "recmd";
string? chosen = picker.SelectedItem;
```

Forward keys to a focused widget so it can navigate:

```csharp
app.KeyReceived += key => list.HandleKey(key);   // Up/Down, etc.
```

### Containers

```csharp
var scroller = new ScrollView(bigWidget, contentWidth: 120, contentHeight: 400);
scroller.ScrollBy(0, 10);                          // scrollbars appear automatically

var tabs = new TabView().Add("Logs", logsPane).Add("Stats", statsWidget);

var section = new Collapsible("Read src/foo.cs", detailWidget) { Expanded = false };
```

### Form inputs

```csharp
var editor = new TextEditor();                     // multi-line, undo/redo, kill-ring
var field  = new TextField { Value = "name" };     // single line
var check  = new Checkbox("verbose", isChecked: true);
var radio  = new RadioGroup(new[] { "Dark", "Light", "High-contrast" });
```

---

## 8. Modals and notifications

Modals trap focus and stack; background panes keep updating behind them.

```csharp
using TUIKit.Modals;

var confirm = new MessageModal("Allow tool call?", "Run `rm -rf build/`?", new[] { "Allow", "Deny" });
app.Modals.Push(confirm);
object? result = await confirm.Completion;   // 0 = Allow, 1 = Deny, -1 = Escape
```

Modals honor `ContentPadding` so text never touches the frame. A modal can refuse to close (unsaved changes) by overriding `CanClose`.

Toasts never steal focus, stack, expire on a timeout, and carry a severity color:

```csharp
app.Notify("saved", NotificationSeverity.Success);
app.Notify("disk low", NotificationSeverity.Warning, timeoutMilliseconds: 5000);
```

---

## 9. Theming

A theme is a set of styles plus semantic roles. Switch it at runtime and the whole UI restyles.

```csharp
using TUIKit.Theming;

app.Theme = Theme.Light;          // Dark, Light, HighContrast built in
app.Theme = Theme.HighContrast;   // also switches borders to ASCII

// Semantic roles:
log.WriteLine(Text.From("ok").Style(app.Theme.Success));
log.WriteLine(Text.From("fail").Style(app.Theme.Error));
// Text, Accent, Border, Muted, Success, Warning, Error, Info, Selection, Disabled

// Custom named styles:
app.Theme.SetStyle("banner", CellStyle.Default.WithForeground(Color.FromRgb(0x4F, 0xC1, 0xE9)));
```

Themes carry an explicit background, so light and dark genuinely reverse the palette; pane content composes over the theme background via `CellStyle.Over`.

---

## 10. Diagnostics

```csharp
using TUIKit.Diagnostics;

var stats = new FrameStats();
stats.Record(frameMs);                 // avg, last, FPS

app.RenderOverlay = surface => {
    if (showDebug) DebugOverlay.Render(surface, app.Layout, stats);  // region outlines + timing
};

var recording = new InputRecording();  // record/replay a session
recording.Add(bytes, delayMs: 50);
recording.Replay(headlessBackend);
```

---

## 11. Testing your UI headlessly

TUIKit renders into an in-memory buffer, so you can assert your UI as text — the same way TUIKit tests itself. Drive a `HeadlessBackend`, feed input, and snapshot a frame.

```csharp
using TUIKit.Terminal;
using TUIKit.Testing;

var backend = new HeadlessBackend(40, 10);
using var app = new TuiApplication(backend);
var log = app.AddPane("log");
log.WriteMarkup("[green]ready[/]");

app.Start();
backend.FeedInput("");   // Ctrl+Q
app.PumpInputOnce();           // dispatch input deterministically
app.RenderOnce();              // compose one frame

string frame = backend.TakeOutput();       // raw escape stream
// or snapshot a widget directly:
string text = Snapshot.RenderWidget(new Gauge { Value = 1 }, 4, 1);   // "████"
```

`PumpInputOnce`/`RenderOnce` let a test advance the app one step at a time without a real terminal or a running loop.

---

## 12. Cross-platform, SSH, and tmux

TUIKit is designed to run the same everywhere:

- **Frameworks.** It multi-targets `netstandard2.0`, `net8.0`, and `net10.0`, so it runs on modern .NET, .NET Framework, Mono, and Unity. The Unicode width and grapheme tables are bundled, so behavior does not depend on the host runtime's Unicode version.
- **Windows / Linux / macOS.** `ConsoleBackend` enables virtual terminal processing through `SetConsoleMode` on Windows and puts the terminal in raw mode with `stty` on Unix (Linux and macOS). Output is UTF-8. Nothing platform-specific leaks into your app code.
- **Capability detection.** The backend detects truecolor, enhanced keyboard, SGR mouse, OSC 8, and OSC 52 from the environment. Truecolor degrades to 256/16 colors automatically where needed; borders fall back to ASCII under a high-contrast theme.
- **SSH.** Everything is standard VT — SGR mouse, bracketed paste, and OSC 52 clipboard all work over a remote session (OSC 52 is specifically why copy works remotely).
- **tmux.** TUIKit uses SGR mouse mode (1006) and standard sequences that tmux forwards. For enhanced keys inside tmux, enable `set -g extended-keys on` in your tmux config.
- **Not a TTY.** When stdout is redirected or piped (CI, a file), the host degrades to plain line output and emits no escape sequences, so your program stays useful in a pipeline.

A practical portability rule: build and assert your UI against `HeadlessBackend` in CI (fully portable), and smoke-test the live `ConsoleBackend` on the terminals you care about.

---

## 13. Worked example: a live log dashboard

A small but complete app: a titled log on the left, live telemetry on the right, a status bar along the bottom, `Ctrl+L` to clear, `Ctrl+Q` to quit, and a background thread streaming data — everything above, together.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using TUIKit;
using TUIKit.Hosting;
using TUIKit.Layout;
using TUIKit.Modals;
using TUIKit.Widgets;

await TuiApp.RunAsync(app =>
{
    // Layout: a bordered log on the left, telemetry on the right, a status bar along the bottom.
    app.Layout = Layout.Create()
        .Add("log",   r => r.FillWidth(0, 34).FillHeight(0, 1).WithBorder(BorderStyle.Rounded, "Log"))
        .Add("stats", r => r.RightAnchored(0, 33).FillHeight(0, 1).WithBorder(BorderStyle.Rounded, "Telemetry"))
        .Add("status", r => r.FillWidth().BottomAnchored(0, 1))
        .Build();

    var log = new Pane("log");
    app.Bind("log", log);

    var cpu = new Gauge { Value = 0 };
    app.Bind("stats", cpu);

    app.Bind("status", new StatusBar().Add("^L", "clear").Add("^Q", "quit"));

    app.Bind("ctrl+l", () => { log.Clear(); app.Notify("cleared", NotificationSeverity.Info); });
    app.Bind("ctrl+q", app.Quit);

    // Stream from a background thread — the point of the concurrency-first design.
    var random = new Random();
    _ = Task.Run(async () =>
    {
        int n = 0;
        while (true)
        {
            double load = random.NextDouble();
            cpu.Value = load;
            log.WriteMarkup($"[dim]{n:0000}[/] request  [green]200[/]  [dim]{load * 100:00}ms[/]");
            if (load > 0.9)
                app.Notify("high load", NotificationSeverity.Warning, 2000);
            n++;
            await Task.Delay(150);
        }
    });
});
```

That is a real, running dashboard in well under a hundred lines: layout, borders, a bound pane and widget, a status bar, keybindings, notifications, markup, and a background producer streaming into the UI while the host paints.

From here, swap the `Gauge` for a `Table` of per-endpoint stats, add a `TabView` to switch views, drop a `FuzzyList` behind `Ctrl+P` as a command palette, or wrap a tall report in a `ScrollView`. Every piece composes the same way: describe a region, bind a widget, wire a key.

---

## 14. Implementation status

This library is under active development. The table below tracks which capabilities from `POSSIBLE_IMPROVEMENTS.md` are implemented, planned, or deliberately excluded. It is kept current as work lands.

| # | Capability | Status |
|---|---|---|
| 10.1 | Bind any widget to a region (`Pane : IWidget`) | **Implemented** |
| 10.2 | Scroll view + scrollbars (vertical & horizontal) | **Implemented** |
| 10.3 | Fuzzy finder / filterable list | **Implemented** |
| 10.4 | Inline markup parser | **Implemented** |
| 10.5 | Async prompts (`ConfirmAsync`/`PromptAsync`/`SelectAsync`) | **Implemented** |
| 10.6 | Split / grid layout DSL | **Implemented** |
| 10.7 | Status / hint bar widget | **Implemented** |
| 10.8 | Docs, cookbook & templates | **Partial** (this guide; `dotnet new` templates planned) |
| 10.9 | One-step `Bind(chord, action)` + app verbs | **Implemented** |
| 10.10 | One-call bootstrap (`TuiApp.RunAsync`) | **Implemented** |
| 10.11 | Tree / hierarchical list | **Implemented** |
| 10.12 | Diff renderer (`DiffView`, LCS line diff) | **Implemented** |
| 10.13 | Collapsible section widget | **Implemented** |
| 10.14 | Tabs widget | **Implemented** |
| 10.15 | Forms / dialog framework | **Implemented** |
| 10.16 | Expanded theme role vocabulary | **Implemented** |
| 10.17 | Real data table (sortable/virtualized) (`DataTable<T>`) | **Implemented** |
| 10.18 | Syntax highlighting (`SyntaxHighlighter`) | **Implemented** |
| 10.19 | Autocomplete / typeahead popup | **Excluded** (by request) |
| 10.20 | Global focus manager | **Implemented** |
| 10.21 | Find (`Ctrl+F`) / find-replace (`Ctrl+H`) | **Implemented** (`Pane.FindNext`, `TextEditor.Find`/`ReplaceAll`) |
| 10.22 | Nested layouts inside a region (`SplitView`/`SurfaceView`) | **Implemented** |
| 10.23 | Widget-driver test harness (`WidgetTester`) | **Implemented** |
| 10.24 | Multi-task progress (`MultiProgress`/`ProgressTask`) | **Implemented** |
| 10.25 | Menus / menu bar / drop-downs (`MenuBar`/`Menu`/`MenuItem`) | **Implemented** |
| 10.26 | Nerd Font / icon glyphs + ASCII fallback (`Icons`/`Icon`/`IconMode`) | **Implemented** |
| 10.27 | Charts — braille canvas + line/bar (`BrailleCanvas`/`LineChart`/`BarChart`) | **Implemented** |
| 10.28 | Interactive split resize (`SplitView` arrow-key resize) | **Implemented** |
| 10.29 | Markdown completeness (task/ordered lists, tables, nesting) | **Implemented** |
| 10.30 | Data binding / reactive layer (`Observable<T>`) | **Implemented** |
| 10.31 | Suspend/resume + signal restoration | Planned |
| 10.32 | Modal-editing helper | Planned |
| 10.33 | Animation / transitions / timers (`Easing`/`Tween`/`FrameTimer`) | **Implemented** |
| 10.34 | OSC 8 emission + clipboard read + link hints | Planned |
| 10.35 | File browser / open dialog widget (`FileBrowser`) | **Implemented** |
| 10.36 | FIGlet / banner text (`Banner`/`BannerText`) | **Implemented** |
| 10.37 | Color picker widget (`ColorPicker`) | **Implemented** |
| 10.38 | Box shadows / modal drop shadows (`ISurface.DrawShadow`) | **Implemented** |
| 10.39 | Backdrop dimming behind modals (`Backdrop.Dim`) | **Implemented** |
| 10.40 | Image rendering — half-block (`HalfBlockImage`) | **Implemented** (half-block; sixel/kitty not attempted) |
| — | Guided-tour example rewrite | Planned |
| — | Keybinding editor modal | Planned |

Everything already shipped (core engine, layout, panes, input, mouse/links, modals, notifications, theming, diagnostics, headless testing) is documented in the sections above.
