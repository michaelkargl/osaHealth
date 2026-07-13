module osaHealth.Framework.Tests.String_Tests

open Xunit
open osaHealth.Framework
open osaHealth.Framework.Testing.Bdd.Scenario
open osaHealth.Framework.Testing.Bdd.Xunit

let tryFromBase64Cases: obj[] seq =
    seq {
        [| "AQID" :> obj; Some [| 1uy; 2uy; 3uy |] :> obj |] // no padding, 4 chars → 3 bytes
        [| "AAAA" :> obj; Some [| 0uy; 0uy; 0uy |] :> obj |] // zero bytes, no padding
        [| "AA==" :> obj; Some [| 0uy |] :> obj |] // padding reduces output to 1 byte
        [| "" :> obj; Some Array.empty<byte> :> obj |] // empty input → empty output
        [| "!!!" :> obj; None :> obj |] // invalid base64 characters → None
    }

[<Theory>]
[<MemberData(nameof tryFromBase64Cases)>]
let ``tryFromBase64 decodes base64 to bytes`` (input: string) (expected: byte[] option) =
    let context =
        {| Input = input
           Expected = expected
           Result = (None: byte[] option) |}

    Factory.create context
    |> GIVEN $"base64 input '{input}'" id
    |> WHEN "decoding to a byte array" (fun ctx ->
        {| ctx with
            Result = StringUtil.tryFromBase64 ctx.Input |})
    |> THEN "the result matches the expected bytes" (fun ctx ->
        match ctx.Expected with
        | None -> Assert.Equal<byte[] option>(None, ctx.Result)
        | Some expectedBytes ->
            Assert.NotEqual(None, ctx.Result)
            Assert.Equal<byte[]>(expectedBytes, ctx.Result.Value)

        ctx)
    |> run
