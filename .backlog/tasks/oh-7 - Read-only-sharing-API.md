---
id: OH-7
title: Read-only sharing API
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
labels:
  - backend
  - sharing
dependencies: []
priority: medium
ordinal: 7000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Allow a user to grant other family members read-only access to their recordings. This enables the family dashboard and doctor report views without allowing shared write access.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can grant read-only access to another user by email
- [ ] #2 User can revoke a previously granted share
- [ ] #3 Viewer can list recordings for users who have shared with them
- [ ] #4 Share scope can be limited to a specific category or granted for all data
- [ ] #5 Write endpoints reject requests from viewers (non-owners)
<!-- AC:END -->
