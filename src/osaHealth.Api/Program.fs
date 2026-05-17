open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Oxpecker
open Oxpecker.OpenApi

let endpoints = [
    route "/" <| text "Hello World!"

    GET [
        route "/health" (text "OK")
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
    ]
]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services
        .AddRouting()
        .AddOxpecker()
        .AddOpenApi()
    |> ignore

    let app = builder.Build()

    // SwaggerUI is registered before routing so it serves its assets at /swagger
    // without being short-circuited by the Oxpecker not-found handler.
    app.UseSwaggerUI(fun opt -> opt.SwaggerEndpoint("/openapi/v1.json", "osaHealth API v1"))
        .UseRouting()
        .Use(Default.exceptionMiddleware)
        .UseOxpecker(endpoints)
        .Run(Default.notFoundHandler)

    // Serves the OpenAPI document consumed by the Swagger UI at /openapi/v1.json.
    app.MapOpenApi() |> ignore

    app.Run()

    0 // Exit code
