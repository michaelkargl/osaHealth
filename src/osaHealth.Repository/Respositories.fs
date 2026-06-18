module osaHealth.Repositories

open System.Threading.Tasks
open MongoDB.Driver
open osaHealth.Repository.Entities

[<Literal>]
let CollectionName = "recordings"

module Recordings =
    let upsert (collection: IMongoCollection<Recording>) (recording: Recording) : Task<unit> =
        task {
            let filter = Builders<Recording>.Filter.Eq("_id", recording.Id)
            let options = ReplaceOptions(IsUpsert = true)
            let! _ = collection.ReplaceOneAsync(filter, recording, options)
            ()
        }