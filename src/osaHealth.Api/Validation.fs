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

let validateListRecordingsRequest
    (userId: string)
    (from: DateTime option)
    (``to``: DateTime option)
    (cursor: string option)
    (limit: int)
    : Result<unit, ApiError list> =
    let errors =
        [ if String.IsNullOrWhiteSpace userId then ApiError.FieldMissingOrEmpty (nameof userId)
          if limit < 1 then ApiError.ConstraintViolation((nameof limit), "must be > 0")
          if cursor |> Option.exists String.IsNullOrWhiteSpace then ApiError.FieldMissingOrEmpty (nameof cursor)
          match from, ``to`` with
          | Some f, Some t when f > t -> ApiError.ConstraintViolation((nameof from), "must be <= to")
          | _ -> () ]

    match errors with
    | [] -> Ok()
    | errs -> Error errs