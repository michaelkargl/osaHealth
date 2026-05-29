// 02-pagination-token.fsx
// ── Dapr Query API spike: pagination token = skip-offset ──
// Demonstrates that the token is literally a skip offset, not an opaque
// keyset cursor. Also shows the "empty token = caught up" gotcha.
//
// Run: dotnet fsi 02-pagination-token.fsx [--dapr-endpoint http://localhost:3500] [--store-name statestore]
// Prerequisites: Dapr sidecar + MongoDB reachable, statestore component loaded.

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

let argumentParser = ArgumentParser.Create<CliArguments>(programName = "dotnet fsi 02-pagination-token.fsx")
let cliArguments = argumentParser.Parse(fsi.CommandLineArgs)

let daprHttpEndpoint = cliArguments.GetResult(DaprEndpoint, defaultValue = "http://localhost:3500")
let storeName = cliArguments.GetResult(StoreName, defaultValue = "statestore")

// ── Json helpers ──
module Json =
    let serialize (value: obj) = JsonSerializer.Serialize(value)

// ── Dapr HTTP API ──
module Api =
    let httpClient = new HttpClient(BaseAddress = Uri(daprHttpEndpoint))

    let postQueryAsync (path: string) (body: string) = task {
        let content = new StringContent(body, Encoding.UTF8, "application/json")
        let! response = httpClient.PostAsync(path, content)
        let! responseText = response.Content.ReadAsStringAsync()
        return response.StatusCode, responseText.Trim()
    }

let decodePaginationToken (token: string) =
    if String.IsNullOrEmpty token then "(empty)"
    else
        let mutable parsedInteger = 0
        if Int32.TryParse(token, &parsedInteger) then $"raw=\"%s{token}\"  asInt=%d{parsedInteger}  ← skip-offset"
        else $"raw=\"%s{token}\" (non-numeric)"

// ── Test cases ──
module Tests =

    // Seed 12 user-A recordings (total > page size so pagination is forced).
    let seedRecordings = task {
        let recordings = [|
            for index in 0..11 do
                {| key = $"rec-A-%02d{index + 1}"
                   value = {|
                       userId = "user-A"
                       date_epoch = 20260501 + index
                       updated_ms = 1700000000000L + int64 index
                       deleted = false
                   |} |}
        |]
        let! response = Json.serialize recordings |> Api.postQueryAsync $"/v1.0/state/{storeName}"
        printfn "Seed 12 records: HTTP %A  %s\n" (fst response) (if snd response = "" then "(ok)" else snd response)
    }

    // ── Paginate through the full set with limit=4 ──
    // Expected: opaque keyset token; last data page returns empty token.
    // Actual:   token = skip offset (4, 8, 12); last data page still has a
    //           non-empty token; the empty token only arrives on a TRAILING
    //           zero-result page.
    let paginateAllRecordings = task {
        printfn "=== Paginating user-A recordings, limit=4, sorted by (updated_ms) ==="
        let mutable paginationToken = ""
        let mutable page = 0
        let mutable seenKeys = []
        let mutable isComplete = false

        while not isComplete do
            page <- page + 1
            let queryBody = {|
                filter = {| EQ = {| userId = "user-A" |} |}
                sort = [| {| key = "updated_ms"; order = "ASC" |} |]
                page = {| limit = 4
                          token = paginationToken |}
            |}
            let! statusCode, body = Json.serialize queryBody |> Api.postQueryAsync $"/v1.0-alpha1/state/{storeName}/query"

            if statusCode <> System.Net.HttpStatusCode.OK then
                printfn "page %d: HTTP %A — %s" page statusCode body
                isComplete <- true
            else
                let responseDocument = JsonDocument.Parse(if body = "" then "{}" else body)
                let results = responseDocument.RootElement.GetProperty("results")
                let pageKeys = [| for result in results.EnumerateArray() -> result.GetProperty("key").GetString() |]
                let currentToken =
                    let mutable tokenElement = Unchecked.defaultof<JsonElement>
                    if responseDocument.RootElement.TryGetProperty("token", &tokenElement) then
                        let tokenValue = tokenElement.GetString()
                        if isNull tokenValue then "" else tokenValue
                    else ""

                printfn "page %d: keys=[%s]" page (String.Join(", ", pageKeys))
                printfn "         token=%s" (decodePaginationToken currentToken)
                if pageKeys.Length = 0 then
                    printfn "         ← zero-result page — NOW empty token means done\n"
                else
                    printfn "         ← still has data; empty token only on zero-result page\n"

                seenKeys <- seenKeys @ (List.ofArray pageKeys)
                paginationToken <- currentToken

                if String.IsNullOrEmpty currentToken then isComplete <- true
                elif page >= 20 then
                    printfn "SAFETY STOP"
                    isComplete <- true

        printfn "Total seen: %d  unique: %d" (List.length seenKeys) (List.distinct seenKeys |> List.length)
    }

task { do! Tests.seedRecordings; do! Tests.paginateAllRecordings } |> _.Wait()

printfn "Done. Key takeaway: the token is a skip(N) offset, not a keyset cursor."
printfn "A client that treats empty-token as 'caught up' pays one extra round-trip."
printfn "Use results.length < limit as the real done-signal."
