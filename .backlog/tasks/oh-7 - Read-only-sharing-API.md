---
id: OH-7
title: Read-only sharing API
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - backend
  - sharing
dependencies: []
priority: medium
ordinal: 7000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Allow users to grant family members read-only access to their health data. Write access always stays with the data owner. A Share document links an ownerId to a viewerId with an optional scope (all data, or a specific categoryId). The sharing API is used by the Flutter report view (OH-12) to let a viewer fetch another user's recordings. Share grants and revocations are persisted via DAPR state. No data is duplicated — viewers query the owner's recordings through the standard recording endpoints, with an authorization check that allows reads for viewers with a valid Share.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can grant read-only access to another user by email
- [ ] #2 User can revoke a previously granted share
- [ ] #3 Viewer can list recordings for users who have shared with them
- [ ] #4 Share scope can be limited to a specific category or granted for all data
- [ ] #5 Write endpoints reject requests from viewers (non-owners)
<!-- AC:END -->
