module osaHealth.Api.QueryHandlers

open System
open System.Buffers.Text
open System.Threading.Tasks
open FSharp.UMX
open FsToolkit.ErrorHandling
open osaHealth.Api.Queries
open osaHealth.Domain.Entities
open osaHealth.Domain.ErrorHandling
open osaHealth.Domain.Measures

module private CursorToken =    
    let decode (token: string<Base64>) : Result<(DateTime * Guid), DomainError> =
        result {
            // byte index:   0        7  8                     23
            //               ├────────┤  ├──────────────────────┤
            //                DateTicks   Guid (16 bytes)
            //               (Int64, 8B)
            let token = token |> UMX.untag
            
            
            if not (Convert.TryFromBase64 (token, Span(buffer))) 
            let tokenBytes = token |> UMX.untag |> Convert.FromBase64String
            let ticks = BitConverter.ToInt64(tokenBytes, 0)
            let date = DateTime(ticks, DateTimeKind.Utc)
            let id = Guid(tokenBytes[8..])

            return Ok (date, id)
        }

    let tryDecode (token: string<Base64> option) : (DateTime * Guid) option =
        match token |> Option.map UMX.untag with
        | None -> None
        | Some token when String.IsNullOrWhiteSpace token -> None
        | Some token -> token |> UMX.tag<Base64> |> decode |> Some

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
