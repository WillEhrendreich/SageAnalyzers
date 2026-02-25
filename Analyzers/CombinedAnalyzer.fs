module SageAnalyzers.CombinedAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols

[<Literal>]
let Name = "Sage Combined Analyzer"

[<Literal>]
let Description =
  "Runs all SAGE analyzers (001-009) in a single pass to avoid thread pool starvation"

let private analyzers: (FSharpImplementationFileContents -> Message list) list =
  [
    ResultIgnoredAnalyzer.analyze
    MutableEventTypeAnalyzer.analyze
    AsyncIgnoredAnalyzer.analyze
    ExceptionInDomainLayerAnalyzer.analyze
    PrimitiveObsessionAnalyzer.analyze
    MixedErrorStrategyAnalyzer.analyze
    MutableInPureFunctionAnalyzer.analyze
    ImpureProjectionAnalyzer.analyze
    RefCellAnalyzer.analyze
  ]

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  analyzers |> List.collect (fun f -> f typedTree)

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
