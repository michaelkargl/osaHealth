---
id: OH-5
title: Global Metric and Category seed data
status: To Do
assignee: []
created_date: '2026-05-16 11:47'
updated_date: '2026-05-16 13:47'
labels:
  - backend
  - data
dependencies: []
priority: high
ordinal: 5000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
Define and seed the global catalog of Metrics and Categories that all users share as a buffet — no per-user metric management UI is needed. A Metric is the atomic unit (e.g. Pulse: number, bpm). A Category groups an ordered list of Metrics (e.g. Blood Pressure = [Systolic, Diastolic, Pulse]). This design allows Pulse to appear in multiple categories while remaining a single source of truth. Seed data is stored via DAPR state on service startup. Baseline categories to seed: Blood Pressure (Systolic mmHg, Diastolic mmHg, Pulse bpm), Body Weight (Weight kg), Blood Glucose (Glucose mmol/L), Body Temperature (Temperature °C).
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [ ] #1 Metric entity is defined with id, name, label, type, and unit fields
- [ ] #2 Category entity is defined with id, name, and an ordered list of metric references
- [ ] #3 A baseline set of categories is seeded (e.g. Blood Pressure with Systolic/Diastolic/Pulse, Body Weight, Blood Glucose)
- [ ] #4 Seed data is loaded on service startup if not already present
- [ ] #5 API endpoint returns the full list of categories with their metrics
<!-- AC:END -->
