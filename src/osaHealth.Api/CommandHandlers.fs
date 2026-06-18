module osaHealth.Api.CommandHandlers

open System.Threading.Tasks
open FSharp.UMX
open osaHealth.Domain
open osaHealth.Domain.Measures
open osaHealth.Api.Commands

module Recordings =
    let upsert (persist: Recording -> Task<unit>) (cmd: UpsertRecordingCommand) : Task<unit> =
        let recording: Recording =
            { Id = cmd.Id |> UMX.tag<RecordingId>
              UserId = cmd.UserId |> UMX.tag<UserId>
              DateEpoch = cmd.DateEpoch
              UpdatedAt = cmd.UpdatedAt
              Deleted = cmd.Deleted }
        persist recording