module osaHealth.Api.ErrorHandling

open osaHealth.Domain.ErrorHandling


type ApiError =
    | FieldMissingOrEmpty of fieldName: string
    | ConstraintViolation of fieldName: string * reason: string
    | InvalidFormat of paramName: string * received: string
    | NotFound of entity: string * id: string
    | InternalError

type ApiErrorDto = { Message: string; Reason: string }

module ApiError =
    let toDto (error: ApiError) : ApiErrorDto =
        error
        |> function
            | ApiError.FieldMissingOrEmpty field ->
                { Message = $"'{field}' is required"
                  Reason = nameof ApiError.FieldMissingOrEmpty }
            | ApiError.ConstraintViolation(field, reason) ->
                { Message = $"'{field}' {reason}"
                  Reason = nameof ApiError.ConstraintViolation }
            | ApiError.InvalidFormat(param, received) ->
                { Message = $"'{param}' must be a valid number, got: '{received}'"
                  Reason = nameof ApiError.InvalidFormat }
            | ApiError.NotFound(entity, id) ->
                { Message = $"{entity} '{id}' was not found"
                  Reason = nameof ApiError.NotFound }
            | ApiError.InternalError ->
                { Message = "An unexpected error occurred"
                  Reason = nameof ApiError.InternalError }

type ApiErrorsDto = { Errors: ApiErrorDto list }

module ApiErrorsDto =
    let create (errors: ApiError list) : ApiErrorsDto =
        { Errors = errors |> List.map ApiError.toDto }

module DomainError =
    let toApiError =
        function
        | DomainError.NotFound(entity, id) -> ApiError.NotFound(entity, id)
        | DomainError.Conflict reason -> ApiError.ConstraintViolation("", reason)
        | DomainError.InvalidState reason -> ApiError.ConstraintViolation("", reason)
        | DomainError.InvalidCursor (token, _) -> ApiError.InvalidFormat("cursor", token)

    let toApiErrors (error: DomainError) : ApiError list = [ (toApiError error) ]

    let toHttpResponse (error: DomainError) : int * ApiError list =
        match error with
        | DomainError.NotFound _ -> 404, toApiErrors error
        | DomainError.Conflict _ -> 409, toApiErrors error
        | DomainError.InvalidState _ -> 422, toApiErrors error
        | DomainError.InvalidCursor _ -> 400, toApiErrors error
