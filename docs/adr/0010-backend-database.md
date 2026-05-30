# ADR-0010: Backend Database — Query Layer for the Sync Loop

- **Status:** Proposed (evaluation in progress — no decision made)
- **Date:** 2026-05-30
- **Deciders:** Michael Kargl, Remy Okafor (Architecture review)
- **Tags:** backend, database, sync, pagination

---

## Context

> For the sync loop design, client UUID rationale, and a full explanation of cursor pagination, see **[docs/offline-sync.md](../offline-sync.md)**.

osaHealth's sync loop pulls server-side recordings via cursor-paginated queries while the result set receives concurrent writes. Stable keyset pagination is a hard requirement — offset pagination delivers duplicates under concurrent inserts, which is a correctness bug in a sync loop.

The initial architecture choice was Dapr with a MongoDB-backed state store, for portability across backing stores. The Dapr State Query API (`v1.0-alpha1`) was evaluated against this requirement. The evaluation is complete. A direct MongoDB approach is now being evaluated as an alternative.

---

## Decision

**Pending.** The Dapr State Store query layer has been disqualified (see below). The direct MongoDB evaluation is in progress. This ADR will be updated to record the decision once that evaluation concludes.

---

## Options considered

### Option A — Dapr State Store Query API *(evaluated — disqualified)*

Dapr's state store provides a query API (`v1.0-alpha1`) that supports filter, sort, and pagination over a backing store (here: MongoDB).

**Evaluation spike:** `src/Repl/dapr-query-api/` — three F# scripts against a live Dapr sidecar + MongoDB container.

**Finding 1 — pagination token is a skip-offset, not a keyset cursor**
(`02-pagination-token.fsx`)

The `token` field returned by the query API decodes to a plain `{"skip": N}` offset. It does not encode the last-seen value of the sort field. A client treating empty token as "caught up" pays an extra round-trip; the correct done-signal is `results.length < limit`.

**Finding 2 — offset pagination is unstable under concurrent inserts**
(`03-stability-under-insert.fsx`)

Inserting a recording whose `updated_ms` sorts before the current page boundary causes the offset to shift. The test reproducibly delivers a duplicate row on page 2 when an insert occurs between page 1 and page 2. This is a structural property of offset pagination, not a bug that can be configured away. See [docs/offline-sync.md](../offline-sync.md) for the side-by-side sequence diagram.

**Finding 3 — manual keyset is possible in theory but closes on examination**

Dapr's filter DSL supports `GT`/`GTE` operators, which would allow the client to implement keyset pagination by ignoring the page token and passing `updated_ms > lastSeenValue` as a filter on each subsequent query. Three conditions block this in practice:

- GT/GTE support is limited to numeric fields. The production data model stores timestamps that require preprocessing to numeric epoch integers before they are comparable — schema migration plus ongoing write-path changes.
- Tie-breaking requires a compound cursor: `(updated_ms > last) OR (updated_ms = last AND key > lastKey)`. It is unclear whether the Dapr filter DSL can express this compound `OR` condition.
- The query API is flagged `v1.0-alpha1`. Building a custom pagination engine against an unstable, undocumented contract surface is an ongoing maintenance liability, not a one-time cost.

**Verdict:** Dapr State Store is not fit for the sync-loop query path. Dapr remains in the architecture for pub/sub, service invocation, secrets, and bindings — capabilities it delivers reliably. The state store component is removed from the query path.

**Reversal condition:** If the Dapr State Query API reaches a stable (`v1.0`) release with native keyset cursor support (not offset token), this option is worth re-evaluating. Until that release exists, the evaluation above stands.

---

### Option B — Direct MongoDB *(evaluation in progress)*

MongoDB's native query operators (`$gt`, `$gte`) support keyset pagination as a first-class pattern. The `cursor.skip()` manual page explicitly recommends range queries over skip-based pagination for result-set stability and performance at depth.

The expected query shape for the sync loop pull:

```javascript
db.recordings.find({
  userId: "user-A",
  $or: [
    { updated_ms: { $gt: lastCursor } },
    { updated_ms: lastCursor, _id: { $gt: lastId } }
  ]
}).sort({ updated_ms: 1, _id: 1 }).limit(N)
```

The compound cursor `(updated_ms, _id)` handles ties — two recordings written within the same millisecond — because MongoDB's `_id` (ObjectId) is inherently monotonic and unique.

A compound index on `(userId, updated_ms, _id)` covers the filter and sort without a collection scan.

**Evaluation in progress:** The same three test scenarios from the Dapr spike will be reproduced against a direct MongoDB connection:
1. Filter basics
2. Pagination token stability (expected: no offset token, cursor is application-managed)
3. Stability under mid-pagination insert (expected: no duplicate delivery)

**Additional consideration — Change Streams**

MongoDB Change Streams are noted as a future optionality. If the sync loop evolves toward near-realtime push rather than poll-based pull, Change Streams eliminate pagination entirely. Choosing MongoDB now does not foreclose that path; it opens it.

This ADR will be updated with findings and a final decision once the evaluation concludes.

---

### Option C — PostgREST + PostgreSQL *(considered, not yet evaluated)*

PostgREST exposes a PostgreSQL schema as a REST API. Keyset pagination is expressible via query parameters (`?order=updated_ms&updated_ms=gt.CURSOR`). PostgreSQL's query planner handles compound index scans efficiently.

This option was identified during the Dapr evaluation as a coherent alternative in a Dapr-adjacent architecture: Dapr handles the write-path building blocks; PostgREST handles the read path from Postgres. If the direct MongoDB evaluation does not close cleanly, PostgREST + PostgreSQL is the next candidate for a spike.

---

## Consequences

*To be completed when the decision is recorded.*

---

## Reversal conditions

*To be completed when the decision is recorded. The Dapr reversal condition is recorded under Option A above.*

---

## References

- Evaluation spike: `src/Repl/dapr-query-api/` — `01-filter-basics.fsx`, `02-pagination-token.fsx`, `03-stability-under-insert.fsx`
- [MongoDB `cursor.skip()` manual — recommends range queries over skip](https://docs.mongodb.com/manual/reference/method/cursor.skip/)
- [MongoDB `$gt` query operator](https://www.mongodb.com/docs/manual/reference/operator/query/gte/)
- [Pagination in MongoDB: Right Way vs Common Mistakes](https://www.mongodb.com/community/forums/t/pagination-in-mongodb-right-way-to-do-it-vs-common-mistakes/208429)
- [ADR-0005](0005-flutter-state-management.md) — Bloc state management; references ADR-0010 cursor-based sync pull.
