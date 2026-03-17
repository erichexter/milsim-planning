---
created: 2026-03-16T15:37:57.038Z
resolved: 2026-03-17
resolution: HierarchyBuilder verified functional post-EventMembership fix. All HIER-01..06 shipped in v1.0. Leader role assignment (point 2) deferred to v1.1 as out of scope for MVP.
title: Faction commander hierarchy management UI
area: ui
files:
  - web/src/pages/roster/HierarchyBuilder.tsx
  - web/src/main.tsx
  - milsim-platform/src/MilsimPlanning.Api/Controllers/HierarchyController.cs
---

## Problem

The faction commander currently has no functional UI for managing the hierarchy after a roster import. They need to:

1. **Create platoons** — name and add platoons to the faction
2. **Create squads** — add squads under each platoon
3. **Assign players to squads** — drag/drop or select players from the unassigned pool into squads
4. **Assign platoon leaders and squad leaders** — promote players to leadership roles (role elevation within the event)

The backend API already supports all of these operations (HierarchyController has CreatePlatoon, CreateSquad, AssignSquad endpoints). The HierarchyBuilder page exists but the hierarchy page currently returns 403 for newly created events (now fixed via EventMembership backfill). Need to verify the HierarchyBuilder UI is fully functional and covers all commander workflows.

## Solution

1. Audit `HierarchyBuilder.tsx` — verify platoon/squad creation and player assignment flows work end-to-end after the EventMembership fix
2. Add platoon leader / squad leader assignment — either via a role selector on the player card, or via a context menu. Backend: `PUT /api/event-players/:id/role` endpoint needed if not already present
3. Ensure the hierarchy builder is accessible and usable from the event detail page button ("Manage Hierarchy")
4. Consider whether leader assignment is in scope for v1.0 or a v1.1 enhancement
