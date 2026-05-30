// 01-filter-basics.fsx
// ── Dapr Query API spike: filter behaviour ──
// Exercised against Dapr 1.17.7 + state.mongodb/v1.
//
// Run: dotnet fsi 01-filter-basics.fsx [--dapr-endpoint http://localhost:3500] [--store-name statestore]
// Prerequisites: docker-compose up (daprd + MongoDB), or a standalone
// daprd sidecar with a MongoDB state store component named "statestore".

#load "Framework.fsx"

open System

// ── Test cases ──
module Tests =

    // Seed a handful of recordings so we have data to query.
    let seedRecordings = task {
        let recordings = [|
            {| key = "rec-A-01"; value = {| userId = "user-A"; date_epoch = 20260501; updated_ms = 1700000001000L; deleted = false |} |}
            {| key = "rec-A-02"; value = {| userId = "user-A"; date_epoch = 20260505; updated_ms = 1700000002000L; deleted = false |} |}
            {| key = "rec-A-03"; value = {| userId = "user-A"; date_epoch = 20260510; updated_ms = 1700000003000L; deleted = false |} |}
            {| key = "rec-B-01"; value = {| userId = "user-B"; date_epoch = 20260501; updated_ms = 1700000004000L; deleted = false |} |}
        |]
        let! (statusCode, body) = recordings |> Dapr.writeState
        printfn "Seed: HTTP %A  %s\n" statusCode (if body = "" then "(ok)" else body)
    }

    // ── 1. Equality filter on userId ──
    // Expected: returns only user-A keys (3 rows: rec-A-01, rec-A-02, rec-A-03).
    // Actual:   works. Filter keys are UNPREFIXED; { EQ: { "value.userId": ... } } silently matches nothing.
    let equalityFilterTest = task {
        let! response =
            {| filter = {| EQ = {| userId = "user-A" |} |} |}
            |> Dapr.query
        Api.displayResponse "1. EQ filter on userId (expected: 3 user-A records)" response
    }

    // ── 2. Date-range filter on a STRING field ──
    // Expected: ISO date string range works.
    // Actual:   ERR_STATE_QUERY — "string type not permitted".
    //           Range filters force numeric storage (epoch-ms or yyyymmdd int).
    let stringDateRangeTest = task {
        let! response =
            {| filter = {| AND = [|
                {| EQ = {| userId = "user-A" |} |}
                {| GTE = {| date = "2026-05-01" |} |}
                {| LTE = {| date = "2026-05-10" |} |}
            |] |} |}
            |> Dapr.query
        Api.displayResponse "2. String date-range (expected: works / actual: REJECTED — string type not permitted)" response
    }

    // ── 3. Date-range filter on a NUMERIC field ──
    // Expected: range on a numeric field works.
    // Actual:   works. Numeric range filtering is the viable path.
    let numericDateRangeTest = task {
        let! response =
            {| filter = {| AND = [|
                {| EQ = {| userId = "user-A" |} |}
                {| GTE = {| date_epoch = 20260501 |} |}
                {| LTE = {| date_epoch = 20260505 |} |}
            |] |} |}
            |> Dapr.query
        Api.displayResponse "3. Numeric date-range (expected: 2 records, rec-A-01 and rec-A-02)" response
    }

// ── run ──
task {
    do! Tests.seedRecordings
    do! Tests.equalityFilterTest
    do! Tests.stringDateRangeTest
    do! Tests.numericDateRangeTest
} |> _.Wait()

printfn "Done. Key takeaway: store date/timestamp fields as numbers (epoch-ms or yyyymmdd int), not ISO strings."
