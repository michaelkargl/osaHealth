module osaHealth.Api.DependencyInjection

open Oxpecker
open MongoDB.Driver
open osaHealth.Repository.Entities
open osaHealth

module Api =
    let insertRecordingHandler (collection: IMongoCollection<RecordingEntity>) : EndpointHandler =
        let persist = Repositories.Recordings.upsert collection
        let handleCommand = CommandHandlers.handleUpsertRecordingCommand persist
        Api.Endpoints.insertRecordingHandler handleCommand

    let listRecordings (collection: IMongoCollection<RecordingEntity>) : EndpointHandler =
        let listAll () = Repositories.Recordings.listAll collection
        let handleQuery = QueryHandlers.handleListRecordingsQuery listAll
        Api.Endpoints.listRecordingsHandler handleQuery