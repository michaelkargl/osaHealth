module osaHealth.Api.Mappings

open osaHealth.Api.Commands
open osaHealth.Api.Models

module Recording =
    let createCommand (dto: RecordingDto) : UpsertRecordingCommand =
        { Id = dto.Id
          UserId = dto.UserId
          DateEpoch = dto.DateEpoch
          UpdatedAt = dto.UpdatedAt
          Deleted = dto.Deleted }
