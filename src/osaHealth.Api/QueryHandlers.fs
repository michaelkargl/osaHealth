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
open System.Buffers.Binary

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

    let tryDecode (token: string<Base64> option) : Result<(DateTime * Guid) option, DomainError> =
        match token |> Option.map UMX.untag with
        | None -> Ok None
        | Some token when String.IsNullOrWhiteSpace token -> Ok None
        | Some token -> token |> UMX.tag<Base64> |> decode |> Result.map Some

    let encode (date: DateTime) (id: Guid) : string<Base64> =
        let tickBytes = Array.zeroCreate<byte> 8
        BinaryPrimitives.WriteInt64LittleEndian(tickBytes.AsSpan(), date.ToUniversalTime().Ticks)
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
        match
            query.Page.Cursor
            |> Option.map UMX.tag<Base64>
            |> CursorToken.tryDecode
        with
        | Error e -> return Error e
        | Ok cursor ->
            let cursorToken = cursor |> Option.map (fun (date, id) -> (date, id |> UMX.tag))

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
