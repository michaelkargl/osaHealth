---
id: OH-1
title: Docker Compose infrastructure setup
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
labels:
  - infrastructure
dependencies: []
priority: high
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Set up the self-hosted Docker Compose stack that runs all infrastructure components. This is the foundation everything else depends on.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Docker Compose file runs on Windows, Linux, and Mac without modification
- [ ] #2 Postgres container is configured and accessible
- [ ] #3 DAPR sidecar is configured alongside the API service
- [ ] #4 Grafana, Tempo, Prometheus, and Loki containers are included and wired together
- [ ] #5 All services start cleanly with a single docker compose up
<!-- AC:END -->
