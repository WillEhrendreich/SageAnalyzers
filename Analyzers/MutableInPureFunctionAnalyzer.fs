module SageAnalyzers.MutableInPureFunctionAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-007"

[<Literal>]
let Name = "Mutable In Pure Function Analyzer"

[<Literal>]
let Description =
  "Detects mutable bindings inside functions in domain modules"

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
          if modName <> "" && not (isInfrastructureModule modName) then
            let rec walkExpr (expr: FSharpExpr) =
              match expr with
              | Let((bindingVar, bindingExpr, _), bodyExpr) ->
                if bindingVar.IsMutable then
                  violations.Add(
                    bindingVar.DeclarationLocation,
                    sprintf
                      "Mutable binding '%s' in domain function '%s'. Domain functions should be pure — use recursion or fold instead."
                      bindingVar.DisplayName
                      mfv.DisplayName
                  )
                walkExpr bindingExpr
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
              | TryWith(tryBody, _, _, _, catchBody, _, _) ->
                walkExpr tryBody
                walkExpr catchBody
              | Call(_, _, _, _, args) ->
                for arg in args do walkExpr arg
              | Application(funcExpr, _, argExprs) ->
                walkExpr funcExpr
                for arg in argExprs do walkExpr arg
              | DecisionTree(root, cases) ->
                walkExpr root
                for (_, caseExpr) in cases do walkExpr caseExpr
              | _ -> ()
            walkExpr body
    }

  walkTast walker typedTree
  violations |> Seq.toList

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages Code Name Severity.Warning
