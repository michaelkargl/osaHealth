module osaHealth.Framework.Tests.Guid_Tests

open Xunit
open osaHealth.Framework
open osaHealth.Framework.Testing.Bdd.Scenario
open osaHealth.Framework.Testing.Bdd.Xunit

let tryParseCases: obj[] seq =
    seq {
        let validGuid = System.Guid.NewGuid()
        let validBytes = validGuid.ToByteArray()
        [| validBytes :> obj; 0 :> obj; Some validGuid :> obj |]
        
        // Guid with offset
        let anotherGuid = System.Guid.NewGuid()
        let anotherBytes = anotherGuid.ToByteArray()
        let bufferWith24Bytes = Array.append (System.BitConverter.GetBytes(1L)) anotherBytes
        [| bufferWith24Bytes :> obj; 8 :> obj; Some anotherGuid :> obj |]
        
        [| [| 1uy; 2uy; 3uy |] :> obj; 0 :> obj; None :> obj |] // insufficient bytes
        [| [| 1uy; 2uy; 3uy |] :> obj; 1 :> obj; None :> obj |] // offset beyond buffer
        [| [| 1uy; 2uy; 3uy |] :> obj; -1 :> obj; None :> obj |] // offset too small
        [| [| 1uy; 2uy; 3uy |] :> obj; 8 :> obj;  None :> obj |] // offset too large
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
