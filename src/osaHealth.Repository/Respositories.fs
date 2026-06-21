module osaHealth.Repositories

open System.Threading.Tasks
open MongoDB.Driver
open FSharp.UMX
open osaHealth.Domain.Measures
open osaHealth.Domain.Entities
open osaHealth.Repository.Entities
open osaHealth.Repository.Mapping

[<Literal>]
let CollectionName = "recordings"

module Recordings =
    let upsert (collection: IMongoCollection<RecordingEntity>) (recording: Recording) : Task<unit> =
        task {
            let entity = Recording.toEntity recording
            let filter = Builders<RecordingEntity>.Filter.Eq(_.Id, entity.Id)
            let options = ReplaceOptions(IsUpsert = true)
            let! _ = collection.ReplaceOneAsync(filter, entity, options)
            ()
        }

    let listAll
        (collection: IMongoCollection<RecordingEntity>)
        (after: Guid<RecordingId> option)
        (limit: int)
        : Task<Recording list> =
        task {
            let filter =
                match after with
                | Some recordingId -> Builders<RecordingEntity>.Filter.Gt(_.Id, recordingId)
                | None -> Builders<RecordingEntity>.Filter.Empty

             // TODO: replace "_id" magic string with a typed field reference (BsonFields module)
             // TODO: switch cursor field from _id to compound (updatedAt, _id) for chronological pagination stability
            let sort = Builders<RecordingEntity>.Sort.Ascending("_id")

            let! entities = collection.Find(filter).Sort(sort).Limit(limit).ToListAsync()

            return entities |> Seq.map RecordingEntity.toDomain |> Seq.toList
        }
