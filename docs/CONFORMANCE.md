# Conformance and CI Notes

> **Historical snapshot.** The counts in this Phase-20 audit (e.g. "111 cases") predate later development; the suite now stands at **254 Touchstone cases**. The conformance conclusions below still hold.

Tracks how this repository satisfies the requirements in `c:\code\agents\requirements`, including items that are intentionally not applicable. This file also carries the **Phase 20 conformance audit** result.

## Phase 20 audit result (final)

Every requirement file was checked. The repository conforms, with the Docker/DockerHub/SDK items recorded as justified N/A below.

| Requirement file | Result | Evidence |
|---|---|---|
| `CODE_STYLE.md` | **PASS** | `TreatWarningsAsErrors=true` + `GenerateDocumentationFile=true` on the library; Release build clean with 0 warnings across `netstandard2.0;net8.0;net10.0`. Automated checks: no `Console.Write*`, no `var`, no tuples in library code; usings inside namespaces; private fields `_PascalCase`; one type per file; async methods take `CancellationToken` or hold one; specific exceptions with messages; full dispose pattern on resource-holders; nullable enabled with guard clauses. |
| `REPOSITORY_REQUIREMENTS.md` | **PASS** | `.gitignore`, `README.md`, `CHANGELOG.md`, `LICENSE.md` (MIT) present; all source under `src/`. Docker/DockerHub/SDK items N/A (see below). |
| `BACKEND_TEST_ARCHITECTURE.md` | **PASS** | Four projects (`Test.Shared`, `Test.Automated`, `Test.Xunit`, `Test.Nunit`) over one `TUIKitSuites.All` registry; `Test.Shared` references only `Touchstone.Core` + `TUIKit` and writes nothing to the console; exit codes 0/1; `--results` JSON export; CI workflow present. 111 cases green in all three runners. |
| `WRITING_DOCUMENTS.md` | **PASS** | README, CHANGELOG, and the example README reviewed for human voice (specific claims, varied rhythm, no formulaic "This…" openings or generic conclusions). |

`CLAUDE.md` exists at the repo root capturing the full code-style rule set, as `CODE_STYLE.md` requires.

## 1.10.0 conformance

The 1.10.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside and holds one type (`CalendarHeatMap`, `WeekdayLabelMode`, and the internal `UnifiedDiffParser`, `GitPathUnquoter`, and `DiffFile` each have their own file). `DiffView`, `HeatMap`, and `CalendarHeatMap` pass 500 lines or are close to it and use the region blocks. Every public member documents its default, range, nullability, thread safety, and exceptions; validated settings (`LevelCount`, `LevelGlyphs`, `LevelStyles`, `CellWidth`, the legend texts, `Culture`) use explicit getters and setters over `_PascalCase` fields. There is no `var`, tuple, `#if`, or `Console.Write*` in added library code (checked by scan). No custom exception type was needed: argument errors use `ArgumentNullException`, `ArgumentOutOfRangeException`, and `ArgumentException`, and the diff parser never throws on content. `BACKEND_TEST_ARCHITECTURE.md`: four new suites in `Test.Shared`, registered in `TUIKitSuites.All`, with positive and negative cases for all three items; **1115 console cases and 1116 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: no em dash in any new file or added line (checked by scan). `VERSIONING.md`: the owner asked for the release in the request; 1.10.0 is MINOR because every change is additive and each behavior change is listed under "Changed". The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0`.

## 1.9.0 conformance

The 1.9.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside and holds one type (`IValueSnapshot`, `FormFieldError`, `FormValidationMode`, `FormLabelPlacement`, `FormDirtyTracking`, `Wizard`, `WizardStep`, `WizardStepChangingEventArgs`, `WizardStepChangedEventArgs`, `WizardStepListPlacement`, `WizardOutcome`, `WizardModal`, and the internal `FormSection` each have their own file). `Form` and `Wizard` pass 500 lines and use the region blocks. Every public member documents its default, range, nullability, thread safety, and exceptions; validated settings (`LabelWidth`, `ErrorPrefix`, the button labels, `HeaderFormat`, the glyphs, `NaturalWidth`, `NaturalHeight`) use explicit getters and setters over `_PascalCase` fields. There is no `var`, tuple, `#if`, or `Console.Write*` in added library code (checked by scan). No custom exception type was needed: argument errors use `ArgumentNullException`, `ArgumentOutOfRangeException`, and `ArgumentException`. `BACKEND_TEST_ARCHITECTURE.md`: six new suites in `Test.Shared`, registered in `TUIKitSuites.All`, with positive and negative cases for both items; **1084 console cases and 1085 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: no em dash in any new file or added line (checked by scan). `VERSIONING.md`: the owner asked for the release in the request; 1.9.0 is MINOR because every change is additive and nothing changes by default. The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0`.

## 1.8.0 conformance

The 1.8.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside and holds one type (`IPasteTarget`, `DataTablePagingMode`, `DataTableState`, `DataTablePageRequest`, `DataTablePage<T>`, `DataTableLoader<T>`, and the internal `TextWords` each have their own file). `TextField`, `FuzzyList`, and `DataTable` passed 500 lines and use the region blocks. Every public member documents its default, range, nullability, thread safety, and exceptions; validated settings (`MaxLength`, `PageSize`, `PageSizes`, the paging and error formats, the glyphs, `QueryPrompt`) use explicit getters and setters over `_PascalCase` fields. There is no `var`, tuple, `#if`, or `Console.Write*` in added library code (checked by scan). `DataTableLoader<T>` is sealed, so its dispose pattern uses a private `Dispose(bool)`; it uses `ConfigureAwait(false)`, `Interlocked` for its generation counter and loading flag, and exception filters to separate cancellation from failure. Its `LoadAsync` takes a `CancellationToken`. No custom exception type was needed: argument errors use `ArgumentNullException`, `ArgumentOutOfRangeException`, and `ArgumentException`, and a wrong paging mode raises `InvalidOperationException` with the mode in the message. `BACKEND_TEST_ARCHITECTURE.md`: ten new suites in `Test.Shared`, registered in `TUIKitSuites.All`, with positive and negative cases for every item; **1052 console cases and 1053 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: no em dash in any new file or added line (checked by scan). `VERSIONING.md`: the owner asked for the release in the request; 1.8.0 is MINOR because every change is additive and each behavior change is listed under "Changed". The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0`.

## 1.7.0 conformance

The 1.7.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside and holds one type; the internal helpers (`SearchHighlighter`, `MarkdownLayoutCacheKey`) have their own files, and `Pane`'s private highlight helper moved into `SearchHighlighter` so `Pane` and `MarkdownView` share one implementation. Every public member documents its default, range, nullability, thread safety, and exceptions; `MarkdownLayoutCache.Capacity` uses an explicit getter and setter over a `_PascalCase` field; there is no `var`, tuple, `#if`, or `Console.Write*` in added library code (checked by scan). The `IReadOnlyList`-returning `MarkdownLayoutCache.GetRows` has an async variant (`GetRowsAsync`) that takes a `CancellationToken`. The cache uses one `lock` rather than `ReaderWriterLockSlim`, because every hit also updates recency, so no read path is lock-free of writes. No custom exception type was needed: argument errors use `ArgumentNullException` and `ArgumentOutOfRangeException`. `BACKEND_TEST_ARCHITECTURE.md`: six new suites in `Test.Shared`, registered in `TUIKitSuites.All`; **976 console cases and 977 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: no em dash in any new file or added line (checked by scan). `VERSIONING.md`: the owner approved the minor increment in the release request; 1.7.0 is MINOR because every change is additive and each behavior change is listed under "Changed". The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0`.

## 1.6.0 conformance

The 1.6.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside and holds one type; the internal document model (`MarkdownDocument`, `MarkdownBlock` and its seven block types, `MarkdownListItem`, `MarkdownListMarker`, `MarkdownColumnAlignment`) and the engine (`MarkdownParser`, `MarkdownInlineParser`, `MarkdownLayout`, `MarkdownTableLayout`, `MarkdownColumnSolver`, `MarkdownTextFit`) each have their own file. Every public member documents its default, range, nullability, thread safety, and exceptions; validated options use explicit getters and setters over `_PascalCase` fields; there is no `var`, tuple, `#if`, or `Console.Write*` in library code. The `IEnumerable`-returning `Render` has an async variant (`RenderAsync`) that takes a `CancellationToken`. No custom exception type was needed: the renderer never fails on content, and argument errors use the specific `ArgumentNullException`, `ArgumentOutOfRangeException`, and `ArgumentException`. `BACKEND_TEST_ARCHITECTURE.md`: ten new suites in `Test.Shared`, registered in `TUIKitSuites.All`; **934 console cases and 935 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: no em dash in any new file or added line (checked by scan). `VERSIONING.md`: the owner asked for a minor increment; 1.6.0 is MINOR because every change is additive and no default changes. The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0`.

## 1.5.0 conformance (feature/v1.5.0)

The 1.5.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside and holds one type (internal helpers such as `FrameMode`, `FramedStackSlot`, `FramedStackFrame`, and `IFocusChildren` each have their own file), every public member is documented with defaults, ranges, nullability, thread safety, and `<exception>` tags, private fields are `_PascalCase` without docs, and there is no `var`, tuple, or `Console.Write*` in library code. No `#if` was added: the text-field key contract is a companion interface (`ITextEntryKeys`) on every target rather than default interface members. `TELEMETRY_REQUIREMENTS.md`: the per-application `app.instance` metric attribute proposed in the plan was not added, because metric labels must not carry ids; applications that do not claim the terminal skip the session gauges instead. `BACKEND_TEST_ARCHITECTURE.md`: eleven new suites in `Test.Shared`, registered in `TUIKitSuites.All`; **845 console cases and 846 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: no em dashes in any changed file (checked by scan). `VERSIONING.md`: the owner asked for the version increment; 1.5.0 is MINOR because every change is additive, with changed defaults listed under "Changed" in the CHANGELOG. The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0`.

## 1.4.0 conformance (feature/v1.4.0)

The 1.4.0 release was checked against the requirements before publishing. `CODE_STYLE.md`: every new file is `namespace`-first with usings inside, holds one type, documents every public member (defaults, ranges, nullability, thread safety, and `<exception>` tags), uses `_PascalCase` private fields with no docs, and has no `var`, tuples, or `Console.Write*`; guard clauses throw specific exceptions with messages; the only new resource holders (the Example's timer-driven demos) use the dispose pattern. `#if` was not added anywhere. `BACKEND_TEST_ARCHITECTURE.md`: nine new suites in `Test.Shared` (positive and negative cases for every item, deterministic, no console output) registered in `TUIKitSuites.All`; **776 console cases and 777 xUnit/NUnit cases pass on net8.0 and net10.0**. `WRITING_DOCUMENTS.md`: added prose uses no em dashes or smart quotes (checked by scan). `VERSIONING.md`: the MINOR bump to 1.4.0 was approved by the owner before the version fields changed. The solution builds with 0 warnings and 0 errors on `netstandard2.0`, `net8.0`, and `net10.0` with `TreatWarningsAsErrors`.

## 0.6.0 conformance (feature/v0.6.0)

The 0.6.0 horizontal components were built to the same bar. Every new file follows `CODE_STYLE.md`: `namespace`-first with usings inside; one public type per file (`DialogModal`, `CheckList<T>`, `MultiSelectModal<T>`, `DefinitionList`, `DefinitionRow`, `ActivityIndicator`, `StreamingTranscript`, `ActionListView<T>`, `ListAction<T>`, `ReorderableList<T>`, `Command`, `CommandRegistry`, `ISuggestionProvider`, `PrefixSuggestionProvider`, `AutocompleteOverlay`, `IScrollExtent`, `Rule`, `HintText`, `ColumnFormatter`, `SubmitKeyResolver`, `SubmitDecision`, and the generic `ListView<T>`/`FuzzyList<T>`); XML docs on every public member with `<exception>` tags; `_PascalCase` fields with no docs; no `var`, no tuples, no `Console.Write*`; guard clauses throwing specific exceptions; and the async suggestion path takes a `CancellationToken` and uses `ConfigureAwait`. The whole solution builds clean (0 warnings, `TreatWarningsAsErrors=true`) across `netstandard2.0;net8.0;net10.0`, and **363 Touchstone cases pass in the console, xUnit, and NUnit runners on net8.0 and net10.0**. The one breaking change (generic list widgets) is recorded in the CHANGELOG `Breaking` section with a migration note, consistent with the project's alpha `0.x` policy.

The rest of this document is the running record maintained during the build.

## Repository requirements (`REPOSITORY_REQUIREMENTS.md`)

| Item | Requirement | Status | Notes |
|---|---|---|---|
| 1 | Reasonable `.gitignore` | Done | VS/.NET, test, coverage, NuGet outputs. |
| 2 | `.dockerignore` for Docker projects | N/A | TUIKit is a library and example app; it produces no container image. |
| 3 | `README.md` | Done (skeleton) | Finalized in Phase 18. |
| 4 | `DOCKERHUB_README.md` for Docker Hub | N/A | Nothing is published to Docker Hub; there is no image. |
| 5 | `CHANGELOG.md` | Done | Keep-a-Changelog format. |
| 6 | Source under `src/`, `test/`, `dashboard/`, or `sdk/` | Done | All code under `src/`. |
| 7 | SDK per language under `sdk/{language}` | N/A | No SDK surface; TUIKit is consumed directly as a NuGet package. |
| 8 | `LICENSE.md` (MIT unless stated) | Done | MIT. |
| 9 | Docker uses `.yaml`, build contexts | N/A (partial) | No Docker assets. The CI workflow uses the `.yaml` extension per the spirit of the rule. |

## Code style (`CODE_STYLE.md`)

Enforced continuously via `TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true` on the library, and reviewed rule-by-rule in Phase 20. `CLAUDE.md` captures the full rule set.

## Test architecture (`BACKEND_TEST_ARCHITECTURE.md`)

Four projects present: `Test.Shared` (Touchstone.Core + TUIKit only, no console output), `Test.Automated` (Touchstone.Cli console runner, `--results` JSON export, exit 0/1), `Test.Xunit` (Fact + Theory), `Test.Nunit` (Fact + TestCaseSource). All consume the shared `TUIKitSuites.All` registry.

## CI matrix

| Job | OS | TFMs | Coverage |
|---|---|---|---|
| test | ubuntu-latest | net8.0, net10.0 | Automated + xUnit + NUnit (automated) |
| tmux | ubuntu-latest | net8.0 | Interactive smoke under tmux (added when input harness lands) |
| fx-smoke | windows-latest | library .NET Framework build | Compile-only smoke (automated) |

Manual smoke testing (not automated): tier-1 terminals per OS (Windows Terminal, iTerm2/Ghostty, Alacritty/kitty/WezTerm), and behavior over live SSH.
