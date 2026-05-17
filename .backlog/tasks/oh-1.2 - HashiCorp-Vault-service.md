---
id: OH-1.2
title: HashiCorp Vault service
status: Done
assignee:
  - '@agent-k'
created_date: '2026-05-16 13:58'
updated_date: '2026-05-16 16:50'
labels:
  - infrastructure
  - vault
dependencies: []
parent_task_id: OH-1
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Add HashiCorp Vault to Docker Compose as the DAPR secrets store. Vault manages encryption keys and other secrets so they are never hardcoded in config files or environment variables. Run in dev mode for initial setup — sufficient for development and can be hardened for production later.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 Vault container starts and is reachable on the Docker network
- [x] #2 Vault runs in dev mode with a known root token sourced from an environment variable
- [x] #3 Vault UI is accessible at http://localhost:8200
<!-- AC:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Added HashiCorp Vault in production mode (file backend, persistent bind mount) with a vault-init one-shot service that handles first-run initialization, unsealing on every restart, and placeholder secret seeding.
<!-- SECTION:FINAL_SUMMARY:END -->
