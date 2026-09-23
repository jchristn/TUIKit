# Mouse Text Selection & Copy — Implementation Plan (TUIKit)

Status: **plan only — not yet implemented.** This document is a self-contained brief for an
implementing agent to build, test, version-bump, and publish the feature. It was authored against the
TUIKit source at `C:\Code\TUIKit` (package id `TUIKit`, csproj `<Version>1.0.0</Version>`; the
downstream consumer *mux* currently pins `TUIKit 0.13.2`).

All file/line references below were captured while reading the current source; re-verify the exact lines
before editing (they may drift).

---

## 1. Objective

Add a **built-in, application-wide mouse text-selection layer** to `TuiApplication` so that, while the host
has the mouse captured (`MouseCaptureEnabled == true`), the user can:

1. **Click-drag inside any region (pane/rectangle)** to select text, with the selection **constrained to the
   rectangle in which the drag started** (dragging past its edges clamps to that rectangle).
2. **Release, then click-drag in a completely different rectangle** — the previous selection is discarded and
   a new selection is created in the new rectangle. There is always **at most one active selection**, living
   in the rectangle of the most recent drag.
3. **Press `Ctrl+C`** to copy whatever is currently selected (in whichever rectangle the selection lives) to
   the system clipboard, then clear the selection.

It must work **across every region** regardless of the widget type bound there (transcript `Pane`, composer
`TextEditor`, sidebars, list views, etc.). This is achieved by reading text back from the **composited cell
buffer**, so no per-widget cooperation is required.

### Ctrl+C semantics (decided)
- **Selection present:** `Ctrl+C` copies the selection, clears it, and does **not** count toward exit.
- **No selection:** `Ctrl+C` keeps its existing behavior. In *mux* that is
  `CtrlCPolicy.DoubleTapToExit` (press twice within 500 ms to quit). This feature must not change that path
  when nothing is selected.

### Non-goals for v1
- Selecting across two regions at once (selection is always clamped to one rectangle — this is a feature,
  not a limitation).
- Auto-scroll while dragging past the top/bottom edge.
- Keeping a selection "attached" to logical content when the pane scrolls or repaints underneath it (see
  §9 — v1 clears the selection on resize and recommends clearing on scroll).
- Keyboard-driven selection, word/line double/triple-click selection (optional stretch, §11).

---

## 2. Why this belongs in TUIKit (not the consuming app)

*mux* consumes TUIKit as a **NuGet package** (`<PackageReference Include="TUIKit" Version="0.13.2" />` in
`C:\Code\Mux\src\Mux.Cli\Mux.Cli.csproj:20`). The public 0.13.2 API cannot support the requirement because:

- **No pre-route mouse hook.** `TuiApplication` exposes `KeyFilter` (a `Func<KeyEvent,bool>`) but **no mouse
  equivalent**. `MouseReceived` fires only for *unconsumed* events (after modal + widget routing), so a drag
  over a widget that consumes mouse (e.g. `TextEditor` on left-press) never reaches the app. Selection must be
  intercepted *before* `RouteMouse`.
- **No composited-buffer readback.** `ISurface`/`BufferSurface` expose only `Set`/`Fill`, not a cell read.
  Extracting selected text generically requires reading the composed grid.
- **Ctrl+C is host-owned.** `Ctrl+C` is handled inside the host input loop *before* `KeyFilter`
  (`TuiApplication.cs:1265`), so gating it on selection state must happen in the host.

Therefore the whole feature lives in `TuiApplication` (which already owns the hit-test map, the cell buffer,
the render overlay hook, the backend for clipboard writes, and the Ctrl+C policy), exposed through a small
public API the app opts into.

---

## 3. Relevant existing architecture (grounded)

- **Composition:** `TuiApplication.Compose(ISurface root)` (`TuiApplication.cs:1101`). `root` is the
  screen-sized `BufferSurface` (offset 0). It rebuilds `_HitMap`, fills region backgrounds/borders, renders
  each region's widget into a `bufferSurface.CreateView(rect)`, then calls `_OnRenderOverlay?.Invoke(root)`
  (`:1158`) and finally the modal/notification layers.
- **Hit map:** `_HitMap` is a `List<HitTestEntry>` rebuilt every frame at `:1104`/`:1143`. `HitTestEntry`
  (`Hosting/HitTestEntry.cs`) holds `RegionId`, `IWidget Widget`, `Rect Rect` (absolute screen coords).
  `HitTest(int x, int y)` at `:1510` returns the entry under a point.
- **Mouse dispatch:** `DispatchMouse(MouseEvent)` at `:1348`. Order: synthesize multi-click via
  `_ClickSynthesizer` → `UpdateLinkHover` → **modal trap** (`_Modals.IsActive` → `_Modals.HandleMouse`,
  returns, `:1364-1368`) → `RouteMouse` (`:1370`) → else `MouseReceived?.Invoke`. `RouteMouse` (`:1376`)
  converts to region-local coords (`mouse.X - hit.Rect.X`, `:1390-1391`) and calls `IMouseAware.HandleMouse`.
- **Mouse events:** `MouseEvent` (`Input/MouseEvent.cs`) has `Kind` (`Press`/`Release`/`Move`/`Wheel`/
  `Enter`/`Leave`), `Button`, `X`, `Y` (absolute cell coords from the SGR parser), `Modifiers`, `ClickCount`.
  The SGR parser (`Input/InputParser.cs:350` `ParseSgrMouse`) emits `Move` with `Button` set from the low bits
  when the motion flag (bit 32) is present, so a left-drag arrives as `Kind==Move, Button==Left`.
- **Mouse capture:** `MouseCaptureEnabled` (`:249`) writes the SGR enable/disable sequences; `ToggleMouseCapture()`
  (`:318`). *mux* sets it `true` by default and F12 toggles it (`MuxTuiApp.cs:261`). While it is **off**, the
  terminal does its own native selection — the app selection layer must be **inert** in that state.
- **Cell buffer:** `CellBuffer` (`CellBuffer.cs`) has `public Cell Get(int x, int y)` (`:75`). `BufferSurface`
  (`BufferSurface.cs`) wraps a `CellBuffer` privately and currently exposes only `Set`/`Fill`.
- **Cell:** `Cell` (`Cell.cs`) is a readonly struct with `string Grapheme` (`:17`), `CellStyle Style` (`:22`),
  `int Width` (`:28`), and `Cell.Glyph(grapheme, style, width)`. `CellStyle.WithAttribute(CellAttributes.Reverse,
  true)` produces reverse video (already used for the caret in `TextEditor.Render`).
- **Clipboard:** `Content/ClipboardWriter.BuildSequence(text)` → base64 → `Ansi.SetClipboard(payload)` (OSC 52,
  works over SSH). Write via `_Backend.Write(seq); _Backend.Flush();`.
- **Ctrl+C:** input loop `:1265` — `if (IsCtrlC(key) && _CtrlCPolicy != CtrlCPolicy.Custom) { HandleCtrlC(); return; }`.
  `HandleCtrlC()` policy switch at `:1576` (`Kill` → `RequestStop`; `DoubleTapToExit` uses `_LastCtrlC`/500 ms).
  This runs **before** the `KeyFilter` invocation (`:1278`), so the consuming app's key filter never sees Ctrl+C.
- **Multi-click:** `_ClickSynthesizer` sets `ClickCount` in `DispatchMouse` (`:1354`) — available if word/line
  select is added later.

---

## 4. Public API additions

Add to `TuiApplication`:

```csharp
/// <summary>
/// Enables the built-in mouse text-selection layer: while the mouse is captured, a left click-drag
/// selects text within the region where the drag began, and Ctrl+C copies the selection. Defaults to
/// false so existing hosts are unaffected. Has no effect while <see cref="MouseCaptureEnabled"/> is false
/// (the terminal performs native selection then).
/// </summary>
public bool MouseTextSelectionEnabled { get; set; }   // default false

/// <summary>True when a non-empty text selection currently exists.</summary>
public bool HasTextSelection { get; }

/// <summary>
/// The plain text of the current selection (rows joined by '\n', trailing blanks trimmed per row), or an
/// empty string when nothing is selected. Read from the last composed frame's cell buffer.
/// </summary>
public string GetSelectedText();

/// <summary>Clears any active selection (and repaints).</summary>
public void ClearTextSelection();

/// <summary>
/// Optional style used to paint selected cells. When null (default), selected cells are drawn as their
/// existing content with reverse video, which reads correctly on any background.
/// </summary>
public CellStyle? SelectionStyle { get; set; }

/// <summary>Raised after Ctrl+C copies a selection, with the copied text. For host feedback (e.g. a toast).</summary>
public event Action<string>? TextCopied;
```

Add to `BufferSurface` (read-back, mirrors `CellBuffer.Get`, coordinates local to the view):

```csharp
/// <summary>Reads the cell at a local coordinate; returns <see cref="Cell.Empty"/> when out of bounds.</summary>
public Cell Get(int x, int y)
{
    if (x < 0 || x >= _Size.Width || y < 0 || y >= _Size.Height)
        return Cell.Empty;
    return _Buffer.Get(_OffsetX + x, _OffsetY + y);
}
```

(For selection we only read the root surface, whose offset is 0, so `Get(x,y)` maps directly to absolute
screen coords.)

---

## 5. Internal state

Add fields to `TuiApplication`:

```csharp
private bool _SelActive;             // a committed/committing selection exists
private bool _SelDragging;           // left button is down after a press that began in a region
private bool _SelMoved;              // the drag has moved >= 1 cell (distinguishes click from drag)
private Rect _SelRegionRect;         // the rectangle the drag started in; the selection is clamped to it
private int _SelAnchorX, _SelAnchorY; // absolute cell where the drag began (clamped into _SelRegionRect)
private int _SelFocusX, _SelFocusY;   // absolute cell of the current drag point (clamped into _SelRegionRect)
private BufferSurface? _LastRoot;     // the root surface captured each Compose, for text read-back
```

`HasTextSelection => _SelActive`.

Capture the root at the top of `Compose` (before/after the region loop is fine, but before overlay):
`_LastRoot = root as BufferSurface;`

---

## 6. Mouse handling

Insert into `DispatchMouse` **after** the modal trap (`:1368`) and **before** `RouteMouse` (`:1370`):

```csharp
// Built-in text selection. Only while enabled, mouse is captured, and no modal is up (modals own the mouse).
if (MouseTextSelectionEnabled && _MouseCaptureEnabled && !_Modals.IsActive && HandleSelectionMouse(mouse))
    return; // consumed: a drag update or the release that ends one
```

`HandleSelectionMouse` returns `true` only when it consumes the event (a drag move while dragging, or the
release that ends a drag). A **press is never consumed** so click-to-focus and caret placement still work.

```csharp
private bool HandleSelectionMouse(MouseEvent mouse)
{
    if (mouse.Button != MouseButton.Left && mouse.Kind != MouseEventKind.Release)
        return false; // wheel/right/middle are for scrolling & context, not selection

    switch (mouse.Kind)
    {
        case MouseEventKind.Press:
            // A new gesture always discards the previous selection (whether the same or another region).
            ClearSelectionState();
            HitTestEntry? hit = HitTest(mouse.X, mouse.Y);
            if (hit == null)
                return false; // press on a border/gap: nothing to anchor
            _SelRegionRect = hit.Rect;
            _SelAnchorX = Clamp(mouse.X, hit.Rect.Left, hit.Rect.Right - 1);
            _SelAnchorY = Clamp(mouse.Y, hit.Rect.Top, hit.Rect.Bottom - 1);
            _SelFocusX = _SelAnchorX;
            _SelFocusY = _SelAnchorY;
            _SelDragging = true;
            _SelMoved = false;
            _SelActive = false;
            return false; // let the press route (focus / caret)

        case MouseEventKind.Move:
            if (!_SelDragging || mouse.Button != MouseButton.Left)
                return false;
            int fx = Clamp(mouse.X, _SelRegionRect.Left, _SelRegionRect.Right - 1);
            int fy = Clamp(mouse.Y, _SelRegionRect.Top, _SelRegionRect.Bottom - 1);
            if (!_SelMoved && (fx != _SelAnchorX || fy != _SelAnchorY))
                _SelMoved = true;
            _SelFocusX = fx;
            _SelFocusY = fy;
            _SelActive = _SelMoved;
            if (_SelActive)
            {
                RequestRepaint(); // schedule a frame so the highlight follows the drag
                return true;      // consume so the widget doesn't also react to the drag
            }
            return false;

        case MouseEventKind.Release:
            if (_SelDragging && _SelActive)
            {
                _SelDragging = false;
                return true; // finalize; keep _SelActive so Ctrl+C can copy later
            }
            // A plain click (no drag): drop the pending anchor, let the release route normally.
            _SelDragging = false;
            _SelActive = false;
            return false;

        default:
            return false;
    }
}

private void ClearSelectionState()
{
    _SelActive = false;
    _SelDragging = false;
    _SelMoved = false;
}
```

Notes:
- `Clamp` is a trivial `Math.Max(lo, Math.Min(hi, v))` helper (add if not present).
- `RequestRepaint()` — reuse whatever the host already uses to schedule a frame after a mouse event (the same
  path scroll/hover use). If the loop renders every tick at `_TargetFps`, this can be a no-op; verify. Ensure a
  frame renders after `ClearTextSelection()` too.
- `ClearTextSelection()` (public) calls `ClearSelectionState()` + `RequestRepaint()`.
- `MouseButton`/`MouseEventKind` enum member names: confirm (`MouseButton.Left`, `MouseEventKind.Press/Move/Release`).

---

## 7. Highlight rendering

In `Compose`, after the region loop (`~:1156`) and **before** `_OnRenderOverlay?.Invoke(root)` (`:1158`):

```csharp
if (MouseTextSelectionEnabled && _SelActive && root is BufferSurface sel)
    PaintSelection(sel);
```

```csharp
private void PaintSelection(BufferSurface surface)
{
    ForEachSelectedCell((x, y) =>
    {
        Cell under = surface.Get(x, y);
        Cell painted = SelectionStyle.HasValue
            ? Cell.Glyph(string.IsNullOrEmpty(under.Grapheme) ? " " : under.Grapheme, SelectionStyle.Value, Math.Max(1, under.Width))
            : Cell.Glyph(string.IsNullOrEmpty(under.Grapheme) ? " " : under.Grapheme,
                         under.Style.WithAttribute(CellAttributes.Reverse, true), Math.Max(1, under.Width));
        surface.Set(x, y, painted);
    });
}
```

Draw the highlight **before** the overlay/modal layers so overlays and dialogs still paint on top.

### Selection geometry (flow selection, clamped to the region's columns)

`ForEachSelectedCell(Action<int,int> emit)` enumerates cells from the earlier point to the later point in
reading order, wrapping within the anchor region's horizontal bounds:

```csharp
private void ForEachSelectedCell(Action<int, int> emit)
{
    // Order anchor/focus by (y, then x).
    int sx = _SelAnchorX, sy = _SelAnchorY, ex = _SelFocusX, ey = _SelFocusY;
    if (sy > ey || (sy == ey && sx > ex)) { (sx, ex) = (ex, sx); (sy, ey) = (ey, sy); }

    int left = _SelRegionRect.Left, right = _SelRegionRect.Right; // right is exclusive
    for (int y = sy; y <= ey; y++)
    {
        int rowStart = (y == sy) ? sx : left;
        int rowEnd   = (y == ey) ? ex : right - 1; // inclusive
        for (int x = rowStart; x <= rowEnd && x < right; x++)
            emit(x, y);
    }
}
```

This produces a normal text-flow selection (first partial row → full middle rows → last partial row), but never
leaves the anchor region's rectangle — satisfying "constrained to the rectangle the button was clicked in."

---

## 8. Text extraction (`GetSelectedText`)

```csharp
public string GetSelectedText()
{
    if (!_SelActive || _LastRoot == null)
        return string.Empty;

    var sb = new System.Text.StringBuilder();
    int sy = Math.Min(_SelAnchorY, _SelFocusY);
    int ey = Math.Max(_SelAnchorY, _SelFocusY);
    for (int y = sy; y <= ey; y++)
    {
        var row = new System.Text.StringBuilder();
        // Reuse the same per-row span logic as ForEachSelectedCell.
        int left = _SelRegionRect.Left, right = _SelRegionRect.Right;
        int startX, endX;
        ComputeRowSpan(y, left, right, out startX, out endX); // mirrors ForEachSelectedCell's rowStart/rowEnd
        for (int x = startX; x <= endX && x < right; x++)
        {
            Cell c = _LastRoot.Get(x, y);
            if (c.Width > 1)
            {
                row.Append(string.IsNullOrEmpty(c.Grapheme) ? " " : c.Grapheme);
                x += c.Width - 1; // skip continuation columns of a wide glyph
            }
            else
            {
                row.Append(string.IsNullOrEmpty(c.Grapheme) ? ' ' : c.Grapheme[0]);
            }
        }
        if (y != sy) sb.Append('\n');
        sb.Append(TrimEnd(row.ToString())); // trim trailing spaces per row
    }
    return sb.ToString();
}
```

- Factor the per-row start/end computation into a shared helper so `ForEachSelectedCell`, `GetSelectedText`,
  and `PaintSelection` agree exactly.
- **Wide glyphs:** verify how `CellBuffer` stores double-width glyphs (whether continuation columns are blank
  cells or repeats). Adjust the `c.Width > 1` skip accordingly. Add a unit test with a CJK/emoji glyph.
- Trailing-space trim per row matches how terminals copy selected lines.

---

## 9. Ctrl+C integration

At `TuiApplication.cs:1265`, gate the existing Ctrl+C path on selection state:

```csharp
if (IsCtrlC(key) && _CtrlCPolicy != CtrlCPolicy.Custom)
{
    if (MouseTextSelectionEnabled && _SelActive)
    {
        CopyTextSelection();       // build text, write OSC 52, raise TextCopied
        ClearTextSelection();
        _LastCtrlC = long.MinValue; // reset the double-tap timer so a prior lone Ctrl+C doesn't combine
        return;
    }
    HandleCtrlC();
    return;
}
```

```csharp
private void CopyTextSelection()
{
    string text = GetSelectedText();
    if (text.Length == 0) return;
    if (_Backend.IsInteractive)
    {
        _Backend.Write(ClipboardWriter.BuildSequence(text));
        _Backend.Flush();
    }
    TextCopied?.Invoke(text);
}
```

`Custom` policy is intentionally left untouched (those hosts route Ctrl+C themselves; they can call
`GetSelectedText`/`ClearTextSelection` from their own handler).

---

## 10. Edge cases & decisions

1. **Only when captured.** Everything is gated on `_MouseCaptureEnabled`. When the user hits F12 to hand the
   mouse back to the terminal, the app layer is inert and native terminal selection works as before.
2. **Modals.** Selection is suppressed while `_Modals.IsActive` (modals are interactive and own the mouse).
   Clear any selection when a modal opens (optional but tidy).
3. **Resize.** Clear the selection on terminal resize (the stored rect/coords become meaningless). Hook the
   existing resize handler to call `ClearTextSelection()`.
4. **Scroll / content change under a selection.** v1 selection is a snapshot of *screen cells*. If the content
   beneath scrolls or repaints, the highlighted screen cells keep their positions but their meaning changes.
   Recommended: clear the selection when a `Pane` scrolls. Since scroll happens inside `Pane.HandleMouse`
   (wheel) and `ScrollUp/Down`, the cleanest central rule is: **any left-press clears** (already implemented),
   and wheel events while a selection exists also clear it — add `if (_SelActive) ClearTextSelection();` at the
   top of the wheel branch in `DispatchMouse`/`RouteMouse`, or clear on any non-left mouse event. Document as a
   known v1 rough edge; a future version can anchor selections to logical content.
5. **Plain click.** A left click with no drag clears the previous selection and does not create one (matches
   editors). Ctrl+C between release and the next press still copies, because the selection persists until the
   next press/scroll/resize.
6. **Non-interactive/line mode.** `RenderOnce` uses `RenderLineMode` when `!_Backend.IsInteractive` and never
   calls `Compose`; `_LastRoot` stays null and `GetSelectedText` returns "". Fine.
7. **Wide glyphs / zero-width.** Handled in §8; add tests.
8. **Backwards compatibility.** All new behavior is behind `MouseTextSelectionEnabled` (default false), so
   existing TUIKit consumers are unaffected.

---

## 11. Optional stretch (not required for v1)
- **Double-click = select word, triple-click = select line** using `mouse.ClickCount` (already synthesized).
- **Shift+click** to extend an existing selection.
- **Auto-scroll** when dragging past the top/bottom edge of a scrollable pane.
- **Logical-content anchoring** so a selection survives scroll/repaint (requires an `ISelectableContent`
  interface widgets implement).

---

## 12. Tests (add to TUIKit's own test suite)

Use `HeadlessBackend`. Confirm `HeadlessBackend.IsInteractive` is `true` (so `Compose` runs and the buffer is
populated) and that the harness can read bytes the app wrote to the backend (for the OSC 52 assertion). If the
backend lacks an output-capture accessor, add one (e.g. `HeadlessBackend.WrittenText`).

SGR mouse encodings (coordinates are **1-based** on the wire; the parser subtracts 1):
- Left press at screen col `X`, row `Y`: `ESC [ < 0 ; X ; Y M`
- Left drag (motion with left held): `ESC [ < 32 ; X ; Y M`  (32 = motion flag; low bits 0 = left)
- Left release: `ESC [ < 0 ; X ; Y m`
- Ctrl+C: byte `0x03`.

Provide a helper to build these. Suggested cases:
1. **BasicSelectionCopiesText** — seed a region with known text, render, press at start, drag to end, assert
   `GetSelectedText()` equals the expected substring.
2. **SelectionClampsToRegionRectangle** — drag past the region's right/bottom edge; assert the selection stops
   at the region bounds (text does not include neighboring regions).
3. **NewDragInAnotherRegionReplacesSelection** — select in region A, release, press+drag in region B; assert
   `GetSelectedText()` is B's text and none of A's.
4. **CtrlCCopiesAndClears** — make a selection, send `0x03`; assert the backend received an OSC 52 sequence
   whose base64 decodes to the expected text, `HasTextSelection` is false afterward, and the app did **not**
   stop.
5. **CtrlCWithoutSelectionKeepsDoubleTapExit** — with no selection under `DoubleTapToExit`, one `0x03` shows
   the "press again" notice and does not stop; two within 500 ms stops. (Drive the clock via the same
   deterministic `NowMilliseconds` source the multi-click tests use.)
6. **DisabledWhenSelectionOff** — `MouseTextSelectionEnabled = false`: a drag produces no selection and Ctrl+C
   behaves as before.
7. **InertWhenMouseCaptureOff** — `MouseCaptureEnabled = false`: dragging produces no selection.
8. **SuppressedWhileModalActive** — with a modal up, a drag does not select.
9. **PlainClickClearsSelection** — select, then a click with no drag clears it.
10. **WideGlyphExtraction** — a row containing a CJK/emoji glyph copies the correct grapheme without doubling
    or dropping columns.
11. **BufferSurfaceGet** — unit test the new `BufferSurface.Get` (in-bounds and out-of-bounds → `Cell.Empty`).

---

## 13. Versioning & publishing

1. **Pick the target version.** The csproj currently reads `<Version>1.0.0</Version>`, but the published/
   consumed line is `0.13.x` (mux pins `0.13.2`; `local-feed` contains `0.12.0`, `0.13.0`, `0.13.1`).
   **Confirm with the maintainer** whether the next release is `0.14.0` (continue the 0.x line mux tracks) or
   the `1.0.0` GA. Update `<Version>` accordingly. This plan assumes a normal minor bump; do not silently ship
   `1.0.0` if that carries other GA intent.
2. **Pack.** `GeneratePackageOnBuild=True` is set, so `dotnet build -c Release src/TUIKit/TUIKit.csproj`
   emits `TUIKit.<version>.nupkg` under `src/TUIKit/bin/Release/`. Also run `dotnet pack -c Release` explicitly
   to be safe.
3. **Local feed.** Copy the new `.nupkg` into `C:\Code\TUIKit\local-feed\` (that is how `0.12.0`/`0.13.0`/
   `0.13.1` were staged). Verify `local-feed` is a registered NuGet source in the machine/global `NuGet.Config`
   (or add `<add key="tuikit-local" value="C:\Code\TUIKit\local-feed" />`). Clear the cached older copy if
   reusing a version number (never reuse a published version number — bump instead).
4. **Public feed (for builds outside this machine).** `dotnet nuget push` the `.nupkg` to the real feed
   (nuget.org) so downstream/CI builds of mux can restore it. Until this step, mux will only build on machines
   that can see the local feed.
5. **Update the TUIKit changelog/readme** with the new API and behavior.

---

## 14. Downstream (mux) consumption — DEFERRED until TUIKit is published

Do **not** do this until the new TUIKit version is available. Then:

1. Bump `C:\Code\Mux\src\Mux.Cli\Mux.Cli.csproj:20` — `<PackageReference Include="TUIKit" Version="<new>" />`.
2. In `MuxTuiApp` constructor, next to `_App.MouseCaptureEnabled = true;` (`MuxTuiApp.cs:261`), add
   `_App.MouseTextSelectionEnabled = true;`.
3. (Optional) Subscribe to `_App.TextCopied` to show a notice/toast such as "Copied N characters".
4. **No Ctrl+C wiring is needed in mux** — the host handles it before `KeyFilter`
   (`TuiApplication.cs:1265` runs before `:1278`), so `MuxTuiApp.OnKeyFilter` never sees Ctrl+C. Verify this
   ordering still holds in the shipped version.
5. Update mux help/F12 text: with mouse capture on, click-drag selects within a pane and Ctrl+C copies; F12
   still hands the mouse to the terminal for cross-pane native selection.
6. Add mux-side Touchstone coverage (e.g. a `MouseSelectionSuite`, or extend `ComposerSuite`) that drives the
   SGR sequences through `HeadlessBackend` and asserts selection/copy across at least the transcript `Pane` and
   the composer `TextEditor`, proving it works across different widget types.

---

## 15. Summary of files to touch (TUIKit)

- `src/TUIKit/Hosting/TuiApplication.cs` — new public API (§4), state (§5), `HandleSelectionMouse` +
  `DispatchMouse` hook (§6), `PaintSelection`/`ForEachSelectedCell` + `Compose` hook and `_LastRoot` capture
  (§7), `GetSelectedText`/`ComputeRowSpan` (§8), Ctrl+C gate + `CopyTextSelection` (§9), clear-on-resize and
  clear-on-scroll (§10).
- `src/TUIKit/BufferSurface.cs` — public `Get(int x, int y)` (§4).
- `src/TUIKit/Terminal/HeadlessBackend.cs` — add written-output accessor if none exists (for tests, §12).
- TUIKit test project — new selection suite (§12).
- `src/TUIKit/TUIKit.csproj` — version bump (§13).
- Changelog/readme.

No changes to `mux` in this task — that is deferred to §14 after publish.
```
