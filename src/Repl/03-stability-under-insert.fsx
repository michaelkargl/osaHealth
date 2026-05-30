// 03-stability-under-insert.fsx
// ── Dapr Query API spike: offset pagination is unstable under concurrent writes ──
// Reproduces a duplicate row when a record sorting before the current page
// is inserted mid-pagination. This is the disqualifier for the sync loop.
//
// Run: dotnet fsi 03-stability-under-insert.fsx [--dapr-endpoint http://localhost:3500] [--store-name statestore]
// Prerequisites: Dapr sidecar + MongoDB reachable, statestore component loaded.

#load "Framework.fsx"

open System
open System.Text.Json

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
        let! (statusCode, body) = recordings |> Dapr.writeState
        printfn "Seed 6 records: HTTP %A  %s\n" statusCode (if body = "" then "(ok)" else body)
    }

    let private parseKeysAndToken (body: string) =
        let document = JsonDocument.Parse(if body = "" then "{}" else body)
        let keys = [| for result in document.RootElement.GetProperty("results").EnumerateArray() -> result.GetProperty("key").GetString() |]
        let token =
            let mutable tokenElement = Unchecked.defaultof<JsonElement>
            if document.RootElement.TryGetProperty("token", &tokenElement) then
                let tokenValue = tokenElement.GetString()
                if isNull tokenValue then "" else tokenValue
            else ""
        keys, token

    // ── Stability test ──
    let stabilityTest = task {
        printfn "=== Stability under mid-pagination insert ===\n"

        // Page 1
        let pageOneQuery = {|
            filter = {| EQ = {| userId = "user-A" |} |}
            sort = [| {| key = "updated_ms"; order = "ASC" |} |]
            page = {| limit = 3 |}
        |}
        let! (pageOneStatus, pageOneBody) = pageOneQuery |> Dapr.query
        let pageOneKeys, pageOneToken = parseKeysAndToken pageOneBody

        printfn "EXPECTED: stable keyset — page 1 returns rec-01, rec-02, rec-03"
        printfn "  page 1: keys=[%s]  token=\"%s\"\n" (String.Join(", ", pageOneKeys)) pageOneToken

        // Insert a NEW recording whose updated_ms sorts at position 0 (before rec-01).
        printfn "INSERT:  rec-INSERT with updated_ms=1699999999000 (before rec-01)\n"
        let! _ = [|
            {| key = "rec-INSERT"
               value = {|
                   userId = "user-A"
                   updated_ms = 1699999999000L
                   deleted = false
               |} |}
        |] |> Dapr.writeState

        // Resume from page-1 token
        let pageTwoQuery = {|
            filter = {| EQ = {| userId = "user-A" |} |}
            sort = [| {| key = "updated_ms"; order = "ASC" |} |]
            page = {| limit = 3; token = pageOneToken |}
        |}
        let! (pageTwoStatus, pageTwoBody) = pageTwoQuery |> Dapr.query
        let pageTwoKeys, _ = parseKeysAndToken pageTwoBody

        printfn "EXPECTED: page 2 returns rec-04, rec-05, rec-06 (no offset shift)"
        printfn "  page 2: keys=[%s]" (String.Join(", ", pageTwoKeys))
        let allSeenKeys = Array.append pageOneKeys pageTwoKeys
        let duplicateKeys = allSeenKeys |> Array.groupBy id |> Array.filter (fun (_, occurrences) -> Seq.length occurrences > 1) |> Array.map fst
        let insertedNotSeen = not (Array.contains "rec-INSERT" allSeenKeys)
        printfn ""
        printfn "RESULT:   duplicates=[%s]  inserted-observed=%b"
            (if duplicateKeys.Length = 0 then "none" else String.Join(", ", duplicateKeys))
            (not insertedNotSeen)
        printfn ""
        if duplicateKeys.Length > 0 then
            printfn "FAIL: duplicate row delivered — offset pagination is unstable under insert."
        else
            printfn "(No duplicates — offset was stable for this particular timing.)"
    }

task { do! Tests.seedRecordings; do! Tests.stabilityTest } |> _.Wait()

printfn "Done. Key takeaway: offset-based pagination skips or duplicates rows when"
printfn "the result set changes between pages. A sync loop needs a keyset cursor."
