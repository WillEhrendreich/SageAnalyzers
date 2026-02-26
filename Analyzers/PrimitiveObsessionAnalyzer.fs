module SageAnalyzers.PrimitiveObsessionAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open SageAnalyzers.Util

[<Literal>]
let Code = "SAGE-005"

[<Literal>]
let Name = "Primitive Obsession Analyzer"

[<Literal>]
let Description =
  "Detects public functions with swappable primitive parameters of the same type"

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
          (_body: FSharpExpr)
          =
          let modName = currentModule.Value
          if modName <> "" && not (isInfrastructureModule modName) then
            if mfv.Accessibility.IsPublic then
              let primitiveParams =
                mfv.CurriedParameterGroups
                |> Seq.collect id
                |> Seq.filter (fun p -> isPrimitiveType p.Type)
                |> Seq.map (fun p -> resolveTypeName p.Type, p.DisplayName)
                |> Seq.toList
              let duplicateTypes =
                primitiveParams
                |> List.map fst
                |> List.countBy id
                |> List.filter (fun (_, count) -> count >= 2)
              for (typeName, _) in duplicateTypes do
                let paramNames =
                  primitiveParams
                  |> List.filter (fun (t, _) -> t = typeName)
                  |> List.map snd
                  |> String.concat ", "
                violations.Add(
                  mfv.DeclarationLocation,
                  sprintf
                    "Parameters [%s] are all '%s' in public function '%s'. Swappable primitives — consider domain-specific type wrappers."
                    paramNames
                    typeName
                    mfv.DisplayName
                )
    }

  walkTast walker typedTree
  violations |> Seq.toList

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages Code Name Severity.Warning
