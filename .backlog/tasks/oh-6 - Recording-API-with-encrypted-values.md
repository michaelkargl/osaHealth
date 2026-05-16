---
id: OH-6
title: Recording API with encrypted values
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
labels:
  - backend
  - data
dependencies: []
priority: high
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Implement the core health data recording endpoints. A Recording groups one or more Entry values (one per metric) taken at the same moment. Metric values are encrypted at rest via DAPR Cryptography while metadata remains plaintext for queryability.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Recording can be created with a categoryId, date, and optional notes
- [ ] #2 Each Entry value is encrypted via DAPR Cryptography HTTP API before storage
- [ ] #3 Recording metadata (userId, categoryId, date) is stored plaintext and queryable
- [ ] #4 Recordings can be listed filtered by userId and date range
- [ ] #5 Individual recording can be fetched with decrypted values
- [ ] #6 Unsynced recordings are tracked with a syncedAt field for offline-first support
<!-- AC:END -->
