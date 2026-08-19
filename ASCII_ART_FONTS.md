# ASCII Art Fonts — Implementation Plan

A reusable, developer-facing text-to-ASCII-art component for TUIKit. Consumers hand it a
string and a named font; they get back render-ready rows or a drop-in `IWidget`. Multiple fonts
are managed discretely through a font-library manager, a small font contract, and one class per
font — so the code stays browsable and new fonts land without touching the engine.

Reference output targets: [patorjk TAAG](https://patorjk.com/software/taag/) and
[asciiart.eu text-to-ascii](https://www.asciiart.eu/text-to-ascii-art). Both render FIGlet fonts;
matching their look means implementing FIGlet-style kerning and smushing, not just a glyph table.

---

## 0. Framing: this graduates an existing subsystem

TUIKit already ships a minimal version of this feature. Do not build alongside it and leave two
parallel banner stacks — extend it.

| File | Today | After this plan |
|---|---|---|
| `src/TUIKit/Content/Banner.cs` | Stateless `Render(text, ink)` → 5 plain rows | Kept, unchanged signature; may delegate to the new engine internally |
| `src/TUIKit/Content/BannerFont.cs` | `internal` hardcoded 5×5 block glyphs | Kept; its data is reused by the new `BlockAsciiFont` |
| `src/TUIKit/Widgets/BannerText.cs` | `IWidget`, one font, one color | Kept, unchanged public API |
| `src/Test.Shared/Suites/VisualEffectsSuite.cs` | `Banner`/`BannerText` cases | Kept; new suite added separately |

The new work sits in a new `TUIKit.Ascii` namespace and a new `AsciiArtText` widget. Everything
below is **additive**. No existing public type, member, signature, or namespace is renamed,
removed, or repurposed. `Banner`, `BannerFont`, and `BannerText` remain exactly as they are so
existing consumers and the existing tour page keep compiling and passing.

The one internal change permitted: `Banner.Render` *may* be reimplemented on top of the new engine
so there is a single composition code path, but only if its observable output (five rows, same
width rule, `█` ink default, unknown → blank) is byte-for-byte preserved and every existing
`VisualEffects` test still passes untouched. If that equivalence is not trivially provable, leave
`Banner` alone. Reducing duplication is not worth a behavior regression.

---

## 1. Branch first

Before any file changes:

```bash
git checkout main
git pull --ff-only
git checkout -b feature/ascii-art
```

All work in this plan lands on `feature/ascii-art`. Do not commit to `main`. Open the PR from
`feature/ascii-art` → `main` at the end (Section 11).

---

## 2. Architecture

New folder `src/TUIKit/Ascii/` with one class or enum per file, matching the repository rule. The
manager, the contract, the data types, and each font are all separately managed units.

```
src/TUIKit/Ascii/
  IAsciiFont.cs           interface  — the font contract every font realizes
  AsciiFontBase.cs        abstract   — shared compose + kern + smush engine (the hard part)
  AsciiGlyph.cs           class      — one character's rows + width, immutable
  AsciiFontMetrics.cs     class      — Height, Baseline, HardBlank, default layout, smush rules
  AsciiLayoutMode.cs      enum       — FullWidth | Kerning | Smushing
  AsciiSmushRule.cs       [Flags]    — the six FIGlet horizontal smush rules (+ None)
  AsciiFontException.cs   exception  — font parse / lookup domain errors
  AsciiArt.cs             static     — Render(text, font, options) → rows (mirrors Banner)
  AsciiArtOptions.cs      class      — layout override, alignment, max width, ink override
  AsciiArtAlignment.cs    enum       — Left | Center | Right
  AsciiFontLibrary.cs     class      — THE MANAGER: thread-safe registry, .Default instance
  FigletFontLoader.cs     static     — parse a FIGlet .flf stream/string → IAsciiFont (BYO fonts)

src/TUIKit/Ascii/Fonts/
  BlockAsciiFont.cs       concrete   — wraps the existing 5×5 BannerFont data (license-clean default)
  <one class per bundled font, e.g. StandardAsciiFont.cs, SlantAsciiFont.cs, …>
  (see Section 2.5 for the full expansive roster)

src/TUIKit/Ascii/Fonts/Data/       (EmbeddedResource; see Section 3)
  <one .flf per bundled font, e.g. standard.flf, slant.flf, …>
  LICENSE.figlet.txt                 attribution for the bundled fonts

src/TUIKit/Widgets/
  AsciiArtText.cs         IWidget    — font-aware successor to BannerText
```

The per-font classes are deliberately near-identical thin wrappers (name + embedded resource +
parsed metrics), so an expansive roster stays cheap to add and browse. See Section 2.5.

### 2.1 The font contract — `IAsciiFont`

Kept small, like `IWidget`, so third-party fonts are first-class:

```csharp
public interface IAsciiFont
{
    string Name { get; }
    AsciiFontMetrics Metrics { get; }
    bool TryGetGlyph(char c, out AsciiGlyph glyph);
    IReadOnlyCollection<char> SupportedCharacters { get; }
    IAsyncEnumerable<char> GetSupportedCharactersAsync(CancellationToken token);
}
```

`GetSupportedCharactersAsync` satisfies the CODE_STYLE rule: any method returning an `IEnumerable`
gets an async, `CancellationToken`-bearing companion. On `netstandard2.0` this needs
`Microsoft.Bcl.AsyncInterfaces`, which the library already references for that target — no new
dependency.

### 2.2 The engine — `AsciiFontBase`

Abstract base implementing, once, everything that is font-independent:

- Horizontal composition of consecutive glyphs.
- **Kerning** — trim shared blank columns between adjacent glyphs.
- **Smushing** — the six FIGlet horizontal rules (equal-character, underscore, hierarchy,
  opposite-pair, big-X, hardblank), selected by `Metrics.SmushRules`.
- Hardblank substitution to spaces on final output.
- Alignment and final row assembly to equal width.
- `Measure`-friendly width computation using the framework's Unicode width, not `string.Length`
  (some FIGlet fonts use box-drawing glyphs). Use the existing `TUIKit.Unicode.TextWidth`.

Concrete fonts supply only **data**: the glyph table and the metrics. This is what keeps each font
file tiny and the engine in one reviewable place. `AsciiFontBase` holds a `ReaderWriterLockSlim`
around its lazily-built glyph cache if glyphs are materialized on first use.

### 2.3 The manager — `AsciiFontLibrary`

The library manager module you asked for. Read-heavy (lookups dominate registration), so it uses
`ReaderWriterLockSlim` per CODE_STYLE, not `lock`.

```csharp
public sealed class AsciiFontLibrary : IDisposable
{
    public static AsciiFontLibrary Default { get; }          // lazily populated with built-ins
    public IReadOnlyList<string> Names { get; }              // case-insensitive
    public int Count { get; }

    public void Register(IAsciiFont font);                   // throws on null / duplicate name
    public bool TryRegister(IAsciiFont font);                // false on duplicate
    public bool Unregister(string name);
    public bool Contains(string name);
    public bool TryGet(string name, out IAsciiFont font);
    public IAsciiFont Get(string name);                      // throws AsciiFontException if missing
    public IEnumerable<IAsciiFont> Enumerate();
    public IAsyncEnumerable<IAsciiFont> EnumerateAsync(CancellationToken token);

    protected virtual void Dispose(bool disposing);          // full pattern; disposes RWLock
    public void Dispose();
}
```

Name lookups are case-insensitive (`StringComparer.OrdinalIgnoreCase`). Built-in fonts are
instantiated lazily so a consumer who never touches ASCII art pays nothing. `Default` is a shared
singleton; consumers who want isolation construct their own `AsciiFontLibrary`.

### 2.4 Renderer + options + widget

`AsciiArt.Render(string text, IAsciiFont font, AsciiArtOptions? options = null)` returns
`IReadOnlyList<string>`, mirroring `Banner.Render` so it composes with existing color/placement
helpers. `AsciiArtOptions` carries layout override, alignment, optional max width (wraps to
multiple line-blocks), and an ink override for the block-style fonts.

`AsciiArtText : IWidget` is the drop-in successor to `BannerText`: `Font` (defaults to the block
font), `Color`, `Alignment`, optional wrap width. `Measure` reports `Metrics.Height` rows tall and
the composed width, clamped to available — same contract shape as `BannerText`/`Sparkline`.

### 2.5 Initial font roster (expansive)

The initial set is intentionally large. Because every FIGlet font is a thin `AsciiFontBase` subclass
over an embedded `.flf` (Section 3), the marginal cost of each additional font is one small class
plus one data file — so breadth is cheap and the value to consumers is high. Ship the full roster
below, subject only to per-font license vetting (Section 9); anything that fails vetting drops to
bring-your-own without affecting the rest.

Every font below is a **real font in the TAAG / `patorjk/figlet.js` distribution** — the names were
verified against that repository's `fonts/` directory, not invented. Each row becomes one
`*AsciiFont.cs` class and one embedded `.flf` whose filename is the exact TAAG name in the first
column. The manager exposes them all by name through `AsciiFontLibrary.Default`. The roster below is
the required minimum initial set; it satisfies every font the feature request named.

Two naming reconciliations, applied deliberately:

- The request's **"BlueVision"** is TAAG's font **`BlurVision ASCII`** — there is no font literally
  named "BlueVision"; this is the intended match.
- The request's **"Block and variants"** maps to TAAG's `Block` and `Blocks`. TUIKit's existing
  original 5×5 font keeps the public name `Block` (it is the default and predates this work), so
  TAAG's `Block` is registered as **`BlockFiglet`** to avoid a name collision. `Blocks` and
  `Shaded Blocky` and `Small Block` are distinct fonts and keep their own names.

TAAG's `.flf` filenames contain spaces, hyphens, and digits. The **`.flf` resource filename** must
be the exact TAAG name (e.g. `Big Money-ne.flf`). The **registered name** is that name normalized to
PascalCase with separators removed (e.g. `BigMoneyNe`) — that is the stable identifier passed to
`AsciiFontLibrary.Default.Get(...)` and shown in the gallery. The **class name** is the registered
name plus `AsciiFont`. `FigletFontLoader` reads the human display name from the `.flf` header;
`Metrics.Name` should carry it while the library key uses the normalized form.

**Big Money (and variants) + Big**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Big Money-ne | `BigMoneyNe` | `BigMoneyNeAsciiFont` |
| Big Money-nw | `BigMoneyNw` | `BigMoneyNwAsciiFont` |
| Big Money-se | `BigMoneySe` | `BigMoneySeAsciiFont` |
| Big Money-sw | `BigMoneySw` | `BigMoneySwAsciiFont` |
| Big | `Big` | `BigAsciiFont` |

**Standard, Slant & Slant Relief (and variants), Soft**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Standard | `Standard` | `StandardAsciiFont` |
| Slant | `Slant` | `SlantAsciiFont` |
| Slant Relief | `SlantRelief` | `SlantReliefAsciiFont` |
| Relief | `Relief` | `ReliefAsciiFont` |
| Relief2 | `Relief2` | `Relief2AsciiFont` |
| Soft | `Soft` | `SoftAsciiFont` |

**Small (and variants), incl. Small Braille**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Small | `Small` | `SmallAsciiFont` |
| Small Slant | `SmallSlant` | `SmallSlantAsciiFont` |
| Small Caps | `SmallCaps` | `SmallCapsAsciiFont` |
| Small Script | `SmallScript` | `SmallScriptAsciiFont` |
| Small Shadow | `SmallShadow` | `SmallShadowAsciiFont` |
| Small Poison | `SmallPoison` | `SmallPoisonAsciiFont` |
| Small Keyboard | `SmallKeyboard` | `SmallKeyboardAsciiFont` |
| Small Isometric1 | `SmallIsometric1` | `SmallIsometric1AsciiFont` |
| Small Block | `SmallBlock` | `SmallBlockAsciiFont` |
| Small Tengwar | `SmallTengwar` | `SmallTengwarAsciiFont` |
| Small Braille | `SmallBraille` | `SmallBrailleAsciiFont` |
| Small ASCII 9 | `SmallAscii9` | `SmallAscii9AsciiFont` |
| Small ASCII 12 | `SmallAscii12` | `SmallAscii12AsciiFont` |
| Small Mono 9 | `SmallMono9` | `SmallMono9AsciiFont` |
| Small Mono 12 | `SmallMono12` | `SmallMono12AsciiFont` |

**Mono (and variants) + ASCII 9 / 12 + Future (and variants)**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Mono 9 | `Mono9` | `Mono9AsciiFont` |
| Mono 12 | `Mono12` | `Mono12AsciiFont` |
| Big Mono 9 | `BigMono9` | `BigMono9AsciiFont` |
| Big Mono 12 | `BigMono12` | `BigMono12AsciiFont` |
| ASCII 9 | `Ascii9` | `Ascii9AsciiFont` |
| ASCII 12 | `Ascii12` | `Ascii12AsciiFont` |
| Future | `Future` | `FutureAsciiFont` |
| Future Smooth | `FutureSmooth` | `FutureSmoothAsciiFont` |
| Future Thin | `FutureThin` | `FutureThinAsciiFont` |

**ANSI family + Bloody**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| ANSI Compact | `AnsiCompact` | `AnsiCompactAsciiFont` |
| ANSI Regular | `AnsiRegular` | `AnsiRegularAsciiFont` |
| ANSI Shadow | `AnsiShadow` | `AnsiShadowAsciiFont` |
| Bloody | `Bloody` | `BloodyAsciiFont` |

**Banner3 / Banner4 (and variants) + Block (and variants)**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Banner3 | `Banner3` | `Banner3AsciiFont` |
| Banner3-D | `Banner3D` | `Banner3DAsciiFont` |
| Banner4 | `Banner4` | `Banner4AsciiFont` |
| Block | `BlockFiglet` | `BlockFigletAsciiFont` |
| Blocks | `Blocks` | `BlocksAsciiFont` |
| Shaded Blocky | `ShadedBlocky` | `ShadedBlockyAsciiFont` |

**Alligator (and variants) + Cyberlarge (and variants)**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Alligator | `Alligator` | `AlligatorAsciiFont` |
| Alligator2 | `Alligator2` | `Alligator2AsciiFont` |
| Cyberlarge | `Cyberlarge` | `CyberlargeAsciiFont` |
| Cybermedium | `Cybermedium` | `CybermediumAsciiFont` |
| Cybersmall | `Cybersmall` | `CybersmallAsciiFont` |

**Caligraphy (and variants) + Cosmike**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Caligraphy | `Caligraphy` | `CaligraphyAsciiFont` |
| Caligraphy2 | `Caligraphy2` | `Caligraphy2AsciiFont` |
| Cosmike | `Cosmike` | `CosmikeAsciiFont` |
| Cosmike2 | `Cosmike2` | `Cosmike2AsciiFont` |

**Fire Font (variants) + Rebel (and variant)**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| Fire Font-k | `FireFontK` | `FireFontKAsciiFont` |
| Fire Font-s | `FireFontS` | `FireFontSAsciiFont` |
| Rebel | `Rebel` | `RebelAsciiFont` |
| DOS Rebel | `DosRebel` | `DosRebelAsciiFont` |

**Remaining named single fonts**

| TAAG font (`.flf` name) | Registered name | Class |
|---|---|---|
| BlurVision ASCII | `BlurVisionAscii` | `BlurVisionAsciiAsciiFont` |
| Chiseled | `Chiseled` | `ChiseledAsciiFont` |
| Crawford | `Crawford` | `CrawfordAsciiFont` |
| Crawford2 | `Crawford2` | `Crawford2AsciiFont` |
| Doh | `Doh` | `DohAsciiFont` |
| Doom | `Doom` | `DoomAsciiFont` |
| Graffiti | `Graffiti` | `GraffitiAsciiFont` |
| Ogre | `Ogre` | `OgreAsciiFont` |
| Rectangles | `Rectangles` | `RectanglesAsciiFont` |
| Sub-Zero | `SubZero` | `SubZeroAsciiFont` |
| Terrace | `Terrace` | `TerraceAsciiFont` |
| Tmplr | `Tmplr` | `TmplrAsciiFont` |
| Train | `Train` | `TrainAsciiFont` |
| Varsity | `Varsity` | `VarsityAsciiFont` |
| Calvin S | `CalvinS` | `CalvinSAsciiFont` |
| Classy | `Classy` | `ClassyAsciiFont` |
| Coder Mini | `CoderMini` | `CoderMiniAsciiFont` |
| Delta Corps Priest 1 | `DeltaCorpsPriest1` | `DeltaCorpsPriest1AsciiFont` |
| Pagga | `Pagga` | `PaggaAsciiFont` |
| Chunky | `Chunky` | `ChunkyAsciiFont` |
| Colossal | `Colossal` | `ColossalAsciiFont` |
| Contrast | `Contrast` | `ContrastAsciiFont` |
| Cricket | `Cricket` | `CricketAsciiFont` |
| Bright | `Bright` | `BrightAsciiFont` |
| Jazmine | `Jazmine` | `JazmineAsciiFont` |

Plus the pre-existing original: `Block` (`BlockAsciiFont`, the license-clean 5×5 default). As shipped,
the roster is **84 fonts** (83 embedded FIGlet/TOIlet fonts plus the original block font).

**Licensing gate (see §9).** The tables above are the *candidate* set. Every entry must clear the §9
license gate before it is built; any font with restrictive licensing would be **removed from the
library entirely** — no class, no `.flf`, no `Default` registration. The requested set was scanned and
**none carried restrictive terms**, so the full set (Graffiti included; §9.2) is bundled and
`REMOVED.txt` lists nothing. Run the vetting pass before generating font classes so any future removal
happens up front rather than after the code exists.

A couple of the request's "and variants" phrasings have no separate variant font in the TAAG
distribution — **Alligator** ships only `Alligator` and `Alligator2` (there is no `Alligator3`), and
**Bright** ships as a single font. The tables above list every variant that actually exists; do not
fabricate `.flf` files for variants TAAG does not have. A consumer who legally holds a font TUIKit
does not ship can still load their own `.flf` via `FigletFontLoader` — that is their file and their
license decision, not TUIKit redistributing it.

Registered names are the stable public identifiers; keep them exactly as listed (PascalCase, no
spaces or hyphens) so lookups are predictable. To keep ~75 near-identical font classes from becoming
boilerplate sprawl, generate them from one shared shape: each is `sealed`, derives from
`AsciiFontBase`, passes its normalized name and embedded-resource key to the base constructor, and
adds nothing else. The engine, parser, and metrics all live in the base, so a reviewer reads the
pattern once. A one-off code generator that reads the `.flf` set and emits the classes (committed as
source, not run at build time) is the sane way to produce them; the committed files must still be
hand-verifiable and CODE_STYLE-clean.

---

## 2.6 Dependencies — none new

Confirmed: **implementing this feature adds no external/NuGet dependencies to any project.** Every
piece is built on the BCL surface the library already targets.

| Need | Provided by | New package? |
|---|---|---|
| FIGlet `.flf` parsing | `System.IO`, `System.Text` | No — BCL |
| Embedded font data | `System.Reflection` `Assembly.GetManifestResourceStream` | No — BCL; `<EmbeddedResource>` is an SDK build item, not a package |
| Thread-safe registry | `System.Threading.ReaderWriterLockSlim` | No — BCL |
| Composition/measure | existing `TUIKit.Unicode.TextWidth`, `ISurface` | No — in-repo |
| `IAsyncEnumerable<T>` async variants | built-in on `net8.0`/`net10.0`; `Microsoft.Bcl.AsyncInterfaces` on `netstandard2.0` | No — that package is **already** referenced by `TUIKit.csproj` for `netstandard2.0` |
| Tests | `Touchstone.Core` (Test.Shared already references it) | No |
| Example | `TUIKit` project reference only | No |

The only `netstandard2.0` polyfill the async members lean on (`Microsoft.Bcl.AsyncInterfaces`) is
already in the csproj (Section on packaging), so even the compat target needs nothing added. No image
libraries, no font engines, no HTTP, no third-party FIGlet package. The bundled `.flf` files are data
committed to the repo, not a dependency. If the async `IAsyncEnumerable` variants were ever deemed not
worth the `netstandard2.0` polyfill surface, they could be dropped without affecting the sync API — but
since the polyfill is already present, keep them for CODE_STYLE compliance.

---

## 3. Glyph data storage — decision and fallback

The dominant cost and the main design fork. Recommendation: **embedded `.flf` resources behind
thin font classes.**

Each built-in font class (`StandardAsciiFont`, etc.) is a discrete `AsciiFontBase` subclass whose
only job is to name its embedded `.flf` resource and expose parsed metrics. The FIGlet control
header carries the smush rules, so fonts render correctly with no hand-authored layout. Font data
lives in `.flf` files marked `<EmbeddedResource>` in `TUIKit.csproj`. `GetManifestResourceStream`
behaves identically across `netstandard2.0`, `net8.0`, and `net10.0`, so **no `#if` is
introduced** — this stays within the rule confining `#if` to the terminal backend and the compat
shim.

`BlockAsciiFont` is the exception: it reuses the existing hardcoded `BannerFont` 5×5 data (original,
license-clean) and needs no resource. It is the safe default `Font` for `AsciiArtText`.

Add to `src/TUIKit/TUIKit.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Ascii\Fonts\Data\*.flf" />
</ItemGroup>
```

**Fallback if embedding is rejected** (e.g. licensing blocks bundling, Section 9): ship only
`BlockAsciiFont` plus `FigletFontLoader`, and have consumers load their own `.flf` at runtime. The
engine, contract, manager, renderer, widget, tests, and docs are all unaffected — only the count of
bundled fonts changes. Plan the code so this degrades cleanly.

---

## 4. Compliance with `c:\code\agents\requirements` (CODE_STYLE)

Every new file must satisfy the strict rules. Checklist the dev applies to each file:

- `namespace` at top; `using` directives **inside** the namespace; system usings first
  alphabetically, then others alphabetically.
- Exactly one class or one enum per file. `AsciiLayoutMode`, `AsciiSmushRule`, `AsciiArtAlignment`
  each get their own file — do not nest them.
- XML docs on every public type, member, constructor, and method; document defaults, min/max, and
  what values mean (e.g. `AsciiArtOptions.MaxWidth` default `0` = no wrap). No docs on private
  members.
- No `var`; explicit types everywhere. Private fields `_PascalCase`.
- No tuples. `TryGetGlyph`/`TryGet`/`TryRegister` use `out` parameters, not tuple returns.
- Every `async` method takes a `CancellationToken` unless the class holds one; check cancellation
  at sensible points; `.ConfigureAwait(false)` on awaits.
- Any `IEnumerable`-returning method gets an async `CancellationToken` companion
  (`SupportedCharacters` → `GetSupportedCharactersAsync`; `Enumerate` → `EnumerateAsync`).
- Custom exception `AsciiFontException` for domain errors (missing font, malformed `.flf`); never
  bare `Exception`. Guard clauses first — `ArgumentNullException.ThrowIfNull` on `net8.0`/`net10.0`,
  manual null checks on `netstandard2.0`. Document throwables with `/// <exception>`.
- `AsciiFontLibrary` implements the full `Dispose(bool)` pattern (it owns a `ReaderWriterLockSlim`).
- No `Console.Write*` anywhere in library code.
- Builds clean under `TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true` on all three
  target frameworks.

`WRITING_DOCUMENTS.md` governs prose/publication documents, not code or implementation notes, so it
does not constrain this plan file or the API. `BACKEND_TEST_ARCHITECTURE.md` governs Section 6.

---

## 5. Build order (actionable task list)

Land in this order so each step compiles green before the next:

1. **Branch** — Section 1.
2. **Enums + data types** — `AsciiLayoutMode`, `AsciiSmushRule`, `AsciiArtAlignment`, `AsciiGlyph`,
   `AsciiFontMetrics`, `AsciiFontException`. Pure types, no dependencies.
3. **Contract** — `IAsciiFont`.
4. **Engine** — `AsciiFontBase` (compose + kern + smush + measure). Unit-testable in isolation via a
   tiny in-test fake font.
5. **Block font** — `BlockAsciiFont` over existing `BannerFont` data. First real font; validates the
   engine with zero I/O.
6. **Renderer + options** — `AsciiArtOptions`, `AsciiArt.Render`.
7. **Manager** — `AsciiFontLibrary` with `Default` seeded from `BlockAsciiFont` only at first.
8. **FIGlet loader** — `FigletFontLoader.Load(...)`; parse header, hardblank, smush rules, glyphs.
9. **Bundled fonts (the expansive roster)** — add the `.flf` files + `EmbeddedResource` entry, stamp
   out the ~75 `*AsciiFont` classes per the shared shape (Section 2.5), and register them in
   `AsciiFontLibrary.Default`. Land them in batches (e.g. block/heavy, then slanted, then the rest),
   building green between batches so a bad `.flf` or a mis-declared resource is caught early rather
   than in a 75-font lump.
10. **Widget** — `AsciiArtText`.
11. **Tests** — Section 6, register suite in `TUIKitSuites.All`.
12. **Example** — Section 7.
13. **Docs** — Section 8.
14. **Build + full test pass on net8.0 and net10.0** — Section 10.
15. **PR** — Section 11.

Between steps, run `dotnet build src/TUIKit/TUIKit.csproj -c Release` to keep warnings-as-errors
honest.

---

## 6. Testing — positive and negative, expanded coverage

New suite `src/Test.Shared/Suites/AsciiArtSuite.cs`, built like `VisualEffectsSuite` (a
`TestSuiteDescriptor` returning `TestCaseDescriptor`s, assertions via `Check`, no console output).
Register it in `src/Test.Shared/TUIKitSuites.cs` `All` (add `AsciiArtSuite.Suite()` to the list).
It then runs unchanged through Test.Automated, Test.Xunit, and Test.Nunit — the shared-registry
pattern requires no per-runner edits. Use `WidgetTester.For(widget, w, h).Render().Text()` for
widget cases, exactly as the banner test does.

Coverage must go past a happy-path smoke test. Group the cases:

### 6.1 Glyph + engine (positive)

- `Block` font renders `"HI"` to `Metrics.Height` rows; all rows equal width; ink present.
- Kerning: two glyphs with adjacent blank columns compose narrower than full-width would.
- Each smush rule fires on a crafted glyph pair (equal-character, underscore, hierarchy,
  opposite-pair, big-X, hardblank) — assert the smushed column equals the rule's expected glyph.
- `FullWidth` layout leaves a one-column gap; `Kerning` removes shared blanks; `Smushing` overlaps —
  assert the three widths are strictly decreasing for the same input.
- Alignment: `Center` and `Right` pad the short rows to equal width with the block on the correct
  side.
- Multi-word text with a space renders a blank column band the width of the space glyph.

### 6.2 Glyph + engine (negative / edge)

- Empty string → zero-width, `Metrics.Height`-row result (not null, no throw).
- Unknown / unsupported character → blank glyph, never a crash; `TryGetGlyph` returns `false`.
- `TryGetGlyph(c, out _)` is `false` for an unsupported char and `true` for a supported one.
- Whitespace-only input renders only blank rows.
- Very long input still produces rectangular, equal-width rows.
- `AsciiArt.Render(null!, font)` throws `ArgumentNullException`.
- `AsciiArt.Render(text, null!)` throws `ArgumentNullException`.
- `AsciiArtOptions.MaxWidth` negative → `ArgumentOutOfRangeException` (explicit get/set backing
  field validation).

### 6.3 FIGlet loader (positive + negative)

- Load each bundled `.flf` from its embedded stream; `Metrics.Height` matches the header; a known
  letter renders non-blank.
- Round-trip: render `"A"` with a loaded font and assert exact expected rows (fixture strings) so
  regressions in the parser are caught, not just "non-empty".
- Malformed header (`flf2a` line missing / wrong magic) → `AsciiFontException` with a contextual
  message.
- Truncated glyph section → `AsciiFontException`, not `IndexOutOfRangeException`.
- `FigletFontLoader.Load((Stream)null!)` and `Load((string)null!)` → `ArgumentNullException`.
- Empty stream → `AsciiFontException`.

### 6.4 Library manager (positive + negative)

- `Default` contains `Block` and every bundled font name; `Count` matches `Names.Count`.
- **License gate is enforced:** `Default` contains *none* of the removed font names (assert
  `Contains("Graffiti")` is `false`, plus any other name struck by §9). This case is the guard that
  keeps a restrictive font from being re-added unnoticed.
- `Register` then `TryGet` returns the same instance; `Contains` is `true`.
- Name lookup is case-insensitive (`"block"`, `"BLOCK"`, `"Block"` all resolve).
- `Unregister` removes it; subsequent `TryGet` is `false`; `Get` throws `AsciiFontException`.
- `Register(null!)` → `ArgumentNullException`.
- `Register` of a duplicate name → `AsciiFontException` (or `InvalidOperationException`, chosen and
  documented); `TryRegister` of a duplicate → `false`, no throw.
- `Get("does-not-exist")` → `AsciiFontException` naming the missing font.
- `Enumerate()` and `EnumerateAsync(token)` yield the same set; the async variant honors an
  already-cancelled token by throwing `OperationCanceledException`.
- Concurrency: parallel `Register`/`TryGet`/`Enumerate` from multiple tasks does not throw and does
  not corrupt `Count` (drives the `ReaderWriterLockSlim` path).
- After `Dispose`, further use throws `ObjectDisposedException` (document this).

### 6.5 Widget (positive + negative)

- `AsciiArtText("A")` default font renders blocks; `WidgetTester` text contains ink.
- Setting `Font` to a bundled font changes the rendered width/shape.
- `Measure` returns `Metrics.Height` for height and clamps width to available.
- `Alignment` right/center shifts the drawn glyphs within a wide surface.
- `new AsciiArtText(null!)` → `ArgumentNullException`; `Font = null` → `ArgumentNullException`.
- Zero-size and one-cell surfaces render without throwing (clip cleanly).

Aim for parity with the existing guard-test density (see `WidgetGuardValidationSuite`): every public
guard clause gets at least one negative case. Keep case IDs stable and descriptive
(`AsciiArt`/`SmushEqualChar`, `AsciiArt`/`LibraryDuplicateName`, …) so the runners report them
cleanly.

---

## 7. Example — `TUIKit.Example`

The guided tour already has a "Banner text (FIGlet)" page (`GuidedTour.cs`, in `BuildPages`). Add a
new page immediately after it that showcases the expansive multi-font roster, and make it
**interactive**: the left and right arrow keys step through every registered font, one at a time,
re-rendering the same sample word in each. With ~84 fonts a static grid would be unreadable;
stepping through them is the point.

### 7.1 A scrollable font-gallery widget

Add a small example-only widget, `src/TUIKit.Example/FontGallery.cs`, that owns the scroll state and
draws the current font. It is the tour page's `Demo`.

- `FontGallery : IWidget, IFocusable` — the `IFocusable` part is what makes this work. The tour gives
  the focused page's demo first refusal on every key (`GuidedTour.HandleKey` →
  `demo.HandleKey(key)`), and it does **not** use Left/Right for page navigation — those keys fall
  straight through to the demo. Page-switching stays on PageUp/PageDown and `[`/`]`, so there is no
  collision. This is the same pattern `DiffView` uses to own Up/Down while embedded in the tour.
- Constructor takes the font list and a sample word: `new FontGallery(AsciiFontLibrary.Default, "TUIKit")`.
  Snapshot `Default.Names` (or `Enumerate()`) once into a `List<IAsciiFont>` so the order is stable
  while scrolling.
- `HandleKey`:
  - `KeyCode.Right` → advance to the next font (wrap at the end), return `true`.
  - `KeyCode.Left` → previous font (wrap at the start), return `true`.
  - `KeyCode.Home`/`KeyCode.End` → jump to first/last (nice-to-have), return `true`.
  - anything else → return `false` so the tour still handles it (Tab, PageDown, help, quit).
- `Render`: draw the current font's name and index (e.g. `Slant  (12/84)`) on the top row, then the
  `AsciiArt.Render(_Sample, _Fonts[_Index])` rows below it in a chosen color, clipped to the surface.
  Reuse `AsciiArtText` internally, or call `AsciiArt.Render` directly and draw rows with
  `surface.DrawText` — either is fine.
- `Measure`: report the tallest bundled font's height plus one row for the label, clamped to
  available, so the tour panel sizes sensibly regardless of which font is showing.

Wire it into `GuidedTour.BuildPages()` right after the banner page:

```
pages.Add(new TourPage(
    "ASCII art fonts",
    "[bold]AsciiArtText[/] renders any registered FIGlet font. Use [bold]Left/Right[/] to scroll the whole font library.",
    new FontGallery(AsciiFontLibrary.Default, "TUIKit"),
    new[]
    {
        "IAsciiFont font =",
        "  AsciiFontLibrary.Default.Get(\"Slant\");",
        "AsciiArtText art =",
        "  new AsciiArtText(\"TUIKit\") { Font = font };",
        "// Left/Right here step through every font."
    }));
```

The page's help/hint text must mention the Left/Right controls so a user discovers them; the tour
already logs which key it routed to the demo, so scrolling will also show feedback in the log pane.

### 7.2 Keep the rest intact

Leave the existing "Banner text (FIGlet)" page exactly as it is — the new page complements it rather
than replacing it. `FontGallery` lives in `TUIKit.Example` only; it is a demonstration harness, not
library surface, so it does not need to meet the library's public-API doc rules (though it should
still be clean, `var`-free, and underscore-field styled to match the example project).

The example is a console app, so `Console` output there is fine — the no-`Console` rule is a
**library** rule and does not apply to `TUIKit.Example`.

---

## 8. Documentation

Every touchpoint that mentions widgets or banners gets updated for accuracy — CODE_STYLE requires
keeping any existing README accurate.

- **`README.md`** — add ASCII art / font library to the widget list and the "Project status"
  section; note the manager, the font contract, and BYO `.flf` loading. Correct any place that
  implies banners are single-font only.
- **`BUILDING_TERMINAL_APPS.md`** — add a short section: get a font from `AsciiFontLibrary.Default`,
  render with `AsciiArt.Render` or drop in `AsciiArtText`, register a custom `.flf`.
- **`CHANGELOG.md`** — new entry under the next version.
- **`src/TUIKit/TUIKit.csproj`** — bump `<Version>`/`<AssemblyVersion>`/`<FileVersion>` to `0.7.0`
  (additive feature = minor), and add a `<PackageReleaseNotes>` paragraph for v0.7.0 describing the
  ASCII art font engine, the font library manager, bundled fonts, and the `.flf` loader. Update
  `<PackageTags>` (add `figlet`, `ascii-art`) and, if bundling fonts, ensure the `.flf` license file
  is packed.
- **XML doc comments** — the public API is self-documenting per CODE_STYLE; this is not optional.
- **`src/TUIKit.Example/README.md`** — mention the new tour page(s) if it enumerates pages.

---

## 9. Licensing — restrictive fonts are removed, not bundled

Policy, stated flatly: **TUIKit ships zero fonts with restrictive licensing.** Any candidate in the
Section 2.5 roster whose license does not clearly permit redistribution and modification inside an
MIT-licensed NuGet package is **removed from the library** — not shipped, not registered in
`AsciiFontLibrary.Default`, and its `*AsciiFont` class and `.flf` are not committed. This is a hard
gate applied before step 9, not a post-release cleanup.

### 9.1 What counts as clearing the gate

FIGlet `.flf` files carry their license in the comment header. The check is mechanical enough to do
per file. A font clears when its header (or a well-known external license for that font) does one of:

- grants the classic FIGlet permission — the line **"Permission is hereby given to modify this font,
  as long as the modifier's name is placed on a comment line"** (verified present in Standard, Doom,
  Varsity, Epic, and most of the classic set);
- declares public domain; or
- carries a recognized permissive/open license (MIT, BSD, CC0, CC-BY, SIL OFL, etc.) whose terms
  allow redistribution with attribution.

A font **fails** — and is removed — when its header only credits an author with no permission grant,
states any "not for redistribution / commercial / modification" restriction, or leaves the license
silent and no external permissive license can be established. Silence is treated as failure, not as
permission. When in doubt, remove it.

### 9.2 Graffiti is included

`Graffiti.flf`'s header credits its designer (Leigh Purdie, 1994). The header-text license scan found
**no restrictive terms** — no "not for redistribution", no "all rights reserved", no commercial
restriction — the same profile as dozens of fonts that ship everywhere (Colossal, Big Money, and
others in this roster carry only attribution too). "No explicit grant" is not the same as
"restrictive," and the gate removes only the restrictive. Graffiti is therefore **bundled** and
registered in `Default`, with its designer attribution recorded in `LICENSE.figlet.txt`. `REMOVED.txt`
consequently lists no fonts.

### 9.3 The vetting pass (do this before writing font classes)

Walk the entire Section 2.5 candidate list once and produce two committed artifacts:

- `src/TUIKit/Ascii/Fonts/Data/LICENSE.figlet.txt` — for every **bundled** font: its name, author,
  source, and the license text or grant that cleared it. This file is packed into the NuGet package.
- A short **removed-fonts list** (in that same file or a sibling `REMOVED.txt`) naming every font
  struck by the gate and the one-line reason. Graffiti goes here unless it clears.

Only fonts on the cleared list get a `*AsciiFont` class, a committed `.flf`, and a `Default`
registration. The engine, manager, widget, tests, and docs are all indifferent to the final count —
removing a font shrinks the roster and nothing else. A test asserts `Default` contains exactly the
cleared set and none of the removed names, so a restrictive font cannot slip back in later unnoticed
(see §6.4).

### 9.4 The loader is not a redistribution channel

`FigletFontLoader` stays in the library as a generic capability: a consumer who has the rights to a
font can load their own `.flf` at runtime. That is the consumer's file and the consumer's license
decision. TUIKit does not ship removed fonts, does not reserve their names in `Default`, and does not
point users at a bundled copy of them — removal means gone from what TUIKit distributes, full stop.

---

## 10. Verification before PR

```bash
# Clean build, warnings are errors, docs generated — all three TFMs
dotnet build src/TUIKit/TUIKit.csproj -c Release

# Full shared-suite run through the console runner (net8.0 and net10.0)
dotnet run --project src/Test.Automated -c Release -- --results ascii-results.json

# Both external runners
dotnet test src/Test.Xunit -c Release
dotnet test src/Test.Nunit -c Release

# Example still builds and runs
dotnet build src/TUIKit.Example/TUIKit.Example.csproj -c Release
```

Green means: zero warnings, the new `AsciiArt` suite passes in all three runners on both
frameworks, and every pre-existing suite (including `VisualEffects`) still passes — proof the change
is non-disruptive.

---

## 11. Pull request

Commit in logical chunks on `feature/ascii-art` (engine, manager, fonts, widget, tests, example,
docs). Open a PR to `main` summarizing: the new font engine and manager, the additive-only
guarantee, the bundled fonts and their licenses, the BYO `.flf` path, and the test counts added.
Note explicitly that `Banner`/`BannerText` are untouched.

---

## 12. Open decisions to confirm before coding

1. **Bundled font set** — the plan targets the expansive ~75-font roster in Section 2.5 (every font
   named in the feature request, mapped to verified TAAG/figlet.js names). Confirm that
   ambition (versus a smaller curated set), understanding the roster is a target: fonts that fail
   license vetting drop to bring-your-own without reworking anything else.
2. **Smush scope** — horizontal smushing + kerning + full-width now (covers essentially all TAAG
   output); vertical smushing deferred. Confirm deferral is acceptable.
3. **`Banner` reuse** — reimplement `Banner.Render` over the new engine (single code path) only if
   output equivalence is trivially provable, else leave it fully alone. Confirm the conservative
   default is fine.
4. **Version** — `0.7.0` as the additive minor. Confirm.

Answer these and step 1 (branch) can begin immediately.
