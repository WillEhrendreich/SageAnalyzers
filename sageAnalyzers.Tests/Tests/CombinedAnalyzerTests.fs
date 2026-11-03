module SageAnalyzers.Tests.CombinedAnalyzerTests

open Expecto
open Expecto.Flip
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open SageAnalyzers

let finding (code: string) : Message =
  { Type = "probe"
    Message = "a finding"
    Code = code
    Severity = Severity.Warning
    Range = Range.range0
    Fixes = [] }

let codes (messages: Message list) : string list = messages |> List.map (fun m -> m.Code)

[<Tests>]
let tests =
  testList "CombinedAnalyzer.runIsolated" [

    test "every analyzer's findings come through, in the order the analyzers are listed" {
      let analyzers =
        [ "First", (fun () -> [ finding "SAGE-001" ])
          "Second", (fun () -> [ finding "SAGE-002"; finding "SAGE-003" ]) ]
      CombinedAnalyzer.runIsolated Range.range0 analyzers ()
      |> codes
      |> Expect.equal "findings in order, no fault" [ "SAGE-001"; "SAGE-002"; "SAGE-003" ]
    }

    test "WHY — an analyzer that throws is reported, and the ones after it still run" {
      let analyzers =
        [ "First", (fun () -> [ finding "SAGE-001" ])
          "Broken", (fun () -> failwith "boom")
          "Last", (fun () -> [ finding "SAGE-009" ]) ]
      CombinedAnalyzer.runIsolated Range.range0 analyzers ()
      |> codes
      |> Expect.equal "the broken one is a fault in its place; the others are untouched"
           [ "SAGE-001"; CombinedAnalyzer.faultCode; "SAGE-009" ]
    }

    test "a fault names the analyzer and what it threw, at the range the caller gave, as information" {
      let where = Range.mkRange "Some.fs" (Position.mkPos 1 0) (Position.mkPos 1 0)
      let fault =
        CombinedAnalyzer.runIsolated where [ "Broken", (fun () -> invalidOp "cannot translate") ] ()
        |> List.exactlyOne
      fault.Message |> Expect.stringContains "names the analyzer" "Broken"
      fault.Message |> Expect.stringContains "says what it threw" "cannot translate"
      fault.Range |> Expect.equal "at the caller's range" where
      fault.Severity |> Expect.equal "information, not a build failure" Severity.Info
    }

    test "several analyzers can fault in one file, each reported once" {
      let analyzers =
        [ "One", (fun () -> failwith "a")
          "Two", (fun () -> failwith "b") ]
      CombinedAnalyzer.runIsolated Range.range0 analyzers ()
      |> List.length
      |> Expect.equal "two faults" 2
    }
  ]
