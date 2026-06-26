module osaHealth.Framework.Tests.Guid_Tests

open Xunit
open osaHealth.Framework
open osaHealth.Framework.Testing.Bdd.Scenario

let tryParseCases: obj[] seq =
    seq {
        let validGuid = System.Guid.NewGuid()
        let validBytes = validGuid.ToByteArray()
        [| box validBytes; box 0; Some validGuid |> box |]
        
        // Guid with offset
        let anotherGuid = System.Guid.NewGuid()
        let anotherBytes = anotherGuid.ToByteArray()
        let bufferWith24Bytes = Array.append (System.BitConverter.GetBytes(1L)) anotherBytes
        [| box bufferWith24Bytes; box 8; Some anotherGuid |> box |]
        
        [| box [| 1uy; 2uy; 3uy |]; box 0; box None |] // insufficient bytes
        [| box [| 1uy; 2uy; 3uy |]; box 1; box None |] // offset beyond buffer
        [| box [| 1uy; 2uy; 3uy |]; box -1; box None |] // offset too small
        [| box [| 1uy; 2uy; 3uy |]; box 8; box None |] // offset too large
    }

[<Theory>]
[<MemberData(nameof tryParseCases)>]
let ``tryParse parses bytes to Guid`` (bytes: byte[]) (offset: int) (expected: System.Guid option) =
    let context =
        {| Bytes = bytes
           Offset = offset
           Expected = expected
           Result = None |}

    Factory.create context
    |> GIVEN $"bytes of length {bytes.Length}" (fun ctx -> {| ctx with Bytes = bytes |})
    |> AND $"offset {offset}" (fun ctx -> {| ctx with Offset = offset |})
    |> WHEN "parsing to a Guid" (fun ctx ->
        {| ctx with
            Result = Guid.tryParse ctx.Bytes ctx.Offset |})
    |> THEN "the result matches the expected Guid" (fun ctx ->
        Assert.Equal(ctx.Expected, ctx.Result)
        ctx)
    |> run
