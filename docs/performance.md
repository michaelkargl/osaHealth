# Performance

Stack-specific performance guidance for osaHealth's MongoDB-backed F# backend.
The focus is on database interaction — it is almost always the bottleneck before
the application code is.

---

## Database calls inside loops

Issuing a database call per item inside a loop is the MongoDB equivalent of an N+1
query. Batch the work into one round-trip.

```fsharp
// ❌ one find per recording — N+1
let enrichRecordings recordings =
    recordings |> List.map (fun r ->
        let user = usersCollection.Find(fun u -> u.Id = r.UserId).FirstOrDefault()
        { r with UserName = user.Name }
    )

// ✅ collect IDs, fetch in one query, then merge in memory
let enrichRecordings recordings =
    let userIds = recordings |> List.map (fun r -> r.UserId) |> List.distinct
    let userFilter = Builders<UserEntity>.Filter.In((fun u -> u.Id), userIds)
    let users = usersCollection.Find(userFilter).ToList() |> List.ofSeq
    let userMap = users |> List.map (fun u -> u.Id, u.Name) |> Map.ofList
    recordings |> List.map (fun r -> { r with UserName = userMap[r.UserId] })
```

`bulkWrite` is the equivalent for writes — `ReplaceOneAsync` inside a loop is the
same anti-pattern.

---

## Read projection

Don't hydrate a full domain entity when the handler only needs a subset of fields.
Pull what the endpoint actually returns, not the whole document.

```fsharp
// ❌ pulls every field, returns three
let! recording = collection.Find(fun e -> e.Id = id).FirstOrDefaultAsync()
return {| Id = recording.Id; Date = recording.DateEpoch; Title = recording.Title |}

// ✅ projects only the needed fields
let projection = Builders<RecordingEntity>.Projection
    .Include(fun e -> e.Id)
    .Include(fun e -> e.DateEpoch)
    .Include(fun e -> e.Title)
let! result = collection.Find(fun e -> e.Id = id).Project(projection).FirstOrDefaultAsync()
```

This matters more as documents grow. A `Recording` with embedded metadata, tags,
and transcript blobs can be kilobytes; the list endpoint might need 100 bytes per row.

---

## Indexes

Every query path that filters or sorts must be backed by an index. A new handler
that adds a `where` clause or an `orderBy` without a corresponding index will
degrade to a full collection scan — fine at 100 documents, crippling at 100,000.

When reviewing a PR that introduces a new query, ask: does this collection have an
index that serves this filter/sort combination? If the answer isn't obvious from the
existing index definitions, flag it.

MongoDB's `explain()` is the verification tool:

```javascript
db.recordings.find({ UserId: "...", DateEpoch: { $gte: ... } })
             .sort({ DateEpoch: -1 })
             .explain("executionStats")
```

If `winningPlan.stage` is `COLLSCAN`, the query needs an index.

---

## Cursor pagination

Our pagination cursor is `(DateEpoch, Id)` — a compound key. Every paginated query
must be backed by a compound index on those two fields in that order. Without it,
each page request triggers a full scan to find the cursor position, which defeats
the entire point of keyset pagination.

> See also: [Cursor Pagination](glossary.md#cursor-pagination).
