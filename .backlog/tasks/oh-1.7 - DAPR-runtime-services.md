---
id: OH-1.7
title: DAPR runtime services
status: To Do
assignee: []
created_date: '2026-05-16 13:59'
updated_date: '2026-05-16 14:03'
labels:
  - infrastructure
  - dapr
dependencies: []
parent_task_id: OH-1
ordinal: 19000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Add the DAPR control plane services required for the DAPR runtime to function in Docker Compose self-hosted mode. This includes the DAPR placement service (required even without actors) and the daprd sidecar wired alongside the F# API container sharing its network namespace. The sidecar is configured with --log-as-json so its logs flow through Fluentd to Loki.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 DAPR placement service starts and is reachable on the Docker network
- [ ] #2 DAPR sidecar starts alongside the API service container
- [ ] #3 DAPR sidecar loads component YAMLs from the components directory (OH-2)
- [ ] #4 DAPR HTTP API is reachable from the F# service at localhost:3500
- [ ] #5 DAPR sidecar runs with --log-as-json flag
<!-- AC:END -->
