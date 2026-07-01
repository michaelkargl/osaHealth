module osaHealth.Api.Tests.ListRecordingsSteps

open System
open System.Net
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open FSharp.UMX
open MongoDB.Driver
open osaHealth.Domain.Entities
open osaHealth.Domain.Measures
open osaHealth.Repositories
open osaHealth.Repository.Entities
open Xunit

type Context =
    { Collection: IMongoCollection<RecordingEntity>
      Client: HttpClient
      UserId: string
      From: DateTime option
      To: DateTime option
      Items: JsonElement list
      NextCursor: string option }

let createRecording (id: Guid) (userId: string) (dateEpoch: DateTime) : Recording =
    { Id = id |> UMX.tag<RecordingId>
      UserId = userId |> UMX.tag<UserId>
      DateEpoch = dateEpoch
      UpdatedAt = DateTime.UtcNow
      Deleted = false }

let recordingId (recording: Recording) : Guid = UMX.untag recording.Id

let seed (recordings: Recording list) (ctx: Context) : Task<Context> =
    task {
        for r in recordings do
            do! Recordings.upsert ctx.Collection r
        return ctx
    }

let private buildUrl (userId: string) (limit: int) (cursor: string option) (from: DateTime option) (``to``: DateTime option) =
    [ yield $"userId={Uri.EscapeDataString userId}"
      yield $"limit={limit}"
      yield! cursor |> Option.toList |> List.map (fun c -> $"cursor={Uri.EscapeDataString c}")
      yield! from   |> Option.toList |> List.map (fun f -> let s = f.ToString "o" in $"from={Uri.EscapeDataString s}")
      yield! ``to`` |> Option.toList |> List.map (fun t -> let s = t.ToString "o" in $"to={Uri.EscapeDataString s}") ]
    |> String.concat "&"
    |> sprintf "/recordings?%s"

let private fetch (client: HttpClient) (url: string) : Task<JsonElement list * string option> =
    task {
        let! response = client.GetAsync(url)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode)

        let! body = response.Content.ReadAsStringAsync()
        let doc = JsonDocument.Parse(body)

        let items =
            doc.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList

        let nextCursor =
            let el = doc.RootElement.GetProperty("nextCursor")
            if el.ValueKind = JsonValueKind.Null then None else el.GetString() |> Option.ofObj

        return items, nextCursor
    }

let requestPage (userId: string) (limit: int) (cursor: string option) (from: DateTime option) (``to``: DateTime option) (ctx: Context) : Task<Context> =
    task {
        let! items, nextCursor = fetch ctx.Client (buildUrl userId limit cursor from ``to``)
        return { ctx with Items = items; NextCursor = nextCursor }
    }

/// Fetches every page by following the cursor until it runs out, accumulating all items in order.
/// The page walk lives here (inside the WHEN step) so scenarios keep a single end-state THEN
/// instead of a when/then/when/then chain. See docs/coding-guidelines-fsharp.md (Testing).
let walkAllPages (userId: string) (limit: int) (from: DateTime option) (``to``: DateTime option) (ctx: Context) : Task<Context> =
    task {
        let mutable cursor = None
        let mutable items = []
        let mutable morePages = true

        while morePages do
            let! pageItems, nextCursor = fetch ctx.Client (buildUrl userId limit cursor from ``to``)
            items <- items @ pageItems
            cursor <- nextCursor
            morePages <- nextCursor.IsSome

        return { ctx with Items = items; NextCursor = None }
    }

let assertItemIds (expectedIds: Guid list) (ctx: Context) : Task<Context> =
    task {
        let actualIds = ctx.Items |> List.map (_.GetProperty("id").GetGuid())
        Assert.Equal<Guid list>(expectedIds, actualIds)
        return ctx
    }

let assertCursorIsNone (ctx: Context) : Task<Context> =
    task {
        Assert.True(ctx.NextCursor.IsNone, $"Expected no cursor but got: {ctx.NextCursor}")
        return ctx
    }

let assertCursorIsSome (ctx: Context) : Task<Context> =
    task {
        Assert.True(ctx.NextCursor.IsSome, "Expected a cursor but got none")
        return ctx
    }
