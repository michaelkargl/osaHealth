module osaHealth.Framework.Tests.DateTime_Tests

open System
open Xunit
open osaHealth.Framework
open osaHealth.Framework.Testing.Bdd.Scenario
open osaHealth.Framework.Testing.Bdd.Xunit

let tryParseUtcCases: obj[] seq =
    seq {
        let validDate = DateTime.UtcNow
        let validBytes = BitConverter.GetBytes(validDate.Ticks)

        [| box validBytes; box 0; validDate |> Some |> box |]

        let minDate = DateTime.MinValue
        let minBytes = BitConverter.GetBytes(minDate.Ticks)

        [| box minBytes; box 0; Some minDate |> box |]

        let maxDate = DateTime.MaxValue
        let maxBytes = BitConverter.GetBytes(maxDate.Ticks)

        [| box maxBytes; box 0; maxDate |> Some |> box |]

        [| BitConverter.GetBytes(-1L) |> box; box 0; box None |] // ticks below min
        [| box (BitConverter.GetBytes(Int64.MaxValue)); box 0; box None |] // ticks above max
        [| box [| 1uy; 2uy; 3uy |]; box 0; box None |] // insufficient bytes
        [| box [| 1uy; 2uy; 3uy |]; box 1; box None |] // offset beyond buffer
        [| box validBytes; box 8; box None |] // offset too large
    }

[<Theory>]
[<MemberData(nameof tryParseUtcCases)>]
let ``tryParseUtc parses bytes to DateTime`` (bytes: byte[]) (offset: int) (expected: DateTime option) =
    let context =
        {| Bytes = Array.empty
           Offset = -1
           Expected = expected
           Result = None |}

    Factory.create context
    |> GIVEN $"bytes of length {bytes.Length}" (fun ctx -> {| ctx with Bytes = bytes |})
    |> AND $"offset {offset}" (fun ctx -> {| ctx with Offset = offset |})
    |> WHEN "parsing to a DateTime" (fun ctx ->
        {| ctx with
            Result = DateTime.tryParseUtc ctx.Bytes ctx.Offset |})
    |> THEN "the result matches the expected DateTime" (fun ctx ->
        Assert.Equal(ctx.Expected, ctx.Result)
        ctx)
    |> run
