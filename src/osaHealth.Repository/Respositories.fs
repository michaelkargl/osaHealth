module osaHealth.Repositories

open System
open System.Threading.Tasks
open MongoDB.Driver
open FSharp.UMX
open osaHealth.Domain.Measures
open osaHealth.Domain.Entities
open osaHealth.Repository.Entities
open osaHealth.Repository.Mapping

// TODO: shouldn't this be a configurable string?
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
        (userId: string<UserId>)
        (from: DateTime option)
        (``to``: DateTime option)
        (after: Guid<RecordingId> option)
        (limit: int)
        : Task<Recording list> =
        task {
            let filter =
                [ Builders<RecordingEntity>.Filter.Eq(RecordingEntity.BsonFieldNames.UserId, userId)
                  |> Some

                  after
                  |> Option.map (fun a -> Builders<RecordingEntity>.Filter.Gt(RecordingEntity.BsonFieldNames.Id, a))

                  from
                  |> Option.map (fun f ->
                      Builders<RecordingEntity>.Filter.Gte(RecordingEntity.BsonFieldNames.DateEpoch, f))

                  ``to``
                  |> Option.map (fun t ->
                      Builders<RecordingEntity>.Filter.Lte(RecordingEntity.BsonFieldNames.DateEpoch, t)) ]
                |> List.choose id
                |> Builders<RecordingEntity>.Filter.And

            let! query =
                collection
                    .Find(filter)
                    .Sort(Builders<RecordingEntity>.Sort.Ascending(RecordingEntity.BsonFieldNames.DateEpoch))
                    .Limit(limit)
                    .ToListAsync()

            return query |> Seq.map RecordingEntity.toDomain |> Seq.toList
        }
