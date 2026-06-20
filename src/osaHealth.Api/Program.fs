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
        |> Persistence.getMongoDbCollection<RecordingEntity> envVars CollectionName

    let app = builder.Build()

    app
    |> OpenApi.registerOpenApi
    |> _.UseRouting()
    |> _.Use(Default.exceptionMiddleware)
    |> _.UseOxpecker(Router.endpoints recordingsCollection)
    |> _.Run(Default.notFoundHandler)

    app.Run()
    0
