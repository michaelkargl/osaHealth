---
id: OH-5
title: Global Metric and Category seed data
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
labels:
  - backend
  - data
dependencies: []
priority: high
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Define and seed the global set of metrics and categories that all users share. No UI needed — this is managed directly in the data layer as the buffet of available health measurements.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Metric entity is defined with id, name, label, type, and unit fields
- [ ] #2 Category entity is defined with id, name, and an ordered list of metric references
- [ ] #3 A baseline set of categories is seeded (e.g. Blood Pressure with Systolic/Diastolic/Pulse, Body Weight, Blood Glucose)
- [ ] #4 Seed data is loaded on service startup if not already present
- [ ] #5 API endpoint returns the full list of categories with their metrics
<!-- AC:END -->
