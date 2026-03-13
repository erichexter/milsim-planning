---
phase: 02-commander-workflow
plan: 02
subsystem: api
tags: [dotnet, aspnetcore, efcore, events, crud, integration-tests, rbac, idor]

# Dependency graph
requires:
  - phase: 02-commander-workflow
    provides: Event/Faction/Platoon/Squad/EventPlayer entities, DTOs, Phase2Schema migration (02-01)
  - phase: 01-foundation
    provides: ICurrentUser/CurrentUserService, ForbiddenException, ScopeGuard, RequireFactionCommander policy, IEmailService
provides:
  - EventService: create, list, publish, duplicate business logic with faction-owner scope check
  - EventsController: POST /api/events, GET /api/events, GET /api/events/{id}, PUT /api/events/{id}/publish, POST /api/events/{id}/duplicate
  - EventTests: real integration tests for EVNT_Create, EVNT_List, EVNT_Publish, EVNT_Duplicate
affects:
  - 02-04 (HierarchyController needs Faction/Platoon context established here)
  - 02-05 (React event pages call EventsController endpoints)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AssertCommanderAccess: checks Faction.CommanderId == currentUser.UserId (not EventMembership) — faction ownership check for write operations"
    - "PublishEventAsync: no IEmailService call — publish is status-flip only (EVNT-06 contract enforced)"
    - "EventTestsBase: shared setup base class with two commander clients and one player client"

key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Services/EventService.cs
    - milsim-platform/src/MilsimPlanning.Api/Controllers/EventsController.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Events/EventTests.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Program.cs

key-decisions:
  - "AssertCommanderAccess checks Faction.CommanderId (not EventMembership) for write ops — event write requires faction ownership, not just membership"
  - "EventTestsBase shared fixture class: avoids copy-paste DB setup across 4 test classes"
  - "GetById route added to EventsController: needed for CreatedAtAction redirect on POST create/duplicate"

patterns-established:
  - "Faction ownership check pattern: Faction.CommanderId == currentUser.UserId → ForbiddenException (distinct from ScopeGuard.AssertEventAccess which checks EventMembership)"
  - "EVNT-06 contract: PublishEventAsync MUST NOT reference IEmailService — verified by grep in self-check"

requirements-completed:
  - EVNT-01
  - EVNT-02
  - EVNT-03
  - EVNT-04
  - EVNT-05
  - EVNT-06

# Metrics
duration: 11min
completed: 2026-03-13
---

# Phase 2 Plan 02: Event CRUD API Summary

**EventService (create/list/publish/duplicate), EventsController (5 endpoints), and real integration tests replacing all EVNT stubs — `dotnet build` exits 0; EVNT-06 confirmed (no email on publish)**

## Performance

- **Duration:** 11 min (includes completing 02-01 prerequisites)
- **Started:** 2026-03-13T15:04:35Z
- **Completed:** 2026-03-13T15:15:58Z
- **Tasks:** 2 (EventService + EventsController+Tests)
- **Files modified:** 4 (3 new + 1 modified)

## Accomplishments

- EventService with all 4 business operations: CreateEventAsync, ListEventsAsync, PublishEventAsync, DuplicateEventAsync — including faction-owner scope check and EVNT-06 compliance (zero email service references)
- EventsController with all 5 endpoints: POST create (201), GET list (200), GET by ID (200), PUT publish (204/409/403), POST duplicate (201/403)
- EventTests.cs with 12 real integration tests replacing all Assert.True(true) stubs
  - EVNT_Create: 201 + Draft status, 400 on missing name, 403 for player role
  - EVNT_List: scope isolation between commanders (Commander A sees only their events)
  - EVNT_Publish: 204 → Published status, 409 on re-publish, Times.Never email assertion
  - EVNT_Duplicate: platoon/squad structure copied, null dates, Draft status, CopyInfoSectionIds accepted

## Task Commits

Each task was committed atomically:

1. **Task 1: EventService business logic** - `4df01b2` (feat)
2. **Task 2: EventsController + integration tests** - `860d490` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `src/MilsimPlanning.Api/Services/EventService.cs` — all 4 event operations, faction ownership check, no email calls
- `src/MilsimPlanning.Api/Controllers/EventsController.cs` — 5 endpoints with proper HTTP status codes
- `src/MilsimPlanning.Api.Tests/Events/EventTests.cs` — 12 real integration tests across 4 test classes
- `src/MilsimPlanning.Api/Program.cs` — `builder.Services.AddScoped<EventService>()`

## Decisions Made

- **AssertCommanderAccess vs ScopeGuard**: Event write operations check `Faction.CommanderId == currentUser.UserId` (faction ownership), not EventMembership. ScopeGuard checks EventMembership — appropriate for reads. Faction ownership check is required for writes to prevent commanders from modifying other commanders' events.
- **EventTestsBase base class**: Shared `IAsyncLifetime` setup with 3 test clients avoids duplication across 4 test classes (EventCreate, EventList, EventPublish, EventDuplicate).
- **GetById helper route**: Added `/api/events/{id:guid}` for `CreatedAtAction` redirect and for test verification after publish/duplicate.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Moq.Verify 'because' parameter does not exist**
- **Found during:** Task 2 (EventPublish test writing)
- **Issue:** Used `Times.Never, because: "..."` — Moq's Verify doesn't accept a named 'because' parameter (that's FluentAssertions)
- **Fix:** Removed the `because:` argument from `_emailMock.Verify()`
- **Files modified:** `src/MilsimPlanning.Api.Tests/Events/EventTests.cs`
- **Verification:** `dotnet build milsim-platform.slnx` exits 0
- **Committed in:** 860d490 (Task 2 commit)

**2. [Rule 3 - Blocking] 02-01 SUMMARY and React stubs incomplete**
- **Found during:** Pre-execution (02-01-SUMMARY.md missing, web stubs not committed)
- **Issue:** 02-02 depends_on 02-01 but 02-01-SUMMARY.md didn't exist; React test stubs were uncommitted
- **Fix:** Generated missing Phase2Schema migration (already exists in cb27730), committed React stubs (30284ae), created 02-01-SUMMARY.md
- **Files modified:** `.planning/phases/02-commander-workflow/02-01-SUMMARY.md`, `web/src/__tests__/` (4 files)
- **Verification:** All 13 vitest tests pass; dotnet build 0 errors
- **Committed in:** 30284ae

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking/prerequisite)
**Impact on plan:** Both necessary. No scope creep. The 02-01 completion was prerequisite work done inline.

## Issues Encountered

**Docker not running (known issue):** Testcontainers integration tests cannot execute from the current bash shell environment (Docker Desktop named pipe not accessible). This is the same known limitation documented in 01-02-SUMMARY.md and 01-03-SUMMARY.md. All 12 tests compile cleanly and architecture is verified. Run from Windows PowerShell with Docker Desktop active:
```
dotnet test src/MilsimPlanning.Api.Tests --filter "Category=EVNT_Create|Category=EVNT_List|Category=EVNT_Publish|Category=EVNT_Duplicate"
```

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- EventsController and EventService complete — commanders can create, list, publish, and duplicate events
- EventTests stubs replaced with real assertions — CI will fail if EVNT behavior regresses
- DuplicateEventAsync structure ready for Phase 3 CopyInfoSectionIds implementation
- No blockers for Phase 2 parallel plans (02-03 CSV import, 02-04 hierarchy)

## Self-Check: PASSED

All 3 key files found on disk. Commits 4df01b2 and 860d490 verified in git log. No IEmailService references in EventService.cs (EVNT-06 compliant).

---
*Phase: 02-commander-workflow*
*Completed: 2026-03-13*
