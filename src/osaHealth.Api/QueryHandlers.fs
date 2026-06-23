module osaHealth.Api.QueryHandlers

open System
open System.Threading.Tasks
open FSharp.UMX
open osaHealth.Api.Queries
open osaHealth.Domain.Entities
open osaHealth.Domain.ErrorHandling
open osaHealth.Domain.Measures

module private CursorToken =
    let decode (token: string<Base64>) : Guid =
        token |> UMX.untag |> Convert.FromBase64String |> Guid

    let tryDecode (token: string<Base64> option) : Guid option =
        match token |> Option.map UMX.untag with
        | None -> None
        | Some token when String.IsNullOrWhiteSpace token -> None
        | Some token -> token |> UMX.tag<Base64> |> decode |> Some

    let encode (id: Guid) : string<Base64> =
        id.ToByteArray() |> Convert.ToBase64String |> UMX.tag

let handleListRecordingsQuery
    (findAll: string<UserId> -> DateTime option -> DateTime option -> Guid<RecordingId> option -> int -> Task<Recording list>)
    (query: ListRecordingsQuery)
    : Task<Result<ListRecordingsQueryResult, DomainError>> =
    task {
        let recordingId =
            query.Page.Cursor
            |> Option.map UMX.tag<Base64>
            |> CursorToken.tryDecode
            |> Option.map UMX.tag<RecordingId>

        let! recordings = findAll query.UserId query.From query.To recordingId query.Page.Limit

        let nextCursor =
            if recordings.Length < query.Page.Limit then
                None
            else
                recordings
                |> List.last
                |> _.Id
                |> UMX.untag
                |> CursorToken.encode
                |> UMX.untag
                |> Some

        return
            Ok
                { NextCursor = nextCursor
                  Items = recordings }
    }
