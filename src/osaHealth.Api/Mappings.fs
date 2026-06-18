module osaHealth.Api.Mappings

open osaHealth.Api.Commands
open osaHealth.Api.Models

module Recordings =
    let toCommand (dto: RecordingDto) : UpsertRecordingCommand =
        { Id = dto.Id
          UserId = dto.UserId
          DateEpoch = dto.DateEpoch
          UpdatedAt = dto.UpdatedAt
          Deleted = dto.Deleted }
