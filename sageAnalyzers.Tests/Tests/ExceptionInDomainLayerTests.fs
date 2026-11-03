module SageAnalyzers.Tests.ExceptionInDomainLayerTests

open Expecto
open Expecto.Flip
open FSharp.Analyzers.SDK

// SAGE-004 is meant to flag a `raise`, `failwith` or `reraise` that the programmer wrote in a domain module. Run
// over a real codebase it also reported the rethrow the compiler adds when a `try ... with` pattern does not match
// and the stub it adds for an incomplete `match`, on lines that spell none of those words. These tests compile real
// snippets and ask the real analyzer.

/// The target framework the snippets are compiled against. The analyzer reads typed trees, which do not depend on it,
/// so this is whichever reference pack the machine running the tests has.
let private snippetFramework = "net11.0"

let private options = lazy ((Testing.mkOptionsFromProject snippetFramework []).GetAwaiter().GetResult())

/// The lines (1-based) SAGE-004 reports for a snippet.
let private hitLines (source: string) : int list =
  let ctx = Testing.getContext options.Value source
  match ctx.TypedTree with
  | None -> failtest "the snippet did not produce a typed tree"
  | Some tree -> SageAnalyzers.ExceptionInDomainLayerAnalyzer.analyze ctx.SourceText tree |> List.map (fun m -> m.Range.StartLine)

[<Tests>]
let tests =
  testList "SAGE-004 ExceptionInDomainLayerAnalyzer" [

    test "a raise the programmer wrote is reported, on its own line" {
      hitLines "module Domain.Probe\nlet g () : int = raise (System.Exception \"x\")\n"
      |> Expect.equal "line 2" [ 2 ]
    }

    test "a failwith the programmer wrote is reported, on its own line" {
      hitLines "module Domain.Probe\nlet h () : int = failwith \"x\"\n"
      |> Expect.equal "line 2" [ 2 ]
    }

    test "a reraise the programmer wrote is reported, on its own line" {
      hitLines "module Domain.Probe\nlet r () : int =\n  try 1\n  with e -> reraise ()\n"
      |> Expect.equal "line 4" [ 4 ]
    }

    test "WHY — plain arithmetic is not a raise" {
      hitLines "module Domain.Probe\nlet times4 (p: int) = p * 4\n"
      |> Expect.equal "nothing" []
    }

    test "WHY — the rethrow the compiler adds when a with-pattern does not match is not a reraise the programmer wrote" {
      hitLines "module Domain.Probe\nlet f () : int =\n  try 1\n  with :? System.ArgumentException -> 2\n"
      |> Expect.equal "nothing" []
    }

    test "WHY — an incomplete match is not a raise the programmer wrote" {
      hitLines "module Domain.Probe\nlet f (x: int option) =\n  match x with\n  | Some v -> v\n"
      |> Expect.equal "nothing" []
    }

    test "WHY — a raise the programmer wrote inside a try is still reported when the with-pattern is partial" {
      hitLines "module Domain.Probe\nlet f () : int =\n  try raise (System.Exception \"x\")\n  with :? System.ArgumentException -> 2\n"
      |> Expect.equal "the raise on line 3 and nothing for the implicit rethrow" [ 3 ]
    }
  ]
