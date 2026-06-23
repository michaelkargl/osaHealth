module osaHealth.Repository.Entities

open System
open FSharp.UMX
open MongoDB.Bson.Serialization.Attributes
open osaHealth.Domain
open osaHealth.Domain.Measures


module RecordingEntity =
    module BsonFieldNames =
        [<Literal>]
        let Id = "_id"
        
        [<Literal>]
        let UserId = "user_id"
        
        [<Literal>]
        let DateEpoch = "date_epoch"
        
        [<Literal>]
        let UpdatedAt = "updated_at"

        [<Literal>]
        let Deleted = "deleted"


type RecordingEntity =
    { [<BsonId>]
      Id: Guid<RecordingId>
      [<BsonElement(RecordingEntity.BsonFieldNames.UserId)>]
      UserId: string<UserId>
      [<BsonElement(RecordingEntity.BsonFieldNames.DateEpoch)>]
      DateEpoch: DateTime
      [<BsonElement(RecordingEntity.BsonFieldNames.UpdatedAt)>]
      UpdatedAt: DateTime
      [<BsonElement(RecordingEntity.BsonFieldNames.Deleted)>]
      Deleted: bool }

