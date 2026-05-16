---
id: OH-1
title: Docker Compose infrastructure setup
status: In Progress
assignee:
  - '@michael'
created_date: '2026-05-16 11:46'
updated_date: '2026-05-16 14:21'
labels:
  - infrastructure
dependencies:
  - OH-1.1
  - OH-1.2
  - OH-1.3
  - OH-1.4
  - OH-1.5
  - OH-1.6
  - OH-1.7
priority: high
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Set up the self-hosted Docker Compose stack that forms the foundation for all other services. The stack must run identically on Windows, Linux, and Mac using only Docker Desktop / Docker Engine as a prerequisite. Services to include: MongoDB (state store), HashiCorp Vault (secrets), DAPR sidecar (wired to the F# API service), Zipkin (distributed tracing), Fluentd (log ingestion), Loki (log storage), and Grafana (unified observability UI for traces and logs). All services communicate over a shared Docker network. Configuration is environment-variable driven so secrets are never hardcoded in the compose file.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Docker Compose file runs on Windows, Linux, and Mac without modification
- [ ] #2 MongoDB container is configured and accessible
- [ ] #3 DAPR sidecar is configured alongside the API service
- [ ] #4 All services start cleanly with a single docker compose up
- [ ] #5 Grafana, Zipkin, Fluentd, Loki containers are included and wired together
- [ ] #6 Pre-built dashboards for logs and traces are included and visible in Grafana on first run
- [ ] #7 Grafana is provisioned with Loki and Zipkin data sources on first run — no manual UI configuration required
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Create docker-compose.yml at the repository root with a top-level networks section (osa_network bridge) and a top-level volumes section
<!-- SECTION:PLAN:END -->
