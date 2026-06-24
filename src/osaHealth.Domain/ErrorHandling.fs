module osaHealth.Domain.ErrorHandling

type DomainError =
    | NotFound of entity: string * id: string
    | Conflict of reason: string
    | InvalidState of reason: string
    | InvalidCursor of token: string * reason: string
