module Endpoints

open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open Oxpecker.OpenApi
open Types
open osaHealth.Api


module private Handlers =
    let rootGetHandler: EndpointHandler = text "Hello World!"
    let healthGetHandler: EndpointHandler = text "OK"

    let randomGetHandler: EndpointHandler =
        fun ctx ->
            let logger =
                ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")

            let value = Random.Shared.Next(1, 101)
            logger.LogInformation("Random value {RandomValue}", value)
            ctx |> json {| randomValue = value |}

    let recordingsPostHandler: EndpointHandler =
        fun ctx ->
            task {
                let logger =
                    ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")

                let client = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient()
                let! rawInput = ctx.Request.ReadFromJsonAsync<RecordingInput>()                
                let recordingInput =
                    match rawInput |> Option.ofObj with
                    | Some input ->
                        {
                            Id = Guid.NewGuid().ToString()
                            UserId = input.UserId
                            RecordedAt = DateTimeOffset.Parse(input.RecordedAt).ToUnixTimeMilliseconds()
                            Notes = input.Notes
                        }
                    | None -> failwith "INVALID_INPUT: Request body is required."

                do! Dapr.saveRecording client recordingInput
                logger.LogInformation(
                    "Saved recording {RecordingId} for user {UserId}",
                    recordingInput.Id,
                    recordingInput.UserId)

                return! ctx |> json recordingInput
            }

    let recordingsGetHandler (userId: string) (fromMs: int64) (toMs: int64) : EndpointHandler =
        fun ctx ->
            task {
                let logger =
                    ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")

                let client = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient()
                let! recordings = Dapr.queryRecordings client userId fromMs toMs
               
                logger.LogInformation(
                    "Queried {Count} recordings for user {UserId}",
                    recordings.Length,
                    userId)

                return! ctx |> json recordings
            }

// ── Endpoints ──────────────────────────────────────────────────────────────────

let healthGetEndpoint =
    route "/health" Handlers.healthGetHandler
    |> addOpenApi (
        OpenApiConfig(
            responseBodies = [ ResponseBody(typeof<string>) ],
            configureOperation =
                fun operation _ _ ->
                    operation.OperationId <- "GetHealth"
                    operation.Summary <- "Service health probe"
                    operation.Description <- "Returns 200 OK while the service is running."
                    Task.CompletedTask
        )
    )

let rootGetEndpoint = route "/" Handlers.rootGetHandler

let randomGetEndpoint =
    route "/random" Handlers.randomGetHandler
    |> addOpenApi (
        OpenApiConfig(
            responseBodies = [ ResponseBody(typeof<{| randomValue: int |}>) ],
            configureOperation =
                fun operation _ _ ->
                    operation.OperationId <- "GetRandom"
                    operation.Summary <- "Generate a random number"

                    operation.Description <-
                        "Returns a random integer (1-100) and emits it as a structured log entry."

                    Task.CompletedTask
        )
    )

let recordingsPostEndpoint =
    route "/recordings" Handlers.recordingsPostHandler
    |> addOpenApi (OpenApiConfig(responseBodies = [ ResponseBody(typeof<Recording>) ]))

let recordingsGetEndpoint =
    routef "/recordings/userid/{%s}/from/{%d}/to/{%d}" Handlers.recordingsGetHandler
    |> addOpenApi (OpenApiConfig(responseBodies = [ ResponseBody(typeof<Recording array>) ]))
