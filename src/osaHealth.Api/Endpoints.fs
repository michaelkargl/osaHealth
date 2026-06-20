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
open osaHealth.Domain.Entities

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
            | Ok() -> do! input |> Recording.createCommand |> handleCommand
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

let listRecordingsHandler (handleQuery: ListRecordingsQuery -> Task<Recording list>) : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let! records = handleQuery ()
            return! records |> List.map Recording.toDto |> ctx.WriteJson
        })
        :> Task
