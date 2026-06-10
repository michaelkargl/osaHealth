// 02-pagination-token.fsx
// ── Dapr Query API spike: pagination token = skip-offset ──
// Demonstrates that the token is literally a skip offset, not an opaque
// keyset cursor. Also shows the "empty token = caught up" gotcha.
//
// Run: dotnet fsi 02-pagination-token.fsx [--dapr-endpoint http://localhost:3500] [--store-name statestore]
// Prerequisites: docker-compose up (daprd + MongoDB), or a standalone
// daprd sidecar with a MongoDB state store component named "statestore".

#r "nuget: Argu"
#r "nuget: FSharp.UMX"

// Requires: dotnet build ../../osaHealth.Framework/osaHealth.Framework.fsproj
#r @"../../osaHealth.Framework/bin/Debug/net11.0/osaHealth.Framework.dll"

open System
open System.Net
open System.Text.Json
open System.Threading.Tasks
open Argu
open osaHealth.Framework
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

    let queryUserPage       = Dapr.queryUserPage       daprHttpEndpoint storeName
    let decodePaginationToken = Dapr.decodePaginationToken
    let getPageKeys           = Dapr.getPageKeys
    let getPageToken          = Dapr.getPageToken

let sectionSeparator = String.replicate 72 "═"

let printSection (n: int) (title: string) (description: string) : unit =
    printfn "\n%s" sectionSeparator
    printfn "  TEST %d — %s" n title
    printfn "%s" sectionSeparator
    printfn "%s\n" description



module Tests =

    let seedRecordings () : Task<unit> =
        task {
            printfn "\n%s" sectionSeparator
            printfn "  SEED — writing 12 test records to the state store"
            printfn "%s" sectionSeparator
            printfn "Writes rec-A-01 through rec-A-12 for user-A."
            printfn "Fields: userId (string), date_epoch (int yyyymmdd), updated_ms (int epoch-ms), deleted (bool).\n"

            let recordings =
                [| for index in 0..11 do
                       {| key = $"rec-A-%02d{index + 1}"
                          value =
                           {| userId = "user-A"
                              date_epoch = 20260501 + index
                              updated_ms = 1700000000000L + int64 index
                              deleted = false |} |} |]

            let! statusCode, response = Dapr.writeState recordings
            let response = response |> UMX.untag
            printfn "Seed: HTTP %A  %s\n" statusCode (if response = "" then "(ok)" else response)
        }

    let paginateAllRecordings (userId: string) (pageSize: int) : Task<unit> =
        task {
            printSection
                1
                $"Paginate all %s{userId} recordings in blocks of %i{pageSize}"
                """Query: { EQ: { userId: "user-A" } }, sort=[updated_ms ASC].
Expected: opaque keyset token; empty token on last data page means "nothing more to read".
          Keyset means: Uses the value to remember the position instead of relying on an offset.
                        Instead of "give me rows 5-8" => "give me the next n rows where updated_ms > lastrow)
          Opaque means: The server encodes the value into a blob the client can't and shouldn't inspect or construct itself.
                        It might look like "eyJ1cGRhdGVkX21zIjoxNzAwMDAwMDAzMDAwfQ==" and the client treats it as black box. 
Actual:   token = skip offset (4, 8, 12); last data page returns a non-empty token;
          empty token only arrives on a trailing zero-result page.
Takeaway: use results.length < limit as the real done-signal, not empty token."""

            let mutable paginationToken = ""
            let mutable page = 0
            let mutable seenKeys = []
            let mutable isComplete = false

            // iterate the pages
            while not isComplete do
                page <- page + 1

                let! statusCode, response = Dapr.queryUserPage userId pageSize paginationToken
                let response = response |> UMX.untag |> StringUtil.defaultIfNullOrWhiteSpace "{}"

                if statusCode <> HttpStatusCode.OK then
                    printfn $"page %d{page}: HTTP %A{statusCode} — %s{response}"
                    isComplete <- true
                else
                    let responseDocument = JsonDocument.Parse(response)
                    let pageKeys = responseDocument |> Dapr.getPageKeys                    
                    let currentToken = responseDocument |> Dapr.getPageToken
                        
                    printfn "page %d: keys=[%s]" page (String.Join(", ", pageKeys))
                    printfn $"        token=%s{Dapr.decodePaginationToken currentToken}"
                    printfn $"        raw=%s{response}"

                    if pageKeys.Length = 0 then
                        printfn "         ← zero-result page — NOW empty token means done\n"
                    else
                        printfn "         ← still has data; empty token only on zero-result page\n"

                    seenKeys <- seenKeys @ (List.ofArray pageKeys)
                    paginationToken <- currentToken

                    if String.IsNullOrEmpty currentToken then
                        isComplete <- true
                    elif page >= 20 then
                        printfn "SAFETY STOP"
                        isComplete <- true

            printfn $"Total seen: %d{List.length seenKeys}  unique: %d{List.distinct seenKeys |> List.length}"
        }

task {
    do! Tests.seedRecordings ()
    do! Tests.paginateAllRecordings "user-A" 4
}
|> _.Wait()

printfn $"\n%s{sectionSeparator}"
printfn "  SUMMARY"
printfn $"%s{sectionSeparator}"
printfn "The pagination token is a skip(N) offset, not a keyset cursor."
printfn "A client treating empty-token as 'caught up' pays one extra round-trip."
printfn "Use results.length < limit as the real done-signal."
printfn $"%s{sectionSeparator}\n"
