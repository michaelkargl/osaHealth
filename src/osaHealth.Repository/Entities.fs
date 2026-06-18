module osaHealth.Repository.Entities

open System
open FSharp.UMX
open MongoDB.Bson.Serialization.Attributes

[<Measure>]
type RecordingId

type Recording =
    { [<BsonId>]
      Id: Guid<RecordingId>
      [<BsonElement("user_id")>]
      UserId: string
      [<BsonElement("date_epoch")>]
      DateEpoch: DateTime
      [<BsonElement("updated_at")>]
      UpdatedAt: DateTime
      [<BsonElement("deleted")>]
      Deleted: bool }
