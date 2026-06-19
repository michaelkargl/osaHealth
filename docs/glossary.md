# Glossary

---

## A

### Application Core

See [onion-architecture.md](onion-architecture.md#application-core).

### Application Services Layer

See [onion-architecture.md](onion-architecture.md#application-services-layer).

---

## D

### Domain Model Layer

See [onion-architecture.md](onion-architecture.md#domain-model-layer).

### Domain Services Layer

See [onion-architecture.md](onion-architecture.md#domain-services-layer).

---

## E

### Error

An expected, predictable failure that the code is designed to handle. Errors are part of normal operation — a field missing from a request, a value that violates a constraint, a resource that does not exist. In osaHealth, errors are modeled as `Result<'T, 'E>` values so that the caller is forced to handle both outcomes.

An error is not surprising. If it can happen, it is in the return type.

### Exception

An unexpected failure caused by factors outside the code's control — network down, disk full, database unreachable — or a programmer mistake (null reference, division by zero). Exceptions are not part of normal operation and are not modeled in return types.

In osaHealth, exceptions are only caught at system boundaries ([Infrastructure Layer](onion-architecture.md#infrastructure-layer)). They are never used for control flow.

---

## O

### Onion Architecture

A software architecture pattern that organises code into concentric layers where dependencies only ever point inward. The innermost layer (Domain Model) has no knowledge of outer layers. Outer layers (Application Services, Infrastructure) depend on inner ones — never the reverse. This makes the domain independent of any database, framework, or delivery mechanism.

The layers from inside out: **Domain Model** → **Domain Services** → **Application Services** → **Infrastructure**.

**Application Core** is a grouping label (not a layer) for everything inside Infrastructure: Domain Model + Domain Services + Application Services together.

**Layers, inside out:**
- [Infrastructure Layer](onion-architecture.md#infrastructure-layer) — databases, HTTP frameworks, external systems
- [Application Services Layer](onion-architecture.md#application-services-layer) — handles a user action end to end
- [Domain Services Layer](onion-architecture.md#domain-services-layer) — cross-entity domain logic (currently unused in osaHealth)
- [Domain Model Layer](onion-architecture.md#domain-model-layer) — the core; knows nothing outside itself

See [onion-architecture.md](onion-architecture.md) for the full explanation and diagram.

---

## I

### Idempotency

A property of an operation where calling it multiple times with the same input leaves the world in the same state as calling it once. `f(f(x)) = f(x)`.
Idempotency requires explicit design for impure operations (I/O, persistence) where side effects can accumulate

| Operation | Pure? | Idempotent? |
|---|---|---|
| `Recording.tryCreate(...)` | ✅ | ✅ trivially — no world to change |
| `DELETE /recordings/:id` | ✗ (I/O) | ✅ deleting twice = gone once |
| `upsert recording` | ✗ (I/O) | ✅ designed explicitly — `IsUpsert = true` |
| `INSERT recording` | ✗ (I/O) | ✗ second call → duplicate or error |
| `appendToLog(msg)` | ✗ (I/O) | ✗ 10 calls = 10 log entries |

### Infrastructure Layer

See [onion-architecture.md](onion-architecture.md#infrastructure-layer).

### Invariant

A rule that must always be true, no matter what. If it is ever false, the system's correctness or security model breaks.

*Example from this codebase:* the server always derives `userId` from the auth token — never from the request body or URL. This must hold for every request without exception.

---

## M

### Measure Types

See [Phantom Types](#phantom-types).

---

## P

### Phantom Types

A technique where a type parameter is present in the type signature but erased at runtime — it exists only to carry compile-time information. The runtime value is unchanged; the compiler uses the tag to reject incorrect usage.

In osaHealth, `FSharp.UMX` provides phantom measure types for primitive IDs:

| Type | Underlying | Tag |
|---|---|---|
| `Guid<RecordingId>` | `Guid` | `RecordingId` |
| `string<UserId>` | `string` | `UserId` |

Passing a `string<UserId>` where a `Guid<RecordingId>` is expected is a compile error. At runtime, both are the plain underlying value — zero overhead.

See also: [UMX Measure Types](#umx-measure-types).

### Pure function

A function with no side effects that always returns the same output for the same inputs. No I/O, no mutation, no randomness, no time access — everything the function needs is passed in as a parameter.

**Pure functions are always [idempotent](#idempotency).** Because they have zero effect on external state, calling them N times is indistinguishable from calling them once.

| Operation | Pure? | Idempotent? |
|---|---|---|
| `Recording.tryCreate(...)` | ✅ | ✅ trivially — no world to change |
| `DELETE /recordings/:id` | ✗ (I/O) | ✅ deleting twice = gone once |
| `upsert recording` | ✗ (I/O) | ✅ designed explicitly — `IsUpsert = true` |
| `INSERT recording` | ✗ (I/O) | ✗ second call → duplicate or error |
| `appendToLog(msg)` | ✗ (I/O) | ✗ 10 calls = 10 log entries |

The practical payoff: a failing customer request can be reproduced exactly as a unit test. Capture the inputs, paste them in, and the test runs the same logic path the customer hit — no clock mocking, no database seeding, no environment setup.

In osaHealth, all domain functions must be pure. I/O (HTTP, MongoDB) lives in `osaHealth.Api` and `osaHealth.Repository`. Time is injected as `now: unit -> DateTime` at the boundary that needs it, not read directly with `DateTime.UtcNow` inside domain logic.

---

## U

### UMX Measure Types

See [Phantom Types](#phantom-types).

---

## 4

### 403 Forbidden

The HTTP 403 Forbidden client error response status code indicates that the server understood the request but refused to process it.

<https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/403>

### 409 Conflict

The HTTP 409 Conflict client error response status code indicates a request conflict with the current state of the target resource.

<https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/409>