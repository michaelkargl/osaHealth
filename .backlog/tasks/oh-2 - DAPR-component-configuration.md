---
id: OH-2
title: DAPR component configuration
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
labels:
  - infrastructure
  - dapr
dependencies: []
priority: high
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Configure all required DAPR building blocks as component YAML files. These abstract the underlying infrastructure from the application code.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 State management component configured with Postgres as backing store
- [ ] #2 Cryptography component configured for per-user value encryption
- [ ] #3 Secrets component configured for managing encryption keys
- [ ] #4 Observability component configured to export traces to Tempo and metrics to Prometheus
- [ ] #5 All components are validated with backlog doctor equivalent (dapr run smoke test)
<!-- AC:END -->
