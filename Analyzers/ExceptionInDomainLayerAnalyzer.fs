module SageAnalyzers.ExceptionInDomainLayerAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-004"

[<Literal>]
let Name = "Exception In Domain Layer Analyzer"

[<Literal>]
let Description =
  "Detects exception-throwing calls in domain modules where Result should be used"

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
          if exceptionFunctions.Contains mfv.FullName then
            let modName = currentModule.Value
            if modName <> "" && not (isInfrastructureModule modName) then
              violations.Add(
                m,
                sprintf
                  "'%s' used in domain module '%s'. Domain logic should return Result<_,_> instead of throwing exceptions."
                  mfv.DisplayName
                  modName
              )
    }

  walkTast walker typedTree
  violations |> Seq.toList

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages Code Name Severity.Warning
