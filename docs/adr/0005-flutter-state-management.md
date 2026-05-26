# ADR-0005: Flutter state management — Bloc

- **Status:** Proposed
- **Date:** 2026-05-26
- **Deciders:** Saoirse Lindqvist (Flutter), Remy Okafor (Architecture review)
- **Tags:** frontend, flutter, architecture, sync

## Context

The walking skeleton is up. Before the first real feature screen is built, we need to lock the state management approach — it shapes how every screen is structured, tested, and maintained, and undoing it mid-stream is costly.

The problem space is not generic Flutter app development. osaHealth is offline-first with an idempotent sync loop (ADR-0010: `PUT /v1/recordings/{client-uuid}`, 201 on first write, 200 on replay, 409 on conflict). The client generates UUIDs locally, writes to Drift/SQLite immediately, and syncs through a background loop. UI state must coordinate three layers simultaneously:

1. **Local Drift state** — source of truth until confirmed; written immediately on user action.
2. **In-flight sync status** — per-record or aggregate: `pending / syncing / synced / conflict / retry_pending`.
3. **Server-confirmed state** — what the backend has accepted; arrives via cursor-paginated sync pull, not in real time.

The 409 conflict case (same UUID, body differs server-side) is user-visible — someone must resolve it. That means conflict is not just an error variant; it is a first-class application state that the UI has to explicitly render.

The two credible candidates:

- **Riverpod** — code-generated providers, composable, no `BuildContext` dependency, test-friendly, fine-grained reactivity.
- **Bloc** — explicit events/states, predictable unidirectional flow, excellent `bloc_test` tooling, CQRS-shaped.

Both are production-ready. The choice is about which model fits the sync state machine better, not about general framework quality.

## Decision

We will use **Bloc** (with `flutter_bloc`) as the state management layer for osaHealth.

In practice: each feature area that touches the sync loop gets a dedicated Bloc with sealed state classes modelling its sync lifecycle. Drift stream queries wire into Bloc via `emit`-in-`on<>` handlers, not as cross-provider dependencies. The sync engine itself is a `SyncBloc` that owns in-flight state atomically.

Feature screens that are purely local (settings, onboarding steps with no sync requirement) may use lightweight `Cubit` from the same package rather than a full Bloc.

## Options considered

### Option A — Bloc (chosen)

Bloc's explicit event/sealed-state model maps directly onto the sync protocol.

The sync lifecycle per recording is enumerable: `LocalOnly → Syncing → Synced | Conflict | RetryPending`. Expressing this as a sealed `SyncState` class means the compiler enforces exhaustive handling at every UI render site — you cannot accidentally forget the `Conflict` case because the `switch` won't compile without it. This is exactly the guarantee you want when the `Conflict` path requires user intervention.

The `SyncBloc` owns all three state layers atomically. A `SyncRequested` event reads local-only records from Drift, issues PUTs against the API, and emits a single `SyncInProgress(Set<ClientUUID> pending)` → `SyncSuccess` or `SyncConflict(List<ConflictedRecord>)` sequence. There is no intermediate frame where in-flight status has updated but the Drift projection hasn't, or vice versa, because both are part of the same emitted state.

Cursor-based pagination (ADR-0010 decision 4) maps cleanly onto Bloc state: `SyncInProgress(cursor: opaque, fetched: int)` advances with each page. The Bloc guards against re-entrant sync naturally — a `SyncRequested` event received while `SyncInProgress` is the current state is dropped or queued. Enforcing this in Riverpod requires manual guards in provider state.

Testing with `blocTest`: each sync scenario is a list of expected state transitions. Verifying that `SyncRequested` on a three-record offline queue produces `[SyncInProgress({a, b, c}), SyncSuccess]` is two lines. Verifying the 409 path produces `[SyncInProgress({a, b, c}), SyncConflict([b])]` is two more. This precision is important because the sync protocol has sharp edges (clock skew, partial uploads, reconnect mid-cursor) that need to be exercised explicitly, not inferred from snapshot tests.

Cons: Bloc is more ceremony than Riverpod. An event class, a state sealed hierarchy, and `on<>` handlers for a feature that is read-heavy adds boilerplate. The mitigation is `Cubit` for read-only or locally-scoped UI state where no sync coordination is needed.

**Why it won:** The three-layer state coordination is the load-bearing constraint. Bloc makes invalid state combinations unrepresentable via sealed classes, and makes the sync state machine legible to anyone who reads the code. That legibility compounds — the developer who adds the "amendment" recording feature in v2 will read the `SyncBloc` and immediately understand the state machine they're extending, rather than reconstructing it from a graph of interdependent providers.

### Option B — Riverpod (not chosen)

Riverpod's granular reactivity is genuinely well-suited to read-heavy apps where state is naturally composable from smaller atoms.

For a simpler sync model (fire-and-forget POST, server-generated IDs, eventual consistency) Riverpod's pattern — a Drift reactive provider + an `AsyncValue<SyncStatus>` provider + a derived display provider — would be clean and maintainable.

The specific problem is the three-layer coordination under concurrent sync. The three providers that represent local data, in-flight status, and server-confirmed state have an execution ordering dependency: when a PUT returns 200, the local Drift record must be marked synced and the in-flight set must be cleared atomically from the UI's perspective. Riverpod's provider graph does not guarantee emission order under simultaneous invalidations. In practice this means there is a window — usually a single frame, sometimes more — where the UI can observe `(localRecord: dirty, syncStatus: synced)`, which is incoherent and potentially user-visible as a flicker or, worse, a misleading sync indicator.

The 409 conflict case amplifies this: the conflict must surface as a first-class state, not as an `AsyncError` variant. Building a custom `ConflictState` on top of Riverpod's `AsyncValue<T>` is possible, but it bypasses the `AsyncValue.when` ergonomics that make Riverpod UI code concise. You end up with a hybrid that has neither the compositional clarity of pure Riverpod nor the explicit state machine of Bloc.

Cursor-based sync (page over server state until `nextCursor == null`) requires stateful iteration. Encoding that in a Riverpod provider family requires a mutable cursor held in provider state, which interacts awkwardly with provider auto-dispose and ref lifecycle. A Bloc state naturally holds the cursor as part of its `SyncInProgress` payload.

**Why it lost:** Riverpod's provider composition model optimises for read-oriented, granular reactivity. Our bottleneck is write-oriented, transactional sync coordination. Forcing Riverpod's model onto that problem adds accidental complexity rather than removing inherent complexity. The boilerplate Bloc adds is smaller than the discipline tax Riverpod imposes to keep sync state coherent.

## Consequences

**Easier:**
- Sync lifecycle states are expressed as sealed class hierarchies — invalid combinations are a compile error, not a runtime bug.
- `bloc_test` gives precise, deterministic coverage of the sync state machine including the 409 conflict path, the reconnect-mid-cursor path, and the crash-and-retry path.
- New contributors reading the `SyncBloc` get the entire sync algorithm in one file, in execution order.
- The conflict resolution UX has an obvious home: a `ConflictResolutionRequested` event and a `ConflictResolved` state.

**Harder:**
- Read-only or locally-scoped UI components carry Bloc/Cubit boilerplate. Mitigation: `Cubit` for simple cases; the boilerplate is more template than logic.
- Provider auto-invalidation (a Riverpod strength) is not free in Bloc — the Bloc must explicitly subscribe to Drift stream changes and emit accordingly. This is `on<_DriftDataChanged>` handlers, which add a small amount of wiring.
- Team members coming from Riverpod backgrounds will find the explicit event model initially more verbose.

**Follow-up work this ADR creates:**
- `SyncBloc` design as part of the first real feature screen — the state sealed class hierarchy and event list should be sketched in the feature issue before implementation starts.
- Decide `Cubit` vs full `Bloc` threshold per feature — codify in a short CLAUDE.md note once pattern is stable.

## Reversal conditions

We revisit this ADR if any of the following becomes true:

- **Provider-wiring friction becomes the dominant code review topic.** Concrete signal: across five consecutive feature PRs, the most common review comment is "this Bloc wires up the same Drift subscription as the previous one." At that point the Riverpod provider-composition model may save more than it costs. Threshold: five PRs, not one or two.
- **The sync state machine simplifies significantly.** If ADR-0010 is revised to drop client-UUID ownership (e.g. server-generated IDs + correlation header), the three-layer coordination problem weakens and Riverpod's advantages become more relevant. The trigger is an ADR-0010 revision, not performance intuition.
- **`bloc` or `flutter_bloc` packages show sustained maintenance decline.** Signal: no meaningful release in 18 months and open critical issues unaddressed. The ecosystem risk is low today (Bloc is one of the most actively maintained Flutter packages), but it is worth naming.
- **A senior team member with deep Riverpod expertise joins and makes the case from a position of direct familiarity with this codebase.** This ADR does not settle the question for all time — if someone can demonstrate that our specific sync coordination problem is solved cleanly in Riverpod without the atomicity risks described above, that argument deserves a hearing.

## References

- [ADR-0001](0001-record-architecture-decisions.md) — Record architecture decisions.
- [ADR-0010](0010-backend-api-contract-flutter-client.md) (Proposed) — Backend ↔ Flutter client API contract. The PUT-with-client-UUID + 409-conflict shape directly informed the sync state design here.
- OSA-38 — the issue that prompted this ADR.
- `flutter_bloc` package — https://pub.dev/packages/flutter_bloc
- `bloc_test` package — https://pub.dev/packages/bloc_test
- Riverpod — https://riverpod.dev (the runner-up; referenced for completeness)
