---
id: OH-9
title: Auth flow in Flutter
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - flutter
  - auth
dependencies: []
priority: high
ordinal: 9000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Implement registration and login screens in Flutter that wire to the OH-4 user management API. On successful login the JWT token is stored using flutter_secure_storage (hardware-backed keystore on iOS/Android, encrypted file on Web). An HTTP interceptor attaches the token as a Bearer header on every API request. When the server returns 401 the app clears the stored token and navigates to the login screen.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 User can register with email and password
- [ ] #2 User can log in with valid credentials
- [ ] #3 Auth token is stored securely on device
- [ ] #4 Token is automatically included in API requests
- [ ] #5 User is redirected to login when token is missing or expired
<!-- AC:END -->
