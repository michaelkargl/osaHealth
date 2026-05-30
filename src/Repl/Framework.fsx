// Framework.fsx
// Shared CLI arguments, HTTP client, JSON helpers, and Dapr query API
// for the OSA-15 Dapr Query API spike scripts.
//
// Load with: #load "Framework.fsx"

#r "nuget: FSharp.UMX"

open System
open System.Net
open System.Threading.Tasks
open System.Net.Http
open System.Text
open System.Text.Json
open FSharp.UMX

[<Measure>]
type HttpResponse


module String =
    let defaultIfNullOrWhiteSpace (defaultStr: string) (str: string) : string =
        if String.IsNullOrWhiteSpace str then defaultStr else str


// ── JSON helpers ──
module Json =
    let serialize (value: obj) = JsonSerializer.Serialize(value)

    let prettyPrint (jsonString: string) =
        try
            JsonSerializer.Serialize(
                JsonDocument.Parse(jsonString).RootElement,
                JsonSerializerOptions(WriteIndented = true)
            )
        with _ ->
            jsonString

    let tryGetJsonElement (prop: string) (document: JsonDocument) : JsonElement option =
        let mutable tokenElement = Unchecked.defaultof<JsonElement>

        if document.RootElement.TryGetProperty(prop, &tokenElement) then
            Some tokenElement
        else
            None

    let getJsonElement (prop: string) (document: JsonDocument) : JsonElement =
        document |> tryGetJsonElement prop |> _.Value
    
    let tryGetStringValue (prop: string) (document: JsonDocument) : string option =
        match tryGetJsonElement prop document with
        | Some element -> Some (element.GetString())
        | _ -> None

    let getStringValue (prop: string) (document: JsonDocument): string =
        document |> tryGetStringValue prop |> _.Value

// ── Dapr HTTP API ──
module Api =
    [<Measure>]
    type HttpEndpoint

    [<Measure>]
    type HttpPath

    [<Measure>]
    type HttpBody

    let buildHttpClient (endpoint: string<HttpEndpoint>) =
        new HttpClient(BaseAddress = Uri(endpoint |> UMX.untag))

    let postQueryAsync
        (endpoint: string<HttpEndpoint>)
        (path: string<HttpPath>)
        (body: string<HttpBody>)
        : Task<HttpStatusCode * string<HttpResponse>> =
        task {
            let body = body |> UMX.untag
            let path = path |> UMX.untag

            let client = buildHttpClient endpoint
            let content = new StringContent(body, Encoding.UTF8, "application/json")
            let! response = client.PostAsync(path, content)
            let! responseText = response.Content.ReadAsStringAsync()
            return response.StatusCode, (responseText.Trim() |> UMX.tag)
        }

    let displayResponse (label: string) (statusCode: HttpStatusCode, body: string<HttpResponse>) : unit =
        let body = body |> UMX.untag

        printfn "--- %s ---" label
        printfn "HTTP %A" statusCode

        if body = "" then
            printfn "(empty body)\n"
        else
            body |> Json.prettyPrint |> printfn "%s\n"

// ── Dapr state & query operations ──
open Api

module Dapr =
    [<Measure>]
    type StoreName

    let private buildQueryPath (storeName: string<StoreName>) : string<HttpPath> =
        $"/v1.0-alpha1/state/{storeName}/query" |> UMX.tag

    let private buildStatePath (storeName: string<StoreName>) : string<HttpPath> = $"/v1.0/state/{storeName}" |> UMX.tag

    let query
        (endpoint: string<HttpEndpoint>)
        (storeName: string<StoreName>)
        (queryBody: obj)
        : Task<HttpStatusCode * string<HttpResponse>> =
        task {
            let path = buildQueryPath storeName
            let! statusCode, response = Json.serialize queryBody |> UMX.tag |> Api.postQueryAsync endpoint path
            return (statusCode, response)
        }

    let writeState
        (endpoint: string<HttpEndpoint>)
        (storeName: string<StoreName>)
        (items: obj)
        : Task<HttpStatusCode * string<HttpResponse>> =
        let path = buildStatePath storeName
        Json.serialize items |> UMX.tag |> Api.postQueryAsync endpoint path
