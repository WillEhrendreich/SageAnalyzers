module SageAnalyzers.Util

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

/// Safely get the type of an FSharpExpr, returning None if it throws
let tryGetExprType (expr: FSharpExpr) =
  try Some expr.Type
  with _ -> None

/// What the combined analyzer hands each analyzer: the file's typed tree and its source text. Most analyzers read only the tree.
type AnalysisInput =
  { TypedTree: FSharpImplementationFileContents
    SourceText: ISourceText }

/// Whether the source at `where` spells `name` as a word. A call the programmer wrote does. One the compiler added does
/// not: the rethrow after a `try ... with` whose patterns did not match, and the stub for an incomplete `match`, are typed
/// as calls to `reraise` and `raise` and sit on the range of the pattern or the match, whose text never says either word.
/// When the text cannot be read the answer is yes, so a finding is never dropped for want of evidence.
let spellsCall (source: ISourceText) (name: string) (where: range) : bool =
  try
    System.Text.RegularExpressions.Regex.IsMatch(source.GetSubTextFromRange where, @"\b" + System.Text.RegularExpressions.Regex.Escape name + @"\b")
  with _ -> true

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

/// Check if a fully-qualified name has "Events" as an exact dotted segment
let isEventsModule (name: string) =
  let parts = name.Split('.')
  parts |> Array.exists (fun p -> p = "Events")

/// Check if a function is in a projection/aggregate context
let isProjectionContext (moduleName: string) (_functionName: string) =
  moduleName.Contains("Projection")
  || moduleName.Contains("Aggregate")

/// Shared message builder for all analyzers
let toMessages code name severity (violations: (range * string) list) : Message list =
  violations
  |> List.map (fun (range, msg) ->
    {
      Type = name
      Message = msg
      Code = code
      Severity = severity
      Range = range
      Fixes = []
    })

