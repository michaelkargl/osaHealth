---
id: OH-12
title: Report view in Flutter
status: To Do
assignee: []
created_date: '2026-05-16 11:48'
updated_date: '2026-05-16 13:47'
labels:
  - flutter
  - mobile
  - sharing
dependencies: []
priority: medium
ordinal: 12000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
A printable table view designed for doctor appointments. The user selects a Category and a date range; the app renders a clean table where each row is a Recording date and each column is a Metric from that Category. The table must be legible on both mobile and Flutter Web. On web it can be printed directly; on mobile it is designed to be screenshotted. The screen also lets a user view shared reports — if another family member has granted them read-only access (OH-7) their data appears in a separate section using the same table layout.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can select a category and date range to generate a report
- [ ] #2 Report renders as a table with date rows and metric columns
- [ ] #3 Report is readable on both mobile and web (Flutter Web)
- [ ] #4 Table is clean enough to screenshot and share directly
- [ ] #5 User can access shared reports from family members they have view access to
<!-- AC:END -->
