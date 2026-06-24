# Onion Architecture

Onion Architecture organises code into concentric layers where **dependencies only ever point inward**. The inner layers define core business concepts and have zero knowledge of the outer ones. Outer layers depend on inner layers — never the reverse. This means you can swap your database, HTTP framework, or validation strategy without touching the domain.

```
┌─────────────────────────────────────────────────┐
│              Infrastructure                     │
│    (UI, databases, frameworks, external APIs)   │
│  ╔═══════════════════════════════════════════╗  │
│  ║           Application Core                ║  │
│  ║  ┌─────────────────────────────────────┐  ║  │
│  ║  │        Application Services         │  ║  │
│  ║  │  (use cases, orchestration, CQRS)   │  ║  │
│  ║  │  ┌───────────────────────────────┐  │  ║  │
│  ║  │  │       Domain Services         │  │  ║  │
│  ║  │  │  (domain logic, validation)   │  │  ║  │
│  ║  │  │  ┌─────────────────────────┐  │  │  ║  │
│  ║  │  │  │      Domain Model       │  │  │  ║  │
│  ║  │  │  │  (entities, measures,   │  │  │  ║  │
│  ║  │  │  │   value objects)        │  │  │  ║  │
│  ║  │  │  └─────────────────────────┘  │  │  ║  │
│  ║  │  └───────────────────────────────┘  │  ║  │
│  ║  └─────────────────────────────────────┘  ║  │
│  ╚═══════════════════════════════════════════╝  │
└─────────────────────────────────────────────────┘
```

**Application Core** is a collective label — not a single layer. It is everything that is *not* Infrastructure: Domain Model + Domain Services + Application Services together. If you swapped MongoDB for Postgres, the Application Core would be untouched.

---

## The One Rule — and the Common Misconception

The rule is: **inner layers must never know about outer layers**. Outer layers depending on inner layers is not just allowed — it is the point.

```
Domain Model        ← innermost, knows nothing outside itself
Application Services ← knows Domain, knows nothing about Infrastructure
Infrastructure      ← knows everything, is known by nothing
```

What Onion prevents is the reverse:

| Dependency | Verdict | Reason |
|---|---|---|
| `Application → Domain` | ✅ Correct | Application layer uses domain types |
| `Infrastructure → Domain` | ✅ Correct | Persistence maps to/from domain types |
| `Domain → Infrastructure` | ✗ Violation | Domain would know about MongoDB |
| `Domain → Application` | ✗ Violation | Domain would know about HTTP |

The confusion usually comes from mixing Onion up with strict N-tier / layered architecture where you can *only* talk to the adjacent layer (`Presentation → Business Logic → Data Access`). Onion is more permissive: any outer layer can reach directly into any inner layer. The domain sits at the centre and everything depends on it — that's why it's a circle, not a stack.

---

## Layers

### Infrastructure Layer

Adapts the domain to external systems — databases, HTTP frameworks, message brokers. The only layer allowed to have side effects. It knows about domain types; the domain knows nothing about it.

```
┌───────────────────────────────────────────────────┐
│                  Infrastructure                   │
│  ╔═════════════════════════════════════════════╗  │
│  ║              Application Core               ║  │
│  ║  ┌─────────────────────────────────────┐   ║  │
│  ║  │       Application Services          │   ║  │
│  ║  │  ┌───────────────────────────────┐  │   ║  │
│  ║  │  │      Domain Services          │  │   ║  │
│  ║  │  │  ┌─────────────────────────┐  │  │   ║  │
│  ║  │  │  │      Domain Model       │  │  │   ║  │
│  ║  │  │  └─────────────────────────┘  │  │   ║  │
│  ║  │  └───────────────────────────────┘  │   ║  │
│  ║  └─────────────────────────────────────┘   ║  │
│  ╚═════════════════════════════════════════════╝  │
└───────────────────────────────────────────────────┘
```

In osaHealth: `osaHealth.Repository` — `RecordingEntity`, `Recordings.upsert`.

| Allowed | Not allowed |
|---|---|
| BSON-attributed entity types (`RecordingEntity`) | Business rules or validation logic |
| Domain ↔ entity mapping (`toDomain`, `toEntity`) | HTTP framework types |
| Persistence operations (`ReplaceOneAsync`) | |

### Application Core

A grouping label — not a layer. It names everything that is *not* Infrastructure: [Domain Model Layer](#domain-model-layer) + [Domain Services Layer](#domain-services-layer) + [Application Services Layer](#application-services-layer) together. The double-line border in diagrams marks the Application Core boundary.

If you swapped MongoDB for Postgres or Oxpecker for a different HTTP framework, the Application Core would be untouched.

```
╔═════════════════════════════════════════╗
║           Application Core             ║
║  ┌─────────────────────────────────┐   ║
║  │      Application Services       │   ║
║  │  ┌───────────────────────────┐  │   ║
║  │  │     Domain Services       │  │   ║
║  │  │  ┌─────────────────────┐  │  │   ║
║  │  │  │    Domain Model     │  │  │   ║
║  │  │  └─────────────────────┘  │  │   ║
║  │  └───────────────────────────┘  │   ║
║  └─────────────────────────────────┘   ║
╚═════════════════════════════════════════╝
```

### Application Services Layer

The layer that handles one user action end to end. It takes an incoming request, validates it, coordinates domain and infrastructure, and sends back a response. It does no actual work itself — it delegates to the layers that do.

```
┌─────────────────────────────────────┐
│       Application Services          │
│  ┌───────────────────────────────┐  │
│  │       Domain Services         │  │
│  │  ┌─────────────────────────┐  │  │
│  │  │      Domain Model       │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

In osaHealth: `osaHealth.Api`.

| Allowed | Not allowed |
|---|---|
| Validate the request (`validateInsertRecordingRequest`) | Business rules — those belong in [Domain Model Layer](#domain-model-layer) |
| Map input to a command (`Recording.createCommand`) | Calling the database directly — that is [Infrastructure Layer](#infrastructure-layer)'s job |
| Apply UMX tags when constructing a domain type | BSON attributes, MongoDB driver types |
| Handle HTTP request / response | Domain-branded types (`string<UserId>`) in Commands/Queries — see [UMX layer rule](coding-guidelines-fsharp.md#layer-rule) |
| Wire dependencies via partial application | |

### Domain Services Layer

[Pure](glossary.md#pure-function) domain logic that involves more than one entity. No I/O.

```
┌───────────────────────────────────┐
│         Domain Services           │
│  ┌─────────────────────────────┐  │
│  │        Domain Model         │  │
│  └─────────────────────────────┘  │
└───────────────────────────────────┘
```

osaHealth does not currently have a distinct Domain Services layer — the project is simple enough that all domain logic lives directly in Domain Model types. This layer would be introduced when logic spans entities (e.g. "a user may not have more than N active recordings").

### Domain Model Layer

The innermost layer. [Pure](glossary.md#pure-function) types and construction validation only. No I/O, no framework imports, no side effects.

```
┌─────────────────────────────┐
│        Domain Model         │
└─────────────────────────────┘
```

In osaHealth: `osaHealth.Domain` — `Recording`, `RecordingId`, `UserId`.

| Allowed | Not allowed |
|---|---|
| Record types (`Recording`) | I/O of any kind |
| UMX measure types (`RecordingId`, `UserId`) | BSON attributes, MongoDB driver types |
| `tryCreate` returning `Result<_, ValidationError list>` | HTTP framework types |
| Private constructors enforcing invariants | `DateTime.UtcNow` — inject `now: unit -> DateTime` instead |

---

## Terminology Cross-Reference

The same architectural pattern appears under several names. osaHealth uses **Onion** vocabulary. This table maps equivalent concepts if you encounter the other traditions:

| Concept | Onion (osaHealth) | Clean Architecture | N-tier / Layered |
|---|---|---|---|
| Core business types and rules | Domain Model | Entities | Business Logic |
| Application-specific use cases | Application Services | Use Cases | Business Logic |
| Boundary translation and external tools | Infrastructure | Interface Adapters + Frameworks & Drivers | Presentation + Data Access |

**Why Onion over Clean Architecture?** Clean Architecture introduces four rings where Onion has three, splitting Application Services into "Use Cases" and "Interface Adapters." At osaHealth's scale that distinction adds naming overhead without adding clarity. Onion's three rings are sufficient and unambiguous.

**Why not N-tier?** N-tier collapses Domain Model and Application Services into a single "Business Logic" layer, which hides the boundary between *what the system is* (domain types) and *what the system does* (use cases). Keeping them separate is the point.
