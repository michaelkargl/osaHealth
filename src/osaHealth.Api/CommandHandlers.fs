module osaHealth.Api.CommandHandlers

open System.Threading.Tasks
open FSharp.UMX
open osaHealth.Domain.Entities
open osaHealth.Domain.Measures
open osaHealth.Api.Commands

let handleUpsertRecordingCommand
    (upsertRecording: Recording -> Task<unit>)
    (cmd: UpsertRecordingCommand)
    : Task<unit> =
    let recording: Recording =
        { Id = cmd.Id |> UMX.tag<RecordingId>
          UserId = cmd.UserId |> UMX.tag<UserId>
          DateEpoch = cmd.DateEpoch
          UpdatedAt = cmd.UpdatedAt
          Deleted = cmd.Deleted }

    upsertRecording recording
