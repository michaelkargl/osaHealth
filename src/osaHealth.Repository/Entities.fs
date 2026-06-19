module osaHealth.Repository.Entities

open System
open FSharp.UMX
open MongoDB.Bson.Serialization.Attributes
open osaHealth.Domain
open osaHealth.Domain.Measures

type RecordingEntity =
    { [<BsonId>]
      Id: Guid<RecordingId>
      [<BsonElement("user_id")>]
      UserId: string<UserId>
      [<BsonElement("date_epoch")>]
      DateEpoch: DateTime
      [<BsonElement("updated_at")>]
      UpdatedAt: DateTime
      [<BsonElement("deleted")>]
      Deleted: bool }

module RecordingEntity =
    let toDomain (entity: RecordingEntity) : Recording =
        { Id = entity.Id
          UserId = entity.UserId
          DateEpoch = entity.DateEpoch
          UpdatedAt = entity.UpdatedAt
          Deleted = entity.Deleted }
