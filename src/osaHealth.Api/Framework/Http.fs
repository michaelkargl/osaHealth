module osaHealth.Api.Framework.Http

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Oxpecker
open osaHealth.Api.ErrorHandling

module HttpContext =

    let tryGetQueryParam (key: string) (ctx: HttpContext) : string option =
        ctx.Request.Query.TryGetValue(key)
        |> function
            | true, value -> value.ToString() |> Some
            | false, _ -> None

    let getRequiredStringParam (key: string) (ctx: HttpContext) : Result<string, ApiError> =
        tryGetQueryParam key ctx
        |> function
            | None -> ApiError.FieldMissingOrEmpty key |> Error
            | Some str -> Ok str

    let getRequiredIntParam (key: string) (ctx: HttpContext) : Result<int, ApiError> =
        getRequiredStringParam key ctx
        |> function
            | Error err -> Error err
            | Ok str ->
                match Int32.TryParse str with
                | true, num -> Ok num
                | false, _ -> InvalidFormat(key, str) |> Error

    let getFromJsonAsync<'TResult> (ctx: HttpContext) : Task<'TResult> = ctx.BindJson<'TResult>()

    let writeErrors (statusCode: int) (errors: ApiError list) (ctx: HttpContext) : Task =
        ctx.SetStatusCode statusCode
        errors |> ApiErrorsDto.create |> ctx.WriteJson

    let writeError (statusCode: int) (error: ApiError) (ctx: HttpContext) : Task =
        ctx |> writeErrors statusCode [ error ]