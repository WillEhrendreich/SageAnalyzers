module SageAnalyzers.Util

open FSharp.Compiler.Symbols

/// Safely get the type of an FSharpExpr, returning None if it throws
let tryGetExprType (expr: FSharpExpr) =
  try Some expr.Type
  with _ -> None

/// Check if an FSharpType is Result<_,_>
let isResultType (t: FSharpType) =
  if t.HasTypeDefinition then
    match t.TypeDefinition.TryGetFullName() with
    | Some "Microsoft.FSharp.Core.FSharpResult`2" -> true
    | _ -> false
  else
    false

/// Check if an FSharpType is Async<_> or Task<_> (non-unit)
let isAsyncOrTaskType (t: FSharpType) =
  if t.HasTypeDefinition then
    match t.TypeDefinition.TryGetFullName() with
    | Some "Microsoft.FSharp.Control.FSharpAsync`1" -> true
    | Some "System.Threading.Tasks.Task`1" -> true
    | Some "System.Threading.Tasks.ValueTask`1" -> true
    | _ -> false
  else
    false

/// Infrastructure namespace segments that exempt code from domain-level checks
let infrastructureNamespaces =
  set [
    "Infrastructure"; "Handlers"; "Program"; "Startup"; "Migrations"
  ]

/// Check if a fully-qualified module name belongs to infrastructure
let isInfrastructureModule (fullName: string) =
  let parts = fullName.Split('.')
  parts |> Array.exists (fun p -> infrastructureNamespaces.Contains p)

/// Primitive .NET type full names
let primitiveTypeNames =
  set [
    "System.String"; "System.Int32"; "System.Int64"
    "System.Guid"; "System.Decimal"; "System.Double"
    "System.Single"; "System.Boolean"
  ]

/// Resolve through F# type abbreviations to check for primitive types
let rec isPrimitiveType (t: FSharpType) =
  if t.HasTypeDefinition then
    let td = t.TypeDefinition
    match td.TryGetFullName() with
    | Some name when primitiveTypeNames.Contains name -> true
    | _ ->
      if td.IsFSharpAbbreviation then isPrimitiveType td.AbbreviatedType
      else false
  else
    false

/// Resolve through abbreviations to get the underlying type name
let rec resolveTypeName (t: FSharpType) =
  if t.HasTypeDefinition then
    let td = t.TypeDefinition
    match td.TryGetFullName() with
    | Some name -> name
    | None ->
      if td.IsFSharpAbbreviation then resolveTypeName td.AbbreviatedType
      else td.DisplayName
  else
    t.ToString()

/// Known exception-throwing function full names
let exceptionFunctions =
  set [
    "Microsoft.FSharp.Core.Operators.failwith"
    "Microsoft.FSharp.Core.Operators.failwithf"
    "Microsoft.FSharp.Core.Operators.invalidArg"
    "Microsoft.FSharp.Core.Operators.invalidOp"
    "Microsoft.FSharp.Core.Operators.raise"
    "Microsoft.FSharp.Core.Operators.reraise"
  ]

/// Result module function full names
let resultModuleFunctions =
  set [
    "Microsoft.FSharp.Core.Result.map"
    "Microsoft.FSharp.Core.Result.mapError"
    "Microsoft.FSharp.Core.Result.bind"
    "Microsoft.FSharp.Core.Result.isOk"
    "Microsoft.FSharp.Core.Result.isError"
    "Microsoft.FSharp.Core.Result.defaultValue"
    "Microsoft.FSharp.Core.Result.defaultWith"
    "Microsoft.FSharp.Core.Result.toOption"
    "Microsoft.FSharp.Core.Result.toValueOption"
  ]

/// Known impure function full names (non-deterministic)
let impureCallNames =
  set [
    "System.DateTime.get_Now"
    "System.DateTime.get_UtcNow"
    "System.DateTimeOffset.get_Now"
    "System.DateTimeOffset.get_UtcNow"
    "System.Guid.NewGuid"
  ]

/// Check if a function is in a projection/aggregate context
let isProjectionContext (moduleName: string) (functionName: string) =
  moduleName.Contains("Projection")
  || moduleName.Contains("Aggregate")
  || functionName = "Apply"
  || functionName = "Create"
  || functionName = "Evolve"
  || functionName = "Fold"

