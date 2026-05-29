# Dapr Query API Spike — Repl Scripts

F# scripts that exercise the Dapr State Query API (`v1.0-alpha1`) against
a MongoDB-backed state store and validate its fitness for the osaHealth
recordings sync loop.

**Verdict: NOT fit for sync-loop pagination.** The token is a skip-offset,
not an opaque keyset cursor, and offset pagination is unstable under
concurrent inserts.

## Prerequisites

A minimal Docker Compose stack is included alongside the scripts:

```bash
# From the repo root:
docker compose -f src/Repl/docker-compose.yml up -d

# Wait for healthy (mongosh ping succeeds, daprd listens on 3500):
docker compose -f src/Repl/docker-compose.yml ps

# Tear down when done:
docker compose -f src/Repl/docker-compose.yml down -v
```

All images are pinned to exact versions — `mongo:8.0.6-noble`,
`daprio/placement:1.17.7-linux-amd64`, `daprio/daprd:1.17.7-stablecomponents` —
so this stack is reproducible years later.

The scripts default to `http://localhost:3500` (the daprd sidecar exposed by
the compose stack above) and state store name `statestore`. Override with
`--dapr-endpoint` (`-e`) and `--store-name` (`-s`) if you are running against
a different Dapr instance or a differently named state store:

```bash
dotnet fsi 01-filter-basics.fsx
dotnet fsi 02-pagination-token.fsx
dotnet fsi 03-stability-under-insert.fsx

# Or against a custom endpoint and store:
dotnet fsi 01-filter-basics.fsx -- --dapr-endpoint http://localhost:3600 --store-name my-store
```

## Scripts

### `01-filter-basics.fsx` — Filter & sort behaviour

| What we expected | What actually happens |
|---|---|
| EQ filter on `userId` works | Confirmed. Filter keys are **unprefixed**; prefixing with `value.` silently matches nothing |
| `GTE`/`LTE` on ISO date strings (`"2026-05-01"`) works | **Rejected** — `ERR_STATE_QUERY: string type not permitted` |
| `GTE`/`LTE` on numeric fields works | Confirmed. This forces numeric storage for all range-filterable fields |

**Implication:** Store `date`, `updated_at`, and any cursor tie-breaker as
numbers (epoch-ms or `yyyymmdd` int), not ISO strings.

### `02-pagination-token.fsx` — Token is a skip-offset

| What we expected | What actually happens |
|---|---|
| Opaque keyset cursor that resumes stably | Token is literally the offset: page 1 → `"4"`, page 2 → `"8"`, page 3 → `"12"` — `skip(N)` wearing a token costume |
| Empty token means "caught up" after last data page | The last data page **still** carries a non-empty token; the empty token only arrives on a **trailing zero-result page**. A client honoring only the empty-token signal always pays one extra empty round-trip |

**Implication:** The token is not a keyset cursor. Use `results.length < limit`
as the real done-signal. Do not pass Dapr's token through to the Flutter
client.

### `03-stability-under-insert.fsx` — Offset instability under concurrent writes

| What we expected | What actually happens |
|---|---|
| Pagination is stable under concurrent insert — no skips or duplicates | Inserting a record that sorts **before** the current page causes a **duplicate**: the row that straddles the offset boundary appears on both pages. An insert **after** the cursor causes a **skip**. Both are disqualifying for a sync loop |

**Implication:** The Dapr Query API token cannot back the sync-loop cursor.
The cursor must be server-owned and self-issued (numeric keyset, or manual
secondary index).

## Design decision

The [OSA-42 ADR](/docs/adr/) pagination section should resolve as:

> **Decided: self-issued keyset cursor with numeric sort keys.**
> Dapr's Query API token is a skip-offset and is unstable under concurrent
> writes. The F# service owns the cursor — it stores a numeric
> `(updated_ms, tiebreaker)` keyset, issues it as a base64-encoded opaque
> token, and resumes from it using Dapr Query API range filters on the
> numeric fields.

The Query API remains fine for bounded, non-sync reads (a report view
paging over a near-static snapshot). The filter and sort primitives
work — only the pagination token is the problem.
