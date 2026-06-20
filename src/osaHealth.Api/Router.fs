module osaHealth.Api.Router

open System.Threading.Tasks
open MongoDB.Driver
open Oxpecker
open Oxpecker.OpenApi
open osaHealth.Api.Models
open osaHealth.Repository.Entities

module DI = osaHealth.Api.DependencyInjection

let endpoints (recordingsCollection: IMongoCollection<RecordingEntity>) =
    [ route "/" <| text "Hello World!"

      GET
          [ route "/health" (text "OK")
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

            route "/random" Endpoints.randomHandler
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

            route "/recordings" (DI.Api.listRecordings recordingsCollection)
            |> addOpenApi (
                OpenApiConfig(
                    configureOperation =
                        fun operation _ _ ->
                            operation.OperationId <- "ListRecordings"
                            operation.Summary <- "Lists all recordings"
                            Task.CompletedTask
                )
            ) ]

      POST
          [ route "/recordings" (DI.Api.insertRecordingHandler recordingsCollection)
            |> addOpenApi (
                OpenApiConfig(
                    requestBody = RequestBody(typeof<RecordingDto>),
                    configureOperation =
                        fun operation _ _ ->
                            operation.OperationId <- "UpsertRecording"
                            operation.Summary <- "Upsert a recording"
                            Task.CompletedTask
                )
            ) ] ]
