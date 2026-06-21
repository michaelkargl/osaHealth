module osaHealth.Api.Commands

open System

type UpsertRecordingCommand =
    { Id: Guid
      UserId: string
      DateEpoch: DateTime
      UpdatedAt: DateTime
      Deleted: bool }