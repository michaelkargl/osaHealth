module osaHealth.Api.Dapr

open System.Net.Http
open System.Net.Http.Json
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Types

let saveRecording (client: HttpClient) (recording: Recording) : Task =
    task {
        let payload =
            [| {| key = recording.Id
                  value = recording |} |]

        let query = $"%s{Config.DaprBaseUrl}/v1.0/state/%s{Config.StoreName}"
        let! result = client.PostAsJsonAsync(query, payload)

        if not result.IsSuccessStatusCode then
            let! body = result.Content.ReadAsStringAsync()
            failwith $"DAPR_SAVE_FAILED: {recording.UserId} status={int result.StatusCode} body={body}"
    }

let private buildQueryJson (userId: string) (fromMs: float) (toMs: float) : string =
    {|
        filter = {|
            AND =
                [|
                    {| EQ = {| userId = userId |} |} :> obj
                    {| GTE = {| recordedAt = fromMs |} |} :> obj
                    {| LTE = {| recordedAt = toMs |} |} :> obj
                |]
        |}
    |}
    |> JsonSerializer.Serialize

let queryRecordings
    (client: HttpClient)
    (userId: string)
    (fromMs: float)
    (toMs: float)
    (logger: ILogger)
    : Task<Recording array> =
    task {
        let query = buildQueryJson userId fromMs toMs
        let content = new StringContent(query, Encoding.UTF8, "application/json")

        let query = $"%s{Config.DaprBaseUrl}/v1.0-alpha1/state/%s{Config.StoreName}/query"
        logger.LogDebug("Querying {Query}", query)
        let! response = client.PostAsync(query, content)

        let! resultText = response.Content.ReadAsStringAsync()
        logger.LogDebug("Received response: {ResponseText}", resultText)

        let! result = resultText |> Json.tryDeserializeAsync

        return
            match result with
            | Ok value -> value.Results |> Array.map _.Data
            | Error _ -> [||]
    }
