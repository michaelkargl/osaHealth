---
id: OH-11
title: History screen in Flutter
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - flutter
  - mobile
dependencies: []
priority: high
ordinal: 11000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The personal health timeline screen. Recordings are loaded from local SQLite first (fast, works offline) and the user can pull-to-refresh to sync from the server. The list is ordered by date descending and can be filtered by Category. Each list item shows the date, category name, and a one-line value summary (e.g. 120/80 mmHg, 72 bpm). Tapping opens a detail view showing all metric values for that recording with their labels and units.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User sees a list of recordings ordered by date descending
- [ ] #2 User can filter recordings by category
- [ ] #3 Each list item shows date, category name, and a summary of values
- [ ] #4 Tapping a recording shows full detail with all metric values
- [ ] #5 Data is loaded from local SQLite, with a pull-to-refresh that syncs from server
<!-- AC:END -->
