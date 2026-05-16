---
id: OH-1.6
title: Grafana service with provisioning
status: To Do
assignee: []
created_date: '2026-05-16 13:59'
updated_date: '2026-05-16 14:02'
labels:
  - infrastructure
  - grafana
dependencies: []
parent_task_id: OH-1
ordinal: 18000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Add Grafana to Docker Compose as the unified observability UI. Grafana must be fully provisioned on first run via mounted YAML files — no manual UI configuration should be required. Data sources (Loki, Zipkin) and pre-built dashboards are committed to the repository and loaded automatically at startup.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Grafana container starts and UI is accessible at http://localhost:3000
- [ ] #2 Loki data source is auto-provisioned and connected
- [ ] #3 Zipkin data source is auto-provisioned and connected
- [ ] #4 A pre-built Loki log exploration dashboard is visible without any manual setup
<!-- AC:END -->
