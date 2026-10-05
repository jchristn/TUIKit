# TUIKit `dotnet new` templates

Project templates for scaffolding TUIKit apps with the .NET CLI.

## Install

From the repository root:

```bash
dotnet new install ./templates
```

Or install the published template package (once available on NuGet):

```bash
dotnet new install TUIKit.Templates
```

## Use

```bash
dotnet new tuikit-app -n MyTerminalApp
cd MyTerminalApp
dotnet run
```

Options:

- `--Framework net8.0|net10.0`: target framework (default `net8.0`).

The generated project references the `TUIKit` NuGet package (1.4 or later) and lays out a
header, a list and a notes field in framed regions, and a footer. The focused region draws a
heavy frame, `Tab` moves focus, the footer lists the keys that work for whatever has focus, and
`Ctrl+Q` quits. Edit `Program.cs` to build your interface.

## Uninstall

```bash
dotnet new uninstall ./templates
```
