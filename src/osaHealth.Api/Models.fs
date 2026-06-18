module osaHealth.Api.Models

open System

type RecordingInput =
    { Id: Guid
      UserId: string
      DateEpoch: DateTime
      UpdatedAt: DateTime
      Deleted: bool }
