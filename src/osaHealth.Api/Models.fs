module osaHealth.Api.Models

open System

type RecordingDto =
    { Id: Guid
      UserId: string
      DateEpoch: DateTime
      UpdatedAt: DateTime
      Deleted: bool }

type CursorPageDto<'TItemDto> =
    {
        Items: 'TItemDto list
        Cursor: string option
    }

type ListRecordingsCursorPagedQueryResultDto = CursorPageDto<RecordingDto>
