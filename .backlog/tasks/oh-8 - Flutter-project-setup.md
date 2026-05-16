---
id: OH-8
title: Flutter project setup
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
labels:
  - flutter
  - mobile
dependencies: []
priority: high
ordinal: 8000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Bootstrap the Flutter application targeting iOS, Android, and Web from a single codebase. Establish project structure, API client wiring, and offline-first SQLite storage.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Flutter project runs on iOS, Android, and Web
- [ ] #2 Local SQLite database is set up via sqflite for offline storage
- [ ] #3 HTTP client is configured to communicate with the F# backend
- [ ] #4 Offline sync queue tracks recordings with syncedAt flag
- [ ] #5 App navigates between the three main screens (Record, History, Report)
<!-- AC:END -->
