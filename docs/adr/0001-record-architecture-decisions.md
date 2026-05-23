# ADR-0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-05-23
- **Deciders:** Remy Okafor (Software Architecture), Kami (project lead)
- **Tags:** process, documentation

## Context

osaHealth is small enough today that architectural decisions live in chat, tickets, and the heads of whoever happened to be in the room. That scales for a week. It does not scale for a multi-component stack (F# backend on Oxpecker + DAPR, Flutter offline-first client, MongoDB with field-level encryption, Vault, Grafana/Loki/Zipkin) where a single choice — for example, "which state-management approach do we use in Flutter?" — touches several people, several PRs, and several months of follow-on code.

We have already started accumulating decision-shaped open questions in the issue tracker:

- [OSA-38](https://github.com/michaelkargl/osaHealth) — Flutter state management (Riverpod vs Bloc)
- [OSA-42](https://github.com/michaelkargl/osaHealth) — Backend API contract for the Flutter client
- [OSA-46](https://github.com/michaelkargl/osaHealth) — Coding guidelines in `/docs`

Without a shared place for the rationale, those issues will resolve into ticket-comment prose that decays the moment the ticket is closed.

## Decision

We will record significant architectural decisions as Architecture Decision Records (ADRs) under `docs/adr/`, following the lightweight Nygard format.

- One ADR per decision.
- Files are numbered sequentially: `NNNN-short-kebab-title.md` (e.g. `0002-flutter-state-management.md`).
- New ADRs start from `docs/adr/0000-template.md`.
- ADRs are immutable once accepted: corrections happen in a new ADR that supersedes the old one (set `Status: Superseded by ADR-XXXX` on the original).
- ADRs are reviewed via normal pull requests — the PR discussion is part of the record.

A decision is "significant" if at least one of these holds: it is hard to reverse, it constrains how a whole subsystem is built, it crosses a team or component boundary, or removing it would surprise a new contributor.

## Options considered

### Option A — ADRs in `docs/adr/` (chosen)
- Pros: Lives with the code, versioned with the code, reviewable via PR. Zero new tooling.
- Cons: Discoverability depends on contributors knowing the folder exists — mitigated by linking from the top-level README once ADR-0002 lands.

### Option B — Decisions in a wiki (GitHub Wiki / Notion / Confluence)
- Pros: Easier rich formatting, comments.
- Cons: Drifts from the code, not versioned alongside the change that implements the decision, not enforceable via PR review, often inaccessible in air-gapped or offline contexts. Lost.

### Option C — Decisions as long-form issue comments only
- Pros: Zero overhead.
- Cons: This is the status quo and it is already failing — see the OSA-38/42/46 backlog above. Rationale gets lost the moment an issue is closed. Lost.

## Consequences

**Easier:** future contributors can read `docs/adr/` and reconstruct *why* the system looks the way it does, not only *what* it looks like. Reversing or revising a decision becomes a concrete artefact (a new ADR) instead of a tribal-knowledge event.

**Harder:** every non-trivial architectural change now carries a small documentation tax — usually 15–30 minutes to draft the ADR. That is the point; if a decision is not worth 30 minutes of writing, it probably is not significant enough to need an ADR.

**Follow-up work this ADR creates:**

- ADR-0002: Flutter state management (resolves [OSA-38](https://github.com/michaelkargl/osaHealth))
- ADR-0003: Backend ↔ Flutter client API contract (resolves [OSA-42](https://github.com/michaelkargl/osaHealth))
- Link `docs/adr/` from the top-level `README.md` once ADR-0002 is accepted.

## Reversal conditions

We revisit this ADR if any of the following becomes true:

- The team grows past the point where ADRs are written and read consistently (signal: more than two consecutive significant decisions ship without an ADR).
- We adopt a workspace-wide architecture record system at the Multica platform level that subsumes per-repo ADRs.
- The format itself becomes a barrier — e.g. contributors routinely complain that the template is too heavyweight for the decisions being recorded. In that case, prune the template; do not abandon the practice.

## References

- Michael Nygard, *Documenting Architecture Decisions* (2011) — the original ADR format this is based on.
- `docs/adr/0000-template.md` — the template new ADRs start from.
