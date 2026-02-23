module SageAnalyzers.ImpureProjectionAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-008"

[<Literal>]
let Name = "Impure Projection Analyzer"

[<Literal>]
let Description =
  "Detects non-deterministic calls in projection/aggregate functions"

let collectViolations (typedTree: FSharpImplementationFileContents) =
  let violations = ResizeArray<range * string>()
  let currentModule = ref ""

  let walker =
    { new TypedTreeCollectorBase() with
        override _.WalkEntity (entity: FSharpEntity) (_decls: FSharpImplementationFileDeclaration list) =
          currentModule.Value <-
            entity.TryGetFullName()
            |> Option.defaultValue ""

        override _.WalkMemberOrFunctionOrValue
          (mfv: FSharpMemberOrFunctionOrValue)
          (_curriedArgs: FSharpMemberOrFunctionOrValue list list)
          (body: FSharpExpr)
          =
          let modName = currentModule.Value
          if isProjectionContext modName mfv.DisplayName then
            let rec walkExpr (expr: FSharpExpr) =
              match expr with
              | Call(_, calledMfv, _, _, args) ->
                if impureCallNames.Contains calledMfv.FullName then
                  violations.Add(
                    expr.Range,
                    sprintf
                      "'%s' called in projection/aggregate function '%s'. Projections must be deterministic — use event metadata timestamps instead."
                      calledMfv.DisplayName
                      mfv.DisplayName
                  )
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
              | NewObject(_, _, args) ->
                for arg in args do
                  walkExpr arg
              | _ -> ()
            walkExpr body
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
      Severity = Severity.Info
      Range = range
      Fixes = []
    })

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages
