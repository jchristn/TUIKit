\# TUIKit — Requirements, Round 3



A high-performance, concurrent C# TUI framework for embedding multi-pane layouts into .NET console applications. Primary target: an AI agent control harness with streaming text, live diagnostics, interactive prompts, and overlay dialogs.



\*\*How to use this doc:\*\* Part 0 contains contradictions that need resolving before anything else. Part 1 records what's settled. Part 2 asks the follow-ups implied by those answers. Part 3 is everything still open. Answer inline with `JC>` as before.



\---



\# Part 0 — Conflicts to resolve first



These are places where a round-1 answer collides with how terminals actually behave, or with another answer.



\## 0.1 "Revert to 160×80" isn't something a library can do



There is no portable way to force a terminal back to a size. `CSI 8;h;w t` exists but is widely disabled, and it does nothing at all over SSH or inside tmux where the pty size is set by the parent. If the user drags the window to 100×30, TUIKit gets a 100×30 surface and must decide what to draw in it.



So the real question is what happens to your 160×80 logical surface when the physical surface is smaller:



\* \*\*(a) Clip.\*\* Render the full 160×80 buffer, blit the top-left 100×30 of it. Content outside is simply invisible and unreachable.

\* \*\*(b) Pan/viewport.\*\* Render 160×80, blit a movable 100×30 window onto it, with a key (or mouse drag) to pan. Nothing is unreachable, but the user is peering through a letterbox.

\* \*\*(c) Block.\*\* Take over the whole surface with a "Terminal too small — need 160×80, have 100×30" message. Resume normal rendering when the terminal grows back. This is what most TUIs do, and it's the least surprising.

\* \*\*(d) Reflow.\*\* Panes shrink below their declared minimums. Contradicts your fixed-size model, listed for completeness.



Also worth attempting regardless of the choice: emit the resize request sequence on startup as a best-effort hint, and don't depend on it.



\*\*Q0.1 — Which of (a)–(d)? Should it be configurable per-app, with one of them as the default?\*\*



JC>



\## 0.2 Is 80 rows really the minimum?



160 columns is unremarkable. 80 \*rows\* is not — a maximized terminal on a 1080p display at a typical font size is roughly 200×55, and a 1440p display gets you to about 240×70. On a laptop you're looking at 45–50 rows. A hard 80-row minimum means a large share of users hit condition 0.1 permanently, on every launch.



\*\*Q0.2 — Is 80 rows a real product requirement, or was it a placeholder? What's the smallest surface the agent harness genuinely needs to be usable on?\*\*



JC>



\## 0.3 Fixed-size panes contradict the original elastic requirement



Your opening description was "the left two rectangles will never scale as the window grows, but the other two can." Round 1 answered "fixed width and height." Those are different systems, and the difference determines whether you need a layout \*solver\* at all.



\* \*\*Everything fixed:\*\* the UI is a fixed-size island. At 200×60 (vs. a 160×48 layout), you have 40 columns and 12 rows of dead space. Where does it go — letterboxed/centered, anchored top-left, or does the last pane absorb it?

\* \*\*Mixed fixed/elastic:\*\* you need per-pane constraints (`Fixed(n)`, `Fill(weight)`, `Min(n)`, `Max(n)`) and a solver that distributes remainder. More work, but it's what makes the harness feel native at any window size.



\*\*Q0.3a — Fixed-only, or fixed + elastic?\*\*



JC>



\*\*Q0.3b — If fixed-only: what fills the leftover space when the terminal exceeds the layout size?\*\*



JC>



\*\*Q0.3c — With an arbitrary number of fixed-size rectangles, how is position expressed?\*\*

\* Absolute placement — each pane declares `(col, row, width, height)` and TUIKit derives the minimum surface as the max extent. Simplest to implement, and "arbitrary number of rectangles" falls out for free. But it means overlapping panes are possible — is that legal (z-order) or an error at build time?

\* Split tree — nested horizontal/vertical splits, each node carrying a fixed size. No overlap possible, composes better, harder to express irregular layouts.



JC>



\*\*Q0.3d — If the minimum surface is derived from the layout rather than declared, does the explicit "160×80" requirement go away entirely?\*\*



JC>



\## 0.4 Multi-targeting — resolved, with residual questions



\*\*Decided:\*\* multi-target rather than a single `netstandard2.0` base. Proposed TFM list: `netstandard2.0;net8.0;net10.0`.



\*\*One correction to what I said last round.\*\* I overstated the `System.Text.Rune` problem. .NET gives you no East Asian Width data on \*any\* target — you have to ship your own wcwidth table regardless of TFM. So ns2.0 costs you convenience there (manual `char.ConvertToUtf32` / surrogate handling instead of `Rune` enumeration), not capability. The costs that are real:



\* \*\*No default interface methods.\*\* Every future addition to `IWidget`/`IRenderer`/`ITerminalBackend` is a breaking change rather than an additive one. This is the one that will actually hurt over a multi-year library lifetime, and it can't be worked around.

\* \*\*Package references:\*\* `System.Memory`, `System.Threading.Channels`, `Microsoft.Bcl.AsyncInterfaces`. Three dependencies on a library you may want dependency-free, and `Span<T>` on ns2.0 is the portable non-intrinsic implementation.

\* \*\*ns2.0 implicitly promises .NET Framework support\*\*, and that's a different terminal backend, not a recompile — VT isn't enabled by default (`SetConsoleMode` P/Invoke required), console encoding behaves differently, and ConPTY needs Windows 10 1809+. Someone \*will\* file bugs against it.



\*\*On the intrinsics rationale specifically:\*\* I don't think performance is a good reason to multi-target here. A 160×80 surface is 12,800 cells; even at 60fps that's under a million cell comparisons per second, which is noise. Your actual bottleneck is the volume of bytes written to stdout and the terminal emulator's own rendering — so the optimization that matters is minimizing emitted escape sequences (coalescing SGR changes, using relative cursor moves, skipping unchanged runs), not vectorizing the diff. Multi-target for \*\*API evolution (DIMs)\*\* and \*\*staying dependency-free on modern TFMs\*\*; treat intrinsics as irrelevant.



\*\*Q0.4a — Is .NET Framework 4.6.1+ a genuine requirement, or incidental to wanting broad reach?\*\* If no real consumer needs it, `netstandard2.1;net8.0;net10.0` gives you DIMs, `Span`, and `IAsyncEnumerable` while still covering .NET Core 3.x, Mono, and Unity. That's a much cheaper middle tier. (Note .NET Core 3.1 is out of support.)



JC>



\*\*Q0.4b — If .NET Framework stays: what's the minimum Windows version?\*\* VT sequences require Windows 10 1607+; ConPTY requires 1809+. Windows 7/8 gets you essentially nothing.



JC>



\*\*Q0.4c — Feature parity across TFMs, or a reduced ns2.0 surface?\*\* Identical public API everywhere (ns2.0 just slower) is much friendlier to consumers who multi-target themselves. A reduced surface means `#if` in \*their\* code.



JC>



\*\*Q0.4d — How much conditional compilation is acceptable?\*\* Recommendation: confine `#if` to a terminal-backend abstraction and a small compat shim, so the layout/render/input logic compiles once. If `#if` starts appearing inside the render loop, you have two codebases.



JC>



\*\*Q0.4e — CI matrix.\*\* Multi-targeting means you now test on .NET Framework/Windows too. Are you set up for that, and which OS × TFM × terminal combinations get automated coverage vs. manual smoke testing?



JC>



\## 0.5 Your platform list conflicts with your protocol and color choices



macOS Terminal.app supports neither. It has no Kitty keyboard protocol support, and <cite index="12-1">it does not support 24-bit color, unlike iTerm2 on the same platform</cite> — <cite index="14-1">its palette tops out at 256 colors</cite>. Selecting "macOS" and "24-bit truecolor" and "enhanced keyboard protocols" together means Terminal.app is excluded, not supported.



Windows is better but recent: <cite index="4-1">Windows Terminal added built-in Kitty keyboard protocol support in Preview 1.25</cite>, which is new enough that you should verify what's shipped in the stable channel today. Windows Terminal also has its own `win32-input-mode`, which gives you the same disambiguation and has been available far longer — likely the more reliable Windows path.



Broad support today: <cite index="5-1">kitty (which originated the protocol), Ghostty, WezTerm, and foot</cite>, plus <cite index="11-1">Alacritty and iTerm2</cite>.



\*\*Q0.5a — Confirm the tier-1 terminal list. Suggested: Windows Terminal, iTerm2, Ghostty, WezTerm, Alacritty, kitty. Excluded/degraded: macOS Terminal.app, conhost, PuTTY.\*\*



JC>



\*\*Q0.5b — On a terminal that can't report enhanced keys, what happens?\*\*

\* Hard-fail at startup with a clear message

\* Run with a degraded keymap, and expose a capability API so the app can register alternate bindings

\* Run degraded silently, exotic bindings just never fire



JC>



\*\*Q0.5c — Does it need to work inside tmux and over SSH?\*\* tmux only forwards the enhanced protocol on recent versions with `extended-keys` configured, and it interferes with mouse reporting. Supporting it is a meaningful chunk of testing.



JC>



\*\*Q0.5d — Color: truecolor only, or auto-degrade to 256/16?\*\* Degrading needs a color-quantization step and detection via `COLORTERM`/`TERM`, which <cite index="18-1">is the conventional method but isn't reliable — Windows Terminal sets neither variable, so applications simply assume truecolor there</cite>.



JC>



\---



\# Part 1 — Settled



| # | Decision |

|---|---|

| 1 | Key routing via a \*\*central command routing table\*\*; bindings declare `Global` vs `FocusContext` scope |

| 2 | \*\*Enhanced keyboard protocols\*\* (CSI u / Kitty), not legacy ANSI |

| 3 | \*\*Thread-safe direct API\*\* — any thread may call `pane.Write`/`WriteLine` |

| 4 | Scrollback cap is a \*\*code-level setting\*\*: capped ring buffer or unbounded |

| 5 | \*\*Smart scroll lock\*\* — user scrolling up detaches the viewport; returning to the bottom re-attaches |

| 6 | \*\*Modal focus trap\*\*, \*\*nested modals\*\* (top wins), plus \*\*transient notifications\*\* |

| 7 | Background panes keep updating behind an open modal |

| 8 | Targets: Windows + macOS + Linux, 24-bit truecolor (see 0.5) |

| 9 | \*\*Hybrid hyperlinks\*\* — both TUIKit virtual links (C# event/`Action`) and native OSC 8 |

| 10 | \*\*Hover scrolling\*\* (wheel targets the pane under the cursor) \*\*and click-to-focus\*\* |

| 11 | \*\*Multi-targeting\*\*, not a single ns2.0 base (see 0.4) |



\---



\# Part 2 — Follow-ups on the settled decisions



\## 2.1 Command routing table



\*\*Q2.1a — What's the key of the table?\*\* A normalized chord struct (`Key` + `Modifiers`), a string (`"ctrl+shift+3"`), or both with a parser? Strings are far nicer for config files; structs are faster and typo-proof at compile time.



JC>



\*\*Q2.1b — A routing table handles \*commands\*, but a focused text input needs every printable character plus arrows, Home/End, Backspace, and Delete — none of which belong in a command table.\*\* So there's a fallthrough path. What's the precedence?

\* Suggested: `Global` entries → active modal → focused pane's `FocusContext` entries → focused pane's raw input handler → dropped.

\* Does a pane get to \*suppress\* a global binding while it has focus (e.g. an editor that wants `Ctrl+A` for select-all when the app has `Ctrl+A` bound globally)?



JC>



\*\*Q2.1c — Conflict handling.\*\* Two entries claim the same chord in the same scope: throw at registration, last-wins, or first-wins?



JC>



\*\*Q2.1d — Is the table mutable at runtime?\*\* Registering/unregistering bindings while running enables modal keymaps and Vim-style modes. Also: should end users be able to rebind via a config file the host app loads?



JC>



\*\*Q2.1e — Are multi-key chords in scope\*\* (`Ctrl+X Ctrl+S`)? They require a pending-sequence state machine and a timeout.



JC>



\*\*Q2.1f — `Ctrl+C` specifically.\*\* Routable key event, or left as SIGINT so the process can die? What's the default, and can the host override it? Getting this wrong means users can't kill your app, which is the single most common TUI complaint.



JC>



\*\*Q2.1g — `Ctrl+V`.\*\* Bracketed paste (the terminal hands you the whole clipboard as one event, and you must not interpret its contents as key commands) or native clipboard API access, or both? Remote sessions need OSC 52 for clipboard.



JC>



\*\*Q2.1h — Escape timeout.\*\* With the enhanced protocol a bare `Esc` is unambiguous, so this may be moot — but on any degraded fallback path you need a timeout to distinguish `Esc` from the start of a sequence. Default value, and is it tunable?



JC>



\## 2.2 Thread-safe direct API



\*\*Q2.2a — What's the ordering guarantee?\*\* Within a single pane, writes are presumably FIFO by arrival. Across panes, is there any cross-pane ordering guarantee, or are panes fully independent?



JC>



\*\*Q2.2b — Is there an atomic batch scope?\*\* Writing a 5-line block should probably render in one frame rather than being torn across two. Something like `using (pane.BeginBatch()) { ... }`, or an overload taking a collection of lines. Needed?



JC>



\*\*Q2.2c — Backpressure.\*\* A producer outruns the renderer. Does `WriteLine` ever block, does the queue grow unbounded, or do you drop/coalesce? For a capped scrollback, dropping intermediate frames is fine because only the final state is visible — but for an unbounded buffer, "rendered" and "stored" diverge and you need to be explicit about it.



JC>



\*\*Q2.2d — Frame rate.\*\* The requirement mentions 100Hz token streaming, but you almost certainly don't want 100 repaints per second. Do you coalesce to a fixed ceiling (30/60fps), or render on-demand when dirty with a minimum inter-frame interval?



JC>



\*\*Q2.2e — Does TUIKit own a dedicated render thread, or does it render on a timer / thread-pool work item?\*\* A dedicated thread is more predictable and easier to reason about for teardown.



JC>



\*\*Q2.2f — Does TUIKit take ownership of `Console.Out`?\*\* If the host app or a logging framework writes to stdout mid-render, the screen corrupts. Options: redirect stdout to a capture pane, redirect to a null sink, or document "don't do that."



JC>



\## 2.3 Scrollback



\*\*Q2.3a — Default cap when capped?\*\* Cap measured in lines or in bytes? Lines is simpler; bytes is what actually protects you from a runaway agent emitting one 4MB line.



JC>



\*\*Q2.3b — Reflow on resize.\*\* When a pane's width changes, does existing wrapped content re-wrap? Correct behavior, but it re-lays out the entire scrollback, so you need cached wrap results keyed on width. If panes are strictly fixed-size (0.3), this may never happen and you get to skip the whole problem — worth confirming, because it's a large simplification.



JC>



\*\*Q2.3c — Is content append-only or mutable?\*\* This matters a lot for an agent harness: can you update a line already on screen — a tool call going `running…` → `done (1.2s)`, or a progress bar? Append-only is a log and is dramatically simpler. Mutable means every line needs a handle/ID and the renderer needs to invalidate arbitrary regions.



JC>



\*\*Q2.3d — Partial-line streaming.\*\* Tokens arrive with no newline for seconds at a time. Does `Write` (no newline) render immediately, and how does the "current incomplete line" interact with the ring buffer and the smart scroll lock?



JC>



\*\*Q2.3e — What's the text unit?\*\* Plain `string`, styled spans (`Text.From("x").Bold().Red()`), or something richer? Do you want markdown rendering, given agent output is usually markdown?



JC>



\*\*Q2.3f — ANSI passthrough.\*\* Content already containing escape sequences — from a subprocess, or an LLM emitting colored output. Parse and honor it, strip it, or render it literally? Parsing means writing a partial terminal emulator, which is a real subproject.



JC>



\*\*Q2.3g — Smart scroll lock UX.\*\* When detached, do you show an indicator (`↓ 47 new`)? Is there a jump-to-bottom key? Does it re-attach only at the exact bottom, or within a threshold?



JC>



\*\*Q2.3h — Do you need in-pane search, text selection, and copy-to-clipboard?\*\* Selection is the one users miss most, and enabling mouse tracking is exactly what breaks the terminal's native selection.



JC>



\## 2.4 Modals and notifications



\*\*Q2.4a — Async result API?\*\* `var choice = await tui.ShowModalAsync(dialog);` is the most natural thing about writing this in C#, and it composes with nested modals for free. Make it the primary API?



JC>



\*\*Q2.4b — Backdrop dimming.\*\* With truecolor you can scale RGB toward the background, but you have to know the effective color of every cell behind the modal — which means the compositor needs a post-process pass over the region. Is dimming required, or is a border/shadow enough?



JC>



\*\*Q2.4c — Do modals need real widgets\*\* — text field, list, checkbox, radio, buttons, tab order between them? Or is a v1 modal just text plus key-to-dismiss? This is the difference between a weekend and a month.



JC>



\*\*Q2.4d — Dismiss semantics.\*\* `Esc` closes the top modal, always? Click-outside-to-dismiss? Can a modal refuse to close (unsaved changes)?



JC>



\*\*Q2.4e — Can background panes' updates be \*seen\* behind the modal\*\*, or do they just accumulate and paint when the modal closes? You said background threads keep updating visual state — confirming whether that means visibly repainting the uncovered regions.



JC>



\*\*Q2.4f — Notifications:\*\* position (corner/edge), stacking direction, max concurrent, default timeout, dismissible by click, severity levels/styling, and do they steal focus (they shouldn't)?



JC>



\---



\# Part 3 — Still open



\## 3.1 Blockers (these constrain everything downstream)



\*\*Q3.1a — Retained or immediate mode?\*\* Retained: build a widget tree once, mutate objects, library diffs and repaints. Immediate: re-declare the whole UI every frame from your own state. Your thread-safe-direct-write requirement points strongly at retained — a background thread calling `pane.WriteLine()` implies a persistent pane object that owns state. Confirm?



JC>



\*\*Q3.1b — How big is the input editor?\*\* Each pane accepts input. Single-line prompt with history is a day. Multi-line editor with cursor movement, word wrap, selection, undo/redo, and kill-ring is easily the largest single item in this document. Which is v1?



JC>



\*\*Q3.1c — Unicode width handling.\*\* Do you need correct rendering of double-width CJK, combining marks, and emoji grapheme clusters? This is its own subsystem (a wcwidth table plus grapheme segmentation), it's very hard to retrofit once the cell buffer assumes one char per cell, and agent output containing emoji is guaranteed. See also 0.4 — this is the main casualty of `netstandard2.0`.



JC>



\*\*Q3.1d — Headless rendering for tests.\*\* Can TUIKit render to an in-memory cell buffer that you assert against as text? For a library this is close to mandatory — it's how \*you\* test, and it's how your users snapshot-test their UIs. Design requirement or nice-to-have?



JC>



\## 3.2 Lifecycle and hosting



\*\*Q3.2a — What's the entry point?\*\* `await tui.RunAsync(ct)` blocking until quit, or `tui.Start()` / `tui.Stop()` so the host keeps its own loop? "Integrated into a person's console application" suggests the latter matters.



JC>



\*\*Q3.2b — Terminal restoration.\*\* On unhandled exception, `Ctrl+Z`/SIGTSTP, SIGCONT, and SIGTERM, how hard do you try to leave the terminal sane? You need to unwind: alternate screen buffer, cursor visibility, mouse tracking modes, and the enhanced keyboard mode. The Kitty protocol uses <cite index="10-1">a push/pop stack model — push flags at startup, pop on exit — which restores cleanly even when apps are nested</cite>, so use push/pop rather than set/reset.



JC>



\*\*Q3.2c — Can the TUI be suspended and resumed?\*\* Drop back to the normal screen, run something interactive (an editor, a shell), then restore. Common in agent harnesses.



JC>



\*\*Q3.2d — Can multiple TUIKit instances coexist in one process?\*\* Presumably no, but worth an explicit "the terminal is a singleton resource" statement in the API.



JC>



\## 3.3 Mouse and links — hybrid links, hover scroll, click-to-focus



\### The hybrid-link conflict you should know about up front



OSC 8 and virtual links don't cleanly coexist, because \*\*enabling mouse tracking is what breaks OSC 8\*\*. Once TUIKit turns on mouse reporting, the terminal forwards clicks to your application instead of acting on them, and most emulators stop opening OSC 8 URLs entirely — or only honor them under the Shift/Cmd bypass, which is terminal-specific. So "hybrid" in practice means:



\* \*\*Virtual is the live path.\*\* TUIKit hit-tests the click and raises your event. This is what actually fires while the app is running.

\* \*\*OSC 8 is the fallback path.\*\* It's what makes links work when mouse tracking is off, when the user Shift-clicks past your capture, and — importantly — when the pane's content is copy-pasted or the app has exited to the normal screen buffer.



That's still worth doing, but it means OSC 8 is a decoration for graceful degradation, not a second live dispatch mechanism.



\*\*Q3.3a — Given the above, confirm the intended interaction split.\*\* Suggested: plain click → virtual handler; `Ctrl`/`Cmd`-click on a link that carries a URL → let the terminal open it (or open it yourself via `Process.Start`, which is more portable and works identically everywhere). Doing it yourself is arguably better than relying on OSC 8 for the open-in-browser case.



JC>



\*\*Q3.3b — What does a link carry?\*\* Proposed shape: display text (styled), an optional `Uri`, an optional `object` payload, and an optional handler. Is the handler a delegate on the link, an event on the pane you switch on via payload, or a command name resolved through the routing table (2.1)? The command-name route composes best with your keybinding system — the same action can be triggered by key or click.



JC>



\*\*Q3.3c — Security: who is allowed to create links?\*\* This one matters for an agent harness. If TUIKit auto-linkifies URLs found in streamed text, then anything the model emits becomes clickable, and a malicious or prompt-injected response can render `file:///`, `vscode://`, or a lookalike URL that a user clicks. Options:

\* Links are only created by explicit app code; content is never auto-scanned.

\* Auto-linkify, but only an allowlisted scheme set (`http`, `https`, maybe `mailto`).

\* Auto-linkify anything, and show the resolved target in a confirmation modal before acting.



JC>



\*\*Q3.3d — Are links keyboard-reachable?\*\* Tab through them, or Vimium-style hint labels (press `f`, then a letter)? A links-are-mouse-only UI is unusable over a flaky SSH session, and it's the kind of thing that's very hard to add later because it needs a link registry per visible frame.



JC>



\*\*Q3.3e — Multi-line and scrolled links.\*\* A link that wraps across lines is one logical link with several hit rects. A link that's scrolled partly out of view is partially clickable. And the hit-region map has to be rebuilt every frame from the visible viewport, not stored against buffer coordinates. Confirm that's the model — and note that OSC 8 needs an explicit `id=` parameter for wrapped links to be treated as one link by the terminal.



JC>



\*\*Q3.3f — Hover affordance.\*\* Underlining or highlighting a link on hover requires motion tracking (mode 1003), which generates an event per cell of cursor movement and is genuinely laggy over SSH. Is hover feedback required, opt-in, or skipped in favor of always-visible link styling?



JC>



\### Hover scrolling + click-to-focus follow-ups



\*\*Q3.3g — Hover-scroll deliberately decouples scrolling from focus.\*\* That means the user can scroll a pane that doesn't have focus, which detaches \*that\* pane's smart scroll lock (2.3g) without any focus change to signal it. Two panes can be detached at once. Is that the intent, and does the detached indicator need to be visible on every pane rather than just the focused one?



JC>



\*\*Q3.3h — Does click-to-focus also deliver the click to the pane?\*\* Two conventions: (a) the focusing click is consumed, so you click once to focus and again to act, or (b) the click both focuses and is delivered, so clicking a link in an unfocused pane fires it immediately. (b) is what people expect; (a) prevents accidental activation. Which?



JC>



\*\*Q3.3i — Can a pane refuse focus?\*\* A system-stats pane probably shouldn't take keyboard focus. Clicking it then does what — nothing, or scrolls without focusing? Does it get skipped in Tab order?



JC>



\*\*Q3.3j — Which mouse events do you need?\*\* Click, double-click, triple-click, right-click, middle-click (paste on X11), drag, wheel, motion. Note that terminals report only button-press and button-release — \*\*double-click and triple-click have to be synthesized from timestamps by TUIKit\*\*, so you own that threshold. Right-click: does it focus? Do you want context menus?



JC>



\*\*Q3.3k — Drag capture.\*\* If a drag starts in pane A and the cursor moves over pane B, does pane A keep receiving events until release (mouse capture), or does the event go to B? Capture is almost always correct.



JC>



\*\*Q3.3l — Scroll granularity.\*\* Lines per wheel notch, acceleration on rapid scroll, `Shift`+wheel for horizontal, `Ctrl`+wheel for anything? Terminals report wheel as button 4/5 press events with no delta magnitude, so acceleration is also something you synthesize.



JC>



\*\*Q3.3m — Native selection bypass.\*\* With mouse tracking on, the terminal's own click-drag-to-copy stops working, and this is the single most common user complaint about TUIs. Do you implement your own selection with clipboard integration (2.3h), provide a runtime toggle that releases the mouse, rely on the Shift-bypass convention, or all three?



JC>



\*\*Q3.3n — SGR mouse mode.\*\* You'll need mode 1006 (SGR extended coordinates) rather than the legacy X10 encoding, which can't express coordinates past column 223 — and your minimum width is 160 with a plausible maximum well beyond 223. Confirm SGR-only, with a hard failure if unavailable.



JC>



\## 3.4 API shape



\*\*Q3.4a — How is a layout declared?\*\* Fluent builder, plain object graph, attributes on a class, or a markup/DSL file? A builder is the most idiomatic and the easiest to keep discoverable.



JC>



\*\*Q3.4b — Is there data binding?\*\* `INotifyPropertyChanged` / `ObservableCollection` support, or is it purely imperative writes?



JC>



\*\*Q3.4c — Custom widget contract.\*\* Something like `Measure(available)` → `Arrange(rect)` → `Render(ISurface)`. Worth nailing early so built-in widgets aren't privileged over user-authored ones — if the built-ins reach into internals, third-party widgets will always feel second-class.



JC>



\*\*Q3.4d — Theming.\*\* Named styles, a swappable theme object, runtime switching, ASCII fallback when box-drawing glyphs aren't available?



JC>



\## 3.5 Widget scope for v1



\*\*Q3.5a — Which widgets ship first?\*\* The agent harness needs roughly: scrolling text log, input editor, list, and something for stats (gauge, sparkline, or table). Anything beyond that — tree, tabs, sortable table, charts, progress bars, spinners?



JC>



\*\*Q3.5b — Do panes get built-in borders and title bars as a first-class concept, or is that just a decorator widget?\*\*



JC>



\*\*Q3.5c — Does the pane itself have a status/footer line\*\* (e.g. `\[detached · 47 new]`), or is that the app's job?



JC>



\## 3.6 Non-functionals



\*\*Q3.6a — Performance targets.\*\* Max panes, sustained output rate that must not drop frames, frame budget in ms, memory ceiling per pane. Numbers here become your benchmark suite.



JC>



\*\*Q3.6b — Input record/replay\*\* for reproducing user-reported bugs?



JC>



\*\*Q3.6c — Debug overlay\*\* showing layout rects, dirty regions, frame timing?



JC>



\*\*Q3.6d — Non-interactive fallback.\*\* When stdout isn't a TTY (CI, piped, redirected to a file), does TUIKit degrade to plain line-oriented output, or refuse to start? Screen readers do not work with TUIs at all, so this is also the closest thing to an accessibility story.



JC>



\*\*Q3.6e — License, versioning policy, NuGet package layout\*\* (single package vs. `TUIKit` + `TUIKit.Widgets`)?



JC>



\---



\## Summary matrix (updated)



| Domain | Settled | Open |

|---|---|---|

| \*\*Layout\*\* | Arbitrary pane count | Fixed vs elastic (0.3); absolute vs split-tree; undersized-terminal policy (0.1) |

| \*\*Input\*\* | Command routing table; enhanced protocols | Fallthrough to text input; Ctrl+C default; chords; degraded-terminal behavior |

| \*\*Concurrency\*\* | Thread-safe direct API | Ordering, batching, backpressure, frame-rate coalescing |

| \*\*Content\*\* | Configurable cap; smart scroll lock | Mutable vs append-only; ANSI passthrough; Unicode width; markdown |

| \*\*Mouse/Links\*\* | Hybrid links; hover scroll; click-to-focus | OSC 8 vs mouse-capture conflict; link security; keyboard reachability; selection bypass |

| \*\*Modals\*\* | Focus trap, nesting, toasts | Async API, widget scope, dimming |

| \*\*Platform\*\* | Win/macOS/Linux, truecolor, multi-targeting | Whether .NET Framework is real (0.4a); Terminal.app exclusion (0.5) |

| \*\*Testing\*\* | — | Headless rendering (3.1d) |

