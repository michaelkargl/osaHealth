module osaHealth.Framework.Dapr

open System
open System.Net
open System.Text.Json
open System.Threading.Tasks
open osaHealth.Framework.Api
open osaHealth.Framework.Json

[<Measure>]
type StoreName

let private buildQueryPath (storeName: string) : string = $"/v1.0-alpha1/state/{storeName}/query"

let private buildStatePath (storeName: string) : string = $"/v1.0/state/{storeName}"

let query (endpoint: string) (storeName: string) (queryBody: obj) : Task<HttpStatusCode * string> =
    task {
        let path = buildQueryPath storeName
        let! statusCode, response = serialize queryBody |> postQueryAsync endpoint path
        return (statusCode, response)
    }

let writeState (endpoint: string) (storeName: string) (items: obj) : Task<HttpStatusCode * string> =
    let path = buildStatePath storeName
    serialize items |> postQueryAsync endpoint path

let queryUserPage
    (endpoint: string)
    (storeName: string)
    (userId: string)
    (pageSize: int)
    (paginationToken: string)
    : Task<HttpStatusCode * string> =
    {| filter = {| EQ = {| userId = userId |} |}
       sort = [| {| key = "updated_ms"; order = "ASC" |} |]
       page =
        {| limit = pageSize
           token = paginationToken |} |}
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
    let enumerator =
        json |> tryGetJsonElement "results" |> Option.map _.EnumerateArray()

    match enumerator with
    | None -> Array.empty
    | Some e ->
        [| for result in e ->
               match result.TryGetProperty("key") with
               | true, key -> key.GetString() |> Option.ofObj
               | false, _ -> None |]
        |> Array.choose id

let getPageToken (json: JsonDocument) : string =
    match json |> tryGetStringValue "token" with
    | Some value -> value
    | None -> ""
