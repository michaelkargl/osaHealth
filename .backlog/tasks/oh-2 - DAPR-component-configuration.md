---
id: OH-2
title: DAPR component configuration
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
updated_date: '2026-05-16 13:46'
labels:
  - infrastructure
  - dapr
dependencies: []
priority: high
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Write the DAPR component YAML files that wire DAPR building blocks to the concrete backing services from OH-1. Each component is a separate YAML file loaded by the DAPR sidecar at startup. Components needed: (1) State store — MongoDB component so the F# service reads/writes data via DAPR HTTP without direct MongoDB dependency. (2) Cryptography — local key component used to encrypt Entry values before storage; keys stored in Vault via the secrets component. (3) Secrets — HashiCorp Vault component so the F# service retrieves secrets (encryption keys, API secrets) without hardcoding them. (4) Tracing — Zipkin exporter so DAPR automatically forwards distributed traces. (5) Logging — Fluentd output so DAPR sidecar logs are shipped to Loki.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 State management component configured with Postgres as backing store
- [ ] #2 Cryptography component configured for per-user value encryption
- [ ] #3 Secrets component configured for managing encryption keys
- [ ] #4 Observability component configured to export traces to Tempo and metrics to Prometheus
- [ ] #5 All components are validated with backlog doctor equivalent (dapr run smoke test)
- [ ] #6 State management component configured with MongoDB as backing store
- [ ] #7 Cryptography component configured for per-user value encryption
- [ ] #8 Secrets component configured using HashiCorp Vault
- [ ] #9 Tracing component configured to export to Zipkin
- [ ] #10 Logging configured via Fluentd shipping to Loki, visualised in Grafana
- [ ] #11 All components are validated with a dapr run smoke test
<!-- AC:END -->
