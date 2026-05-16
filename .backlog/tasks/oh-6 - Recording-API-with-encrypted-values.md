---
id: OH-6
title: Recording API with encrypted values
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - backend
  - data
dependencies: []
priority: high
ordinal: 6000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Implement the core health data recording endpoints. A Recording is a timestamped session (e.g. a blood pressure measurement at 09:00). It groups one or more Entries, each identified by a compound key (recordingId + metricId) — one Entry per metric in the chosen Category. Entry values are sensitive and encrypted via the DAPR Cryptography HTTP API before storage. Recording metadata (userId, categoryId, date) stays plaintext so the DAPR query API can filter by user and date range without decrypting. The F# service decrypts Entry values after fetching them, before returning to the client. The syncedAt field on Recording supports the Flutter offline-first sync queue.
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
