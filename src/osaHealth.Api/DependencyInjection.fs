module osaHealth.Api.DependencyInjection

open Oxpecker
open MongoDB.Driver
open osaHealth.Api
open osaHealth.Repository.Entities

module Api =
    let insertRecording (collection: IMongoCollection<Recording>) : EndpointHandler =
        Endpoints.insertRecordingHandler collection
