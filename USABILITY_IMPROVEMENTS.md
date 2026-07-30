# TUIKit — Usability Improvements

> **Scope of this document.** This is an architectural analysis of *consumption friction* — what a
> developer must do by hand to build a real interactive app on TUIKit — plus a prioritized plan to
> close the gaps. It deliberately does **not** touch the rendering, layout, or terminal core. Those
> are the library's strongest assets and nothing here proposes changing them.

## Executive summary

TUIKit already ships the hard parts. The diffing renderer, the `ISurface` / `BufferSurface`
abstraction, the region constraint model, correct Unicode column width, headless snapshot rendering,
and the multi-target (`netstandard2.0` / `net8.0` / `net10.0`) story are excellent and effectively
feature-complete — roughly 39 of 40 catalogued capabilities are present.

The usability problem is not a missing feature. It is a **missing interaction contract**. The host
(`TuiApplication`) owns the render and input loops, but it hands the consumer *raw* key and mouse
events and makes them re-assemble the entire interactive skeleton — focus, key routing, mouse
hit-testing, chrome, and modal result handling — from scratch, every time.

The proof is the official example, `src/TUIKit.Example/HarnessApp.cs`: ~450 lines, of which a large
fraction is boilerplate that *every* interactive consumer must replicate. The primitives to avoid
this (`IFocusable`, `FocusManager`, `LinkRegistry`, `StatusBar`, `Layout.Column/Row`) already exist
in the box — the host simply does not use them on the consumer's behalf, and in a few places the
pieces are too thin to be wired as-is.

**The fix is a coherent, host-owned interaction contract that is strictly additive and overridable.**
No rewrite. Existing escape hatches (`KeyReceived`, `MouseReceived`) stay, so deliberate app-level
overrides (e.g. the harness routing `PageUp` to a non-focused pane) remain expressible.

---

## The core finding: the input↔widget seam

To stand up a standard interactive app today, a developer must hand-write all of the following.
Line references are to `HarnessApp.cs` unless noted.

| Concern | What the consumer writes by hand | Why it shouldn't be their job |
|---|---|---|
| **Key routing** (`OnKey`, ~162–183) | Manually branch `PageUp`/`PageDown` → pane scroll, `Enter` → submit, else → `_Composer.HandleKey`. | `IFocusable` + `FocusManager` exist but `TuiApplication` never uses them. `FocusContext` is only a string tag for scoped keybindings; it is not connected to any focusable widget. |
| **Mouse dispatch** (`OnMouse`, ~196–208) | Manual wheel→pane routing and manual `LinkRegistry.HitTest`. | The host re-emits raw `MouseEvent` and does no hit-testing (`TuiApplication.cs:698`). "Click-to-focus" is advertised but not implemented by the host. |
| **Chrome** (`DrawOverlay` → header/footer/telemetry/notifications, ~295–403) | ~110 lines of rect math drawn *outside* the layout region system. | Header/footer aren't regions, so every rect is computed by hand against `width`/`height`. `StatusBar`/`MenuBar` widgets exist but there is nowhere in the layout to dock them. |
| **Modal results** (~233–292) | `ShowAsync` returns `Task<object?>`; custom modals need `ContinueWith(..., TaskScheduler.Default)` + `is int value ? value : -1` casts. | Untyped results, and the continuation mutates shared UI state off the render thread — an encouraged thread-safety trap. |

---

## Confirmed defects (correctness, not just ergonomics)

These were verified against the source during analysis and are bugs, not preferences.

1. **`Bind` cannot parse the documented multi-key syntax.**
   `BUILDING_TERMINAL_APPS.md:241` shows `app.Bind("ctrl+k ctrl+t", CycleTheme)`, but
   `TuiApplication.Bind` (`TuiApplication.cs:364`) passes the whole string to `KeyChord.Parse`, which
   splits only on `+` (`KeyChord.cs:65`). The space-separated chord becomes an invalid token — the
   documented example throws or mis-binds. Multi-key chords are only reachable via
   `Commands.RegisterSequence`.

2. **Sequence timeout is documented but never wired — and it eats the next keystroke.**
   `CommandRouter` documents that a pending sequence "should be cleared with `ResetPending` after the
   sequence timeout elapses" (`CommandRouter.cs:9,85`), but nothing in the app loop ever calls it.
   `DispatchKey` only early-returns on `Pending` (`TuiApplication.cs:726`) with no timer. Once
   `_HasPending` is set, the *next* key is consumed as the second-of-sequence; if it doesn't complete
   a chord it returns `None` (`CommandRouter.cs:64`) and therefore never reaches `KeyReceived`. A
   dangling prefix doesn't merely stall — it **silently swallows the following keystroke**.

3. **Command-router precedence swallows a focused widget's own keys.**
   `HarnessApp` binds `Ctrl+K Ctrl+T` globally (`HarnessApp.cs:151`) while `TextEditor.HandleKey`
   uses `Ctrl+K` for kill-to-end-of-line (`TextEditor.cs:104`). Because the command router runs
   before `KeyReceived` (`TuiApplication.cs:719`), the composer never sees the first `Ctrl+K`. This
   proves the fix is an explicit precedence chain, not just "auto-route keys to focus."

4. **Focus routing and visual focus can diverge.**
   `IFocusable` exposes only `HandleKey` (`IFocusable.cs:16`). `FocusManager` moves an internal
   index and forwards keys but never sets any focus state (`FocusManager.cs:83–97`). `IsFocused`
   exists ad hoc on only `TextField` and `TextEditor`. If the host drove focus through the manager
   as-is, focus would move but the cursor would keep rendering on the old widget and the new widget
   would show no selection — **visibly wrong frames**. Expanding the focus contract is therefore a
   hard prerequisite to wiring it, not polish.

5. **`app.Layout = …` silently discards helper-created regions.**
   Two layout-construction paths coexist: incremental (`AddPane`/`AddRegion`/`AddWidget`) and
   builder (`Layout.Create()....Build()` then assign `app.Layout`). Assigning `Layout` after using
   the incremental helpers wipes the regions just created. (Note: `AddRegion` also rebuilds the
   immutable `Layout` on each call, but layouts are tiny and immutable by design — this is a
   lifecycle-clarity issue, **not** a performance one.)

6. **Version / documentation drift.** Project metadata is `0.3.1` while the README still says
   `v0.2.0` in places. Low effort, high trust impact.

---

## Recommended plan

Everything below is **additive and overridable**. The default behavior improves; the raw escape
hatches (`KeyReceived`, `MouseReceived`, manual overlay drawing) remain for advanced overrides.

### Tier 1 — the interaction contract (highest ROI)

1. **Expand the focus contract, then give the host an owned focus model.**
   - Add focus-enter/leave + visual focus state to `IFocusable` (e.g. `OnFocusChanged(bool)` or a
     host-set `IsFocused`) so every focus transition updates what's rendered. This is the
     prerequisite that makes host-driven focus produce correct frames.
   - Give the host an owned focus ring: `app.Focus(widget)`, `Tab`/`Shift+Tab` traversal, focus-
     changed events, and automatic `FocusContext` updates so scoped commands follow focus.

2. **Define one explicit input-precedence chain and wire the missing pieces.**
   Canonical order: **modal trap → optional app pre-filter → focus-scoped commands → focused widget
   (first refusal on its own prefix keys) → global commands → fallback `KeyReceived`.**
   - Give the focused widget first refusal on keys that collide with its own bindings (fixes the
     `Ctrl+K` collision).
   - Wire the sequence timeout (`ResetPending` on a timer) so dangling prefixes don't swallow the
     next key.
   - Add conflict diagnostics that surface global-vs-widget chord collisions at bind time.

3. **Host-owned mouse input map (built during the draw pass).**
   The host already computes each bound widget's content rect and creates a view for it
   (`TuiApplication.cs:640–644`). Capture `(regionId, widget, rect)` into a **per-frame** hit-test
   map right there.
   - Region-level routing is the **floor** and is enough for click-to-focus, wheel scroll, and
     folding in `LinkRegistry.HitTest`.
   - Sub-region routing is **opt-in**: containers that create private child views (`Form`,
     `TabView`, `SplitView`) contribute child entries via an `IHitTestable` / `IInputRegionProvider`
     interface, and the host merges them into the same map.
   - Do **not** store `LastBounds` on widgets. A per-frame, host-owned map is rebuilt each frame and
     is safe when the same widget instance is bound twice or rendered headlessly at multiple sizes;
     a mutable per-widget field would clobber itself and break snapshot rendering.
   - Add an optional `IMouseAware { bool HandleMouse(MouseEvent) }`; give panes / `ScrollView`
     built-in wheel scroll.

4. **Typed modals + an app-loop scheduler.**
   - Add `Task<T> ShowAsync<T>(...)` and extend the `ConfirmAsync`/`PromptAsync`/`SelectAsync`
     pattern to custom modals so nobody writes `ContinueWith` + cast again.
   - **Typed results alone do not fix off-loop mutation** (a `TaskCompletionSource` with
     `RunContinuationsAsynchronously` still resumes on the thread pool). Add an
     `app.Post(Action)` / `InvokeOnLoop` queue drained each frame, and layer typed results on top so
     continuations mutate UI state on the loop thread.

### Tier 2 — consumption paths and defect fixes

5. **Fix `Bind` multi-key parsing** (or make `BindSequence` the single documented convenience and
   remove the broken example). Land **one** canonical command-registration path and deprecate — do
   not delete — the others.
6. **Make layout construction unambiguous.** Pick one canonical path (`AddPane`/`AddWidget`
   recommended) and make mixing safe: either merge helper-created regions on `Layout` assignment or
   make it a hard error after content is configured.
7. **Fix the version/doc drift** (README `v0.2.0` → metadata `0.3.1`) and the broken `Bind` example.

### Tier 3 — chrome, docs, scaffolding

8. **App-shell layout helpers as sugar over real regions.** `Layout.Column`/`Row` already cover the
   simple header/body/footer case; the genuine gap is the four-way shell (top + bottom + sidebar +
   main). Add dock/frame helpers (e.g. `DockTop(1).DockBottom(1).Fill(...)`) that produce **real
   regions** you bind `StatusBar`/`MenuBar` into. This deletes the 110 lines of overlay math while
   staying entirely inside the layout system — no special host chrome subsystem.
9. **Docs and scaffolding.** Replace the passive-log quick-start and the pane-only project template
   with a *focused, interactive* widget end-to-end, plus a short cookbook of the handful of things
   every interactive app needs (layout → bind widgets → set focus → run).

---

## Guiding principles

- **Do not touch the core.** Renderer, surfaces, region constraints, Unicode width, and headless
  rendering are the crown jewels and are out of scope.
- **Additive and overridable.** Defaults improve; every raw escape hatch stays so deliberate
  app-level overrides remain expressible.
- **The host should use the primitives it already ships.** Most of this is wiring `IFocusable`,
  `FocusManager`, `LinkRegistry`, `StatusBar`, and `Layout` together on the consumer's behalf — plus
  the minimal contract expansions (focus state, mouse-aware/hit-testable, loop scheduler) needed to
  do that correctly.

**Bottom line:** convert *"read 450 lines of `HarnessApp.cs` and replicate the wiring"* into
*"bind widgets, set focus, run."*
