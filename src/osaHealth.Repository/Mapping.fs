module osaHealth.Repository.Mapping

open osaHealth.Domain
open osaHealth.Repository.Entities


module Recordings =
    let toEntity (recording: Recording) : RecordingEntity =
        { Id = recording.Id
          UserId = recording.UserId
          DateEpoch = recording.DateEpoch
          UpdatedAt = recording.UpdatedAt
          Deleted = recording.Deleted }

