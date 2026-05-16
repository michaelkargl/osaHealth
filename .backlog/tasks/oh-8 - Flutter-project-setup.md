---
id: OH-8
title: Flutter project setup
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - flutter
  - mobile
dependencies: []
priority: high
ordinal: 8000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Bootstrap the Flutter app targeting iOS, Android, and Web from a single codebase. The app has three top-level screens: Record (OH-10), History (OH-11), and Report (OH-12). Local data is stored in SQLite via sqflite for offline-first behaviour. An HTTP client (dio or http package) communicates with the F# backend. The sync mechanism tracks each local Recording with a syncedAt field — null means pending sync. A background sync service attempts to push pending recordings when connectivity is available. The Flutter Web target serves as the report/share view accessible from a browser without installing the app.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Flutter project runs on iOS, Android, and Web
- [ ] #2 Local SQLite database is set up via sqflite for offline storage
- [ ] #3 HTTP client is configured to communicate with the F# backend
- [ ] #4 Offline sync queue tracks recordings with syncedAt flag
- [ ] #5 App navigates between the three main screens (Record, History, Report)
<!-- AC:END -->
