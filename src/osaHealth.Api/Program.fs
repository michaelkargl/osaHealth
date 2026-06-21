open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Diagnostics
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Serializers
open MongoDB.Driver
open Oxpecker
open osaHealth.Api
open osaHealth.Api.EnvVars
open osaHealth.Api.ErrorHandling
open osaHealth.Api.Framework.Http
open osaHealth.Repository.Entities
open osaHealth.Repositories

module OpenApi =
    let registerOpenApi (app: WebApplication) : WebApplication =
        app.UseSwaggerUI(_.SwaggerEndpoint("/openapi/v1.json", "osaHealth API v1"))
        |> ignore

        app.MapOpenApi() |> ignore
        app

module Persistence =
    let buildMongoClient (envVars: EnvVars) : MongoClient =
        new MongoClient(envVars.ConnectionString)

    let getMongoDbCollection<'TCollection> (envVars: EnvVars) (collectionName: string) (mongoClient: MongoClient) =
        mongoClient.GetDatabase(envVars.DatabaseName).GetCollection<'TCollection>(collectionName)

module ErrorHandling =
    let exceptionHandler (builder: IApplicationBuilder) =
        builder.Run(fun ctx ->
            task {
                match ctx |> HttpContext.tryGetRaisedException with
                | None -> ()
                | Some exn ->
                    let logger = ctx |> HttpContext.getLogger "osaHealth.Api"
                    logger.LogError(exn, "Unhandled exception")

                // As a security measure, we intentionally do not include the exception message here
                // We do not want to leak application internal details  
                return! ctx |> HttpContext.writeError 500 ApiError.InternalError
            })


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
    |> _.UseExceptionHandler(ErrorHandling.exceptionHandler)
    |> _.UseOxpecker(Router.endpoints recordingsCollection)
    |> _.Run(Default.notFoundHandler)

    app.Run()
    0
