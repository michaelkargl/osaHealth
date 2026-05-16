---
id: OH-1.5
title: Fluentd service
status: To Do
assignee: []
created_date: '2026-05-16 13:59'
updated_date: '2026-05-16 14:02'
labels:
  - infrastructure
  - fluentd
dependencies: []
parent_task_id: OH-1
ordinal: 17000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Add Fluentd to Docker Compose as the log ingestion layer. All containers use the Fluentd Docker logging driver so their stdout logs are automatically forwarded to Fluentd. Fluentd parses the JSON logs from DAPR and the F# service and forwards them to Loki via the fluent-plugin-grafana-loki plugin.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Fluentd container starts and is reachable on the Docker network
- [ ] #2 All other containers are configured to use the Fluentd Docker logging driver
- [ ] #3 Fluentd successfully forwards logs to Loki
- [ ] #4 Fluentd config handles JSON log parsing from both DAPR sidecar and F# application
<!-- AC:END -->
