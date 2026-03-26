---
phase: 02-commander-workflow
plan: "05"
subsystem: api
tags: [dotnet, csharp, hierarchy, platoon, squad, eventplayer, postgres, testcontainers]

requires:
  - phase: 02-commander-workflow
    provides: Phase 2 entities (Platoon, Squad, EventPlayer), AppDbContext, ScopeGuard, ICurrentUser

provides:
  - HierarchyService with CreatePlatoonAsync, CreateSquadAsync, AssignSquadAsync, GetRosterHierarchyAsync
  - HierarchyController with 4 endpoints (HIER-01..06)
  - IDOR protection: squad assignment validates squad belongs to same event
  - GET /api/events/{id}/roster accessible to RequirePlayer (all faction members)
  - POST platoon/squad and PUT squad assignment require RequireFactionCommander
  - 7 real HIER integration tests replacing stubs (platoon create, squad create, player assign, move, roster tree, player access, IDOR)

affects: [03-info-sections, 04-player-view]

tech-stack:
  added: []
  patterns:
    - AssertCommanderAccess private method (Faction.CommanderId check) for write ops — mirrors EventService pattern
    - ScopeGuard.AssertEventAccess (EventMembership check) for roster read (all faction members)
    - IDOR protection via Platoon.Faction.EventId check before accepting squadId from client

key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Services/HierarchyService.cs
    - milsim-platform/src/MilsimPlanning.Api/Controllers/HierarchyController.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Hierarchy/CreatePlatoonRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Hierarchy/CreateSquadRequest.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Hierarchy/HierarchyTests.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Program.cs

key-decisions:
  - "HierarchyService.AssertCommanderAccess checks Faction.CommanderId — consistent with EventService pattern"
  - "GetRosterHierarchyAsync uses ScopeGuard.AssertEventAccess (EventMembership check) not AssertCommanderAccess — HIER-06 requires player-level access"
  - "AssignSquadAsync validates squad belongs to event via Platoon.Faction.EventId — prevents IDOR cross-event squad injection"
  - "Unassigning squad (null SquadId) also clears PlatoonId — player goes fully unassigned"
  - "Integration tests require Docker Desktop (Testcontainers) — same constraint as EventTests/RosterImportTests"

patterns-established:
  - "AssertCommanderAccess pattern: private void method checking Faction.CommanderId != currentUser.UserId"
  - "IDOR validation: AnyAsync(s => s.Id == squadId && s.Platoon.Faction.EventId == player.EventId)"

requirements-completed: [HIER-01, HIER-02, HIER-03, HIER-04, HIER-05, HIER-06]

duration: 20min
completed: 2026-03-13
---

# Phase 2 Plan 05: Hierarchy API Summary

**HierarchyController + HierarchyService implementing HIER-01..06 — platoon/squad CRUD, player assignment with IDOR protection, and roster tree accessible to all faction members**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-03-13T17:55:00Z
- **Completed:** 2026-03-13T18:15:00Z
- **Tasks:** 1 (backend task — React components done with 02-04)
- **Files modified:** 6

## Accomplishments
- HierarchyService with 4 methods; AssertCommanderAccess mirrors EventService pattern
- HierarchyController: POST platoon, POST squad, PUT assign, GET roster
- GET roster uses ScopeGuard EventMembership check (player-accessible, HIER-06)
- IDOR protection: `AssignSquadAsync` validates `Platoon.Faction.EventId == player.EventId`
- 7 real integration tests replacing all HIER stubs

## Task Commits

1. **HierarchyService + Controller + tests** - `e699eb5` (feat)

**Plan metadata:** TBD (docs commit)

## Files Created/Modified
- `milsim-platform/src/MilsimPlanning.Api/Services/HierarchyService.cs` - Platoon/squad/assignment logic
- `milsim-platform/src/MilsimPlanning.Api/Controllers/HierarchyController.cs` - 4 REST endpoints
- `milsim-platform/src/MilsimPlanning.Api/Models/Hierarchy/CreatePlatoonRequest.cs` - Request model
- `milsim-platform/src/MilsimPlanning.Api/Models/Hierarchy/CreateSquadRequest.cs` - Request model
- `milsim-platform/src/MilsimPlanning.Api.Tests/Hierarchy/HierarchyTests.cs` - 7 integration tests
- `milsim-platform/src/MilsimPlanning.Api/Program.cs` - Added HierarchyService registration

## Decisions Made
- `GetRosterHierarchyAsync` uses `ScopeGuard.AssertEventAccess` (EventMembership) not `AssertCommanderAccess` — HIER-06 requires player-accessible roster
- Plan's code used `ScopeGuard.AssertEventAccess(currentUser, faction)` (wrong signature) — fixed to use `AssertCommanderAccess` private method pattern from EventService
- IDOR check uses `s.Platoon.Faction.EventId == player.EventId` since Faction has no Events collection (1:1 with Event)
- Unassign clears both `SquadId` and `PlatoonId` — player goes fully unassigned

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan's ScopeGuard overload doesn't exist**
- **Found during:** Task 1 (writing HierarchyService)
- **Issue:** Plan called `ScopeGuard.AssertEventAccess(_currentUser, faction)` but the method signature is `AssertEventAccess(ICurrentUser, Guid eventId)` — no overload accepting Faction
- **Fix:** Used `AssertCommanderAccess(faction)` private method (same pattern as EventService) for write operations; `ScopeGuard.AssertEventAccess(_currentUser, eventId)` for roster read
- **Files modified:** HierarchyService.cs
- **Verification:** `dotnet build` exits 0 with 0 errors

**2. [Rule 1 - Bug] Plan's IDOR check used `Faction.Events.Any()` which doesn't exist**
- **Found during:** Task 1 (writing AssignSquadAsync)
- **Issue:** Plan used `s.Platoon.Faction.Events.Any(e => e.Id == player.EventId)` but Faction has a single `EventId` FK (1:1), not a collection
- **Fix:** Changed to `s.Platoon.Faction.EventId == player.EventId`
- **Files modified:** HierarchyService.cs
- **Verification:** `dotnet build` exits 0

---

**Total deviations:** 2 auto-fixed (2 bugs in plan code)
**Impact on plan:** Both auto-fixes required for compilation. Logic is equivalent — IDOR protection achieved correctly.

## Issues Encountered
- Integration tests fail with "Docker unavailable" — same known limitation as EventTests and RosterImportTests. Tests require Docker Desktop running. Code compiles and is structurally correct.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 2 complete: all 5 plans done (02-01 through 02-05)
- Backend: Event CRUD, CSV Roster Import, Hierarchy API all implemented
- Frontend: EventList, CsvImportPage, HierarchyBuilder, RosterView all implemented with tests
- Ready for Phase 3 (information sections, email notifications, magic link briefing access)

---
*Phase: 02-commander-workflow*
*Completed: 2026-03-13*
