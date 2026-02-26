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
          let rec walkExpr (expr: FSharpExpr) : struct(bool * bool) =
            match expr with
            | TryWith(tryBody, _, _, _, catchBody, _, _) ->
              let struct(_, r1) = walkExpr tryBody
              let struct(_, r2) = walkExpr catchBody
              struct(true, r1 || r2)
            | Call(_, calledMfv, _, _, args) ->
              let hasResult = resultModuleFunctions.Contains calledMfv.FullName
              let struct(t, r) =
                args |> List.fold (fun (struct(at, ar)) arg ->
                  let struct(ct, cr) = walkExpr arg
                  struct(at || ct, ar || cr)) (struct(false, false))
              struct(t, r || hasResult)
            | Let((_, letExpr, _), bodyExpr) ->
              let struct(t1, r1) = walkExpr letExpr
              let struct(t2, r2) = walkExpr bodyExpr
              struct(t1 || t2, r1 || r2)
            | Sequential(e1, e2) ->
              let struct(t1, r1) = walkExpr e1
              let struct(t2, r2) = walkExpr e2
              struct(t1 || t2, r1 || r2)
            | IfThenElse(cond, ifTrue, ifFalse) ->
              let struct(t1, r1) = walkExpr cond
              let struct(t2, r2) = walkExpr ifTrue
              let struct(t3, r3) = walkExpr ifFalse
              struct(t1 || t2 || t3, r1 || r2 || r3)
            | Lambda(_, lambdaBody) ->
              walkExpr lambdaBody
            | Application(funcExpr, _, argExprs) ->
              let struct(t1, r1) = walkExpr funcExpr
              let struct(t2, r2) =
                argExprs |> List.fold (fun (struct(at, ar)) arg ->
                  let struct(ct, cr) = walkExpr arg
                  struct(at || ct, ar || cr)) (struct(false, false))
              struct(t1 || t2, r1 || r2)
            | DecisionTree(root, cases) ->
              let struct(t1, r1) = walkExpr root
              let struct(t2, r2) =
                cases |> List.fold (fun (struct(at, ar)) (_, caseExpr) ->
                  let struct(ct, cr) = walkExpr caseExpr
                  struct(at || ct, ar || cr)) (struct(false, false))
              struct(t1 || t2, r1 || r2)
            | _ -> struct(false, false)

          let struct(hasTryWith, hasResultOp) = walkExpr body

          if hasTryWith && hasResultOp then
            violations.Add(
              mfv.DeclarationLocation,
              sprintf
                "Function '%s' mixes try/with AND Result operations. Pick one error handling strategy per function."
                mfv.DisplayName
            )
    }

  walkTast walker typedTree
  violations |> Seq.toList

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages Code Name Severity.Warning
