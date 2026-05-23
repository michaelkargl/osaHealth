module Types

open System.Text.Json.Serialization

// ── API types ──────────────────────────────────────────────────────────────────

[<CLIMutable>]
type RecordingInput = {
    [<JsonPropertyName("userId")>] UserId: string
    [<JsonPropertyName("recordedAt")>] RecordedAt: float
    [<JsonPropertyName("notes")>] Notes: string
}

[<CLIMutable>]
type Recording = {
    [<JsonPropertyName("id")>] Id: string
    [<JsonPropertyName("userId")>] UserId: string
    [<JsonPropertyName("recordedAt")>] RecordedAt: float
    [<JsonPropertyName("notes")>] Notes: string
}

// Three-field error envelope (OSA-43)
type ApiError = {
    [<JsonPropertyName("code")>] Code: string
    [<JsonPropertyName("message")>] Message: string
    [<JsonPropertyName("target")>] Target: string
}

// ── DAPR state query response ──────────────────────────────────────────────────

[<CLIMutable>]
type DaprQueryItem = {
    [<JsonPropertyName("key")>] Key: string
    [<JsonPropertyName("data")>] Data: Recording
}

[<CLIMutable>]
type DaprQueryResponse = {
    [<JsonPropertyName("results")>] Results: DaprQueryItem array
}
