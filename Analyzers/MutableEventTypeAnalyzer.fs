module SageAnalyzers.MutableEventTypeAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

[<Literal>]
let Code = "SAGE-002"

[<Literal>]
let Name = "Mutable Event Type Analyzer"

[<Literal>]
let Description =
  "Detects mutable fields in event types within Events modules"

let collectViolations (typedTree: FSharpImplementationFileContents) =
  let violations = ResizeArray<range * string>()

  let walker =
    { new TypedTreeCollectorBase() with
        override _.WalkEntity (entity: FSharpEntity) (_decls: FSharpImplementationFileDeclaration list) =
          let isInEventsModule =
            match entity.TryGetFullName() with
            | Some name -> name.Contains("Events")
            | None ->
              entity.DeclaringEntity
              |> Option.bind (fun e -> e.TryGetFullName())
              |> Option.map (fun n -> n.Contains("Events"))
              |> Option.defaultValue false

          if isInEventsModule then
            if entity.IsFSharpRecord then
              for field in entity.FSharpFields do
                if field.IsMutable then
                  violations.Add(
                    field.DeclarationLocation,
                    sprintf
                      "Mutable field '%s' in event type '%s'. Event types must be immutable for safe event sourcing replay."
                      field.Name
                      entity.DisplayName
                  )

            if entity.IsFSharpUnion then
              for uc in entity.UnionCases do
                for field in uc.Fields do
                  if field.IsMutable then
                    violations.Add(
                      field.DeclarationLocation,
                      sprintf
                        "Mutable field '%s' in union case '%s' of event type '%s'. Event types must be immutable."
                        field.Name
                        uc.Name
                        entity.DisplayName
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
      Severity = Severity.Error
      Range = range
      Fixes = []
    })

let analyze (typedTree: FSharpImplementationFileContents) : Message list =
  collectViolations typedTree |> toMessages
