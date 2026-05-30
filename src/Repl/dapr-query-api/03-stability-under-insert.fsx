// 03-stability-under-insert.fsx
// ── Dapr Query API spike: offset pagination is unstable under concurrent writes ──
// Reproduces a duplicate row when a record sorting before the current page
// is inserted mid-pagination. This is the disqualifier for the sync loop.
//
// Run: dotnet fsi 03-stability-under-insert.fsx [--dapr-endpoint http://localhost:3500] [--store-name statestore]
// Prerequisites: docker-compose up (daprd + MongoDB), or a standalone
// daprd sidecar with a MongoDB state store component named "statestore".

#r "nuget: Argu"
#r "nuget: FSharp.UMX"
#load "Framework.fsx"

open System
open System.Net
open System.Text.Json
open System.Threading.Tasks
open Argu
open Framework
open FSharp.UMX

type CliArguments =
    | DaprEndpoint of daprEndpoint: string
    | StoreName of storeName: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | DaprEndpoint _ -> "Dapr HTTP endpoint. Default: http://localhost:3500"
            | StoreName _ -> "Dapr state store name. Default: statestore"

let argumentParser =
    ArgumentParser.Create<CliArguments>(programName = "dotnet fsi <script>.fsx")

let cliArguments = argumentParser.Parse(fsi.CommandLineArgs |> Array.tail)

let daprHttpEndpoint =
    cliArguments.GetResult(DaprEndpoint, defaultValue = "http://localhost:3500")
    |> UMX.tag

let storeName =
    cliArguments.GetResult(StoreName, defaultValue = "statestore") |> UMX.tag

module Dapr =
    let writeState: obj -> Task<HttpStatusCode * string<HttpResponse>> =
        Dapr.writeState daprHttpEndpoint storeName

    let query: obj -> Task<HttpStatusCode * string<HttpResponse>> =
        Dapr.query daprHttpEndpoint storeName

    let queryUserPage = Dapr.queryUserPage daprHttpEndpoint storeName
    let getPageKeys   = Dapr.getPageKeys
    let getPageToken  = Dapr.getPageToken

let sectionSeparator = String.replicate 72 "═"

let printSection (n: int) (title: string) (description: string) : unit =
    printfn "\n%s" sectionSeparator
    printfn "  TEST %d — %s" n title
    printfn "%s" sectionSeparator
    printfn "%s\n" description

// ── Test cases ──
module Tests =
    // We use 6 recordings + limit=3 so:
    //   Page 1 → rec-01, rec-02, rec-03
    //   Page 2 → rec-04, rec-05, rec-06  (when no concurrent writes)
    let seedRecordings () : Task<unit> =
        task {
            printfn "\n%s" sectionSeparator
            printfn "  SEED — writing 6 test records to the state store"
            printfn "%s" sectionSeparator
            printfn "Writes rec-01 through rec-06 for user-A, sorted by updated_ms."
            printfn "With limit=3: page 1 → rec-01..03, page 2 → rec-04..06 (absent concurrent writes).\n"

            let recordings =
                [| for index in 0..5 do
                       {| key = $"rec-%02d{index + 1}"
                          value =
                           {| userId = "user-A"
                              updated_ms = 1700000000000L + int64 (index * 1000)
                              deleted = false |} |} |]

            let! statusCode, response = Dapr.writeState recordings
            let response = response |> UMX.untag
            printfn "Seed: HTTP %A  %s\n" statusCode (if response = "" then "(ok)" else response)
        }

    let private parseResponse (response: string) : JsonDocument =
        response |> String.defaultIfNullOrWhiteSpace "{}" |> JsonDocument.Parse

    let private findDuplicates (keys: string array) : string array =
        keys
        |> Array.groupBy id
        |> Array.filter (fun (_, occurrences) -> Seq.length occurrences > 1)
        |> Array.map fst

    let stabilityTest (userId: string) (pageSize: int) : Task<unit> =
        task {
            printSection 1 "Stability under mid-pagination insert" $"""Fetch page 1 (limit=%i{pageSize}, sort=updated_ms ASC), then insert a record whose
updated_ms sorts BEFORE rec-01, then fetch page 2 with the page-1 token.
Expected: keyset cursor — page 2 returns rec-04, rec-05, rec-06; no duplicates.
Actual:   offset cursor shifts — rec-03 re-appears on page 2 as a duplicate.
Takeaway: offset pagination is unstable under concurrent writes; a sync loop
          needs a keyset cursor or a change-feed, not an offset token.
Keyset means: Uses the value to remember the position instead of relying on an offset.
              Instead of "give me rows 5-8" => "give me the next n rows where updated_ms > lastrow)"""

            // Page 1
            let! _, pageOneResponse = Dapr.queryUserPage userId pageSize ""
            let pageOne = pageOneResponse |> UMX.untag |> parseResponse
            let pageOneKeys = pageOne |> Dapr.getPageKeys
            let pageOneToken = pageOne |> Dapr.getPageToken

            printfn "  page 1: keys=[%s]  token=\"%s\"" (String.Join(", ", pageOneKeys)) pageOneToken

            // Insert a NEW recording whose updated_ms sorts at position 0 (before rec-01).
            printfn "\n  INSERT: rec-INSERT with updated_ms=1699999999000 (before rec-01)\n"
            let! _ =
                [| {| key = "rec-INSERT"
                      value =
                       {| userId = userId
                          updated_ms = 1699999999000L
                          deleted = false |} |} |]
                |> Dapr.writeState

            // Resume from the page-1 token.
            let! _, pageTwoResponse = Dapr.queryUserPage userId 3 pageOneToken
            let pageTwo = pageTwoResponse |> UMX.untag |> parseResponse
            let pageTwoKeys = pageTwo |> Dapr.getPageKeys

            printfn "  page 2: keys=[%s]" (String.Join(", ", pageTwoKeys))

            let allSeenKeys = Array.append pageOneKeys pageTwoKeys
            let duplicateKeys = findDuplicates allSeenKeys

            printfn ""
            printfn "  EXPECTED: page 2 returns rec-04, rec-05, rec-06 (no offset shift)"
            printfn "  RESULT:   duplicates=[%s]  rec-INSERT seen=%b"
                (if duplicateKeys.Length = 0 then "none" else String.Join(", ", duplicateKeys))
                (Array.contains "rec-INSERT" allSeenKeys)
            printfn ""

            if duplicateKeys.Length > 0 then
                printfn "FAIL: duplicate row delivered — offset pagination is unstable under insert."
            else
                printfn "(No duplicates observed — offset was stable for this particular timing.)"
        }

task {
    do! Tests.seedRecordings ()
    do! Tests.stabilityTest "user-A" 3
}
|> _.Wait()

printfn $"\n%s{sectionSeparator}"
printfn "  SUMMARY"
printfn $"%s{sectionSeparator}"
printfn "Offset-based pagination skips or duplicates rows when the result set changes"
printfn "between pages. A sync loop needs a keyset cursor or change-feed, not an offset token."
printfn $"%s{sectionSeparator}\n"
