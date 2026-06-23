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

module ListRecordings =
    module QueryParams =
        [<Literal>]
        let Limit = "limit"

        [<Literal>]
        let UserId = "userId"

        [<Literal>]
        let Cursor = "cursor"

        [<Literal>]
        let From = "from"

        [<Literal>]
        let To = "to"

    let parseQueryParams (ctx: HttpContext) =
        result {
            let! limit = ctx |> HttpContext.getRequiredIntParam QueryParams.Limit
            let! userId = ctx |> HttpContext.getRequiredStringParam QueryParams.UserId
            let cursor = ctx |> HttpContext.tryGetQueryParam QueryParams.Cursor
            let! from = ctx |> HttpContext.tryGetDateTimeQueryParam QueryParams.From
            let! ``to`` = ctx |> HttpContext.tryGetDateTimeQueryParam QueryParams.To
            return (userId, from, ``to``, cursor, limit)
        }

    let listRecordingsHandler
        (handleQuery: ListRecordingsQuery -> Task<Result<ListRecordingsQueryResult, DomainError>>)
        : EndpointHandler =
        fun (ctx: HttpContext) ->
            (taskResult {
                let! userId, from, ``to``, cursor, limit =
                    ctx
                    |> parseQueryParams
                    |> TaskResult.ofResult
                    |> TaskResult.mapError (fun e -> 400, [ e ])

                do!
                    validateListRecordingsRequest userId from ``to`` cursor limit
                    |> TaskResult.ofResult
                    |> TaskResult.mapError (fun e -> 400, e)

                let! queryResult =
                    { Page = { Cursor = cursor; Limit = limit }
                      UserId = UMX.tag<UserId> userId
                      From = from
                      To = ``to`` }
                    |> handleQuery
                    |> TaskResult.mapError DomainError.toHttpResponse

                queryResult |> ListRecordingsQueryResult.toDto |> ctx.WriteJson |> ignore

             }
             |> TaskResult.mapError (fun (statusCode, errors) -> ctx |> HttpContext.writeErrors statusCode errors))
            :> Task
