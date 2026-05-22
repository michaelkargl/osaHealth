module Types

open System.Text.Json.Serialization

// ── API types ──────────────────────────────────────────────────────────────────

[<CLIMutable>]
type RecordingInput = {
    [<JsonPropertyName("userId")>] UserId: string
    [<JsonPropertyName("recordedAt")>] RecordedAt: string // ISO 8601 UTC
    [<JsonPropertyName("notes")>] Notes: string
}

[<CLIMutable>]
type Recording = {
    [<JsonPropertyName("id")>] Id: string
    [<JsonPropertyName("userId")>] UserId: string
    [<JsonPropertyName("recordedAt")>] RecordedAt: int64 // Unix ms UTC
    [<JsonPropertyName("notes")>] Notes: string
}

// Three-field error envelope (OSA-43)
type ApiError = {
    [<JsonPropertyName("code")>] Code: string
    [<JsonPropertyName("message")>] Message: string
    [<JsonPropertyName("target")>] Target: string
}

// ── DAPR state query response ──────────────────────────────────────────────────

// v1.0-alpha1/state/{store}/query response shape
[<CLIMutable>]
type DaprQueryItem = {
    [<JsonPropertyName("key")>] Key: string
    [<JsonPropertyName("data")>] Data: Recording
}

[<CLIMutable>]
type DaprQueryResponse = {
    [<JsonPropertyName("results")>] Results: DaprQueryItem array
}
