---
id: OH-10
title: Record screen in Flutter
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
labels:
  - flutter
  - mobile
dependencies: []
priority: high
ordinal: 10000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The primary data entry screen. User picks a category from the global buffet, fills in values for each metric, and saves. Works offline with background sync when connectivity returns.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can browse and select a category
- [ ] #2 Form dynamically renders input fields based on the selected category's metrics
- [ ] #3 Recording is saved locally to SQLite immediately
- [ ] #4 Recording is synced to the backend when online (syncedAt updated on success)
- [ ] #5 User sees a clear indicator when a recording is pending sync
<!-- AC:END -->
