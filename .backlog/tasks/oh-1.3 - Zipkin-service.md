---
id: OH-1.3
title: Zipkin service
status: To Do
assignee: []
created_date: '2026-05-16 13:58'
updated_date: '2026-05-16 14:02'
labels:
  - infrastructure
  - zipkin
dependencies: []
parent_task_id: OH-1
ordinal: 16000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Add Zipkin to Docker Compose as the distributed tracing backend. DAPR automatically forwards traces from all service invocations to Zipkin via its tracing component (OH-2). Zipkin's own UI provides trace exploration out of the box.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Zipkin container starts and is reachable on the Docker network
- [ ] #2 Zipkin UI is accessible at http://localhost:9411
<!-- AC:END -->
