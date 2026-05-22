open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker

module OpenApi =
    let registerOpenApi (app: WebApplication) : WebApplication =
        // Serves the OpenAPI spec file
        app.UseSwaggerUI(_.SwaggerEndpoint("/openapi/v1.json", "osaHealth API v1"))
        |> ignore
        // Serves the Swagger UI
        app.MapOpenApi() |> ignore

        app

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

    let app = builder.Build()

    app
    // SwaggerUI is registered before routing so it serves its assets at /swagger
    // without being short-circuited by the Oxpecker not-found handler.
    |> OpenApi.registerOpenApi
    |> _.UseRouting()
    |> _.Use(Default.exceptionMiddleware)
    |> _.UseOxpecker(
        [ GET
              [ Endpoints.rootGetEndpoint
                Endpoints.healthGetEndpoint
                Endpoints.randomGetEndpoint
                Endpoints.recordingsGetEndpoint ]
          POST [ Endpoints.recordingsPostEndpoint ] ]
    )
    |> _.Run(Default.notFoundHandler)

    app.Run()
    0 // Exit code
