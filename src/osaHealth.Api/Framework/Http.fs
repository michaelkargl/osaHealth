module osaHealth.Api.Framework.Http

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Diagnostics
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Oxpecker
open osaHealth.Api.ErrorHandling

module HttpContext =

    let tryGetRaisedException (ctx: HttpContext) : exn option =
        let feature = ctx.Features.Get<IExceptionHandlerFeature>()

        match feature with
        | null -> None
        | f -> Some f.Error

    let getLogger (scope: string) (ctx: HttpContext) : ILogger =
        ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(scope)

    let tryGetQueryParam (key: string) (ctx: HttpContext) : string option =
        ctx.Request.Query.TryGetValue(key)
        |> function
            | true, value -> value.ToString() |> Some
            | false, _ -> None

    let tryGetDateTimeQueryParam (key: string) (ctx: HttpContext) : Result<DateTime option, ApiError> =
        match ctx |> tryGetQueryParam key with
        | None -> Ok None
        | Some s ->
            match DateTimeOffset.TryParse s with
            | true, dt -> Ok(Some dt.UtcDateTime)
            | false, _ -> Error(InvalidFormat(key, s))

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
