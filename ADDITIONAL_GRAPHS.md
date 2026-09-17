# Additional Graph Widgets

Status: **proposed** · Target release: **0.13.0** (minor bump from 0.12.0) · Owner: _unassigned_

This plan adds the chart widgets TUIKit is currently missing so that a consuming app can render a
full analytics surface — token/cost bars, over-time trends, **and** the per-metric distribution
("candlestick") views — without hand-rolling glyph math. It is written to be worked top to bottom:
every task is a checkbox, every task names the file it touches, and each phase ends green (build +
tests + example) before the next begins.

## Why this exists

TUIKit already ships `BarChart`, `LineChart`, `Sparkline`, `Gauge`, and the low-level `BrailleCanvas`
and `HalfBlockImage`. That covers magnitudes, single trends, and compact live telemetry. It does **not**
cover the shape of a distribution — the min / average / p95 / p99 / max summary that latency, time-to-
first-token, streaming time, and throughput are read through in practice. A consumer that wants those
views today has to draw the whiskers and the box by hand against a raw surface, which is exactly the
kind of work a widget library should absorb. The same gap shows up for frequency (how many requests
landed in each latency bucket) and for two-dimensional intensity (activity by hour-of-day across a
week). Three widgets close it: a distribution/box-plot chart, a histogram, and a heat map.

The distribution chart is the load-bearing one and ships first. The histogram and heat map round out
the release and are independently useful, but if the release has to be trimmed, they are the parts to
cut, not `BoxPlotChart`.

## Non-goals

- No interactive drill-down, zoom, or brushing. These are static render widgets like the existing
  charts; a host that wants interactivity composes them with its own input handling.
- No new color model. All three reuse `Color.FromPalette` and the existing `Color`/`CellStyle` types.
- No dependency additions. Rendering stays pure block/Braille glyphs on `ISurface`, same as `BarChart`.
- No change to the existing chart widgets' public API. Additive only, so 0.13.0 stays backward
  compatible and the bump is minor, not major.

## Conventions every new widget follows

Read `docs/CONFORMANCE.md`, `docs/SURFACE_COVERAGE.md`, and the `../agents/requirements/CODE_STYLE.md`
rules before writing code, then hold to the shape the existing widgets already use:

- Implement `IWidget` (`Measure(Size)` + `Render(ISurface)`); one public type per file under
  `src/TUIKit/Widgets/`. Match `BarChart.cs` for structure, guard clauses, and XML docs on every
  public member.
- Usings inside the namespace; no `var`; no tuples in public signatures; `_PascalCase` private fields;
  sealed classes unless there is a reason to inherit.
- Builder-style `Add(...)` returns `this` for chaining, mirroring `BarChart.Add`. Provide a `Clear()`.
- Never throw on empty data — an empty chart renders nothing and `Measure` returns a zero/■minimal size,
  matching `BarChart` and `Sparkline`.
- Clamp negative or non-finite (`NaN`, `Infinity`) values on the draw path rather than rejecting them,
  so a live feed with a bad sample degrades instead of crashing.
- Honor the surface size exactly; never write outside `surface.Size`. Verify against the headless
  `BufferSurface` the same way `ChartsIconsColorSuite` does.

---

## Phase 1 — `BoxPlotChart` (distribution / candlestick)

The priority widget. Renders one row per category, each row a horizontal box-and-whisker summary from a
five-number set. The five numbers are caller-supplied (not computed) so the widget stays presentation-
only and the host decides whether "box" means quartiles or, as mux uses it, min / avg / p95 / p99 / max.

### 1.1 Widget

- [ ] Add `src/TUIKit/Widgets/BoxSummary.cs` — an immutable value type holding `Label` plus the five
      doubles `Min`, `Low`, `Mid`, `High`, `Max` (generic names, not `P95`, so the widget is not tied to
      one statistic). Validate ordering leniently: sort the five on construction so an out-of-order
      caller still renders sanely; keep `Label` non-null (throw `ArgumentNullException` like `BarEntry`).
- [ ] Add `src/TUIKit/Widgets/BoxPlotChart.cs` implementing `IWidget`:
  - `BoxPlotChart Add(string label, double min, double low, double mid, double high, double max)` →
    `this`; plus `Add(BoxSummary)` and `Clear()`.
  - `Color WhiskerColor` (default `Color.FromPalette(8)`), `Color BoxColor` (default
    `Color.FromPalette(4)`), `Color MidColor` (default `Color.FromPalette(6)`).
  - `bool ShowAxis` (default true) — a bottom row of min/max scale ticks shared across all rows so the
    boxes are comparable.
  - A single shared value range across every row (global min of all `Min`, global max of all `Max`) so
    rows are visually comparable, with an override hook `SetRange(double min, double max)` for a host
    that wants a fixed axis.
  - Layout per row: left-padded label (common width, like `BarChart`), then the plot region: whiskers as
    `─`, the box as shaded blocks (`█`/one-eighth partials for sub-cell precision, reusing the partials
    array pattern from `BarChart`), the mid marker as a distinct glyph (`┃`) in `MidColor`. Degenerate
    case (all five equal) renders a single mid marker.
  - `Measure`: width = available; height = `Count` (+1 when `ShowAxis`).
- [ ] Optional per-row value readout on the right (`bool ShowValues`, default false) printing
      `mid` in the row's units — off by default to keep narrow terminals clean.

### 1.2 Tests

- [ ] Extend `src/Test.Shared/Suites/ChartsIconsColorSuite.cs` (or add
      `src/Test.Shared/Suites/DistributionChartSuite.cs` and register it in
      `src/Test.Shared/TUIKitSuites.cs`) with cases that render into a `BufferSurface` and assert glyphs:
  - box positions scale with value (a larger `High` pushes the box farther right);
  - the mid marker sits between `Low` and `High`;
  - a degenerate summary (all equal) renders exactly one marker and no whiskers;
  - an empty chart renders nothing and `Measure` reports height 0;
  - negative / `NaN` / `Infinity` inputs clamp instead of throwing;
  - `SetRange` fixes the axis so two identical datasets with different ranges render differently;
  - label column aligns across rows of differing label length.
- [ ] Confirm the shared cases run under all three runners (Test.Automated console, Test.Xunit,
      Test.Nunit) — they aggregate from `TUIKitSuites.All`, so registration is the only wiring needed.

### 1.3 Docs & example

- [ ] Add a `BoxPlotChart` walkthrough paragraph + code snippet to `README.md` alongside the existing
      chart coverage, and note the five-number model is caller-supplied.
- [ ] Add a `CHANGELOG.md` entry under a new `## 0.13.0` heading.
- [ ] Update `docs/SURFACE_COVERAGE.md` (and `docs/CONFORMANCE.md` if it enumerates widgets) to list the
      new widget and its surface guarantees.
- [ ] Add a Guided Tour page in `src/TUIKit.Example/`: create `DistributionDemo.cs` (mirror
      `DualLineChart.cs`) seeded with representative latency-style summaries, then register a
      `new TourPage(...)` in `GuidedTour.BuildPages()` with a short description and the code snippet, the
      same way the `LineChart`/`DualLineChart` page is wired (~line 717).

### 1.4 Gate

- [ ] `dotnet build src/TUIKit/TUIKit.csproj -c Debug` → 0 warnings / 0 errors.
- [ ] `dotnet run --project src/Test.Automated -c Debug` → all green.
- [ ] `dotnet run --project src/TUIKit.Example` → the new tour page renders and navigates.

---

## Phase 2 — `Histogram`

Bins a numeric series into buckets and draws bucket frequency as vertical columns, for "how many
samples fell where" (latency distribution, token-count distribution). Complements `BoxPlotChart`, which
shows the summary; the histogram shows the shape.

### 2.1 Widget

- [ ] Add `src/TUIKit/Widgets/Histogram.cs` implementing `IWidget`:
  - `void SetValues(IEnumerable<double> values)` and `void Push(double value, int capacity)` mirroring
    `Sparkline` so it can back a live feed.
  - `int BucketCount` (default 10, clamped ≥ 1); optional `SetRange(double min, double max)` to fix the
    domain (otherwise derived from the data).
  - `Color BarColor` (default `Color.FromPalette(2)`); `bool ShowCounts` (default false) to print the
    per-bucket count under each column when width allows.
  - Vertical columns drawn with the eighth-block ramp (`▁▂▃▄▅▆▇█`, the `Sparkline` glyph set) scaled to
    the tallest bucket; columns share the full height.
  - `Measure`: width = min(available, `BucketCount`-derived), height = available.
- [ ] Edge cases: empty series renders nothing; all-equal values fall into one bucket; out-of-range
      pushed values clamp to the end buckets.

### 2.2 Tests

- [ ] Add histogram cases (same suite file as Phase 1) asserting: bucket counts sum to sample count;
      the tallest bucket reaches full height; `BucketCount` clamps at 1; empty renders nothing; a fixed
      `SetRange` reproduces the same layout across two series with different natural ranges.

### 2.3 Docs & example

- [ ] README paragraph + snippet; CHANGELOG line under `## 0.13.0`; `docs/SURFACE_COVERAGE.md` entry.
- [ ] Extend `DistributionDemo.cs` (or add a second tour page) with a histogram of a sampled series.

### 2.4 Gate

- [ ] Build, full test run, and example all green (same commands as 1.4).

---

## Phase 3 — `HeatMap` (stretch within 0.13.0)

A grid of intensity cells — rows × columns of values shaded by magnitude — for two-dimensional density
such as activity by hour-of-day across a week. Whole-library value beyond mux's immediate need; ship it
if Phases 1–2 land with time to spare, otherwise defer to 0.14.0 and say so in the CHANGELOG.

### 3.1 Widget

- [ ] Add `src/TUIKit/Widgets/HeatMap.cs` implementing `IWidget`:
  - `void SetCells(double[,] values)` plus optional `RowLabels` / `ColumnLabels` string arrays.
  - A shaded ramp mapping normalized magnitude to `░▒▓█` (or a palette gradient via `Color.FromPalette`),
    with `Color LowColor` / `Color HighColor` if a two-color gradient is preferred over glyph shading.
  - `SetRange` override for a fixed scale; empty/zero-size grid renders nothing.
  - `Measure`: accounts for label gutters plus one cell (or two, for aspect) per column.
- [ ] Decide glyph-shade vs. color-gradient rendering by testing both against the headless buffer and
      the real terminal; pick the one that reads clearly in the Guided Tour and document the choice.

### 3.2 Tests

- [ ] HeatMap cases: normalization maps the max cell to the densest glyph and the min to the lightest;
      labels offset the grid correctly; a fixed range changes shading; empty renders nothing.

### 3.3 Docs & example

- [ ] README paragraph + snippet; CHANGELOG line; `docs/SURFACE_COVERAGE.md` entry; a Guided Tour page.

### 3.4 Gate

- [ ] Build, full test run, and example all green.

---

## Release wrap-up (do last)

- [ ] Bump `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in `src/TUIKit/TUIKit.csproj` to
      `0.13.0` / `0.13.0.0`.
- [ ] Finalize the `## 0.13.0` CHANGELOG section: summarize the widgets shipped and explicitly note any
      deferred (e.g., HeatMap → 0.14.0) so the release notes match what actually landed.
- [ ] Re-read `README.md` end to end so the new widgets sit naturally in the existing chart coverage
      rather than reading as a bolted-on list.
- [ ] Run the whole test matrix once more (Automated + Xunit + Nunit) and the Example app; confirm 0
      warnings across `dotnet build TUIKit.sln -c Release`.
- [ ] Verify `docs/CONFORMANCE.md` and `docs/SURFACE_COVERAGE.md` no longer omit any 0.13.0 widget.

## Coverage target

Every new widget must arrive with tests, not gain them later. Treat "widget added, suite not extended"
as an incomplete task, not a follow-up. The distribution chart in particular carries the most rendering
logic (whiskers, box, mid marker, shared axis, degenerate cases) and should have the densest case list —
aim for the branch coverage the existing `ChartsIconsColorSuite` holds on `BrailleCanvas`, at minimum.

## Downstream note (not part of this repo's work)

mux's usage dashboards on the web, desktop, and VS Code surfaces already render these distribution views
with hand-rolled HTML/SVG; the mux TUI ships the token/cost bars and over-time trend today and will adopt
`BoxPlotChart` for the latency/TTFT/streaming/throughput percentile views once 0.13.0 is published and
the mux `TUIKit` package reference is advanced. No coordination is required beyond the version bump — the
widget API above is designed to accept mux's min/avg/p95/p99/max quintuple directly.
