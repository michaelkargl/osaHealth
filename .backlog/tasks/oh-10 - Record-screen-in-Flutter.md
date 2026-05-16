---
id: OH-10
title: Record screen in Flutter
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - flutter
  - mobile
dependencies: []
priority: high
ordinal: 10000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The primary data entry screen. The user selects a Category from the global catalog fetched from OH-5. The form dynamically renders a numeric input field for each Metric in that Category (label, unit, and type come from the Metric schema — no hardcoded fields). On save, the Recording and its Entries are written to local SQLite immediately so the app feels instant. A background sync service (OH-8) then pushes the recording to the backend when online and marks syncedAt. A small visual indicator (e.g. cloud icon) shows pending-sync state per recording.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can browse and select a category
- [ ] #2 Form dynamically renders input fields based on the selected category's metrics
- [ ] #3 Recording is saved locally to SQLite immediately
- [ ] #4 Recording is synced to the backend when online (syncedAt updated on success)
- [ ] #5 User sees a clear indicator when a recording is pending sync
<!-- AC:END -->
