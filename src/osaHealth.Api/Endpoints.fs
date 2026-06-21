module osaHealth.Api.Endpoints

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open FSharp.Core
open Oxpecker
open FsToolkit.ErrorHandling
open osaHealth.Api.Commands
open osaHealth.Api.ErrorHandling
open osaHealth.Api.Queries
open osaHealth.Api.Mappings
open osaHealth.Api.Models
open osaHealth.Api.Validation
open osaHealth.Api.Framework.Http
open osaHealth.Domain.ErrorHandling

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
    (handleQuery: CursorPagedQuery -> Task<Result<ListRecordingsCursorPagedQueryResult, DomainError>>)
    : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {

            let queryParams =
                result {
                    let! limit = ctx |> HttpContext.getRequiredIntParam "limit"
                    let cursor = ctx |> HttpContext.tryGetQueryParam "cursor"
                    return (cursor, limit)
                }
            
            match queryParams with
            | Error apiError -> return! ctx |> HttpContext.writeError 400 apiError
            | Ok(cursor, limit) ->
                match validateListRecordingsQuery cursor limit with
                | Error apiErrors -> return! ctx |> HttpContext.writeErrors 400 apiErrors
                | Ok() ->
                    let! queryResult = { Cursor = cursor; Limit = limit } |> handleQuery

                    match queryResult with
                    | Error err ->
                        let statusCode, errors = DomainError.toHttpResponse err
                        return! ctx |> HttpContext.writeErrors statusCode errors
                    | Ok result -> return! result |> ListRecordingsCursorPagedQueryResult.toDto |> ctx.WriteJson
        })
        :> Task
