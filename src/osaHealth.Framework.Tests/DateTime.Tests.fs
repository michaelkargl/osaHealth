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

        [| validBytes :> obj; 0 :> obj; validDate |> Some :> obj |]

        let minDate = DateTime.MinValue
        let minBytes = BitConverter.GetBytes(minDate.Ticks)

        [| minBytes :> obj; 0 :> obj; Some minDate :> obj |]

        let maxDate = DateTime.MaxValue
        let maxBytes = BitConverter.GetBytes(maxDate.Ticks)

        [| maxBytes :> obj; 0 :> obj; maxDate |> Some :> obj |]

        [| BitConverter.GetBytes(-1L) :> obj; 0 :> obj; None :> obj |] // ticks below min
        [| BitConverter.GetBytes(Int64.MaxValue) :> obj; 0 :> obj; None :> obj |] // ticks above max
        [| [| 1uy; 2uy; 3uy |] :> obj; 0 :> obj; None :> obj |] // insufficient bytes
        [| [| 1uy; 2uy; 3uy |] :> obj; 1 :> obj; None :> obj |] // offset beyond buffer
        [| validBytes :> obj; 8 :> obj; None :> obj |] // offset too large
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
