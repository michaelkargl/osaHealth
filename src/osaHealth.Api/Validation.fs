module osaHealth.Api.Validation

open System
open osaHealth.Api.ErrorHandling
open osaHealth.Api.Models

let validateInsertRecordingRequest (dto: RecordingDto) : Result<unit, ApiError list> =
    let errors =
        [ if String.IsNullOrWhiteSpace(dto.UserId) then
              ApiError.FieldMissingOrEmpty(nameof dto.UserId)
          if dto.UpdatedAt < dto.DateEpoch then
              ApiError.ConstraintViolation(nameof dto.UpdatedAt, "must be >= DateEpoch") ]

    match errors with
    | [] -> Ok()
    | errs -> Error errs

let validateListRecordingsQuery (cursor: string option) (limit: int): Result<unit, ApiError list> =
    let errors = [
        if limit < 1 then ApiError.ConstraintViolation("limit", "must be > 0")
        if cursor |> Option.exists String.IsNullOrWhiteSpace then ApiError.FieldMissingOrEmpty "cursor"
    ]
    
    match errors with
    | [] -> Ok()
    | e -> Error e