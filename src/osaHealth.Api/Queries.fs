module osaHealth.Api.Queries

open osaHealth.Domain.Entities

type CursorPagedQuery =
    {
        Cursor: string option
        Limit: int
    }

type CursorPage<'TItem> =
    {
        Items: 'TItem list
        Cursor: string option
    }


type ListRecordingsCursorPagedQuery = CursorPagedQuery
type ListRecordingsCursorPagedQueryResult = CursorPage<Recording>