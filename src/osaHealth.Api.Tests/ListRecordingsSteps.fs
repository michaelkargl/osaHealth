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
      Items: JsonElement list
      NextCursor: string option }

let mkRecording (id: Guid) (userId: string) (dateEpoch: DateTime) : Recording =
    { Id = id |> UMX.tag<RecordingId>
      UserId = userId |> UMX.tag<UserId>
      DateEpoch = dateEpoch
      UpdatedAt = DateTime.UtcNow
      Deleted = false }

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

let requestPage (userId: string) (limit: int) (cursor: string option) (from: DateTime option) (``to``: DateTime option) (ctx: Context) : Task<Context> =
    task {
        let! response = ctx.Client.GetAsync(buildUrl userId limit cursor from ``to``)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode)

        let! body = response.Content.ReadAsStringAsync()
        let doc = JsonDocument.Parse(body)

        let items =
            doc.RootElement.GetProperty("items").EnumerateArray() |> Seq.toList

        let nextCursor =
            let el = doc.RootElement.GetProperty("nextCursor")
            if el.ValueKind = JsonValueKind.Null then None else el.GetString() |> Some

        return { ctx with Items = items; NextCursor = nextCursor }
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
