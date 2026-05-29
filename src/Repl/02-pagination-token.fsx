// 02-pagination-token.fsx
// ── Dapr Query API spike: pagination token = skip-offset ──
// Demonstrates that the token is literally a skip offset, not an opaque
// keyset cursor. Also shows the "empty token = caught up" gotcha.
//
// Run: dotnet fsi 02-pagination-token.fsx
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

let decodeToken (t: string) =
    if String.IsNullOrEmpty t then "(empty)"
    else
        let mutable asInt = 0
        if Int32.TryParse(t, &asInt) then $"raw=\"%s{t}\"  asInt=%d{asInt}  ← skip-offset"
        else $"raw=\"%s{t}\" (non-numeric)"

// Seed 12 user-A recordings (total > page size so pagination is forced).
let seed = task {
    let recs = [|
        for i in 0..11 do
            {| key = $"rec-A-%02d{i + 1}"
               value = {|
                   userId = "user-A"
                   date_epoch = 20260501 + i
                   updated_ms = 1700000000000L + int64 i
                   deleted = false
               |} |}
    |]
    let! status, body = post $"/v1.0/state/{STORE}" (json recs)
    printfn "Seed 12 records: HTTP %A  %s\n" status (if body = "" then "(ok)" else body)
}

// ── Paginate through the full set with limit=4 ──
// Expected: opaque keyset token; last data page returns empty token.
// Actual:   token = skip offset (4, 8, 12); last data page still has a
//           non-empty token; the empty token only arrives on a TRAILING
//           zero-result page.
let pageAll = task {
    printfn "=== Paginating user-A recordings, limit=4, sorted by (updated_ms) ==="
    let mutable token = ""
    let mutable page = 0
    let mutable seen = []
    let mutable doneFlag = false

    while not doneFlag do
        page <- page + 1
        let q = {|
            filter = {| EQ = {| userId = "user-A" |} |}
            sort = [| {| key = "updated_ms"; order = "ASC" |} |]
            page = {| limit = 4
                      token = token |}
        |}
        let! status, body = post $"/v1.0-alpha1/state/{STORE}/query" (json q)

        if status <> System.Net.HttpStatusCode.OK then
            printfn "page %d: HTTP %A — %s" page status body
            doneFlag <- true
        else
            let doc = JsonDocument.Parse(if body = "" then "{}" else body)
            let results = doc.RootElement.GetProperty("results")
            let keys = [| for r in results.EnumerateArray() -> r.GetProperty("key").GetString() |]
            let tok = if doc.RootElement.TryGetProperty("token", &doc.RootElement)
                      then let t = doc.RootElement.GetProperty("token").GetString()
                           if isNull t then "" else t
                      else ""

            printfn "page %d: keys=[%s]" page (String.Join(", ", keys))
            printfn "         token=%s" (decodeToken tok)
            if keys.Length = 0 then
                printfn "         ← zero-result page — NOW empty token means done\n"
            else
                printfn "         ← still has data; empty token only on zero-result page\n"

            seen <- seen @ (List.ofArray keys)
            token <- tok

            if String.IsNullOrEmpty tok then doneFlag <- true
            elif page >= 20 then
                printfn "SAFETY STOP"
                doneFlag <- true

    printfn "Total seen: %d  unique: %d" (List.length seen) (List.distinct seen |> List.length)
}

task { do! seed; do! pageAll } |> _.Wait()

printfn "Done. Key takeaway: the token is a skip(N) offset, not a keyset cursor."
printfn "A client that treats empty-token as 'caught up' pays one extra round-trip."
printfn "Use results.length < limit as the real done-signal."
