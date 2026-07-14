# Glossary

---

## A

### Adapter

The infrastructure-side implementation of a [Port](#port) — the code that knows *how* to carry out what the domain
asked for. Thin in interface, arbitrarily thick in implementation: it may hold any amount of **mechanism**, and never
any **policy**.

See [oop.md](oop.md#3-behavior-ports-and-adapters). Contrast with [Shim](#shim).

### Anemic Domain Model

A domain object reduced to a bag of properties — getters, setters, no behavior — while the rules that should live
inside it are held by a separate `*Service` that mutates it from the outside. Technically encapsulated, procedurally
structured.

See [oop.md](oop.md#3-behavior-ports-and-adapters). Related: [Code Smells](#code-smells).

### Application Core

See [onion-architecture.md](onion-architecture.md#application-core).

### Application Services Layer

See [onion-architecture.md](onion-architecture.md#application-services-layer).

### Async / Awaiter State Machine

When you write do!/let! on a task (or await in C#), the compiler turns the method into a state machine. At each await point it generates roughly this:

```fsharp
let awaiter = theTask.GetAwaiter()
if awaiter.IsCompleted then
    // ── FAST PATH ──
    // result is already sitting there; just read it and keep running
    let result = awaiter.GetResult()
    // ...fall straight through to the next line, same thread, same stack frame
else
    // ── SLOW PATH ──
    // not done yet: hand a continuation to the task and RETURN out of the method.
    // the rest of the method runs later, as a callback, when the task completes.
    awaiter.OnCompleted(fun () -> resumeHere result)
    return
```

---

## B

### Big Endian

Most significant byte is stored at the lowest memory address — the "big" end comes first.

```
Value: 0x12345678

Address:    [0]  [1]  [2]  [3]
Contents:  [12] [34] [56] [78]
            ↑
        most significant byte first
```

```fs
// read a little endian value
BinaryPrimitives.ReadInt64BigEndian(System.ReadOnlySpan<byte>(bytes, offset, sizeof<int64>))
```

See [Endianness](#endianness). Contrast with [Little Endian](#little-endian).

### BOM (Byte Order Mark)

A short marker (2–4 bytes) placed at the very start of a text stream to signal its
encoding and byte order, so a receiver can decode it correctly without prior agreement.

```
UTF-16 Little Endian: FF FE  (reads "I am little-endian UTF-16")
UTF-16 Big Endian:    FE FF  (reads "I am big-endian UTF-16")
UTF-8:                EF BB BF (no byte-order ambiguity, but marks it as UTF-8)
```

BOM is one solution to the [Endianness](#endianness) problem: the sender embeds the
byte order as an in-band signal rather than relying on a pre-established contract.

| Approach | Prior agreement? | Marker sent? | Typical use |
|----------|------------------|--------------|-------------|
| **BOM** | No | Yes (2–4 bytes overhead) | Unknown text sources, generic protocols |
| **Contract** | Yes | No | Internal binary formats where both ends are controlled |

In osaHealth, cursor tokens use the **contract** approach — both sides agree to
little-endian (`WriteInt64LittleEndian` / `ReadInt64LittleEndian`) with no marker
sent. BOM would only be needed if cursors could come from arbitrary third-party
sources, which they cannot.

See [Endianness](#endianness).

---

## C

### Code Smells

A symptom in the code that suggests a deeper design problem. The code may work correctly, but the smell signals that a
decision will create friction as the codebase evolves — harder to test, harder to extend, harder to reason about.

Known smells:

- [Fat DTO](#fat-dto) — a DTO serving multiple consumers, accumulating null fields per caller
- [Anemic Domain Model](#anemic-domain-model) — an object with no behavior; its rules live in a `*Service` that
  mutates it from the outside

### Compound Cursor

A cursor encoding two sort fields to precisely identify position when the primary sort key
is not unique (e.g. a timestamp). See [pagination.md](pagination.md#compound-cursor) for
the full explanation including the filter construction and why it uses `OR`, not `AND`.

---

### Cursor

An opaque bookmark encoding the position of the last record seen in a paginated result set.
The server issues it; the client sends it back unchanged on the next request. See
[pagination.md](pagination.md#cursor).

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

### Encapsulation

Hiding state behind behavior so an object can never be observed violating its own
invariants — not merely making fields private. See [oop.md](oop.md#1-encapsulation).

### Endianness

The byte order used when storing a multi-byte value in memory

There are two conventions:

1. [Big Endian](#big-endian) — most significant byte at the lowest address
2. [Little Endian](#little-endian) — least significant byte at the lowest address

`BitConverter.GetBytes` writes bytes in the **system's native byte order** — whichever
the host CPU uses. Within a single process this is invisible: the same process reads back
exactly what it wrote. It becomes a bug the moment a byte array **crosses a boundary**:
sent to a client, cached, persisted, or read by a restarted server on a different host —
because the reader may be on a different architecture and interpret the bytes in the wrong
order.

**Rule:** when a multi-byte primitive crosses a process boundary, sender and receiver must agree on
byte order. Establish the contract explicitly: encode and decode using the **same byte order**, using
`System.Buffers.Binary.BinaryPrimitives` to make it visible (e.g., `WriteInt64LittleEndian` paired with
`ReadInt64LittleEndian`). No flag is sent — just bytes in that order. If both sides keep the contract,
the value round-trips correctly regardless of CPU architecture. Using `BitConverter` (implicit, native
byte order) breaks the contract.

**When it matters:** multi-byte primitives (`Int64`, `Int32`, `Int16`, `Double`) serialized to bytes
crossing a process boundary.

**When it doesn't:** single-byte values (`byte`, `char`), spec-defined structures (`Guid`), opaque byte arrays
(like Base64 output), values that never leave memory.

> Example: Cursor tokens (24-byte Base64 blobs encoding `DateTime.Ticks + Guid`) are returned to clients
and replayed on the next request — crossing the process boundary each time. The `Int64` ticks use
`WriteInt64LittleEndian` (encode) paired with `ReadInt64LittleEndian` (decode); the `Guid` uses its native
`ToByteArray()` / constructor (spec-defined byte order, endianness-independent).

See [Big Endian](#big-endian), [Little Endian](#little-endian).

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

Skip/limit pagination — simple but degrades at depth and suffers from page drift under
concurrent writes. See [pagination.md](pagination.md#offset-pagination).

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

See [oop.md](oop.md#2-object-construction--invariants) for where invariant-enforcement logic should live on an object
(one enforcement point, constructor vs. setter, the assignment-order trap for multi-field invariants).

---

## K

### Keyset Pagination

Cursor-based pagination anchored to record values rather than row offsets — stable under
concurrent writes, O(1) query cost at any depth. See
[pagination.md](pagination.md#keyset-pagination).

---

## L

### Little Endian

Least significant byte is stored at the lowest memory address — the "little" end comes first.

```
Value: 0x12345678

Address:    [0]  [1]  [2]  [3]
Contents:  [78] [56] [34] [12]
            ↑
        least significant byte first
```

```fs
// read a little endian value
BinaryPrimitives.ReadInt64LittleEndian(System.ReadOnlySpan<byte>(bytes, offset, sizeof<int64>))
```

See [Endianness](#endianness). Contrast with [Big Endian](#big-endian).

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

Splitting a large result set into sequentially accessible pages to prevent unbounded
queries. See [pagination.md](pagination.md) for technique comparison and this project's
approach.

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

### Port

An interface **defined by the domain**, describing a capability a domain object needs from the outside world — a socket
it plugs a request into. The object knows *where to enter the request*; it never learns how the request is carried out,
so its rules survive a change of mechanism. Not every interface is a port: a repository is not one, because persisting
itself was never part of being a train.

See [oop.md](oop.md#3-behavior-ports-and-adapters). Implemented by an [Adapter](#adapter).

### Ports and Adapters

An architectural style (also called **Hexagonal Architecture**): the domain sits at the centre, declares [ports](#port)
for everything it needs from the outside world, and infrastructure supplies [adapters](#adapter) that implement them.
Every dependency points inward. The test that the lines are drawn correctly — can you run the object with no hardware
and no database?

See [oop.md](oop.md#3-behavior-ports-and-adapters). Closely related to
[Onion Architecture](#onion-architecture), which this codebase uses: the same inward-pointing rule, described in layers
rather than in sockets.

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

### Shim

A thin layer of code whose only job is to make one interface satisfy another. It holds no rules and makes no decisions —
it translates a call and gets out of the way. If a shim starts deciding things, it has stopped being a shim. Named for
the physical object: a thin piece of material wedged into a gap to make two parts fit.

**Not a synonym for [Adapter](#adapter)**, though the two collapse in CRUD-shaped systems where the domain already
speaks the mechanism's language. A shim is thin *by definition*; an adapter is thin only in its *interface*. See
[oop.md](oop.md#3-behavior-ports-and-adapters).

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