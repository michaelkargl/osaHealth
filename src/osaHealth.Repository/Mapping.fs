module osaHealth.Repository.Mapping

open osaHealth.Domain
open osaHealth.Repository.Entities


module Recording =
    let toEntity (recording: Recording) : RecordingEntity =
        { Id = recording.Id
          UserId = recording.UserId
          DateEpoch = recording.DateEpoch
          UpdatedAt = recording.UpdatedAt
          Deleted = recording.Deleted }

module RecordingEntity =
    let toDomain (entity: RecordingEntity) : Recording =
        { Id = entity.Id
          UserId = entity.UserId
          DateEpoch = entity.DateEpoch
          UpdatedAt = entity.UpdatedAt
          Deleted = entity.Deleted }

