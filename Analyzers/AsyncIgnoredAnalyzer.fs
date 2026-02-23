module SageAnalyzers.AsyncIgnoredAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-003"

[<Literal>]
let Name = "Async Ignored Analyzer"

[<Literal>]
let Description =
  "Detects Async<'T>/Task<'T> values that are silently discarded"

let collectViolations (typedTree: FSharpImplementationFileContents) =
  let violations = ResizeArray<range * string>()

  let walker =
    { new TypedTreeCollectorBase() with

        override _.WalkSequential (firstExpr: FSharpExpr) (_secondExpr: FSharpExpr) =
          match tryGetExprType firstExpr with
          | Some t when isAsyncOrTaskType t ->
            violations.Add(
              firstExpr.Range,
              "Async/Task value is discarded in a sequential expression. The operation may never execute."
            )
          | _ -> ()

        override _.WalkCall
          (_objExprOpt: FSharpExpr option)
          (mfv: FSharpMemberOrFunctionOrValue)
          (_objTypeArgs: FSharpType list)
          (_mfvTypeArgs: FSharpType list)
          (args: FSharpExpr list)
          (m: range)
          =
          if mfv.FullName = "Microsoft.FSharp.Core.Operators.ignore" then
            match args with
            | [ arg ] ->
              match tryGetExprType arg with
              | Some t when isAsyncOrTaskType t ->
                violations.Add(
                  m,
                  "Async/Task value is passed to 'ignore'. The operation may never execute or its result is lost."
                )
              | _ -> ()
            | _ -> ()

        override _.WalkLet
          (bindingVar: FSharpMemberOrFunctionOrValue)
          (bindingExpr: FSharpExpr)
          (_bodyExpr: FSharpExpr)
          =
          if bindingVar.DisplayName = "_" then
            match tryGetExprType bindingExpr with
            | Some t when isAsyncOrTaskType t ->
              violations.Add(
                bindingExpr.Range,
                "Async/Task value is bound to '_' and discarded. The operation may never execute."
              )
            | _ -> ()
    }

  walkTast walker typedTree
  violations |> Seq.toList

let toMessages (violations: (range * string) list) : Message list =
  violations
  |> List.map (fun (range, msg) ->
    {
      Type = Name
      Message = msg
      Code = Code
      Severity = Severity.Error
      Range = range
      Fixes = []
    })

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages
