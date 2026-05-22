module Endpoints

open System
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open Oxpecker.OpenApi

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

    let recordingsPostHandler: EndpointHandler = text "TBD: List all recordings"

    let recordingsGetHandler (userId: string) (fromDateTime: string) (toDateTime: string) : EndpointHandler =
        text $"TBD: Get {userId} recordings between {fromDateTime} and {toDateTime}"


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

let rootGetEndpoint =
    route "/" Handlers.rootGetHandler

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
    |> addOpenApi (OpenApiConfig(responseBodies = [ ResponseBody(typeof<string>) ]))

let recordingsGetEndpoint =
    routef "/recordings/userid/{%s}/from/{%s}/to/{%s}" Handlers.recordingsGetHandler
    |> addOpenApi (OpenApiConfig(responseBodies = [ ResponseBody(typeof<string>) ]))