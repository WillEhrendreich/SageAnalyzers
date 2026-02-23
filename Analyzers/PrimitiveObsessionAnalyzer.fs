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
  "Detects public functions with primitive parameter types in domain modules"

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
              for paramGroup in mfv.CurriedParameterGroups do
                for param in paramGroup do
                  if isPrimitiveType param.Type then
                    violations.Add(
                      mfv.DeclarationLocation,
                      sprintf
                        "Parameter '%s' of type '%s' in public function '%s'. Consider using a domain-specific type wrapper."
                        param.DisplayName
                        (resolveTypeName param.Type)
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

[<CliAnalyzer(Name, Description)>]
let cliAnalyzer (ctx: CliContext) : Async<Message list> =
  async {
    return
      ctx.TypedTree
      |> Option.map analyze
      |> Option.defaultValue []
  }

[<EditorAnalyzer(Name, Description)>]
let editorAnalyzer (ctx: EditorContext) : Async<Message list> =
  async {
    return
      ctx.TypedTree
      |> Option.map analyze
      |> Option.defaultValue []
  }
