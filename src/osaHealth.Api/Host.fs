module osaHealth.Api.Host

open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Serializers
open MongoDB.Driver
open osaHealth.Api.ErrorHandling
open osaHealth.Api.Framework.Http
open Oxpecker
open osaHealth.Repository.Entities

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

let configureLogging (builder: WebApplicationBuilder) : WebApplicationBuilder =
    builder.Logging
        .ClearProviders()
        .AddJsonConsole(fun opts -> opts.JsonWriterOptions <- JsonWriterOptions(Indented = false))
    |> ignore

    builder

let private serializersLock = obj ()
let mutable private serializersRegistered = false

// RegisterSerializer throws if called twice for the same type.
// In tests, MongoClient is created (by MongoFixture) before the WebApplication is built, which triggers the
// driver's lazy GuidSerializer(Unspecified) registration. Calling this function before new MongoClient(...)
// wins the race. The lock makes the check-then-register atomic across parallel test scenarios.
let registerBsonSerializers () =
    lock serializersLock (fun () ->
        if not serializersRegistered then
            BsonSerializer.RegisterSerializer(GuidSerializer(GuidRepresentation.Standard))
            serializersRegistered <- true)

let configureSerializationOptions (builder: WebApplicationBuilder) : WebApplicationBuilder =
    registerBsonSerializers ()
    builder

let configureServices (builder: WebApplicationBuilder) : WebApplicationBuilder =
    builder.Services
    |> _.AddRouting()
    |> _.AddOxpecker()
    |> _.AddOpenApi()
    |> ignore

    builder

let configurePipeline (recordingCollection: IMongoCollection<RecordingEntity>) (app: WebApplication) : WebApplication =
    app
    |> _.UseRouting()
    |> _.UseExceptionHandler(exceptionHandler)
    |> _.UseOxpecker(Router.endpoints recordingCollection)
    |> ignore

    app
