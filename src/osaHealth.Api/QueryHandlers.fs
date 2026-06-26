module osaHealth.Api.QueryHandlers

open System
open System.Threading.Tasks
open FSharp.UMX
open FsToolkit.ErrorHandling
open osaHealth.Framework
open osaHealth.Api.Queries
open osaHealth.Domain.Entities
open osaHealth.Domain.ErrorHandling
open osaHealth.Domain.Measures

module private CursorToken =
    let decode (token: string<Base64>) : Result<(DateTime * Guid), DomainError> =
        result {
            let rawToken = token |> UMX.untag

            let! bytes =
                match StringUtil.tryFromBase64 rawToken with
                | None | Some [||] ->
                    Error (DomainError.InvalidCursor (rawToken, "invalid base64"))
                | Some bytes -> Ok bytes

            // byte index:   0        7  8                     23
            //               ├────────┤  ├──────────────────────┤
            //                DateTicks   Guid (16 bytes)
            //               (Int64, 8B)
            let! date =
                match DateTime.tryParseUtc bytes 0 with
                | Some dt -> Ok dt
                | None -> Error (DomainError.InvalidCursor (rawToken, "invalid date ticks"))

            let! id =
                match Guid.tryParse bytes 8 with
                | Some guid -> Ok guid
                | None -> Error (DomainError.InvalidCursor (rawToken, "invalid guid"))

            return (date, id)
        }

    let tryDecode (token: string<Base64> option) : (DateTime * Guid) option =
        match token |> Option.map UMX.untag with
        | None -> None
        | Some token when String.IsNullOrWhiteSpace token -> None
        | Some token -> token |> UMX.tag<Base64> |> decode |> Result.toOption

    let encode (date: DateTime) (id: Guid) : string<Base64> =
        let tickBytes = BitConverter.GetBytes(date.Ticks) // int64 / 8 = 8 bytes
        let idBytes = id.ToByteArray() // Guid -> (36 chars - 4 hyphens) / 2 hex digits -> 16 raw bytes

        Array.append tickBytes idBytes |> Convert.ToBase64String |> UMX.tag

let handleListRecordingsQuery
    (findAll:
        string<UserId>
            -> DateTime option
            -> DateTime option
            -> (DateTime * Guid<RecordingId>) option
            -> int
            -> Task<Recording list>)
    (query: ListRecordingsQuery)
    : Task<Result<ListRecordingsQueryResult, DomainError>> =
    task {
        let cursorToken =
            query.Page.Cursor
            |> Option.map UMX.tag<Base64>
            |> CursorToken.tryDecode
            |> Option.map (fun (date, id) -> (date, id |> UMX.tag))

        let! recordings = findAll (query.UserId |> UMX.tag<UserId>) query.From query.To cursorToken query.Page.Limit

        let nextCursor =
            if recordings.Length < query.Page.Limit then
                None
            else
                recordings
                |> List.last
                |> fun r -> CursorToken.encode r.DateEpoch (r.Id |> UMX.untag)
                |> UMX.untag
                |> Some

        return
            Ok
                { NextCursor = nextCursor
                  Items = recordings }
    }
