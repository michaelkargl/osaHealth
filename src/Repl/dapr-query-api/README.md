# Dapr Query API Spike — Repl Scripts

F# scripts that exercise the Dapr State Query API (`v1.0-alpha1`) and validate its fitness for the osaHealth recordings sync loop.

**Verdict: NOT fit for sync-loop pagination.** The page token is a skip-offset,
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

```bash
# Run from src/Repl/
dotnet fsi 01-filter-basics.fsx
dotnet fsi 02-pagination-token.fsx
dotnet fsi 03-stability-under-insert.fsx
# Or against a custom endpoint and store:
dotnet fsi 01-filter-basics.fsx -- --dapr-endpoint http://localhost:3600 --store-name my-store
```