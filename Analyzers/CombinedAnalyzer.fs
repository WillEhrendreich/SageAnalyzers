module SageAnalyzers.CombinedAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

[<Literal>]
let Name = "Sage Combined Analyzer"

[<Literal>]
let Description =
  "Runs all SAGE analyzers (001-009) in a single pass to avoid thread pool starvation"

/// The code on a diagnostic that says an analyzer could not finish a file.
[<Literal>]
let faultCode = "SAGE-000"

let private analyzers: (string * (FSharpImplementationFileContents -> Message list)) list =
  [
    ResultIgnoredAnalyzer.Name, ResultIgnoredAnalyzer.analyze
    MutableEventTypeAnalyzer.Name, MutableEventTypeAnalyzer.analyze
    AsyncIgnoredAnalyzer.Name, AsyncIgnoredAnalyzer.analyze
    ExceptionInDomainLayerAnalyzer.Name, ExceptionInDomainLayerAnalyzer.analyze
    PrimitiveObsessionAnalyzer.Name, PrimitiveObsessionAnalyzer.analyze
    MixedErrorStrategyAnalyzer.Name, MixedErrorStrategyAnalyzer.analyze
    MutableInPureFunctionAnalyzer.Name, MutableInPureFunctionAnalyzer.analyze
    ImpureProjectionAnalyzer.Name, ImpureProjectionAnalyzer.analyze
    RefCellAnalyzer.Name, RefCellAnalyzer.analyze
  ]

/// An analyzer that threw, as a diagnostic that names it. Information, so a file the compiler
/// service could not fully translate is a note and never a failed build.
let faultMessage (where: range) (name: string) (ex: exn) : Message =
  { Type = "Sage analyzer fault"
    Message = sprintf "%s did not finish on this file and was skipped for it (%s: %s)." name (ex.GetType().Name) ex.Message
    Code = faultCode
    Severity = Severity.Info
    Range = where
    Fixes = [] }

/// Runs every analyzer on the input. One that throws is reported in its place and the rest still
/// run: the point of a single pass is that one analyzer's trouble cannot cost the others.
let runIsolated (where: range) (analyzers: (string * ('a -> Message list)) list) (input: 'a) : Message list =
  analyzers
  |> List.collect (fun (name, run) ->
    try run input
    with ex -> [ faultMessage where name ex ])

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  let start = Position.mkPos 1 0
  runIsolated (Range.mkRange typedTree.FileName start start) analyzers typedTree

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
