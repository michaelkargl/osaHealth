# Architecture Decision Records

This directory holds the architectural decisions that shape osaHealth.

## What goes here

A decision is worth an ADR if at least one of these is true:

- It is hard to reverse.
- It constrains how a whole subsystem is built.
- It crosses a team or component boundary.
- Removing it would surprise a new contributor.

If a choice fails all four, it probably belongs in a code comment or a PR description, not here.

## How to add one

1. Copy `0000-template.md` to the next free number: `NNNN-short-kebab-title.md`. New ADRs claim the next free **block-of-5** slot (`0005`, `0010`, `0015`, …); the intermediate numbers are reserved so that a future ADR superseding this one can sit next to it (e.g. `0006` supersedes `0005`). See [ADR-0001](0001-record-architecture-decisions.md) for the rationale.
2. Fill out **Context**, **Decision**, **Options considered** (with one paragraph per losing option *and why it lost*), **Consequences**, and **Reversal conditions**.
3. Open a pull request. The PR discussion is part of the record — keep it.
4. On merge, the ADR is **Accepted**. ADRs are immutable after that: corrections happen in a new ADR that supersedes the old one.

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0005](0005-flutter-state-management.md) | Flutter state management — Bloc | Proposed |

See [ADR-0001](0001-record-architecture-decisions.md) for the rationale behind this practice.
