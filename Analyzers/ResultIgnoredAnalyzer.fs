module SageAnalyzers.ResultIgnoredAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-001"

[<Literal>]
let Name = "Result Ignored Analyzer"

[<Literal>]
let Description =
  "Detects Result<'T,'E> values that are silently discarded"

let collectViolations (typedTree: FSharpImplementationFileContents) =
  let violations = ResizeArray<range * string>()

  let walker =
    { new TypedTreeCollectorBase() with

        override _.WalkSequential (firstExpr: FSharpExpr) (_secondExpr: FSharpExpr) =
          match tryGetExprType firstExpr with
          | Some t when isResultType t ->
            violations.Add(
              firstExpr.Range,
              "Result value is discarded in a sequential expression. The error case will be silently ignored."
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
              | Some t when isResultType t ->
                violations.Add(
                  m,
                  "Result value is passed to 'ignore'. The error case will be silently swallowed."
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
            | Some t when isResultType t ->
              violations.Add(
                bindingExpr.Range,
                "Result value is bound to '_' and discarded. The error case will be silently ignored."
              )
            | _ -> ()
    }

  walkTast walker typedTree
  violations |> Seq.toList

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages Code Name Severity.Warning
