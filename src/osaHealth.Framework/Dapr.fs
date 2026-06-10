module osaHealth.Framework.Dapr

open System
open System.Net
open System.Text.Json
open System.Threading.Tasks
open FSharp.UMX
open osaHealth.Framework.Api
open osaHealth.Framework.Json

[<Measure>]
type StoreName

let private buildQueryPath (storeName: string<StoreName>) : string<HttpPath> =
    $"/v1.0-alpha1/state/{storeName}/query" |> UMX.tag

let private buildStatePath (storeName: string<StoreName>) : string<HttpPath> =
    $"/v1.0/state/{storeName}" |> UMX.tag

let query
    (endpoint: string<HttpEndpoint>)
    (storeName: string<StoreName>)
    (queryBody: obj)
    : Task<HttpStatusCode * string<HttpResponse>> =
    task {
        let path = buildQueryPath storeName
        let! statusCode, response = serialize queryBody |> UMX.tag |> postQueryAsync endpoint path
        return (statusCode, response)
    }

let writeState
    (endpoint: string<HttpEndpoint>)
    (storeName: string<StoreName>)
    (items: obj)
    : Task<HttpStatusCode * string<HttpResponse>> =
    let path = buildStatePath storeName
    serialize items |> UMX.tag |> postQueryAsync endpoint path

let queryUserPage
    (endpoint: string<HttpEndpoint>)
    (storeName: string<StoreName>)
    (userId: string)
    (pageSize: int)
    (paginationToken: string)
    : Task<HttpStatusCode * string<HttpResponse>> =
    {| filter = {| EQ = {| userId = userId |} |}
       sort = [| {| key = "updated_ms"; order = "ASC" |} |]
       page = {| limit = pageSize; token = paginationToken |} |}
    |> query endpoint storeName

let decodePaginationToken (token: string) : string =
    if String.IsNullOrEmpty token then
        "(empty)"
    else
        let mutable parsedInteger = 0

        if Int32.TryParse(token, &parsedInteger) then
            $"raw=\"%s{token}\"  asInt=%d{parsedInteger}  ← skip-offset"
        else
            $"raw=\"%s{token}\" (non-numeric)"

let getPageKeys (json: JsonDocument) : string array =
    let enumerator = json |> getJsonElement "results" |> _.EnumerateArray()
    [| for result in enumerator -> result.GetProperty("key").GetString() |]

let getPageToken (json: JsonDocument) : string =
    match json |> tryGetStringValue "token" with
    | Some value -> value
    | None -> ""
