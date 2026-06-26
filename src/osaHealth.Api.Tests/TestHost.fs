module osaHealth.Api.Tests.TestHost

open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open MongoDB.Driver
open osaHealth.Api
open osaHealth.Repository.Entities

let startAsync (collection: IMongoCollection<RecordingEntity>) : Task<HttpClient> =
    task {

        let builder = WebApplication.CreateBuilder()
        builder
        |> Host.configureServices
        |> Host.configureLogging
        |> Host.configureSerializationOptions
        |> _.WebHost.UseTestServer()
        |> ignore

        let app = builder.Build()
        app |> Host.configurePipeline collection |> ignore
        do! app.StartAsync()

        return app.GetTestClient()
    }
