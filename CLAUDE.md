# CLAUDE.md

Guidance for working in this repository. These rules are derived from `c:\code\agents\requirements\CODE_STYLE.md` and apply to every `.cs` file. They must be followed strictly.

## Project

TUIKit is a concurrent terminal UI framework for .NET. It multi-targets `netstandard2.0;net8.0;net10.0`. The build tree lives under `src/`; the phase-by-phase plan is in `archive/TUIKIT_PLAN.md`. Requirements live in `c:\code\agents\requirements` and are authoritative.

## Code style (strict)

### Layout and using directives
- `namespace` declaration at the top of the file. `using` statements go **inside** the namespace block.
- All Microsoft/standard system usings first, alphabetized; then other usings, alphabetized.
- One class or one enum per file. Never nest multiple classes or enums in a single file.

### Documentation
- XML documentation on **all** public members, constructors, and public methods.
- **No** documentation on private members or private methods.
- Document defaults, minimums, and maximums, and what different values mean, where appropriate.
- Document nullability and thread-safety guarantees in XML comments.
- Document exceptions public methods can throw with `/// <exception>` tags.

### Naming and declarations
- Private fields: underscore + PascalCase (`_FooBar`, not `_fooBar`).
- Never use `var`; always the explicit type.
- Do not use tuples unless absolutely necessary.
- Prefer configurable public members backed by private fields with reasonable defaults over magic constants.
- Public members that need range/null validation use explicit get/set over a backing field.

### Async
- Every `async` method takes a `CancellationToken` unless the class already holds a `CancellationToken` or `CancellationTokenSource` member.
- Use `.ConfigureAwait(false)` where appropriate.
- Check cancellation at sensible points.
- When a method returns `IEnumerable`, also provide an async variant taking a `CancellationToken`.

### Errors and resources
- Use specific/custom exception types, never bare `Exception`. Include contextual messages.
- Use exception filters when appropriate (`catch (X ex) when (...)`).
- Full dispose pattern (`protected virtual void Dispose(bool disposing)`), call `base.Dispose()` in derived types, use `using` for disposables.
- Nullable reference types enabled. Guard clauses at method start; `ArgumentNullException.ThrowIfNull` on modern targets, manual checks on `netstandard2.0`.
- Proactively eliminate paths where null could throw.

### Concurrency
- `Interlocked` for simple atomics; prefer `ReaderWriterLockSlim` over `lock` for read-heavy paths.

### General
- No `Console.WriteLine` (or any `Console.Write*`) in library code.
- Prefer LINQ where readability holds; `.Any()` over `.Count() > 0`; beware multiple enumeration; `.FirstOrDefault()` with null checks over `.First()`.
- Compile clean: zero errors and warnings. The library builds with `TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true`.
- Regions (`Public-Members`, `Private-Members`, `Constructors-and-Factories`, `Public-Methods`, `Private-Methods`) are not required for files under 500 lines.
- Confine `#if` to the terminal-backend abstraction and the `netstandard2.0` compat shim. No `#if` in layout/render/input core.

## Testing

Tests use the [Touchstone](https://github.com/jchristn/touchstone) descriptor framework. All suite descriptors live in `src/Test.Shared` (references `Touchstone.Core` and `TUIKit` only, never writes to the console). The same registry (`TUIKitSuites.All`) is consumed by `Test.Automated` (console runner), `Test.Xunit`, and `Test.Nunit`. Assertions are exceptions thrown on failure. Add descriptors alongside the feature they cover.
