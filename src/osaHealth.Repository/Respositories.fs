module osaHealth.Repositories

open System
open System.Threading.Tasks
open MongoDB.Driver
open FSharp.UMX
open osaHealth.Domain.Measures
open osaHealth.Domain.Entities
open osaHealth.Repository.Entities
open osaHealth.Repository.Mapping


module Recordings =
    // TODO: shouldn't this be a configurable string?
    [<Literal>]
    let CollectionName = "recordings"

    module FieldNames = RecordingEntity.BsonFieldNames

    let private FilterBuilder = Builders<RecordingEntity>.Filter
    let private SortBuilder = Builders<RecordingEntity>.Sort

    let upsert (collection: IMongoCollection<RecordingEntity>) (recording: Recording) : Task<unit> =
        task {
            let entity = Recording.toEntity recording
            let filter = FilterBuilder.Eq(FieldNames.Id, entity.Id)
            let options = ReplaceOptions(IsUpsert = true)
            let! _ = collection.ReplaceOneAsync(filter, entity, options)
            ()
        }

    let listAll
        (collection: IMongoCollection<RecordingEntity>)
        (userId: string<UserId>)
        (from: DateTime option)
        (``to``: DateTime option)
        (after: (DateTime * Guid<RecordingId>) option)
        (limit: int)
        : Task<Recording list> =
        task {
            let filter =
                [ FilterBuilder.Eq(FieldNames.UserId, userId) |> Some

                  // Compound cursor: mirrors (DateEpoch, Id) sort order — see docs/pagination.md
                  after
                  |> Option.map (fun (date, id) ->
                      let afterDate = FilterBuilder.Gt(FieldNames.DateEpoch, date)
                      let sameDate = FilterBuilder.Eq(FieldNames.DateEpoch, date)
                      let afterId = FilterBuilder.Gt(FieldNames.Id, id)
                      FilterBuilder.Or(afterDate, FilterBuilder.And(sameDate, afterId)))

                  from |> Option.map (fun f -> FilterBuilder.Gte(FieldNames.DateEpoch, f))

                  ``to`` |> Option.map (fun t -> FilterBuilder.Lte(FieldNames.DateEpoch, t)) ]
                |> List.choose id
                |> FilterBuilder.And

            let sort =
                SortBuilder.Combine(SortBuilder.Ascending(FieldNames.DateEpoch), SortBuilder.Ascending(FieldNames.Id))

            let! query = collection.Find(filter).Sort(sort).Limit(limit).ToListAsync()

            return query |> Seq.map RecordingEntity.toDomain |> Seq.toList
        }
