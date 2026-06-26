module osaHealth.Api.Tests.ListRecordingsFeatureTests

open System
open System.Threading.Tasks
open Xunit
open osaHealth.Framework.Testing.Bdd.Scenario
open osaHealth.Framework.Testing.Bdd.Xunit
open osaHealth.Repositories
open osaHealth.Repository.Entities
open ListRecordingsSteps

let private createContextAsync () : Task<Context> =
    task {
        let dbName = $"test-{Guid.NewGuid()}"
        let! collection = MongoFixture.getDbCollection<RecordingEntity> dbName Recordings.CollectionName
        let! client = TestHost.startAsync collection

        return
            { Collection = collection
              Client = client
              UserId = $"user-{Guid.NewGuid()}"
              From = None
              To = None
              Items = []
              NextCursor = None }
    }

[<Fact>]
let ``recordings sharing a date are returned exactly once and in order when walking every page`` () : Task<unit> =
    task {
        let! ctx = createContextAsync ()
        let userId = ctx.UserId
        let sharedDate = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        let laterDate = sharedDate.AddDays 1.0

        // Hand-picked, ordered ids so the same-date pair has a deterministic (date, id) order.
        let idA = Guid.Parse "11111111-1111-1111-1111-111111111111"
        let idB = Guid.Parse "22222222-2222-2222-2222-222222222222"
        let idC = Guid.Parse "33333333-3333-3333-3333-333333333333"

        let first = createRecording idA userId sharedDate
        let second = createRecording idB userId sharedDate
        let later = createRecording idC userId laterDate

        return!
            Factory.create ctx
            |> GIVEN "two recordings share the same recording date" (seed [ first; second ])
            |> AND "a third recording has a later recording date" (seed [ later ])
            |> WHEN
                "every page is fetched by following the cursor with a page size of two"
                (walkAllPages userId 2 None None)
            |> THENf
                (fun c -> $"the {c.Items.Length} recordings appear exactly once, ordered by date then id")
                (assertItemIds [ idA; idB; idC ])
            |> runAsync
    }

[<Fact>]
let ``from-to filter is inclusive on both boundaries`` () : Task<unit> =
    task {
        let! ctx = createContextAsync ()
        let userId = ctx.UserId
        let windowStart = DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        let windowEnd = windowStart.AddDays 1.0

        let onStart = createRecording (Guid.NewGuid()) userId windowStart
        let onEnd = createRecording (Guid.NewGuid()) userId windowEnd
        let dayBefore = createRecording (Guid.NewGuid()) userId (windowStart.AddDays -1.0)
        let dayAfter = createRecording (Guid.NewGuid()) userId (windowEnd.AddDays 1.0)

        return!
            Factory.create
                { ctx with
                    From = Some windowStart
                    To = Some windowEnd }
            |> GIVENf
                (fun c -> $"a recording on the window start {c.From.Value.ToShortDateString()}")
                (seed [ onStart ])
            |> ANDf (fun c -> $"a recording on the window end {c.To.Value.ToShortDateString()}") (seed [ onEnd ])
            |> AND "a recording the day before the window" (seed [ dayBefore ])
            |> AND "a recording the day after the window" (seed [ dayAfter ])
            |> WHENf
                (fun c ->
                    $"recordings requested from {c.From.Value.ToShortDateString()} to {c.To.Value.ToShortDateString()}")
                (requestPage userId 100 None (Some windowStart) (Some windowEnd))
            |> THENf
                (fun c -> $"the {c.Items.Length} returned recordings are the two in-window ones")
                (assertItemIds [ recordingId onStart; recordingId onEnd ])
            |> AND "no next cursor" assertCursorIsNone
            |> runAsync
    }

[<Fact>]
let ``userId filter returns only that user's recordings`` () : Task<unit> =
    task {
        let! ctx = createContextAsync ()
        let userUnderTest = ctx.UserId
        let otherUser = $"other-{Guid.NewGuid()}"
        let date = DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)

        let firstForUser = createRecording (Guid.NewGuid()) userUnderTest date

        let secondForUser =
            createRecording (Guid.NewGuid()) userUnderTest (date.AddDays 1.0)

        let oneForOther = createRecording (Guid.NewGuid()) otherUser date

        return!
            Factory.create ctx
            |> GIVEN "two recordings for the user under test" (seed [ firstForUser; secondForUser ])
            |> AND "a recording for a different user" (seed [ oneForOther ])
            |> WHENf
                (fun c -> $"recordings are requested for user {c.UserId}")
                (requestPage userUnderTest 10 None None None)
            |> THENf
                (fun c -> $"the {c.Items.Length} returned recordings all belong to the user under test")
                (assertItemIds [ recordingId firstForUser; recordingId secondForUser ])
            |> AND "no next cursor" assertCursorIsNone
            |> runAsync
    }
