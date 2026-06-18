# F# Coding Guidelines

## Functional only

No classes, no inheritance, no interfaces. All dependencies are explicit function parameters. No hidden state, no static initialization with side effects.

## Layer boundaries

| Layer | Examples | Rule |
|---|---|---|
| HTTP (DTOs) | `RecordingInput` | Plain .NET primitives (`Guid`, `string`, `DateTime`) |
| Application (Commands/Queries) | `UpsertRecordingCommand` | Plain .NET primitives — no domain branding |
| Domain / Persistence | `Recording` | Branded types, BSON attributes |

The mapping from unbranded → branded happens exactly once: in the command handler, when constructing a domain entity.

## FSharp.UMX — when to use and when not to

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

Brand at the **domain/persistence layer** where the types live long enough to get confused. Do not brand at the command or DTO layer — commands are short-lived and flow in one direction, so propagating measures upward only creates coupling.

```fsharp
// ✅ correct — tagging happens once, at the entity construction boundary
type UpsertRecordingCommand = { Id: Guid; UserId: string }   // plain

// In the handler:
let recording = { Id = cmd.Id |> UMX.tag<RecordingId>; ... }

// ❌ wrong — leaks Repository types into the Application layer
type UpsertRecordingCommand = { Id: Guid<RecordingId>; ... }
```

## Error handling

Use `Result<'T, 'E>` for expected errors. `failwith` is justified only for:
- True programmer errors / unreachable branches
- Persistence-layer DTO → domain failures that signal data corruption (include all IDs and the raw error in the message)

Never use exceptions for control flow. Catch exceptions only at system boundaries.

## Open statement ordering

Three tiers, no blank lines between tiers, alphabetical within each:
1. System and third-party (`open System`, `open MongoDB.Driver`)
2. Framework namespaces, parent before child
3. Project-specific, shared before specific

## Null safety

No nulls. Use `option<'T>` and `Result<'T, 'E>`. No `Option.Value`, no `unbox`. Pattern match explicitly.