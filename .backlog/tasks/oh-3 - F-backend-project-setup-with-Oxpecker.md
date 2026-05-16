---
id: OH-3
title: F# backend project setup with Oxpecker
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
labels:
  - backend
  - fsharp
dependencies: []
priority: high
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Bootstrap the F# backend service using Oxpecker as the web framework. This establishes the project structure, build pipeline, and baseline wiring to DAPR via HTTP.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 F# project builds and runs inside Docker container
- [ ] #2 Oxpecker routes are reachable via HTTP
- [ ] #3 DAPR sidecar HTTP API is accessible from the F# service (localhost:3500)
- [ ] #4 Health check endpoint returns 200
- [ ] #5 OpenTelemetry traces flow through DAPR sidecar to Tempo
<!-- AC:END -->
