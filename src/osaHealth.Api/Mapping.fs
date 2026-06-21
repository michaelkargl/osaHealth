module osaHealth.Api.Mappings

open System.Threading.Tasks
open FSharp.UMX
open osaHealth.Api.Commands
open osaHealth.Api.Models
open osaHealth.Api.Queries
open osaHealth.Domain.Entities


module Recording =
    let toCommand (dto: RecordingDto) : UpsertRecordingCommand =
        { Id = dto.Id
          UserId = dto.UserId
          DateEpoch = dto.DateEpoch
          UpdatedAt = dto.UpdatedAt
          Deleted = dto.Deleted }

    let toDto (model: Recording) : RecordingDto =
        { Id = model.Id |> UMX.untag
          UserId = model.UserId |> UMX.untag
          DateEpoch = model.DateEpoch
          UpdatedAt = model.UpdatedAt
          Deleted = model.Deleted }

    let toDtoList (recordings: Recording list) : RecordingDto list = recordings |> List.map toDto

module ListRecordingsCursorPagedQueryResult =
    let toDto (result: ListRecordingsCursorPagedQueryResult) : ListRecordingsCursorPagedQueryResultDto =
        { Cursor = result.Cursor
          Items = result.Items |> Recording.toDtoList }

    let toDtoAsync
        (resultTask: Task<ListRecordingsCursorPagedQueryResult>)
        : Task<ListRecordingsCursorPagedQueryResultDto> =
        task {
            let! result = resultTask
            return result |> toDto
        }
