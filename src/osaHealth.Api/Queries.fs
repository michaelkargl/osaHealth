module osaHealth.Api.Queries

open System
open FSharp.UMX
open osaHealth.Domain.Entities
open osaHealth.Domain.Measures

type PageQuery = { Cursor: string option; Limit: int }

type Page<'TItem> =
    {
        Items: 'TItem list
        NextCursor: string option
    }

type ListRecordingsQuery =
    {
        Page: PageQuery
        UserId: string<UserId>
        From: DateTime option
        To: DateTime option
    }

type ListRecordingsQueryResult = Page<Recording>