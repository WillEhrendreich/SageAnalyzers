module SageAnalyzers.MixedErrorStrategyAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-006"

[<Literal>]
let Name = "Mixed Error Strategy Analyzer"

[<Literal>]
let Description =
  "Detects functions that mix try/with AND Result operations"

let collectViolations (typedTree: FSharpImplementationFileContents) =
  let violations = ResizeArray<range * string>()

  let walker =
    { new TypedTreeCollectorBase() with
        override _.WalkMemberOrFunctionOrValue
          (mfv: FSharpMemberOrFunctionOrValue)
          (_curriedArgs: FSharpMemberOrFunctionOrValue list list)
          (body: FSharpExpr)
          =
          let hasTryWith = ref false
          let hasResultOp = ref false

          let rec walkExpr (expr: FSharpExpr) =
            match expr with
            | TryWith(tryBody, _, _, _, catchBody, _, _) ->
              hasTryWith.Value <- true
              walkExpr tryBody
              walkExpr catchBody
            | Call(_, calledMfv, _, _, args) ->
              if resultModuleFunctions.Contains calledMfv.FullName then
                hasResultOp.Value <- true
              for arg in args do
                walkExpr arg
            | Let((_, letExpr, _), bodyExpr) ->
              walkExpr letExpr
              walkExpr bodyExpr
            | Sequential(e1, e2) ->
              walkExpr e1
              walkExpr e2
            | IfThenElse(cond, ifTrue, ifFalse) ->
              walkExpr cond
              walkExpr ifTrue
              walkExpr ifFalse
            | Lambda(_, lambdaBody) ->
              walkExpr lambdaBody
            | Application(funcExpr, _, argExprs) ->
              walkExpr funcExpr
              for arg in argExprs do
                walkExpr arg
            | _ -> ()

          walkExpr body

          if hasTryWith.Value && hasResultOp.Value then
            violations.Add(
              mfv.DeclarationLocation,
              sprintf
                "Function '%s' mixes try/with AND Result operations. Pick one error handling strategy per function."
                mfv.DisplayName
            )
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
      Severity = Severity.Warning
      Range = range
      Fixes = []
    })

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages
