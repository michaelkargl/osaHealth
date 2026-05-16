---
id: OH-1.4
title: Loki service
status: To Do
assignee: []
created_date: '2026-05-16 13:58'
updated_date: '2026-05-16 14:02'
labels:
  - infrastructure
  - loki
dependencies: []
parent_task_id: OH-1
ordinal: 14000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Add Loki to Docker Compose as the log storage backend. Loki receives logs forwarded by Fluentd and makes them queryable by Grafana using LogQL. Uses filesystem storage for simplicity in a self-hosted setup.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Loki container starts and is reachable on the Docker network
- [ ] #2 Loki HTTP endpoint accepts log pushes from Fluentd
- [ ] #3 Loki data persists across restarts via a named volume
<!-- AC:END -->
