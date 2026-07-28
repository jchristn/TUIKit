# Conformance and CI Notes

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
