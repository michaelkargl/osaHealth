module osaHealth.Api.Mappings

open FSharp.UMX
open osaHealth.Api.Commands
open osaHealth.Api.Models
open osaHealth.Domain.Entities

module Recording =
    let createCommand (dto: RecordingDto) : UpsertRecordingCommand =
        { Id = dto.Id
          UserId = dto.UserId
          DateEpoch = dto.DateEpoch
          UpdatedAt = dto.UpdatedAt
          Deleted = dto.Deleted }

    let toDto (model: Recording) : RecordingDto =
        { Id = model.Id |> UMX.untag
          UserId = model.UserId |> UMX.untag
          DateEpoch = model.DateEpoch
          UpdatedAt = model.UpdatedAt
          Deleted = model.Deleted }