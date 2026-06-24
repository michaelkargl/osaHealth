# Pagination

The practice of splitting a large result set into smaller, sequentially accessible chunks.
Pagination prevents unbounded queries and keeps response sizes predictable.

This project uses [keyset pagination](#keyset-pagination). [Offset pagination](#offset-pagination)
was evaluated and rejected — see [ADR-0010](adr/0010-backend-database.md).

---

## Offset Pagination

A pagination strategy where the client requests results by position: *skip the first N
records, return the next M*. Simple to implement and easy to reason about, but degrades
at scale.

As the offset grows, the database scans and discards an increasing number of rows before
returning results. At page 1000 with a page size of 20, the database skips 19,980 rows on
every request.

Offset pagination also suffers from **page drift**: if a record is inserted or deleted
between two requests, the page boundaries shift and the client may see a duplicate or skip
a record entirely.

Appropriate for small, stable datasets or when jumping to a specific page number is a
product requirement.

---

## Keyset Pagination

A pagination strategy where the client tracks position using an opaque cursor — an encoded
pointer to the last record seen — rather than a numeric page or row offset.

On each request, the **API** (server) decodes the cursor, queries for records that come
*after* that position in the sort order, and encodes the last returned record's sort key(s)
as the next cursor. When no cursor is provided, the first page is returned.

The cursor works like a bookmark: the **caller** (client — a mobile app, web frontend, or
another service) receives it, stores it, and sends it back on the next request without
interpreting it. The API is the only one who knows what it encodes (opaque cursor). This
leaves the API free to change the cursor format without breaking callers.

```
Records:   [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
           └─── page 1 ───┘
                           ↑ cursor = "abc..."

           GET /recordings?after=abc...

Records:   [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
                           └─── page 2 ───┘
                                           ↑ cursor = "xyz..."
```

Each page returns a fresh cursor pointing to the last record on that page. The caller
always uses the most recent one; earlier cursors are discarded. The cursor is stateless —
the API stores nothing between requests. The cursor *is* the position, encoded as a string
the caller hands back. When there are no more records, the API returns `null` for the next
cursor. The caller stops paginating — no cleanup required on either side.

| Characteristic            | Keyset                                  | Offset Pagination                               |
|---------------------------|-----------------------------------------|-------------------------------------------------|
| Query cost as depth grows | O(1) — filtered by index                | O(n) — database skips rows                      |
| Page drift                | None — anchor is a stable record        | Present — inserts/deletes shift page contents   |
| Random access             | No — cursor is forward-only             | Yes — jump to any page number                   |

Use keyset pagination when the dataset is large, the sort key is stable, and clients page
forward sequentially. When the sort key is not unique (e.g. a timestamp), a
[compound cursor](#compound-cursor) is required to maintain an exact position.

---

## Cursor

A marker that identifies a specific position in a result set. When paginating, the server
issues a cursor after each page; the client sends it back on the next request to say
"continue from here." The name comes from the same root as a text cursor — a pointer to a
current position.

```
Result set:  [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
                            ↑
                         cursor
                   (last record seen)
```

*Note: SQL cursors (Oracle, MSSQL) are stateful — the database holds the position between
fetches and the cursor must be closed when done. API pagination cursors are the inverse:
the position is encoded in the token and carried by the caller. The server holds nothing.*

---

## Compound Cursor

A [cursor](#cursor) that encodes **two** fields instead of one. Required whenever the
primary sort key is not guaranteed to be unique — which is almost always true for
timestamps, since two devices can write the same value within the same second.

**Why a single-field cursor loses its position**

If two recordings share the same `DateEpoch`, a cursor that only encodes the date cannot
identify *which row on that date* was the last one seen. Re-requesting with
`DateEpoch > cursor.date` re-delivers rows already seen on the previous page.

A compound cursor encodes `(DateEpoch, Id)` — the exact coordinate in a two-column sort:

```
Sort: (DateEpoch ASC, Id ASC)

| DateEpoch | Id  |                                  |
|-----------|-----|----------------------------------|
| Jan 1     | AAA | ← page 1                         |
| Jan 1     | BBB | ← page 1 — cursor = (Jan 1, BBB) |
| Jan 1     | CCC | ← page 2                         |
| Jan 2     | AAA | ← page 2                         |
```

**The filter mirrors the sort**

"Give me every row after cursor `(Jan 1, BBB)`" has exactly two cases:

1. The date is **later** than Jan 1 — the Id doesn't matter, the row is definitely after.
2. The date is **exactly** Jan 1 — the Id must be **higher** than BBB.

```
(DateEpoch > cursor.date)
OR
(DateEpoch = cursor.date AND Id > cursor.id)
```

**Why OR, not AND?**

The OR is necessary to replicate the two-column sort as a filter. The sort has two
criteria — date first, Id only when the date is identical. The filter must express the
same two cases:

1. **Date is later** — the Id is irrelevant; the row is after the cursor by date alone.
2. **Date is the same** — the date alone cannot determine position, so Id decides.

These are two *separate, mutually exclusive* situations — a row cannot be on a later date
and on the same date at the same time. OR joins them correctly; AND would require both to
be true simultaneously, which is impossible.

Using the same table from above, with cursor at `(Jan 1, BBB)`:

```
Sort: (DateEpoch ASC, Id ASC)

| DateEpoch | Id  |                                  |
|-----------|-----|----------------------------------|
| Jan 1     | AAA | ← page 1                         |
| Jan 1     | BBB | ← page 1 — cursor = (Jan 1, BBB) |
| Jan 1     | CCC | ← page 2: same date, Id > BBB ✓  |
| Jan 2     | AAA | ← page 2: later date ✓           |
```

Note that `(Jan 2, AAA)` has a *lower* Id than `BBB` — because `Id` is a random `Guid`,
a later date does not mean a higher Id. With `AND` (`DateEpoch > Jan 1 AND Id > BBB`),
this row fails (`AAA > BBB` is false) and page 2 comes back empty. With `OR`, the first
condition (`Jan 2 > Jan 1`) is satisfied and the Id is never checked.

**The tiebreaker just needs to be unique and stable**

- **Unique** — no two rows share the same `(DateEpoch, Id)` pair, so ties are always
  broken. A random `Guid<RecordingId>` is unique by design.
- **Stable** — the Id assigned to a row never changes. The cursor encodes it, so it must
  still point to the same row on the next request.

The Id carries no time ordering of its own — a record created later can have an Id that
sorts lower. That does not matter. The tiebreaker's only job is to distinguish two rows
that share the same date.

See also: [Phantom Types](glossary.md#phantom-types) — how `Guid<RecordingId>` is typed
in this codebase.
