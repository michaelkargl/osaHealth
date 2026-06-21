module osaHealth.Api.Endpoints

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open osaHealth.Api.Commands
open osaHealth.Api.Queries
open osaHealth.Api.Mappings
open osaHealth.Api.Models
open osaHealth.Api.Validation

let randomHandler: EndpointHandler =
    fun (ctx: HttpContext) ->
        let logger =
            ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")

        let value = Random.Shared.Next(1, 101)
        logger.LogInformation("Random value {RandomValue}", value)
        ctx |> json {| randomValue = value |}

let insertRecordingHandler (handleCommand: UpsertRecordingCommand -> Task<unit>) : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let! input = ctx.BindJson<RecordingDto>()

            match validateInsertRecordingRequest input with
            | Ok() -> do! input |> Recording.toCommand |> handleCommand
            | Error errors ->
                let messages =
                    errors
                    |> List.map (function
                        | NullOrEmpty f -> $"{f} is required"
                        | ConstraintViolation(f, reason) -> $"{f} {reason}")

                ctx.SetStatusCode 422
                return! ctx.WriteJson {| errors = messages |}
        }
        :> Task)

let listRecordingsHandler
    (handleQuery: CursorPagedQuery -> Task<ListRecordingsCursorPagedQueryResult>)
    : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let cursor =
                match ctx.Request.Query.TryGetValue("cursor") with
                | true, value -> Some(value.ToString())
                | false, _ -> None

            let limitResult =
                match ctx.Request.Query.TryGetValue("limit") with
                | true, value ->
                    match Int32.TryParse(value.ToString()) with
                    | true, num -> Ok num
                    | false, _ -> Error $"Expected limit parameter to be a valid number but received: {value}"
                | false, _ -> Error "Parameter 'limit' is required but not provided."

            // TODO: standardize and document error handling
            // TODO: add input validation
            // TODO: create framework functions for these
            match limitResult with
            | Error msg ->
                ctx.SetStatusCode 400
                return! ctx.WriteJson {| error = msg |}
            | Ok limit ->
                let! result =
                    { Cursor = cursor; Limit = limit }
                    |> handleQuery
                    |> ListRecordingsCursorPagedQueryResult.toDtoAsync

                return! ctx.WriteJson result
        })
        :> Task
