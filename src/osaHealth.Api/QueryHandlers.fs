module osaHealth.Api.QueryHandlers

open System.Threading.Tasks
open osaHealth.Api.Queries
open osaHealth.Domain.Entities

let handleListRecordingsQuery (findAll: unit -> Task<Recording list>) (_: ListRecordingsQuery) : Task<Recording list> =
    findAll ()