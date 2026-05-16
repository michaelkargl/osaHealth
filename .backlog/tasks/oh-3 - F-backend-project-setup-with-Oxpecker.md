---
id: OH-3
title: F# backend project setup with Oxpecker
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
updated_date: '2026-05-16 13:46'
labels:
  - backend
  - fsharp
dependencies: []
priority: high
ordinal: 3000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Bootstrap the F# backend service using Oxpecker (a refined Giraffe successor built on ASP.NET Core endpoint routing). The service runs as a Docker container alongside a DAPR sidecar. All data access, secrets, and cryptography go through the DAPR HTTP API on localhost:3500 — no direct MongoDB or Vault SDK dependencies. Application logs are written as structured JSON to stdout using Serilog, which Fluentd collects and forwards to Loki. Oxpecker provides the HTTP routing layer; DAPR provides all infrastructure abstractions.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 F# project builds and runs inside Docker container
- [ ] #2 Oxpecker routes are reachable via HTTP
- [ ] #3 DAPR sidecar HTTP API is accessible from the F# service (localhost:3500)
- [ ] #4 Health check endpoint returns 200
- [ ] #5 Traces flow through DAPR sidecar to Zipkin
<!-- AC:END -->
