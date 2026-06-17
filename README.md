# FixedWrapDiff

FixedWrapDiff is a small WinForms tool for comparing fixed-length text files after virtually wrapping each original line at a specified column count.

## Features

- Compare two text files without modifying the originals.
- Split each original line into virtual wrapped lines.
- Show original line number, wrap index, and source column range.
- Highlight different virtual lines.
- Highlight differing character positions inside changed lines.
- Switch between side-by-side and top-bottom views.
- Synchronize vertical and horizontal scrolling.
- Read files as Shift_JIS or UTF-8.

## Requirements

- Windows
- .NET 8 Desktop Runtime, or a self-contained published build

## Build

```powershell
dotnet build
```

## Run

```powershell
dotnet run
```

## Publish

Framework-dependent:

```powershell
dotnet publish -c Release
```

Self-contained x64 build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```
