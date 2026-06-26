# Security

Stack-specific security guidance for osaHealth's F# / Oxpecker / MongoDB backend.
This is not a general security textbook — it calls out the patterns that have caused
real issues in this stack.

---

## Query injection

MongoDB queries should use the driver's typed `FilterDefinitionBuilder`, never strings
assembled from user input. String-built filters open the same injection surface as
string-built SQL — a user-controlled value that slips past concatenation can alter the
query shape.

```fsharp
// ❌ string-built — user input interpolated into BSON
let filter = sprintf """{ "UserId": "%s" }""" userId
collection.Find(filter).ToListAsync()

// ✅ typed builder — the driver escapes and structures the value
let filter = Builders<RecordingEntity>.Filter.Eq((fun e -> e.UserId), userId)
collection.Find(filter).ToListAsync()
```

JavaScript evaluation (`$where`, `mapReduce`) should not be used without an explicit
security review. The driver's aggregation pipeline and filter builders cover every
query pattern the application needs.

---

## Data exposure

**Exceptions.** Stack traces and exception messages must never reach the HTTP response
body. The `UseExceptionHandler` middleware already logs the full exception server-side
and returns a sanitised `ApiErrorsDto` with status 500 to the client. That separation
must hold — do not add `ex.Message` or `ex.ToString()` to response DTOs.

> See also: [System boundaries — exceptions](coding-guidelines-fsharp.md#system-boundaries--exceptions).

**Logs.** Log statements must not include PII (personally identifiable information)
or credentials. Audit `LogInformation`/`LogError` calls the same way you'd audit a
response body — what lands in the log is as readable as what goes to the client,
and logs are often retained much longer.

**Secrets.** API keys, connection strings, and signing secrets belong in environment
variables or a secrets manager — never in source files, configs committed to the repo,
or hard-coded strings. If a value would compromise the application when the repository
becomes public, it does not belong in the repository.

---

## Access control

A handler must never trust a caller-supplied identifier as proof of identity.
Every endpoint that scopes data to a user must derive the authorised user identity
from the authenticated principal (the token/session), not from the request body or
query string.

```fsharp
// ❌ trusts the caller — anyone can supply any userId
let userId = dto.UserId

// ✅ derives from the authenticated principal
let userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier).Value
```

> See also: [ADR 0015 — Authentication](../docs/adr/0015-authentication.md).

Multi-tenant paths (if added later) would extend this rule: derive both the tenant
and the user identity from the principal — never from the request payload.

---

## Over-posting (mass assignment)

A DTO that maps every field from the request body directly into a domain command
gives the caller control over fields they should not be able to set. Each endpoint
should bind only the fields the caller is authorised to supply.

```fsharp
// ❌ maps every field blindly
let command = { Id = dto.Id; UserId = dto.UserId; CreatedAt = dto.CreatedAt; ... }

// ✅ explicit selection — only the fields the endpoint owns
let command = { Id = dto.Id; UserId = principalUserId; CreatedAt = DateTime.UtcNow; ... }
```

Fields like `CreatedAt`, `UpdatedAt`, `UserId` (when it should be the principal),
and internal status flags should be set by the handler, not by the caller.
