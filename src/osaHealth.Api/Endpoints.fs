module osaHealth.Api.Endpoints

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open FSharp.UMX
open FsToolkit.ErrorHandling
open osaHealth.Api.Commands
open osaHealth.Api.ErrorHandling
open osaHealth.Api.Queries
open osaHealth.Api.Mappings
open osaHealth.Api.Models
open osaHealth.Api.Validation
open osaHealth.Api.Framework.Http
open osaHealth.Domain.ErrorHandling
open osaHealth.Domain.Measures

let randomHandler: EndpointHandler =
    fun (ctx: HttpContext) ->
        let logger =
            ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")

        let value = Random.Shared.Next(1, 101)
        logger.LogInformation("Random value {RandomValue}", value)
        ctx |> json {| randomValue = value |}

let insertRecordingHandler
    (handleCommand: UpsertRecordingCommand -> Task<Result<unit, DomainError>>)
    : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let! input = ctx |> HttpContext.getFromJsonAsync<RecordingDto>

            match validateInsertRecordingRequest input with
            | Error apiErrors -> return! ctx |> HttpContext.writeErrors 422 apiErrors
            | Ok() ->
                let! commandResult = input |> Recording.toCommand |> handleCommand

                match commandResult with
                | Ok() -> ()
                | Error err ->
                    let statusCode, errors = DomainError.toHttpResponse err
                    return! ctx |> HttpContext.writeErrors statusCode errors

        }
        :> Task)

let listRecordingsHandler
    (handleQuery: ListRecordingsQuery -> Task<Result<ListRecordingsQueryResult, DomainError>>)
    : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let queryParams =
                result {
                    let! limit = ctx |> HttpContext.getRequiredIntParam "limit"
                    let! userId = ctx |> HttpContext.getRequiredStringParam "userid"
                    let cursor = ctx |> HttpContext.tryGetQueryParam "cursor"

                    let! from =
                        match ctx |> HttpContext.tryGetQueryParam "from" with
                        | None -> Ok None
                        | Some s ->
                            match DateTimeOffset.TryParse s with
                            | true, dt -> Ok(Some dt.UtcDateTime)
                            | false, _ -> Error(InvalidFormat("from", s))

                    let! ``to`` =
                        match ctx |> HttpContext.tryGetQueryParam "to" with
                        | None -> Ok None
                        | Some s ->
                            match DateTimeOffset.TryParse s with
                            | true, dt -> Ok(Some dt.UtcDateTime)
                            | false, _ -> Error(InvalidFormat("to", s))

                    return (userId, from, ``to``, cursor, limit)
                }

            match queryParams with
            | Error apiError -> return! ctx |> HttpContext.writeError 400 apiError
            | Ok(userId, from, ``to``, cursor, limit) ->
                match validateListRecordingsRequest userId from ``to`` cursor limit with
                | Error apiErrors -> return! ctx |> HttpContext.writeErrors 400 apiErrors
                | Ok() ->
                    let query =
                        { Page = { Cursor = cursor; Limit = limit }
                          UserId = UMX.tag<UserId> userId
                          From = from
                          To = ``to`` }

                    let! queryResult = query |> handleQuery

                    match queryResult with
                    | Error err ->
                        let statusCode, errors = DomainError.toHttpResponse err
                        return! ctx |> HttpContext.writeErrors statusCode errors
                    | Ok result -> return! result |> ListRecordingsQueryResult.toDto |> ctx.WriteJson
        })
        :> Task
