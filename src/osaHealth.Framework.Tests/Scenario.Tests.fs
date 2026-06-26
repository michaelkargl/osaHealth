module osaHealth.Framework.Tests.ScenarioTests

open System.Threading.Tasks
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
        Assert.Equal<int list>([ 1; 2; 3; 4; 5 ], ctx.Result)
        ctx)
    |> run

[<Fact>]
let ``async scenario threads context through awaited steps`` () : Task<unit> =
    let ctx = {| Numbers = []; Result = [] |}

    Factory.create ctx
    |> GIVEN "a list of unsorted integers" (fun ctx ->
        Task.FromResult
            {| ctx with
                Numbers = [ 5; 3; 2; 4; 1 ] |})
    |> WHEN "the list is sorted ascending asynchronously" (fun ctx ->
        task {
            // This step MUST suspend, or the test proves nothing.
            //
            // If all steps returned Task.FromResult (already done), runAsync would run
            // top-to-bottom synchronously and never actually suspend. Task.Yield() forces
            // a real suspension/resumption, so the test catches bugs in how runAsync threads
            // context through truly-async steps.
            //
            // See docs/glossary.md#async--awaiter-state-machine for deep dive.
            do! Task.Yield()

            return
                {| ctx with
                    Result = ctx.Numbers |> List.sort |}
        })
    |> THEN "the result is sorted ascending" (fun ctx ->
        task {
            Assert.Equal<int list>([ 1; 2; 3; 4; 5 ], ctx.Result)
            return ctx
        })
    |> runAsync
