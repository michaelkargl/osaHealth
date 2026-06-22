# Glossary

---

## A

### Application Core

See [onion-architecture.md](onion-architecture.md#application-core).

### Application Services Layer

See [onion-architecture.md](onion-architecture.md#application-services-layer).

---

## C

### Code Smells

A symptom in the code that suggests a deeper design problem. The code may work correctly, but the smell signals that a
decision will create friction as the codebase evolves — harder to test, harder to extend, harder to reason about.

Known smells:

- [Fat DTO](#fat-dto) — a DTO serving multiple consumers, accumulating null fields per caller

### Cursor

A marker that identifies a specific position in a result set. When paginating, the server issues a cursor
after each page; the client sends it back on the next request to say "continue from here." The name comes
from the same root as a text cursor — a pointer to a current position.

```
Result set:  [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
                            ↑
                         cursor
                   (last record seen)
```

*Note: SQL cursors (Oracle, MSSQL) are stateful — the database holds the position between fetches and the
cursor must be closed when done. API pagination cursors are the inverse: the position is encoded in the
token and carried by the caller. The server holds nothing.*

See also: [Cursor Pagination](#cursor-pagination), [Keyset Pagination](#keyset-pagination).

### Cursor Pagination

See [Keyset Pagination](#keyset-pagination).

---

## D

### Domain Model Layer

See [onion-architecture.md](onion-architecture.md#domain-model-layer).

### Domain Services Layer

See [onion-architecture.md](onion-architecture.md#domain-services-layer).

---

## E

### Error

An expected, predictable failure that the code is designed to handle. Errors are part of normal operation — a field
missing from a request, a value that violates a constraint, a resource that does not exist. In osaHealth, errors are
modeled as `Result<'T, 'E>` values so that the caller is forced to handle both outcomes.

An error is not surprising. If it can happen, it is in the return type.

### Exception

An unexpected failure caused by factors outside the code's control — network down, disk full, database unreachable — or
a programmer mistake (null reference, division by zero). Exceptions are not part of normal operation and are not modeled
in return types.

In osaHealth, exceptions are only caught at system
boundaries ([Infrastructure Layer](onion-architecture.md#infrastructure-layer)). They are never used for control flow.

---

## O

### Offset Pagination

A pagination strategy where the client requests results by position: *skip the first N records, return the next M*.
Simple to implement and easy to reason about, but degrades at scale.

As the offset grows, the database scans and discards an increasing number of rows before returning results. At page 1000
with a page size of 20, the database skips 19,980 rows on every request.

Offset pagination also suffers from **page drift**: if a record is inserted or deleted between two requests, the page
boundaries shift and the client may see a duplicate or skip a record entirely.

Appropriate for small, stable datasets or when jumping to a specific page number is a product requirement.

See also: [Keyset Pagination](#keyset-pagination), [Pagination](#pagination).

### Onion Architecture

A software architecture pattern that organises code into concentric layers where dependencies only ever point inward.
The innermost layer (Domain Model) has no knowledge of outer layers. Outer layers (Application Services, Infrastructure)
depend on inner ones — never the reverse. This makes the domain independent of any database, framework, or delivery
mechanism.

The layers from inside out: **Domain Model** → **Domain Services** → **Application Services** → **Infrastructure**.

**Application Core** is a grouping label (not a layer) for everything inside Infrastructure: Domain Model + Domain
Services + Application Services together.

**Layers, inside out:**

- [Infrastructure Layer](onion-architecture.md#infrastructure-layer) — databases, HTTP frameworks, external systems
- [Application Services Layer](onion-architecture.md#application-services-layer) — handles a user action end to end
- [Domain Services Layer](onion-architecture.md#domain-services-layer) — cross-entity domain logic (currently unused in
  osaHealth)
- [Domain Model Layer](onion-architecture.md#domain-model-layer) — the core; knows nothing outside itself

See [onion-architecture.md](onion-architecture.md) for the full explanation and diagram.

---

## F

### Fake

**Input matters.** A fake has actual working logic — simpler than the real implementation, but not
hardwired to a fixed response. Write to it and then read from it, and you get back what you wrote.

A [stub](#stub) ignores its inputs entirely. A fake processes them:

```fsharp
let store = Dictionary<Guid, Recording>()

let fakeUpsert (recording: Recording) =
    store[UMX.untag recording.Id] <- recording
    Task.FromResult ()

let fakeFindAll _ limit =
    store.Values |> Seq.truncate limit |> Seq.toList |> Task.FromResult
```

| Double | Input matters? | Verifies calls? |
|--------|----------------|-----------------|
| Stub   | No             | No              |
| Fake   | Yes            | No              |
| Mock   | Either         | Yes             |

Use a fake when test cases depend on data written by earlier steps — upsert then list, write then read.
A stub cannot do this because it ignores the write and always returns the same fixed response.

See also: [Stub](#stub), [Mock](#mock).

### Fat DTO

A DTO that serves multiple consumers — typically multiple endpoints or delivery mechanisms (HTTP, gRPC, CLI) — causing
fields to accumulate that are only relevant to some callers. The result is a type where half the properties are `null`
depending on who is calling, and each new consumer either adds more nullable fields or inherits noise that does not
apply to them.

There are three ways to fix this, depending on context:

1. **Return the domain type from the application layer** — the handler returns `Recording` and each delivery mechanism (
   HTTP endpoint, gRPC handler) maps it to its own response shape. Each consumer gets a DTO that contains exactly what
   it needs. This is the right fix when the smell comes from a layer boundary violation: the application layer has no
   business knowing what the HTTP response looks like.
1. **Separate DTOs per consumer** — instead of one shared `RecordingDto`, create `RecordingHttpResponse` and
   `RecordingGrpcResponse`. Each is purpose-built and contains only what that consumer needs. This fixes the null fields
   problem but does not fix a layer boundary violation — if the application layer constructs these, it is still coupled
   to the delivery mechanism.
1. **Purpose-built read model** — for complex queries, introduce a read model that lives in the application layer and is
   shaped for a specific query, not a specific delivery format. Not a domain type (it may be a projection, a summary, a
   flat view), but not a DTO either. Appropriate when the domain type is too rich or too deeply nested for efficient
   query results.

See
also: [Application Services Layer](onion-architecture.md#application-services-layer), [Onion Architecture](#onion-architecture).

---

## I

### Idempotency

A property of an operation where calling it multiple times with the same input leaves the world in the same state as
calling it once. `f(f(x)) = f(x)`.
Idempotency requires explicit design for impure operations (I/O, persistence) where side effects can accumulate

| Operation                  | Pure?   | Idempotent?                               |
|----------------------------|---------|-------------------------------------------|
| `Recording.tryCreate(...)` | ✅       | ✅ trivially — no world to change          |
| `DELETE /recordings/:id`   | ✗ (I/O) | ✅ deleting twice = gone once              |
| `upsert recording`         | ✗ (I/O) | ✅ designed explicitly — `IsUpsert = true` |
| `INSERT recording`         | ✗ (I/O) | ✗ second call → duplicate or error        |
| `appendToLog(msg)`         | ✗ (I/O) | ✗ 10 calls = 10 log entries               |

### Infrastructure Layer

See [onion-architecture.md](onion-architecture.md#infrastructure-layer).

### Invariant

A rule that must always be true, no matter what. If it is ever false, the system's correctness or security model breaks.

*Example from this codebase:* the server always derives `userId` from the auth token — never from the request body or
URL. This must hold for every request without exception.

---

## K

### Keyset Pagination

A pagination strategy where the client tracks position using an opaque cursor — an encoded pointer to the last record
seen — rather than a numeric page or row offset.

On each request, the **API** (server) decodes the cursor to a record identifier, queries for records that
come *after* that identifier (`_id > lastId`), and encodes the last returned record's ID as the next cursor.
When no cursor is provided, the first page is returned.

The cursor works like a bookmark: the **caller** (client — a mobile app, web frontend, or another service)
receives it, stores it, and sends it back on the next request without interpreting it. The API is the only
one who knows what it encodes (opaque cursor). This leaves the API free to change the cursor format without
breaking callers.

```
Records:   [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
           └─── page 1 ───┘
                           ↑ cursor = "abc..."

           GET /recordings?after=abc...

Records:   [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
                           └─── page 2 ───┘
                                           ↑ cursor = "xyz..."
```

Each page returns a fresh cursor pointing to the last record on that page. The caller always uses the most
recent one; earlier cursors are discarded. The cursor is stateless — the API stores nothing between
requests. The cursor *is* the position, encoded as a string the caller hands back. When there are no more
records, the API returns `null` for the next cursor. The caller stops paginating — no cleanup required on
either side.

| Characteristic | Keyset | [Offset Pagination](#offset-pagination) |
|---|---|---|
| Query cost as depth grows | O(1) — filtered by index | O(n) — database skips rows |
| Page drift | None — anchor is a stable record | Present — inserts/deletes shift page contents |
| Random access | No — cursor is forward-only | Yes — jump to any page number |

Use keyset pagination when the dataset is large, the sort key is stable, and clients page forward sequentially.

See also: [Offset Pagination](#offset-pagination), [Pagination](#pagination).

---

## M

### Measure Types

See [Phantom Types](#phantom-types).

### Mock

**Verifies interactions.** A mock records how it was called — number of calls, argument values, call
order — and can fail the test if those expectations were not met. The defining characteristic is
*verification*, not implementation.

```fsharp
let calls = ResizeArray<UpsertRecordingCommand>()

let mockPersist (cmd: UpsertRecordingCommand) =
    calls.Add(cmd)
    Task.FromResult (Ok ())

// After the handler runs...
Assert.Equal(1, calls.Count)
Assert.Equal(expectedCommand, calls[0])
```

| Double | Input matters? | Verifies calls? |
|--------|----------------|-----------------|
| Stub   | No             | No              |
| Fake   | Yes            | No              |
| Mock   | Either         | Yes             |

A mock does not need to have [fake](#fake) behaviour. The simplest mock is a [stub](#stub) that also
records calls. A mock can have fake behaviour too — but that is a design choice, not the definition.

In osaHealth, because dependencies are plain functions, mocks are rarely needed. A `ResizeArray` that
captures calls (in test code only) is enough when interaction verification matters — no mocking
framework required.

See also: [Stub](#stub), [Fake](#fake).

---

## P

### Pagination

The practice of splitting a large result set into smaller, sequentially accessible chunks. Pagination prevents unbounded
queries and keeps response sizes predictable.

Known pagination techniques:

- [Keyset Pagination](#keyset-pagination) — cursor-based
- [Offset Pagination](#offset-pagination) — page number or row offset

### Phantom Types

A technique where a type parameter is present in the type signature but erased at runtime — it exists only to carry
compile-time information. The runtime value is unchanged; the compiler uses the tag to reject incorrect usage.

In osaHealth, `FSharp.UMX` provides phantom measure types for primitive IDs:

| Type                | Underlying | Tag           |
|---------------------|------------|---------------|
| `Guid<RecordingId>` | `Guid`     | `RecordingId` |
| `string<UserId>`    | `string`   | `UserId`      |

Passing a `string<UserId>` where a `Guid<RecordingId>` is expected is a compile error. At runtime, both are the plain
underlying value — zero overhead.

See also: [UMX Measure Types](#umx-measure-types).

### Pure function

A function with no side effects that always returns the same output for the same inputs. No I/O, no mutation, no
randomness, no time access — everything the function needs is passed in as a parameter.

**Pure functions are always [idempotent](#idempotency).** Because they have zero effect on external state, calling them
N times is indistinguishable from calling them once.

| Operation                  | Pure?   | Idempotent?                               |
|----------------------------|---------|-------------------------------------------|
| `Recording.tryCreate(...)` | ✅       | ✅ trivially — no world to change          |
| `DELETE /recordings/:id`   | ✗ (I/O) | ✅ deleting twice = gone once              |
| `upsert recording`         | ✗ (I/O) | ✅ designed explicitly — `IsUpsert = true` |
| `INSERT recording`         | ✗ (I/O) | ✗ second call → duplicate or error        |
| `appendToLog(msg)`         | ✗ (I/O) | ✗ 10 calls = 10 log entries               |

The practical payoff: a failing customer request can be reproduced exactly as a unit test. Capture the inputs, paste
them in, and the test runs the same logic path the customer hit — no clock mocking, no database seeding, no environment
setup.

In osaHealth, all domain functions must be pure. I/O (HTTP, MongoDB) lives in `osaHealth.Api` and
`osaHealth.Repository`. Time is injected as `now: unit -> DateTime` at the boundary that needs it, not read directly
with `DateTime.UtcNow` inside domain logic.

---

## S

### Smells

See [Code Smells](#code-smells).

### Stub

**Input is irrelevant.** A stub returns the same pre-configured response no matter what you pass in.
The goal is to satisfy a dependency so the unit under test can run in isolation — nothing more.

```fsharp
// Production — real MongoDB query
let findAll = Recordings.listAll collection

// Stub — always returns an empty page; input is ignored
let findAllStub _ _ = Task.FromResult ([] : Recording list)
```

The `_` parameters are not inspected. Pass in any cursor, any limit — the stub does not care.

| Double | Input matters? | Verifies calls? |
|--------|----------------|-----------------|
| Stub   | No             | No              |
| Fake   | Yes            | No              |
| Mock   | Either         | Yes             |

In osaHealth, stubs are plain F# functions passed as arguments. Because dependencies are injected as
function parameters, a lambda is all you need — no mocking framework required.

See also: [Fake](#fake), [Mock](#mock), [Pure function](#pure-function).

---

## U

### UMX Measure Types

See [Phantom Types](#phantom-types).

---

## 4

### 403 Forbidden

The HTTP 403 Forbidden client error response status code indicates that the server understood the request but refused to
process it.

<https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/403>

### 409 Conflict

The HTTP 409 Conflict client error response status code indicates a request conflict with the current state of the
target resource.

<https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/409>