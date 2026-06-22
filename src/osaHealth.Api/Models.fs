module osaHealth.Api.Models

open System

type RecordingDto =
    { Id: Guid
      UserId: string
      DateEpoch: DateTime
      UpdatedAt: DateTime
      Deleted: bool }

type PageDto<'TItemDto> =
    {
        Items: 'TItemDto list
        NextCursor: string option
    }

type ListRecordingsQueryResultDto = PageDto<RecordingDto>