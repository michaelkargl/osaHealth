// 01-filter-basics.fsx
// ── Dapr Query API spike: filter behaviour ──
// Exercised against Dapr 1.17.7 + state.mongodb/v1.
//
// Run: dotnet fsi 01-filter-basics.fsx [--daprendpoint http://localhost:3500] [--storename statestore]
// Prerequisites: docker-compose up (daprd + MongoDB), or a standalone
// daprd sidecar with a MongoDB state store component named "statestore".

#r "nuget: Argu"
#r "nuget: FSharp.UMX"

// Requires: dotnet build ../../osaHealth.Framework/osaHealth.Framework.fsproj
#r @"../../osaHealth.Framework/bin/Debug/net11.0/osaHealth.Framework.dll"

open System.Net
open System.Threading.Tasks
open Argu
open osaHealth.Framework
open osaHealth.Framework.Api
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

let separator = String.replicate 72 "═"
let printSection (n: int) (title: string) (description: string) =
    printfn "\n%s" separator
    printfn "  TEST %d — %s" n title
    printfn "%s" separator
    printfn "%s\n" description

// ── Test cases ──
module Tests =

    // Seed a handful of recordings so we have data to query.
    let seedRecordings () : Task<unit> =
        task {
            printfn "\n%s" separator
            printfn "  SEED — writing test records to the state store"
            printfn "%s" separator
            printfn "Writes 4 recordings: rec-A-01, rec-A-02, rec-A-03 (user-A) and rec-B-01 (user-B)."
            printfn "Fields: userId (string), date_epoch (int yyyymmdd), updated_ms (int epoch-ms), deleted (bool).\n"

            let recordings =
                [| {| key = "rec-A-01"
                      value =
                       {| userId = "user-A"
                          date_epoch = 20260501
                          updated_ms = 1700000001000L
                          deleted = false |} |}
                   {| key = "rec-A-02"
                      value =
                       {| userId = "user-A"
                          date_epoch = 20260505
                          updated_ms = 1700000002000L
                          deleted = false |} |}
                   {| key = "rec-A-03"
                      value =
                       {| userId = "user-A"
                          date_epoch = 20260510
                          updated_ms = 1700000003000L
                          deleted = false |} |}
                   {| key = "rec-B-01"
                      value =
                       {| userId = "user-B"
                          date_epoch = 20260501
                          updated_ms = 1700000004000L
                          deleted = false |} |} |]

            let! statusCode, response = Dapr.writeState recordings
            let response = response |> UMX.untag
            printfn "Seed: HTTP %A  %s\n" statusCode (if response = "" then "(ok)" else response)
        }

    let equalityFilterTest () : Task<unit> =
        task {
            printSection 1 "EQ filter on userId" """Query: { EQ: { userId: "user-A" } }
Expected: 3 records (rec-A-01, rec-A-02, rec-A-03).
Note: filter keys are UNPREFIXED field names. Using "value.userId" silently matches nothing."""

            let! response = {| filter = {| EQ = {| userId = "user-A" |} |} |} |> Dapr.query
            Api.displayResponse "Result" response
        }

    let stringDateRangeTest () : Task<unit> =
        task {
            printSection 2 "Range filter on a STRING date field" """Query: AND [ EQ userId="user-A", GTE date="2026-05-01", LTE date="2026-05-10" ]
Expected: range filtering on ISO date strings works.
Actual:   REJECTED — Dapr returns ERR_STATE_QUERY "string type not permitted" for GTE/LTE.
Takeaway: you cannot use string fields with range operators; store dates as numbers."""

            let! response =
                {| filter =
                    {| AND =
                        [| {| EQ = {| userId = "user-A" |} |} :> obj
                           {| GTE = {| date = "2026-05-01" |} |} :> obj
                           {| LTE = {| date = "2026-05-10" |} |} :> obj |] |} |}
                |> Dapr.query

            Api.displayResponse "Result" response
        }

    let numericDateRangeTest () : Task<unit> =
        task {
            printSection 3 "Range filter on a NUMERIC date field (yyyymmdd int)" """Query: AND [ EQ userId="user-A", GTE date_epoch=20260501, LTE date_epoch=20260505 ]
Expected: 2 records in the window — rec-A-01 (20260501) and rec-A-02 (20260505).
rec-A-03 (20260510) is outside the range and must not appear.
Takeaway: numeric range filtering works; this is the viable storage pattern."""

            let! response =
                {| filter =
                    {| AND =
                        [| {| EQ = {| userId = "user-A" |} |} :> obj
                           {| GTE = {| date_epoch = 20260501 |} |} :> obj
                           {| LTE = {| date_epoch = 20260505 |} |} :> obj |] |} |}
                |> Dapr.query

            Api.displayResponse "Result" response
        }

task {
    do! Tests.seedRecordings()
    do! Tests.equalityFilterTest()
    do! Tests.stringDateRangeTest()
    do! Tests.numericDateRangeTest()
}
|> _.Wait()

printfn "\n%s" separator
printfn "  SUMMARY"
printfn "%s" separator
printfn "Store date/timestamp fields as numbers (epoch-ms or yyyymmdd int), not ISO strings."
printfn "Dapr rejects GTE/LTE on string fields with ERR_STATE_QUERY at runtime — no compile-time warning."
printfn "%s\n" separator
