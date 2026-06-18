module osaHealth.Api.Endpoints

open System
open FSharp.UMX
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Driver
open Oxpecker
open osaHealth.Repositories
open osaHealth.Repository.Entities
open osaHealth.Api.Models
open System.Threading.Tasks

let randomHandler : EndpointHandler =
    fun (ctx: HttpContext) ->
        let logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")
        let value = Random.Shared.Next(1, 101)
        logger.LogInformation("Random value {RandomValue}", value)
        ctx |> json {| randomValue = value |}

let insertRecordingHandler (collection: IMongoCollection<Recording>) : EndpointHandler =
    fun (ctx: HttpContext) ->
        (task {
            let! input = ctx.BindJson<RecordingInput>()
            let recording : Recording =
                { Id = input.Id |> UMX.tag
                  UserId = input.UserId
                  DateEpoch = input.DateEpoch
                  UpdatedAt = input.UpdatedAt
                  Deleted = input.Deleted }
            do! Recordings.upsert collection recording
        } :> Task)
