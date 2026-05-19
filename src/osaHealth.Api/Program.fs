open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open Oxpecker.OpenApi

module OpenApi =
    let registerOpenApi (app: WebApplication) : WebApplication =
        // Serves the OpenAPI spec file
        app.UseSwaggerUI(_.SwaggerEndpoint("/openapi/v1.json", "osaHealth API v1"))
        |> ignore
        // Serves the Swagger UI
        app.MapOpenApi() |> ignore

        app

let randomHandler : EndpointHandler =
    fun ctx ->
        let logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("osaHealth.Api")
        let value = Random.Shared.Next(1, 101)
        logger.LogInformation("Random value {RandomValue}", value)
        ctx |> json {| randomValue = value |}

let endpoints =
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

            route "/random" randomHandler
            |> addOpenApi (
                OpenApiConfig(
                    responseBodies = [ ResponseBody(typeof<{| randomValue : int |}>) ],
                    configureOperation =
                        fun operation _ _ ->
                            operation.OperationId <- "GetRandom"
                            operation.Summary <- "Generate a random number"
                            operation.Description <- "Returns a random integer (1-100) and emits it as a structured log entry."
                            Task.CompletedTask
                )
            ) ] ]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)

    builder.Logging
        .ClearProviders()
        .AddJsonConsole(fun opts ->
            opts.JsonWriterOptions <- JsonWriterOptions(Indented = false))
    |> ignore

    builder.Services
    |> _.AddRouting()
    |> _.AddOxpecker()
    |> _.AddOpenApi()
    |> ignore

    let app = builder.Build()
    app
        // SwaggerUI is registered before routing so it serves its assets at /swagger
        // without being short-circuited by the Oxpecker not-found handler.
        |> OpenApi.registerOpenApi
        |> _.UseRouting()
        |> _.Use(Default.exceptionMiddleware)
        |> _.UseOxpecker(endpoints)
        |> _.Run(Default.notFoundHandler)

    app.Run()
    0 // Exit code
