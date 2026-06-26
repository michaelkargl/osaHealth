open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open MongoDB.Driver
open osaHealth.Api
open osaHealth.Api.EnvVars
open osaHealth.Repository.Entities
open osaHealth.Repositories

module Persistence =
    let buildMongoClient (envVars: EnvVars) : MongoClient =
        new MongoClient(envVars.ConnectionString)

    let getMongoDbCollection<'TCollection> (envVars: EnvVars) (collectionName: string) (mongoClient: MongoClient) =
        mongoClient.GetDatabase(envVars.DatabaseName).GetCollection<'TCollection>(collectionName)

module OpenApi =
    let registerOpenApi (app: WebApplication) : WebApplication =
        app.UseSwaggerUI(_.SwaggerEndpoint("/openapi/v1.json", "osaHealth API v1"))
        |> ignore

        app.MapOpenApi() |> ignore
        app

[<EntryPoint>]
let main args =
    let builder =
        WebApplication.CreateBuilder(args)
        |> Host.configureServices
        |> Host.configureLogging
        |> Host.configureSerializationOptions

    let envVars = EnvVars.create ()

    let recordingsCollection =
        Persistence.buildMongoClient envVars
        |> Persistence.getMongoDbCollection<RecordingEntity> envVars Recordings.CollectionName

    builder.Build()
    |> Host.configurePipeline recordingsCollection
    |> OpenApi.registerOpenApi
    |> _.Run()

    0
