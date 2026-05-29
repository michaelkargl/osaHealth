// 01-filter-basics.fsx
// ── Dapr Query API spike: filter behaviour ──
// Exercised against Dapr 1.17.7 + state.mongodb/v1.
//
// Run: dotnet fsi 01-filter-basics.fsx
// Prerequisites: docker-compose up (daprd + MongoDB), or a standalone
// daprd sidecar with a MongoDB state store component named "statestore".
// Adjust BASE below if the sidecar listens on a different port.

open System
open System.Net.Http
open System.Text
open System.Text.Json

let BASE = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT")
           |> Option.ofObj
           |> Option.defaultValue "http://localhost:3500"
let STORE = "statestore"

let http = new HttpClient()
http.BaseAddress <- Uri(BASE)

let json (o: obj) = JsonSerializer.Serialize(o)
let pretty (s: string) =
    try JsonSerializer.Serialize(JsonDocument.Parse(s).RootElement, JsonSerializerOptions(WriteIndented = true))
    with _ -> s

let post (path: string) (body: string) = task {
    let! resp = http.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"))
    let! txt = resp.Content.ReadAsStringAsync()
    return resp.StatusCode, txt.Trim()
}

// ── helpers ──
let show (label: string) (status, body) =
    printfn "--- %s ---" label
    printfn "HTTP %A" status
    if body = "" then printfn "(empty body)\n"
    else printfn "%s\n" (pretty body)

// Seed a handful of recordings so we have data to query.
let seed = task {
    let recs = [|
        {| key = "rec-A-01"; value = {| userId = "user-A"; date_epoch = 20260501; updated_ms = 1700000001000L; deleted = false |} |}
        {| key = "rec-A-02"; value = {| userId = "user-A"; date_epoch = 20260505; updated_ms = 1700000002000L; deleted = false |} |}
        {| key = "rec-A-03"; value = {| userId = "user-A"; date_epoch = 20260510; updated_ms = 1700000003000L; deleted = false |} |}
        {| key = "rec-B-01"; value = {| userId = "user-B"; date_epoch = 20260501; updated_ms = 1700000004000L; deleted = false |} |}
    |]
    let! status, body = post $"/v1.0/state/{STORE}" (json recs)
    printfn "Seed: HTTP %A  %s\n" status (if body = "" then "(ok)" else body)
}

// ── 1. Equality filter on userId ──
// Expected: returns only user-A keys (3 rows: rec-A-01, rec-A-02, rec-A-03).
// Actual:   works. Filter keys are UNPREFIXED; { EQ: { "value.userId": ... } } silently matches nothing.
let eqFilter = task {
    let q = {|
        filter = {| EQ = {| userId = "user-A" |} |}
    |}
    let! r = post $"/v1.0-alpha1/state/{STORE}/query" (json q)
    show "1. EQ filter on userId (expected: 3 user-A records)" r
}

// ── 2. Date-range filter on a STRING field ──
// Expected: ISO date string range works.
// Actual:   ERR_STATE_QUERY — "string type not permitted".
//           Range filters force numeric storage (epoch-ms or yyyymmdd int).
let stringRange = task {
    let q = {|
        filter = {| AND = [|
            {| EQ = {| userId = "user-A" |} |}
            {| GTE = {| date = "2026-05-01" |} |}
            {| LTE = {| date = "2026-05-10" |} |}
        |] |}
    |}
    let! r = post $"/v1.0-alpha1/state/{STORE}/query" (json q)
    show "2. String date-range (expected: works / actual: REJECTED — string type not permitted)" r
}

// ── 3. Date-range filter on a NUMERIC field ──
// Expected: range on a numeric field works.
// Actual:   works. Numeric range filtering is the viable path.
let numericRange = task {
    let q = {|
        filter = {| AND = [|
            {| EQ = {| userId = "user-A" |} |}
            {| GTE = {| date_epoch = 20260501 |} |}
            {| LTE = {| date_epoch = 20260505 |} |}
        |] |}
    |}
    let! r = post $"/v1.0-alpha1/state/{STORE}/query" (json q)
    show "3. Numeric date-range (expected: 2 records, rec-A-01 and rec-A-02)" r
}

// ── run ──
task {
    do! seed
    do! eqFilter
    do! stringRange
    do! numericRange
} |> _.Wait()

printfn "Done. Key takeaway: store date/timestamp fields as numbers (epoch-ms or yyyymmdd int), not ISO strings."
