module osaHealth.Api.QueryHandlers

open System.Threading.Tasks
open osaHealth.Api.Queries
open osaHealth.Domain.Entities

let handleFindAllRecordingsQuery (findAll: unit -> Task<Recording list>) (query: FindAllRecordingsQuery) : Task<Recording list> =
    findAll ()