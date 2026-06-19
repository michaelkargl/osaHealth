module osaHealth.Repositories

open System.Threading.Tasks
open MongoDB.Driver
open osaHealth.Domain
open osaHealth.Repository.Entities
open osaHealth.Repository

[<Literal>]
let CollectionName = "recordings"

module Recordings =
    let upsert (collection: IMongoCollection<RecordingEntity>) (recording: Recording) : Task<unit> =
        task {
            let entity = Mapping.Recording.toEntity recording
            let filter = Builders<RecordingEntity>.Filter.Eq("_id", entity.Id)
            let options = ReplaceOptions(IsUpsert = true)
            let! _ = collection.ReplaceOneAsync(filter, entity, options)
            ()
        }