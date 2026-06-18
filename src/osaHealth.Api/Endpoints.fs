module osaHealth.Api.Endpoints

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open osaHealth.Api.Commands
open osaHealth.Api.Mappings
open osaHealth.Api.Models

let randomHandler : EndpointHandler =
    fun (ctx: HttpContext) ->
        let logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")
        let value = Random.Shared.Next(1, 101)
        logger.LogInformation("Random value {RandomValue}", value)
        ctx |> json {| randomValue = value |}

let insertRecordingHandler (upsert: UpsertRecordingCommand -> Task<unit>) : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let! input = ctx.BindJson<RecordingDto>()
            do! input |> Recordings.toCommand |> upsert
        } :> Task)
