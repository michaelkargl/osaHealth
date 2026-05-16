---
id: OH-4
title: User management API
status: To Do
assignee: []
created_date: '2026-05-16 11:46'
updated_date: '2026-05-16 13:46'
labels:
  - backend
  - auth
dependencies: []
priority: high
ordinal: 4000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Implement user registration and login. Each user is isolated — they own their own health data and can optionally share read-only views with others (OH-7). Users are stored as DAPR state documents keyed by userId. Passwords are hashed with bcrypt before storage. Authentication issues a JWT token the Flutter app stores securely and includes in all subsequent requests. The F# service validates the JWT on every protected endpoint without calling an external auth service.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can register with email and password
- [ ] #2 User can authenticate and receive a token
- [ ] #3 User record is persisted via DAPR state management
- [ ] #4 Passwords are never stored in plaintext
- [ ] #5 Authenticated endpoints reject requests without a valid token
<!-- AC:END -->
