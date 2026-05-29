// 03-stability-under-insert.fsx
// ── Dapr Query API spike: offset pagination is unstable under concurrent writes ──
// Reproduces a duplicate row when a record sorting before the current page
// is inserted mid-pagination. This is the disqualifier for the sync loop.
//
// Run: dotnet fsi 03-stability-under-insert.fsx [--dapr-endpoint http://localhost:3500] [--store-name statestore]
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

let argumentParser = ArgumentParser.Create<CliArguments>(programName = "dotnet fsi 03-stability-under-insert.fsx")
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

// ── Test cases ──
module Tests =

    // We use 6 recordings + limit=3 so:
    //   Page 1 → rec-01, rec-02, rec-03
    //   Page 2 → rec-04, rec-05, rec-06  (when no concurrent writes)
    let seedRecordings = task {
        let recordings = [|
            for index in 0..5 do
                {| key = $"rec-%02d{index + 1}"
                   value = {|
                       userId = "user-A"
                       updated_ms = 1700000000000L + int64 (index * 1000)
                       deleted = false
                   |} |}
        |]
        let! response = Json.serialize recordings |> Api.postQueryAsync $"/v1.0/state/{storeName}"
        printfn "Seed 6 records: HTTP %A  %s\n" (fst response) (if snd response = "" then "(ok)" else snd response)
    }

    // ── Stability test ──
    let stabilityTest = task {
        printfn "=== Stability under mid-pagination insert ===\n"

        // Page 1
        let pageOneQuery = {|
            filter = {| EQ = {| userId = "user-A" |} |}
            sort = [| {| key = "updated_ms"; order = "ASC" |} |]
            page = {| limit = 3 |}
        |}
        let! pageOneStatus, pageOneBody = Json.serialize pageOneQuery |> Api.postQueryAsync $"/v1.0-alpha1/state/{storeName}/query"
        let pageOneDocument = JsonDocument.Parse(if pageOneBody = "" then "{}" else pageOneBody)
        let pageOneKeys = [| for result in pageOneDocument.RootElement.GetProperty("results").EnumerateArray() -> result.GetProperty("key").GetString() |]
        let pageOneToken =
            let mutable tokenElement = Unchecked.defaultof<JsonElement>
            if pageOneDocument.RootElement.TryGetProperty("token", &tokenElement) then
                let tokenValue = tokenElement.GetString()
                if isNull tokenValue then "" else tokenValue
            else ""

        printfn "EXPECTED: stable keyset — page 1 returns rec-01, rec-02, rec-03"
        printfn "  page 1: keys=[%s]  token=\"%s\"\n" (String.Join(", ", pageOneKeys)) pageOneToken

        // Insert a NEW recording whose updated_ms sorts at position 0 (before rec-01).
        printfn "INSERT:  rec-INSERT with updated_ms=1699999999000 (before rec-01)\n"
        let! _ = Json.serialize [|
            {| key = "rec-INSERT"
               value = {|
                   userId = "user-A"
                   updated_ms = 1699999999000L
                   deleted = false
               |} |}
        |] |> Api.postQueryAsync $"/v1.0/state/{storeName}"

        // Resume from page-1 token
        let pageTwoQuery = {|
            filter = {| EQ = {| userId = "user-A" |} |}
            sort = [| {| key = "updated_ms"; order = "ASC" |} |]
            page = {| limit = 3; token = pageOneToken |}
        |}
        let! pageTwoStatus, pageTwoBody = Json.serialize pageTwoQuery |> Api.postQueryAsync $"/v1.0-alpha1/state/{storeName}/query"
        let pageTwoDocument = JsonDocument.Parse(if pageTwoBody = "" then "{}" else pageTwoBody)
        let pageTwoKeys = [| for result in pageTwoDocument.RootElement.GetProperty("results").EnumerateArray() -> result.GetProperty("key").GetString() |]

        printfn "EXPECTED: page 2 returns rec-04, rec-05, rec-06 (no offset shift)"
        printfn "  page 2: keys=[%s]" (String.Join(", ", pageTwoKeys))
        let allSeenKeys = Array.append pageOneKeys pageTwoKeys
        let duplicateKeys = allSeenKeys |> Array.groupBy id |> Array.filter (fun (_, occurrences) -> Seq.length occurrences > 1) |> Array.map fst
        let insertedNotSeen = [| "rec-INSERT" |] |> Array.filter (fun key -> not (Array.contains key allSeenKeys))
        printfn ""
        printfn "RESULT:   duplicates=[%s]  inserted-observed=%b"
            (if duplicateKeys.Length = 0 then "none" else String.Join(", ", duplicateKeys))
            (Array.contains "rec-INSERT" allSeenKeys)
        printfn ""
        if duplicateKeys.Length > 0 then
            printfn "FAIL: duplicate row delivered — offset pagination is unstable under insert."
        else
            printfn "(No duplicates — offset was stable for this particular timing.)"
    }

task { do! Tests.seedRecordings; do! Tests.stabilityTest } |> _.Wait()

printfn "Done. Key takeaway: offset-based pagination skips or duplicates rows when"
printfn "the result set changes between pages. A sync loop needs a keyset cursor."
