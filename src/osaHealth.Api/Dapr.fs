module osaHealth.Api.Dapr

open System.Net.Http
open System.Net.Http.Json
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Oxpecker
open Types

let saveRecording (client: HttpClient) (recording: Recording) : Task =
    task {  
        let payload =
            [| {| key = recording.Id
                  value = recording |} |]

        let query = $"%s{Config.DaprBaseUrl}/v1.0/state/%s{Config.StoreName}"
        let! result = client.PostAsJsonAsync(query, payload)
        
        if not result.IsSuccessStatusCode then
            failwith "DAPR_SAVE_FAILED: DAPR save failed for user {UserId}: {Status} {Body}"
    }

// The filter keys use dot-notation paths ("value.userId") which cannot be expressed
// as F# anonymous-record field names, so the body is built with sprintf.
// JsonSerializer.Serialize ensures values are properly quoted and escaped.
// ReSharper disable once FSharpInterpolatedString
let private buildQueryJson (userId: string) (fromMs: int64) (toMs: int64) : string =
    sprintf
        """{"filter":{"AND":[{"EQ":{"value.userId":%s}},{"GTE":{"value.recordedAt":%d}},{"LTE":{"value.recordedAt":%d}}]},"sort":[{"key":"value.recordedAt","order":"ASC"}]}"""
        (JsonSerializer.Serialize(userId))
        fromMs
        toMs

let queryRecordings
    (client: HttpClient)
    (userId: string)
    (fromMs: int64)
    (toMs: int64)
    : Task<Recording array> =
    task {
        let content =
            new StringContent(buildQueryJson userId fromMs toMs, Encoding.UTF8, "application/json")

        let query = $"%s{Config.DaprBaseUrl}/v1.0-alpha1/state/%s{Config.StoreName}/query"
        let! response = client.PostAsync(query, content)
        let! resultPlain = response.Content.ReadAsStringAsync()
        let result = resultPlain |> Json.JsonSerializer.Deserialize

        return
            match result |> Option.ofObj with
            | Some response -> response.Results |> Array.map _.Data
            | None -> [||]
    }
