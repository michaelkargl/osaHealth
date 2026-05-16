---
id: OH-4
title: User management API
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
labels:
  - backend
  - auth
dependencies: []
priority: high
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Implement user registration and authentication endpoints. Users are isolated — each user owns their own health data.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can register with email and password
- [ ] #2 User can authenticate and receive a token
- [ ] #3 User record is persisted via DAPR state management
- [ ] #4 Passwords are never stored in plaintext
- [ ] #5 Authenticated endpoints reject requests without a valid token
<!-- AC:END -->
