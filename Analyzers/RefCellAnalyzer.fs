module SageAnalyzers.RefCellAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-009"

[<Literal>]
let Name = "Ref Cell Analyzer"

[<Literal>]
let Description =
  "Detects ref cell usage in domain modules — ref is hidden mutable state that breaks referential transparency"

/// Functions that create or mutate ref cells
let refCellFunctions =
  set [
    "Microsoft.FSharp.Core.Operators.ref"
    "Microsoft.FSharp.Core.Operators.op_ColonEquals"
    "Microsoft.FSharp.Core.Operators.op_Dereference"
  ]

let collectViolations (typedTree: FSharpImplementationFileContents) =
  let violations = ResizeArray<range * string>()
  let currentModule = ref ""

  let walker =
    { new TypedTreeCollectorBase() with
        override _.WalkEntity (entity: FSharpEntity) (_decls: FSharpImplementationFileDeclaration list) =
          currentModule.Value <-
            entity.TryGetFullName()
            |> Option.defaultValue ""

        override _.WalkCall
          (_objExprOpt: FSharpExpr option)
          (mfv: FSharpMemberOrFunctionOrValue)
          (_objTypeArgs: FSharpType list)
          (_mfvTypeArgs: FSharpType list)
          (_args: FSharpExpr list)
          (m: range)
          =
          if refCellFunctions.Contains mfv.FullName then
            let modName = currentModule.Value
            if modName <> "" && not (isInfrastructureModule modName) then
              let hint =
                match mfv.FullName with
                | "Microsoft.FSharp.Core.Operators.ref" ->
                  "Use immutable values and explicit data flow instead."
                | "Microsoft.FSharp.Core.Operators.op_ColonEquals" ->
                  "Mutation via ':=' breaks purity — thread state explicitly."
                | "Microsoft.FSharp.Core.Operators.op_Dereference" ->
                  "Dereferencing via '!' implies hidden mutable state."
                | _ -> "Avoid ref cells in domain code."
              violations.Add(
                m,
                sprintf
                  "Ref cell operation '%s' in domain module '%s'. %s"
                  mfv.DisplayName
                  modName
                  hint
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
