# Copilot Instructions for sageAnalyzers

## Project Overview

F# console application targeting **net10.0**. Single-project solution (`sageAnalyzers.slnx`).

The project uses F# compiler flags for graph-based checking (`--test:GraphBasedChecking`, `--test:DumpCheckingGraph`) and compile-time profiling (`--times:report.csv`). Output paths are flattened (no TFM/RID subdirectories).

## Build & Run

```shell
dotnet build
dotnet run
dotnet watch run    # hot-reload during development
dotnet publish      # single-file, ReadyToRun AOT
```

## Conventions

- **2-space indentation** — never 4 spaces
- **Functional F# idioms**: immutable types, pattern matching, pipeline operators (`|>`), Result monads for error handling
- **Conventional Commits**: `type(scope): description` (feat, fix, refactor, docs, test, chore, etc.)
- Solution uses `.slnx` format (not `.sln`)

## Git Hooks

A pre-push hook auto-increments the patch version in the `.fsproj` and creates a git tag. Install with:

```powershell
pwsh hooks/install-hooks.ps1
```
