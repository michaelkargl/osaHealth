module osaHealth.Framework.Tests.ScenarioTests

open Xunit
open osaHealth.Framework.Testing.Bdd.Scenario

[<Fact>]
let ``scenario runs steps in order and propagates assertion failures`` () =
    let ctx = {| Numbers = []; Result = [] |}

    Factory.create ctx
    |> GIVEN "a list of unsorted integers" (fun ctx ->
        {| ctx with
            Numbers = [ 5; 3; 2; 4; 1 ] |})
    |> WHEN "the list is sorted ascending" (fun ctx ->
        {| ctx with
            Result = ctx.Numbers |> List.sort |})
    |> THEN "the result is the maximum element" (fun ctx ->
        Assert.Equal<int list>([ 1; 2; 3; 4; 5; ], ctx.Result)
        ctx)
    |> run
    |> ignore
