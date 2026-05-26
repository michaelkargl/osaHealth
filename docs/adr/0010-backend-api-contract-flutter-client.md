# ADR-0010: Backend ↔ Flutter client API contract

- **Status:** Proposed
- **Date:** 2026-05-26
- **Deciders:** Remy Okafor (Software Architecture) — owner. Saoirse Lindqvist (Flutter) — required reviewer before this moves to Accepted.
- **Tags:** backend, api, sync, contract

## Context

The Flutter client is offline-first. It generates UUIDs locally, persists recordings to Drift/SQLite immediately, and replays them to the backend through an idempotent sync loop. That single property — *the client decides identity, the server confirms it* — has knock-on effects across every shape the API can take: HTTP semantics, versioning, error envelope, pagination, deletion semantics, and what "duplicate" means on the wire.

The walking skeleton (OSA-18 / OSA-19) shipped enough endpoints to learn from. Before more endpoints are built on top, the contract needs to be an explicit artefact, not whatever the skeleton happened to do first.

This ADR locks the contract across six interlocking questions. They are bundled because they trade off against each other — picking RFC 9457 for errors, for example, only pays off if every endpoint commits to it, and the verb choice (PUT vs POST) determines what "duplicate" must look like in the error envelope. Splitting them would re-litigate the trade-offs three times.

## Decision

We will design the backend API around the following six commitments. Together they form v1 of the contract.

### 1. HTTP semantics for create — **PUT with client-generated UUID in the path**

`PUT /v1/recordings/{client-uuid}` is the canonical create. The client owns the identifier; the server confirms it. Idempotency is a property of the verb, not a property the server has to reconstruct from a dedup table.

- First write: `201 Created` + canonical resource body + `Location` header.
- Replay of the same body: `200 OK` + canonical resource body (byte-identical to the first response).
- Replay with the same UUID but a *different* body: `409 Conflict` + the server's canonical body in the response, so the client can reconcile (see decision 6).

`POST /v1/recordings` is *not* offered for creates. Adding it later is reversible; offering both now is not — it would force every consumer to choose, and split the dedup logic.

### 2. Versioning — **URL prefix `/v1/...`**

Versioning lives in the path. `/v1/recordings/{id}`, `/v1/users/{id}/recordings`, etc. v2 would live at `/v2/`, with `/v1/` kept alive until clients have migrated.

We reject Accept-header content negotiation (`application/vnd.osa.v1+json`) because it makes browser debugging, curl recipes, and CDN cache keys harder for marginal benefit. We reject "no versioning, decide later" because retro-fitting a prefix is a breaking change for every existing client.

### 3. Error response shape — **RFC 9457 Problem Details for HTTP APIs**

Every non-2xx response carries a `application/problem+json` body with at least `type`, `title`, `status`, and `detail`. Domain-specific fields are added as additional properties (e.g. `conflictingResource` on a 409).

We reject custom envelopes (`{ "error": { "code": "...", "message": "..." } }`) — they're equivalent in expressiveness but cost a small amount of cross-cutting tooling (OpenAPI generators, browser devtools error rendering, language-level client libraries) for no domain benefit.

### 4. Pagination — **Cursor-based, opaque server-issued cursor**

`GET /v1/recordings?cursor=<opaque>&limit=N` returns `{ items: [...], nextCursor: "..." | null }`. The cursor is opaque to the client; the server may encode `(created_at, id)` or any other stable ordering inside it.

Cursor-based is the only honest answer for our case: the sync loop is reading a stream that is being written to. Offset-based pagination skips or duplicates records when inserts arrive between page fetches — exactly when the client is doing a long sync after a period offline.

We use a sentinel `nextCursor: null` to signal end-of-stream. No `total` field — computing it on every page request is wasteful and the client doesn't need it.

### 5. Deletion / tombstones — **Not in v1**

Recordings are append-only in v1. There is no `DELETE` endpoint, no `deleted_at` column, no tombstone propagation through the sync loop. "Correcting" a recording is a future concern (a new ADR), and is more likely to look like an "amendment" recording that references its predecessor than a true delete.

This is the call most likely to be wrong, and we've labelled it as such in the reversal conditions. We choose it because building sync-aware soft-delete from day one — including idempotent tombstone replay, tombstone retention windows, and resurrection semantics — is a non-trivial design in its own right, and we have no concrete use case driving it yet.

### 6. "Already exists" semantics — **Specified explicitly, falls out of decision 1**

When the Flutter client `PUT`s a recording it already created:

| Server state                                    | Response                                                           |
| ----------------------------------------------- | ------------------------------------------------------------------ |
| Resource does not exist                         | `201 Created` + canonical body + `Location: /v1/recordings/{id}`   |
| Resource exists, body byte-identical            | `200 OK` + canonical body (same as the original `201`'s body)      |
| Resource exists, body semantically equivalent\* | `200 OK` + canonical body                                          |
| Resource exists, body differs                   | `409 Conflict` + problem+json + canonical body in `conflict.server` |

\* "Semantically equivalent" means the same recorded fields, ignoring client-side metadata the server doesn't store (e.g. a local `syncedAt` timestamp). The server is the arbiter of equivalence — the client never asks "is this a duplicate?"; it just `PUT`s and reads the response.

This makes the sync loop trivially correct under crash-and-retry: replaying any prior request is safe.

## Options considered

### Verb choice: PUT vs POST

- **PUT (chosen).** Pros: honest about idempotency, identity owned by the client, "did this already arrive?" is unanswerable but also unnecessary. Cons: requires the client to commit to UUIDs as the canonical key forever.
- **POST with server-generated id + client correlation id.** Pros: matches how most REST tutorials are written. Cons: forces the server to maintain a dedup table keyed on correlation id, which is exactly the property PUT gives you for free; introduces a window where the client doesn't yet know the canonical id. Lost.
- **POST with client UUID in body, 200 on duplicate.** Pros: more familiar shape. Cons: encodes "we are pretending this is idempotent" rather than expressing it in the verb; HTTP intermediaries can't help. Lost.

### Versioning

- **URL prefix `/v1/` (chosen).** Pros: trivial to debug, trivial to route, cache-friendly. Cons: cosmetic — the version is in every URL.
- **Accept header content negotiation.** Pros: theoretically cleaner. Cons: invisible in browser devtools, harder curl recipes, more CDN-cache-key complexity. Lost.
- **No versioning.** Pros: less ceremony. Cons: retrofitting a prefix when v2 ships is a breaking change for every existing client. Lost.

### Error envelope

- **RFC 9457 Problem Details (chosen).** Pros: standard, broad tooling support, extensible via additional properties. Cons: slightly more ceremony than a custom shape for the simplest errors.
- **Custom `{ error: { code, message } }` envelope.** Pros: minimal. Cons: re-invents a wheel that has a perfectly good RFC; loses generic client-side error renderers. Lost.

### Pagination

- **Cursor-based (chosen).** Pros: stable under concurrent writes, which is exactly our case. Cons: client can't jump to "page 5".
- **Offset/limit.** Pros: familiar. Cons: skips or duplicates records under concurrent writes — directly hostile to the sync use case. Lost.

### Deletion semantics

- **Not in v1 (chosen).** Pros: smaller surface, less to get wrong, no idempotent-tombstone design needed. Cons: when the first "I need to retract a recording" use case lands, it will need a new ADR and a v1.x or v2 contract change.
- **Soft-delete with tombstone propagation from day one.** Pros: future-proof. Cons: builds non-trivial machinery for a use case we don't have, and pre-commits to a deletion semantics (hard-delete-after-N-days vs forever-retained) that we'd rather decide with a real driver. Lost.

### "Already exists" body

- **Server-authoritative, returned in conflict (chosen).** Pros: client can show the user the divergence and let them decide. Cons: slightly more bytes on the wire for conflicts.
- **`409 Conflict` with no body.** Pros: smaller. Cons: forces the client to do an extra `GET` to recover, which is a guaranteed extra round-trip on the unhappy path. Lost.

## Consequences

**Easier.** The Flutter sync loop becomes "PUT every unsynced recording; on 2xx mark synced, on 409 surface to user, on 5xx retry with backoff." That is the entire algorithm. Idempotency is a property of the contract, not a thing the client has to enforce.

**Easier.** Every endpoint speaks the same error language. A generic client-side error handler can render any failure from any endpoint without per-endpoint code.

**Harder.** Every new endpoint must commit to PUT-with-client-UUID for creates, problem+json for errors, and cursor pagination for lists. Drift is the failure mode — one POST-with-server-id endpoint and the contract has two shapes. Code review enforces this.

**Harder.** When the first deletion use case lands (it will), we have to design tombstone semantics from scratch under deadline pressure, rather than having a partial design already in place. This is the trade we are explicitly making — see reversal conditions.

**Observability.** Every endpoint logs `{verb, path, status, problem.type if non-2xx, client_uuid if present}`. The conflict rate (`409 / 2xx`) on the recordings endpoint becomes a first-class metric — it tells us whether the client's offline-merge logic is misbehaving long before users complain.

**Follow-up work this ADR creates:**

- Update walking-skeleton endpoints to match the contract (OSA-19 / OSA-29).
- OpenAPI spec under `api/osa-recordings-v1.yaml`, generated from or hand-aligned to this ADR.
- Reusable problem+json builder in the F# Oxpecker handlers.
- Conflict-rate dashboard panel in Grafana, alongside existing latency panels.

## Reversal conditions

We revisit this ADR if any of the following becomes true:

- **PUT semantics break under a real client requirement** — e.g. we discover the client genuinely cannot generate UUIDs early enough in some flow (offline media capture before SQLite is initialised, for instance). At that point, the `POST + server-id + correlation` shape becomes worth its weight.
- **The "no soft-delete" call hurts.** Signal: the first retraction use case lands and the workaround (an "amendment" record) is genuinely worse for users than tombstones would have been. At that point, a follow-up ADR specifies tombstones; this ADR is partially superseded.
- **Cursor pagination becomes a UX problem.** Signal: a screen needs random-access pagination (jump to page N). At that point we add offset-based pagination as a second, opt-in mechanism — not a replacement.
- **RFC 9457 tooling regresses.** Unlikely, but if the F# / Flutter ecosystems lose problem+json support, we revisit the envelope.
- **A second client appears with different identity semantics** (e.g. a web client that doesn't want to own UUIDs). At that point the contract may need to support both shapes — but that is a v2 conversation, not a patch to v1.

## References

- RFC 9457 — *Problem Details for HTTP APIs* (the basis for decision 3).
- ADR-0001 — *Record architecture decisions* (the practice this ADR is an instance of).
- OSA-42 — the issue that prompted this ADR.
- OSA-18 / OSA-19 — walking-skeleton endpoints whose shapes this ADR retroactively constrains.
- Saoirse Lindqvist — Flutter-side reviewer; sync-loop ergonomics must work for her before this moves from Proposed to Accepted.
