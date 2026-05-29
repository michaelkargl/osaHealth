// 03-stability-under-insert.fsx
// ── Dapr Query API spike: offset pagination is unstable under concurrent writes ──
// Reproduces a duplicate row when a record sorting before the current page
// is inserted mid-pagination. This is the disqualifier for the sync loop.
//
// Run: dotnet fsi 03-stability-under-insert.fsx
// Prerequisites: Dapr sidecar + MongoDB reachable, statestore component loaded.

open System
open System.Net.Http
open System.Text
open System.Text.Json

let BASE = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT")
           |> Option.ofObj
           |> Option.defaultValue "http://localhost:3500"
let STORE = "statestore"

let http = new HttpClient(BaseAddress = Uri(BASE))

let json (o: obj) = JsonSerializer.Serialize(o)

let post (path: string) (body: string) = task {
    let! resp = http.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"))
    let! txt = resp.Content.ReadAsStringAsync()
    return resp.StatusCode, txt.Trim()
}

// We use 6 recordings + limit=3 so:
//   Page 1 → rec-01, rec-02, rec-03
//   Page 2 → rec-04, rec-05, rec-06  (when no concurrent writes)
let seed = task {
    let recs = [|
        for i in 0..5 do
            {| key = $"rec-%02d{i + 1}"
               value = {|
                   userId = "user-A"
                   updated_ms = 1700000000000L + int64 (i * 1000)
                   deleted = false
               |} |}
    |]
    let! status, body = post $"/v1.0/state/{STORE}" (json recs)
    printfn "Seed 6 records: HTTP %A  %s\n" status (if body = "" then "(ok)" else body)
}

// ── Stability test ──
let test = task {
    printfn "=== Stability under mid-pagination insert ===\n"

    // Page 1
    let q1 = {|
        filter = {| EQ = {| userId = "user-A" |} |}
        sort = [| {| key = "updated_ms"; order = "ASC" |} |]
        page = {| limit = 3 |}
    |}
    let! status1, body1 = post $"/v1.0-alpha1/state/{STORE}/query" (json q1)
    let doc1 = JsonDocument.Parse(if body1 = "" then "{}" else body1)
    let keys1 = [| for r in doc1.RootElement.GetProperty("results").EnumerateArray() -> r.GetProperty("key").GetString() |]
    let token1 =
        let mutable el = Unchecked.defaultof<JsonElement>
        if doc1.RootElement.TryGetProperty("token", &el) then
            let t = el.GetString(); if isNull t then "" else t
        else ""

    printfn "EXPECTED: stable keyset — page 1 returns rec-01, rec-02, rec-03"
    printfn "  page 1: keys=[%s]  token=\"%s\"\n" (String.Join(", ", keys1)) token1

    // Insert a NEW recording whose updated_ms sorts at position 0 (before rec-01).
    printfn "INSERT:  rec-INSERT with updated_ms=1699999999000 (before rec-01)\n"
    let! _ = post $"/v1.0/state/{STORE}" (json [|
        {| key = "rec-INSERT"
           value = {|
               userId = "user-A"
               updated_ms = 1699999999000L
               deleted = false
           |} |}
    |])

    // Resume from page-1 token
    let q2 = {|
        filter = {| EQ = {| userId = "user-A" |} |}
        sort = [| {| key = "updated_ms"; order = "ASC" |} |]
        page = {| limit = 3; token = token1 |}
    |}
    let! status2, body2 = post $"/v1.0-alpha1/state/{STORE}/query" (json q2)
    let doc2 = JsonDocument.Parse(if body2 = "" then "{}" else body2)
    let keys2 = [| for r in doc2.RootElement.GetProperty("results").EnumerateArray() -> r.GetProperty("key").GetString() |]

    printfn "EXPECTED: page 2 returns rec-04, rec-05, rec-06 (no offset shift)"
    printfn "  page 2: keys=[%s]" (String.Join(", ", keys2))
    let all = Array.append keys1 keys2
    let dup = all |> Array.groupBy id |> Array.filter (fun (_, g) -> Seq.length g > 1) |> Array.map fst
    let missing = [| "rec-INSERT" |] |> Array.filter (fun k -> not (Array.contains k all))
    printfn ""
    printfn "RESULT:   duplicates=[%s]  inserted-observed=%b"
        (if dup.Length = 0 then "none" else String.Join(", ", dup))
        (Array.contains "rec-INSERT" all)
    printfn ""
    if dup.Length > 0 then
        printfn "FAIL: duplicate row delivered — offset pagination is unstable under insert."
    else
        printfn "(No duplicates — offset was stable for this particular timing.)"
}

task { do! seed; do! test } |> _.Wait()

printfn "Done. Key takeaway: offset-based pagination skips or duplicates rows when"
printfn "the result set changes between pages. A sync loop needs a keyset cursor."
