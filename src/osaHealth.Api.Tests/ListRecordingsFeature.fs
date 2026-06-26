module osaHealth.Api.Tests.ListRecordingsFeature

open System
open System.Threading.Tasks
open Xunit
open osaHealth.Framework.Testing.Bdd.Scenario
open osaHealth.Framework.Testing.Bdd.Xunit
open osaHealth.Repositories
open osaHealth.Repository.Entities
open ListRecordingsSteps

let private newCtx () : Task<Context> =
    task {
        let dbName = $"test-{Guid.NewGuid()}"
        let! collection = MongoFixture.getDbCollection<RecordingEntity> dbName Recordings.CollectionName
        let! client = TestHost.startAsync collection
        return { Collection = collection; Client = client; Items = []; NextCursor = None }
    }

[<Fact>]
let ``compound cursor tie-break — no duplicates no gaps across page boundary`` () : Task<unit> =
    task {
        let userId = "cursor-user"
        let t = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        let idA = Guid.Parse "00000000-0000-0000-0000-000000000001"
        let idB = Guid.Parse "00000000-0000-0000-0000-000000000002"
        let idC = Guid.Parse "00000000-0000-0000-0000-000000000003"
        let! ctx = newCtx ()

        return!
            Factory.create ctx
            |> GIVEN "R1(T,idA), R2(T,idB), R3(T+1,idC) seeded" (seed [
                mkRecording idA userId t
                mkRecording idB userId t
                mkRecording idC userId (t.AddDays 1.0) ])
            |> WHEN "page 1 requested with limit=2" (requestPage userId 2 None None None)
            |> THEN "returns R1 and R2" (assertItemIds [idA; idB])
            |> AND "cursor is present" assertCursorIsSome
            |> WHEN "page 2 requested with the cursor" (fun ctx ->
                requestPage userId 2 ctx.NextCursor None None ctx)
            |> THEN "returns R3 only" (assertItemIds [idC])
            |> AND "no next cursor" assertCursorIsNone
            |> runAsync
    }

[<Fact>]
let ``from-to filter is inclusive on both boundaries`` () : Task<unit> =
    task {
        let userId = "filter-user"
        let r1Id = Guid.Parse "00000000-0000-0000-0000-000000000011"
        let r2Id = Guid.Parse "00000000-0000-0000-0000-000000000012"
        let r3Id = Guid.Parse "00000000-0000-0000-0000-000000000013"
        let r4Id = Guid.Parse "00000000-0000-0000-0000-000000000014"
        let d1 = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        let d2 = DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        let d3 = DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc)
        let d4 = DateTime(2024, 1, 4, 0, 0, 0, DateTimeKind.Utc)
        let! ctx = newCtx ()

        return!
            Factory.create ctx
            |> GIVEN "recordings at D1 D2 D3 D4" (seed [
                mkRecording r1Id userId d1
                mkRecording r2Id userId d2
                mkRecording r3Id userId d3
                mkRecording r4Id userId d4 ])
            |> WHEN "list requested with from=D2 to=D3" (requestPage userId 10 None (Some d2) (Some d3))
            |> THEN "returns only D2 and D3" (assertItemIds [r2Id; r3Id])
            |> AND "no next cursor" assertCursorIsNone
            |> runAsync
    }

[<Fact>]
let ``userId filter returns only that user's recordings`` () : Task<unit> =
    task {
        let userA = "user-a"
        let userB = "user-b"
        let aId1 = Guid.Parse "00000000-0000-0000-0000-000000000021"
        let aId2 = Guid.Parse "00000000-0000-0000-0000-000000000022"
        let bId1 = Guid.Parse "00000000-0000-0000-0000-000000000031"
        let t = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        let! ctx = newCtx ()

        return!
            Factory.create ctx
            |> GIVEN "recordings for userA and userB" (seed [
                mkRecording aId1 userA t
                mkRecording aId2 userA (t.AddDays 1.0)
                mkRecording bId1 userB t ])
            |> WHEN "list requested for userA" (requestPage userA 10 None None None)
            |> THEN "returns only userA's recordings" (assertItemIds [aId1; aId2])
            |> AND "no next cursor" assertCursorIsNone
            |> runAsync
    }
