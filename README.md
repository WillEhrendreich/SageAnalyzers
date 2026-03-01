<div align="center">

# SageAnalyzers

### Compile-time F# analyzers for domain-driven design

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/license-MIT-22c55e?style=flat-square)](LICENSE)
[![FSharp.Analyzers.SDK](https://img.shields.io/badge/analyzers_sdk-0.36.0-blue?style=flat-square)](https://github.com/ionide/FSharp.Analyzers.SDK)

</div>

9 analyzers that catch DDD and functional programming violations at compile time. Built on the [FSharp.Analyzers.SDK](https://github.com/ionide/FSharp.Analyzers.SDK) — works with Ionide (VS Code), the `fsharp-analyzers` CLI tool, and any editor that supports the SDK.

## Analyzers

| Code | Name | What it catches |
|:---|:---|:---|
| **SAGE-001** | Result Ignored | `Result<'T,'E>` values silently discarded via `ignore`, `let _ =`, or sequential expressions |
| **SAGE-002** | Mutable Event Type | `mutable` fields in event types — events must be immutable |
| **SAGE-003** | Async Ignored | `Async<'T>` / `Task<'T>` values silently discarded (fire-and-forget without intent) |
| **SAGE-004** | Exception in Domain Layer | `failwith`, `raise`, `invalidArg` in domain modules — use `Result` instead |
| **SAGE-005** | Primitive Obsession | Functions with multiple parameters of the same primitive type (e.g., `string -> string -> string`) — wrap in domain types |
| **SAGE-006** | Mixed Error Strategy | Functions that mix `try/with` AND `Result` operations — pick one strategy |
| **SAGE-007** | Mutable in Pure Function | `let mutable` bindings inside domain functions — domain logic should be pure |
| **SAGE-008** | Impure Projection | `DateTime.Now`, `Guid.NewGuid()` etc. inside projection/aggregate functions — projections must be deterministic |
| **SAGE-009** | Ref Cell | `ref` cell usage in domain modules — hidden mutable state that breaks referential transparency |

All 9 run in a **single pass** via the combined analyzer entry point, avoiding thread pool starvation.

## Quick Start

### 1. Build the analyzer DLL

```bash
git clone https://github.com/WillEhrendreich/SageAnalyzers.git
cd SageAnalyzers
dotnet build
```

### 2. Point your project at it

Add to your `.fsproj`:

```xml
<PropertyGroup>
  <OtherFlags>$(OtherFlags) --analyzers-path path/to/SageAnalyzers/bin/Debug/net10.0</OtherFlags>
</PropertyGroup>
```

Or use the `fsharp-analyzers` CLI tool:

```bash
dotnet tool install -g fsharp-analyzers
fsharp-analyzers --project ./MyApp.fsproj --analyzers-path ./path/to/SageAnalyzers/bin/Debug/net10.0
```

### 3. See warnings in your editor

With Ionide (VS Code), analyzers run automatically. You'll see warnings inline:

```
SAGE-001: Result value is discarded in a sequential expression. The error case will be silently ignored.
SAGE-004: Domain module 'Order' calls 'failwith' — use Result<'T,'E> for error handling instead.
SAGE-005: Function 'createUser' has 2 parameters of type 'string' that could be accidentally swapped. Consider domain wrapper types.
```

## Design Philosophy

These analyzers enforce a specific style of F# development:

- **Result over exceptions** — domain logic uses `Result<'T, 'TError>`, never `failwith`/`raise`
- **Immutable events** — event sourcing requires immutable event records
- **Pure domain functions** — no `mutable`, no `ref`, no `DateTime.Now` in domain logic
- **Domain types over primitives** — `UserId` instead of `Guid`, `EmailAddress` instead of `string`
- **Single error strategy** — don't mix `try/with` and `Result.bind` in the same function

The analyzers distinguish between **domain modules** (where these rules apply) and **infrastructure modules** (where exceptions, mutability, and impure calls are expected). Modules with names containing `Infrastructure`, `Handlers`, `Program`, `Startup`, or `Migrations` are automatically exempted from domain-level checks.

## Architecture

```
SageUtil.fs              — shared helpers (type checks, module classification, message builder)
Analyzers/
  ResultIgnoredAnalyzer.fs       — SAGE-001
  MutableEventTypeAnalyzer.fs    — SAGE-002
  AsyncIgnoredAnalyzer.fs        — SAGE-003
  ExceptionInDomainLayerAnalyzer.fs — SAGE-004
  PrimitiveObsessionAnalyzer.fs  — SAGE-005
  MixedErrorStrategyAnalyzer.fs  — SAGE-006
  MutableInPureFunctionAnalyzer.fs — SAGE-007
  ImpureProjectionAnalyzer.fs    — SAGE-008
  RefCellAnalyzer.fs             — SAGE-009
  CombinedAnalyzer.fs            — single entry point running all 9
```

Each analyzer is a pure function: `FSharpImplementationFileContents -> Message list`. The `CombinedAnalyzer` registers with both `[<CliAnalyzer>]` and `[<EditorAnalyzer>]` attributes.

## Contributing

Contributions welcome! Whether it's a new analyzer, better heuristics for existing ones, or test cases.

- **2 spaces for indentation** (non-negotiable)
- **Conventional Commits**: `feat(SAGE-010): add XYZ analyzer`
- Tests use Expecto — the test project is scaffolded and ready for contributions

## Used By

- [SageFs](https://github.com/WillEhrendreich/SageFs) — F# live development environment

## License

[MIT](LICENSE)
