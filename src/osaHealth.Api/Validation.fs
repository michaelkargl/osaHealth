module osaHealth.Api.Validation

open System
open osaHealth.Api.Models

type ValidationError =
    | NullOrEmpty of fieldName: string
    | ConstraintViolation of fieldName: string * reason: string

let validateInsertRecordingRequest (dto: RecordingDto) : Result<unit, ValidationError list> =
    let errors = [
        if String.IsNullOrWhiteSpace(dto.UserId) then NullOrEmpty "UserId"
        if dto.UpdatedAt < dto.DateEpoch then ConstraintViolation("UpdatedAt", "must be >= DateEpoch")
    ]
    match errors with
    | [] -> Ok ()
    | errs -> Error errs
