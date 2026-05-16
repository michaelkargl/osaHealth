---
id: OH-11
title: History screen in Flutter
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
labels:
  - flutter
  - mobile
dependencies: []
priority: high
ordinal: 11000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Display a chronological list of past recordings, filterable by category. Serves as the user's personal health timeline.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User sees a list of recordings ordered by date descending
- [ ] #2 User can filter recordings by category
- [ ] #3 Each list item shows date, category name, and a summary of values
- [ ] #4 Tapping a recording shows full detail with all metric values
- [ ] #5 Data is loaded from local SQLite, with a pull-to-refresh that syncs from server
<!-- AC:END -->
