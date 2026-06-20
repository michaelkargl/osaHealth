module osaHealth.Api.DependencyInjection

open Oxpecker
open MongoDB.Driver
open osaHealth.Repository.Entities
open osaHealth.Repositories

module Api =
    let insertRecording (collection: IMongoCollection<RecordingEntity>) : EndpointHandler =
        let persist = Recordings.upsert collection
        let handle = CommandHandlers.handleUpsertRecordingCommand persist
        Endpoints.insertRecordingHandler handle
