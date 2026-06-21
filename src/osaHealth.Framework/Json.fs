module osaHealth.Framework.Json

open System.Text.Json

let serialize (value: obj) = JsonSerializer.Serialize(value)

let prettyPrint (jsonString: string) =
    try
        JsonSerializer.Serialize(
            JsonDocument.Parse(jsonString).RootElement,
            JsonSerializerOptions(WriteIndented = true)
        )
    with _ ->
        jsonString

let tryGetJsonElement (prop: string) (document: JsonDocument) : JsonElement option =
    let mutable tokenElement = Unchecked.defaultof<JsonElement>

    if document.RootElement.TryGetProperty(prop, &tokenElement) then
        Some tokenElement
    else
        None

let getJsonElement (prop: string) (document: JsonDocument) : JsonElement =
    document |> tryGetJsonElement prop |> _.Value

let tryGetStringValue (prop: string) (document: JsonDocument) : string option =
    match tryGetJsonElement prop document with
    | Some element -> Some (element.GetString())
    | _ -> None

let getStringValue (prop: string) (document: JsonDocument): string =
    document |> tryGetStringValue prop |> _.Value