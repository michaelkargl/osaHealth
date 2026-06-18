open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Serializers
open MongoDB.Driver
open Oxpecker
open Oxpecker.OpenApi
open osaHealth.Api
open osaHealth.Api.EnvVars
open osaHealth.Api.Models
open osaHealth.Repository.Entities
open osaHealth.Repositories

module DI = osaHealth.Api.DependencyInjection

module OpenApi =
    let registerOpenApi (app: WebApplication) : WebApplication =
        app.UseSwaggerUI(_.SwaggerEndpoint("/openapi/v1.json", "osaHealth API v1"))
        |> ignore

        app.MapOpenApi() |> ignore
        app

module Persistence =
    let buildMongoClient (envVars: EnvVars): MongoClient =
        new MongoClient(envVars.ConnectionString)

    let getMongoDbCollection<'TCollection> (envVars: EnvVars) (collectionName: string) (mongoClient: MongoClient) =
        mongoClient.GetDatabase(envVars.DatabaseName).GetCollection<'TCollection>(collectionName)

let endpoints (recordingsCollection: IMongoCollection<Recording>) =
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
            ) ]

      POST
          [ route "/recordings" (DI.Api.insertRecording recordingsCollection)
            |> addOpenApi (
                OpenApiConfig(
                    requestBody = RequestBody(typeof<RecordingInput>),
                    configureOperation =
                        fun operation _ _ ->
                            operation.OperationId <- "UpsertRecording"
                            operation.Summary <- "Upsert a recording"
                            Task.CompletedTask
                )
            ) ] ]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Logging
        .ClearProviders()
        .AddJsonConsole(fun opts -> opts.JsonWriterOptions <- JsonWriterOptions(Indented = false))
    |> ignore

    builder.Services
    |> _.AddRouting()
    |> _.AddOxpecker()
    |> _.AddOpenApi()
    |> ignore

    // Ensure this is called before MongoDB setup
    BsonSerializer.RegisterSerializer(GuidSerializer(GuidRepresentation.Standard))

    let envVars = EnvVars.create ()

    let recordingsCollection =
        Persistence.buildMongoClient envVars
        |> Persistence.getMongoDbCollection<Recording> envVars CollectionName

    let app = builder.Build()

    app
    |> OpenApi.registerOpenApi
    |> _.UseRouting()
    |> _.Use(Default.exceptionMiddleware)
    |> _.UseOxpecker(endpoints recordingsCollection)
    |> _.Run(Default.notFoundHandler)

    app.Run()
    0
