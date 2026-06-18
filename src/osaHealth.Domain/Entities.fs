namespace osaHealth.Domain

open System
open Measures
open FSharp.UMX

type Recording =
    { Id: Guid<RecordingId>
      UserId: string<UserId>
      DateEpoch: DateTime
      UpdatedAt: DateTime
      Deleted: bool }