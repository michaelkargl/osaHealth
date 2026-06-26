module osaHealth.Api.Queries

open System
open osaHealth.Domain.Entities

type PageQuery = { Cursor: string option; Limit: int }

type Page<'TItem> =
    {
        Items: 'TItem list
        NextCursor: string option
    }

type ListRecordingsQuery =
    {
        Page: PageQuery
        UserId: string
        From: DateTime option
        To: DateTime option
    }

type ListRecordingsQueryResult = Page<Recording>