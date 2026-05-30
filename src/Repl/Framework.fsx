// Framework.fsx
// Shared CLI arguments, HTTP client, JSON helpers, and Dapr query API
// for the OSA-15 Dapr Query API spike scripts.
//
// Load with: #load "Framework.fsx"

#r "nuget: Argu"

open System
open System.Net.Http
open System.Text
open System.Text.Json
open Argu

type CliArguments =
    | [<AltCommandLine("-e")>] DaprEndpoint of dapr_endpoint: string
    | [<AltCommandLine("-s")>] StoreName of store_name: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | DaprEndpoint _ -> "Dapr HTTP endpoint. Default: http://localhost:3500"
            | StoreName _ -> "Dapr state store name. Default: statestore"

let argumentParser = ArgumentParser.Create<CliArguments>(programName = "dotnet fsi <script>.fsx")
let cliArguments = argumentParser.Parse(fsi.CommandLineArgs)

let daprHttpEndpoint = cliArguments.GetResult(DaprEndpoint, defaultValue = "http://localhost:3500")
let storeName = cliArguments.GetResult(StoreName, defaultValue = "statestore")

// ── JSON helpers ──
module Json =
    let serialize (value: obj) = JsonSerializer.Serialize(value)
    let prettyPrint (jsonString: string) =
        try JsonSerializer.Serialize(JsonDocument.Parse(jsonString).RootElement, JsonSerializerOptions(WriteIndented = true))
        with _ -> jsonString

// ── Dapr HTTP API ──
module Api =
    let httpClient = new HttpClient(BaseAddress = Uri(daprHttpEndpoint))

    let postQueryAsync (path: string) (body: string) = task {
        let content = new StringContent(body, Encoding.UTF8, "application/json")
        let! response = httpClient.PostAsync(path, content)
        let! responseText = response.Content.ReadAsStringAsync()
        return response.StatusCode, responseText.Trim()
    }

    let displayResponse (label: string) (statusCode, body) =
        printfn "--- %s ---" label
        printfn "HTTP %A" statusCode
        if body = "" then printfn "(empty body)\n"
        else printfn "%s\n" (Json.prettyPrint body)

// ── Dapr state & query operations ──
module Dapr =
    let private queryEndpoint = $"/v1.0-alpha1/state/{storeName}/query"
    let private stateEndpoint = $"/v1.0/state/{storeName}"

    let query (queryBody: obj) =
        Json.serialize queryBody |> Api.postQueryAsync queryEndpoint

    let writeState (items: obj) =
        Json.serialize items |> Api.postQueryAsync stateEndpoint
