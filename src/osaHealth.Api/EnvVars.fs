module osaHealth.Api.EnvVars

open System

type EnvVars =
    { ConnectionString: string
      DatabaseName: string }

let private getOptional (name: string) =
    Environment.GetEnvironmentVariable(name)

let private getRequired (name: string) : string =
    match getOptional name with
    | null -> failwith $"Required environment variable '{name}' is not set."
    | value -> value

module EnvVars =
    let create (): EnvVars =
        { ConnectionString = getRequired "MongoDB__ConnectionString"
          DatabaseName = getRequired "MongoDB__DatabaseName" }
