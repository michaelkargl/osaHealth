# F# Coding Guidelines

## Functional only

No classes, no inheritance, no interfaces. All dependencies are explicit function parameters. No hidden state, no static initialization with side effects.

## Curried parameters over tuples

Default to curried parameters for any function that is a dependency or is partially applied:

```fsharp
// ✅ curried — partially applicable, composes with |>
let encode (date: DateTime) (id: Guid) : string<Base64> = ...

// ❌ tupled — forces all args at once, cannot be partially applied
let encode (date: DateTime, id: Guid) : string<Base64> = ...
```

Curried functions are how dependency injection works in this codebase — `DependencyInjection.fs` partially applies infrastructure dependencies into handlers one argument at a time. A tupled dependency cannot be partially applied.

Tuples are appropriate when the values are genuinely grouped and always travel together — a return type, a matched pair, or a case where partial application makes no sense:

```fsharp
// ✅ tuple as return — these values belong together as one result
let parseQueryParams ctx : Result<string * DateTime option * DateTime option, ApiError> = ...
```

## Domain layer — pure functions only

Domain functions must be [pure](glossary.md#pure-function).

The consequences are:
- **Reproducibility** — a failing customer request can be reconstructed as a deterministic unit test from its inputs alone
- **Retryability** — domain logic can be safely retried on failure without accumulating side effects
- **Testability** — no mocks required; pass inputs, assert outputs

**I/O belongs into the** `infrastructure` layer (HTTP, MongoDB).  
**Time access is** injected as a `now: unit -> DateTime` parameter.

## Layer boundaries

| Layer | Examples | Rule |
|---|---|---|
| HTTP (DTOs) | `RecordingInput` | Plain .NET primitives (`Guid`, `string`, `DateTime`); nested record types are fine — see note below |
| Application (Commands/Queries) | `UpsertRecordingCommand` | Plain .NET primitives — no domain branding |
| Domain / Persistence | `Recording` | Branded types, BSON attributes |

The mapping from unbranded → branded happens exactly once: in the command handler, when constructing a domain entity.

> **"Plain primitives" means leaf types, not flat structure.** A Command or Query can nest record types freely (e.g. `ListRecordingsQuery` has a `Page: PageQuery` field). The constraint is on what a field's type *drags in as a dependency* — a nested record defined in the same assembly is fine; `string<UserId>` is not, because it forces every consumer to reference `FSharp.UMX`.

## Mapping and data transformation

Each layer boundary has exactly one function that crosses it. No transformation logic leaks into the layer it serves.

| Boundary | Example | Location |
|---|---|---|
| HTTP → Application | `Recording.createCommand` | `Api.Mappings` |
| Application → Domain | UMX tagging (inline) | `CommandHandlers` |
| Domain → Infrastructure | `Recording.toEntity` | `Repository.Mapping` |
| Infrastructure → Domain | `RecordingEntity.toDomain` | `Repository.Mapping` |

Both directions live in `Repository.Mapping` — one module (`Recording`) owns both conversions for a given entity.

## Dependency wiring — `DependencyInjection.fs`

`DependencyInjection.fs` contains only wiring. No logic, no validation, no mapping — it composes functions by partial application and hands the result to the endpoint.

```fsharp
let insertRecording (collection: IMongoCollection<RecordingEntity>) : EndpointHandler =
    let persist = Recordings.upsert collection
    let handleCommand = CommandHandlers.handleUpsertRecordingCommand persist
    Endpoints.insertRecordingHandler handleCommand
```

Each line partially applies a dependency into the next layer. That is the entire job of this file.

## FSharp.UMX — when to use and when not to

See [UMX Measure Types](glossary.md#umx-measure-types) in the glossary.

UMX works on **any primitive** — `Guid`, `string`, `int64`, `float`, etc. The rule is not about the type; it is about coexistence:

> Brand when two or more values of the **same underlying type** coexist in the same scope and could realistically be confused.

```fsharp
// ✅ worth branding — three strings, indistinguishable at the call site
let sendToken (accessToken: string<AccessToken>) (refreshToken: string<RefreshToken>) = ...

// ✅ worth branding — ms and seconds are both int64, easy to mix
type EpochMs = int64<EpochMs>
type EpochSec = int64<EpochSec>

// ✅ worth branding — multiple Guid IDs coexist in persistence scope
type Recording =
    { [<BsonId>]
      Id: Guid<RecordingId>
      UserId: string }      // only one string here, no branding needed yet

// ❌ premature — only one string field in scope, no confusion risk
type UserId = string<UserId>  // add this when AccessToken, RefreshToken etc. appear
```

### Layer rule

Brand at the **domain/persistence layer** where the types live long enough to get confused. Do not brand at the command or DTO layer. The reason is dependency coupling: a Command or Query is a contract into the application core — any caller (HTTP handler, test, CLI) must be able to construct it. A branded field forces every consumer to reference the branding library (`FSharp.UMX`). Plain primitives keep the contract constructible with the BCL alone.

```fsharp
// ✅ correct — tagging happens once, at the entity construction boundary
type UpsertRecordingCommand = { Id: Guid; UserId: string }   // plain

// In the handler:
let recording = { Id = cmd.Id |> UMX.tag<RecordingId>; ... }

// ❌ wrong — leaks Repository types into the Application layer
type UpsertRecordingCommand = { Id: Guid<RecordingId>; ... }
```

## Error handling

### Expected errors — `Result<'T, 'E>`

Use `Result<'T, 'E>` for any error a caller is expected to handle. Never use exceptions for control flow.

Validation returns `Result<unit, ApiError list>` — `unit` because validation checks constraints without transforming data; a list so all field errors are collected and returned together rather than short-circuiting at the first failure.

Collect errors with a list comprehension, then pattern-match on whether any were produced:

```fsharp
let validateInsertRecordingRequest (dto: RecordingDto) : Result<unit, ApiError list> =
    let errors = [
        if String.IsNullOrWhiteSpace dto.UserId then ApiError.FieldMissingOrEmpty "UserId"
        if dto.UpdatedAt < dto.DateEpoch then ApiError.ConstraintViolation("UpdatedAt", "must be >= DateEpoch")
    ]
    match errors with
    | [] -> Ok()
    | errs -> Error errs
```

### Programmer errors — `failwith`

`failwith` is justified for two cases only:

- **Unreachable branches** — a match arm that cannot be reached given invariants the type system cannot express
- **Unrecoverable errors** — situations where the program cannot meaningfully continue and operator intervention is required (missing required configuration, corrupted persisted data with no recovery path); include enough context to diagnose the failure

```fsharp
// ✅ unrecoverable — operator must fix configuration
failwith $"Required environment variable '{name}' is not set."

// ✅ unrecoverable — persisted data is in a state the code cannot handle
failwith $"Failed to convert recording {entity.Id} to domain: {ex.Message}"

// ❌ wrong — recoverable error expressed as an exception
failwith "UserId is required"  // use ValidationError instead
```

### System boundaries — exceptions

In pure domain code, exceptions should not happen — there are no external factors (network, disk, database) that could cause one. If an exception surfaces in the domain, it is a bug. Every expected failure — a missing field, a constraint violation — is expressed as a `Result` value, where it appears in the return type and the caller is forced to handle it.

Infrastructure is where external factors live. Network calls fail. Databases go down. Disks fill up. That is why Infrastructure is the only layer that catches exceptions — it is the only layer where they can legitimately occur.

- **HTTP layer** — a custom `UseExceptionHandler` middleware catches unhandled exceptions and returns `ApiErrorsDto` with status 500; the actual exception is logged server-side and never included in the response
- **Configuration** — `failwith` for missing required environment variables; this is a programmer error, not a runtime one

Do not throw or catch exceptions inside domain or application service code. Return a `Result` instead.

> See also: [Error](glossary.md#error) vs [Exception](glossary.md#exception) in the glossary.

## Computation expressions

A computation expression (CE) is a block of code that the compiler desugars into a chain of function calls. The block reads as the happy path — the CE handles the plumbing (awaiting tasks, short-circuiting on errors) so the code inside doesn't have to.

Three CEs are used in this codebase:

| CE | Source | Purpose |
|---|---|---|
| `task { }` | `FSharp.Core` | Wrap async operations |
| `result { }` | `FsToolkit.ErrorHandling` | Chain `Result` values, short-circuit on `Error` |
| `taskResult { }` | `FsToolkit.ErrorHandling` | Combine both — await *and* short-circuit on `Error` |

### `task { }` — async

```fsharp
task {
    let! recordings = findAll recordingId limit   // await Task<Recording list>, bind result
    do! persist recording                          // await Task<unit>, discard result
    return Ok { Items = recordings }              // wrap in completed Task<Result<...>>
}
```

| Keyword | Input type | What it does |
|---|---|---|
| `let! x = e` | `Task<'T>` | Awaits `e`, binds result to `x` |
| `do! e` | `Task` | Awaits `e`, discards result |
| `return v` | `'T` | Wraps `v` in a completed `Task<'T>` |
| `return! e` | `Task<'T>` | Returns the task directly (no extra wrapping) |

### `result { }` — error short-circuit

```fsharp
result {
    let! limit = ctx |> HttpContext.getRequiredIntParam "limit"   // Ok → bind; Error → stop
    let cursor = ctx |> HttpContext.tryGetQueryParam "cursor"     // plain let, not a Result
    return (cursor, limit)                                         // wraps in Ok
}
```

`let!` is the key operator: if the right-hand side is `Ok x`, execution continues with `x` bound. If it is `Error e`, the entire CE immediately evaluates to `Error e` — no further lines run.

> "Bound" just means "assigned to a name" — the same as a regular `let`. The difference is that `let!` unwraps the `Ok` before assigning, so `x` is the inner value, not `Ok x`.

| Keyword | Input type | What it does |
|---|---|---|
| `let! x = e` | `Result<'T, 'E>` | `Ok v` → bind `v` to `x`; `Error e` → short-circuit |
| `do! e` | `Result<unit, 'E>` | Same as `let!` but discards the `Ok` value |
| `return v` | `'T` | Wraps `v` in `Ok v` |

The error type `'E` must be consistent across all `let!` bindings in a single `result { }` block.

### `taskResult { }` — async + error short-circuit

Use when a handler needs to both await async operations *and* short-circuit on the first `Error`. A single `let!` awaits the `Task` *and* unwraps the `Ok` — two things in one operator.

```fsharp
taskResult {
    let! user =
        findUserReturningOk userId                   // Task<Result<User, DbError>>
        |> TaskResult.mapError (fun e -> e.Message)  // normalise the error to a string

    let! recording =
        findRecordingReturningError user             // Task<Result<Recording, DbError>>
        |> TaskResult.mapError (fun e -> e.Message)
    // ^ Error → the block short-circuits HERE; nothing below runs

    do!
        markAsReadReturningOk recording              // do! awaits Task<Result<unit, _>>, discards Ok ()
        |> TaskResult.mapError (fun e -> e.Message)

    return recording
}
// one handler for whatever error bubbled up — it only ever sees the message string
|> TaskResult.mapError (fun errorMessage -> logger.LogError errorMessage)
```

Each inner `TaskResult.mapError` normalises the step's error type to `string`. This keeps the single terminal handler at the bottom simple — it only needs to handle one unified error shape. Map above; keep the bottom handler simple.

| Helper | Purpose |
|---|---|
| `TaskResult.ofResult` | Lift a plain `Result` into `Task<Result>` so it composes inside the block |
| `TaskResult.mapError` | Transform the error type — required when combining steps that produce different error shapes |

The error type `'E` must be consistent across all `let!` / `do!` bindings in a single `taskResult { }` block. Use `TaskResult.mapError` to unify error shapes before binding.

### Why `result { }` is not in FSharp.Core

`task { }` ships with F# because async is a language-level concern. `result { }` does not — the standard library provides `Result<'T, 'E>` as a type but no CE to chain it. `FsToolkit.ErrorHandling` provides the CE; it is the established community standard for this pattern.

### Anti-patterns

```fsharp
// ❌ generic bound name — what is 'result'?
let! result = validateInput dto

// ✅ name the Ok value, not the wrapper
do! validateInput dto              // Result<unit, _>
let! recording = findById id       // Result<Recording, _>

// ❌ let! () = is a verbose pattern match on unit — do! already expresses this
let! () = validateInput dto
```

```fsharp
// ❌ .Result blocks the thread inside a task { } — defeats the purpose
let data = someTask.Result

// ✅ await with let!
let! data = someTask
```

## Open statement ordering

Three tiers, no blank lines between tiers, alphabetical within each:
1. System and third-party (`open System`, `open MongoDB.Driver`)
2. Framework namespaces, parent before child
3. Project-specific, shared before specific

## Compiler confidence

F# has a strong compiler. Exhaustive pattern-match warnings, unused-binding errors, and missing-open diagnostics are caught before the code reaches review. A review comment that duplicates a compiler warning wastes the author's attention. Let the compiler carry what it can; focus review attention on what it cannot see: architectural fit, domain-rule correctness, error-path coverage, and layer-boundary discipline.

## Null safety

No nulls. Use `option<'T>` and `Result<'T, 'E>`. No `Option.Value`, no `unbox`. Pattern match explicitly.

## Testing — Given/When/Then scenarios

Tests are written as Given/When/Then scenarios using the in-house `Bdd.Scenario` framework. Each feature gets its own **feature file** (the scenarios) and **step file** (the reusable step functions and the context record).

### One assertion per test

A scenario verifies **one behavior** and ends with **one `THEN`** carrying a single assertion. A trailing `AND` is permitted only for a trivial guard — a null / non-null check, or an HTTP status check that is a precondition for reading the `THEN`.

Do **not** chain `WHEN → THEN → WHEN → THEN`. That shape is a god-test: it verifies several behaviors at once, so a failure no longer points at one thing and the scenario can't be named for the behavior it covers.

When the behavior is inherently a **sequence** (pagination, a multi-step workflow, a state machine), do not fragment it into multiple `THEN`s and do not split it into separate tests — splitting drops the cross-step assertion that *is* the behavior. Instead absorb the whole sequence into the `WHEN` and assert the end state once:

```fsharp
// ❌ god-test — multiple behaviors, ambiguous failure
|> WHEN "the first page is requested" ...
|> THEN "it holds the same-date pair" ...
|> WHEN "the second page is requested with the cursor" ...
|> THEN "it holds the remaining recording" ...
|> AND  "all rows appear across both pages" ...

// ✅ the sequence lives in the WHEN; one behavior, one assertion
|> WHEN "every page is fetched by following the cursor" (fun ctx ->
    // loop GET /recordings?...&cursor=… until NextCursor is None,
    // accumulating ids into ctx.AllItems
    ...)
|> THEN "each recording appears exactly once, ordered by (date, id)" (fun ctx ->
    Assert.Equal<string list>(expectedOrderFrom ctx.SeededRecordings, ctx.AllItems)
    ctx)
```

The single equality catches both a skipped row and a duplicated row — a stronger regression lock than either the multi-`THEN` or the split-test version.

### Steps operate only on the context record

Step lambdas receive the context record as their parameter and must read and write **only** that record. They must not close over values declared outside the scenario — the context record *is* the channel between steps.

- `GIVEN` steps **set** fields (seeded entities, configuration).
- `WHEN` steps **read** inputs from the context, call the system under test, and **set** the result fields.
- `THEN` steps read context fields for **both sides** of the assertion — expected values come from what a `GIVEN` seeded, never from an outer-scope binding.

```fsharp
// ❌ closes over an outer-scope binding — hidden dependency, not reusable
let expectedIds = [ idA; idB ]
|> THEN "..." (fun ctx -> Assert.Equal<_>(expectedIds, ctx.PageItems); ctx)

// ✅ expected value was seeded into the context by a GIVEN
|> GIVEN "two recordings with the same date" (fun ctx ->
    { ctx with SeededRecordings = [ recordingA; recordingB ] })
|> THEN "..." (fun ctx ->
    Assert.Equal<_>(ctx.SeededRecordings |> List.map _.Id, ctx.PageItems); ctx)
```

### Step wording

Steps read as plain sentences. Avoid abbreviations and cryptic shorthand; split a compound precondition into separate `GIVEN`/`AND` steps rather than packing it into one cryptic line.