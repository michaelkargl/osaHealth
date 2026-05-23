module osaHealth.Api.Json

open System.Text.Json
open System.Threading.Tasks

let tryDeserializeAsync<'T when 'T : not struct and 'T : not null> (str: string) : Task<Result<'T, string>> =
    task {
        try
            let value: 'T | null = JsonSerializer.Deserialize<'T>(str)
            return
                match value with
                | null -> Error "deserialization returned null"
                | value -> Ok value
        with ex ->
            return Error ex.Message
    }