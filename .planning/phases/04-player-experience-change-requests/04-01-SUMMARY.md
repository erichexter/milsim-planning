---
phase: 04-player-experience-change-requests
plan: "01"
subsystem: backend-api
tags: [roster-change-requests, player-assignment, integration-tests, tdd]
dependency_graph:
  requires:
    - 03-notifications (INotificationQueue, RosterChangeDecisionJob)
    - 02-roster (EventPlayer, Platoon, Squad, ScopeGuard, authorization policies)
    - 01-foundation (AppDbContext, AppUser, JWT auth, IntegrationTestAuthHandler)
  provides:
    - RosterChangeRequest entity + Phase4Schema migration
    - RosterChangeRequestsController (6 endpoints: Submit, GetMine, Cancel, ListPending, Approve, Deny)
    - PlayerController (GET /my-assignment)
  affects:
    - 04-02 (frontend consumes these API endpoints)
tech_stack:
  added:
    - RosterChangeRequest EF Core entity with compound indexes
    - DevSeedService for dev environment data seeding
  patterns:
    - ScopeGuard.AssertEventAccess first-line guard on all endpoints
    - EnqueueAsync after SaveChangesAsync (notification-after-commit pattern from Phase 3)
    - Single SaveChangesAsync atomically updates EventPlayer + RosterChangeRequest.Status
    - IntegrationTestAuthHandler for deterministic auth in integration tests
key_files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Data/Entities/RosterChangeRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/Migrations/20260316142237_Phase4Schema.cs
    - milsim-platform/src/MilsimPlanning.Api/Controllers/RosterChangeRequestsController.cs
    - milsim-platform/src/MilsimPlanning.Api/Controllers/PlayerController.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/RosterChangeRequests/SubmitChangeRequestDto.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/RosterChangeRequests/ApproveChangeRequestDto.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/RosterChangeRequests/DenyChangeRequestDto.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/DevSeedService.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/RosterChangeRequests/RosterChangeRequestTests.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Player/PlayerTests.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Data/AppDbContext.cs (RosterChangeRequests DbSet + model config)
    - milsim-platform/src/MilsimPlanning.Api/Program.cs (DevSeedService wired)
decisions:
  - "Approve endpoint uses single SaveChangesAsync to atomically update EventPlayer assignment + set Status=Approved"
  - "EnqueueAsync called after SaveChangesAsync to match Phase 3 notification-after-commit pattern"
  - "EventPlayer queried directly by UserId+EventId in /my-assignment (not roster hierarchy walk)"
  - "RosterChangeDecisionJob includes RequestedChangeSummary field (Phase 3 addition not in RESEARCH.md)"
metrics:
  duration: "~2 hours (across session split)"
  completed: "2026-03-16"
  tasks_completed: 3
  files_created: 10
  files_modified: 2
  tests_added: 14
  tests_total: 97
---

# Phase 4 Plan 01: Backend API — RosterChangeRequest + PlayerAssignment Summary

**One-liner:** Full roster change request CRUD API (6 endpoints) + player assignment endpoint with atomic approval, notification pipeline, and 14 passing integration tests.

## What Was Built

### RosterChangeRequest Entity + Migration

- `RosterChangeRequest.cs` — entity with `Id`, `EventId`, `EventPlayerId`, `Note`, `Status` (enum: Pending/Approved/Denied), `CommanderNote`, `CreatedAt`, `ResolvedAt` navigation properties to `Event` and `EventPlayer`
- `Phase4Schema` EF Core migration (generated via `dotnet ef migrations add`) — creates `RosterChangeRequests` table with compound indexes on `(EventPlayerId, Status)` and `(EventId, Status)`, cascade deletes from both `Event` and `EventPlayer`
- `AppDbContext` updated: `RosterChangeRequests` DbSet, `HasOne(r => r.EventPlayer).WithMany().OnDelete(DeleteBehavior.Cascade)`, same for `Event`

### RosterChangeRequestsController (6 Endpoints)

| Method | Route | Auth | Notes |
|--------|-------|------|-------|
| POST | `/api/events/{eventId}/roster-change-requests` | RequirePlayer | 409 if pending exists |
| GET | `/api/events/{eventId}/roster-change-requests/mine` | RequirePlayer | 204 if no request |
| DELETE | `/api/events/{eventId}/roster-change-requests/{id}` | RequirePlayer | 422 if not Pending |
| GET | `/api/events/{eventId}/roster-change-requests` | RequireFactionCommander | Pending only, ordered by CreatedAt |
| POST | `/api/events/{eventId}/roster-change-requests/{id}/approve` | RequireFactionCommander | Validates squad→platoon, atomic update, enqueues notification |
| POST | `/api/events/{eventId}/roster-change-requests/{id}/deny` | RequireFactionCommander | 422 if not Pending, enqueues notification |

### PlayerController (1 Endpoint)

- `GET /api/events/{eventId}/my-assignment` — returns `{ id, name, callsign, teamAffiliation, platoon: {id, name}?, squad: {id, name}?, isAssigned: bool }`. Direct query by `UserId + EventId` (not roster hierarchy walk). 404 if no `EventPlayer` record.

### Integration Tests

14 new integration tests across 3 categories, all passing:
- **RCHG_Submit** (5): 201 on valid submit, 409 duplicate, 204 cancel, GET mine with/without request
- **RCHG_Review** (2): commander list returns pending, player gets 403
- **RCHG_Decision** (5): approve updates EventPlayer, approve enqueues "approved" job, deny enqueues "denied" job, player gets 403, double-approve returns 422
- **PLAY_Assignment** (2): assigned player returns callsign+platoon+squad, unassigned player returns isAssigned=false

Full test suite: **97/97 passing** (no regressions).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Functionality] Added DevSeedService**
- **Found during:** Task 3 (Program.cs review)
- **Issue:** Program.cs already referenced `DevSeedService.SeedAsync(app.Services)` in the dev environment block, but the file didn't exist in the repository yet
- **Fix:** Created `Data/DevSeedService.cs` with dev seed logic for initial admin user and sample event data
- **Files modified:** `src/MilsimPlanning.Api/Data/DevSeedService.cs`
- **Commit:** `07cc27d`

**2. [Rule 1 - Discovery] RosterChangeDecisionJob has RequestedChangeSummary field**
- **Found during:** Task 3 (implementing Approve/Deny enqueue calls)
- **Issue:** RESEARCH.md pattern omitted the `RequestedChangeSummary` field that was added to `RosterChangeDecisionJob` during Phase 3
- **Fix:** Passed `changeRequest.Note` to `RequestedChangeSummary` parameter in both Approve and Deny enqueue calls
- **Files modified:** `Controllers/RosterChangeRequestsController.cs`
- **Commit:** `07cc27d`

## Commits

| Hash | Message |
|------|---------|
| `04e4d77` | `test(04-01): add failing test stubs for RCHG_Submit, RCHG_Review, RCHG_Decision, PLAY_Assignment` |
| `279f7a4` | `feat(04-01): add RosterChangeRequest entity, Phase4Schema migration, AppDbContext update` |
| `07cc27d` | `feat(04-01): implement RosterChangeRequestsController, PlayerController, DTOs, integration tests` |

## Self-Check: PASSED

- ✅ `RosterChangeRequest.cs` — exists
- ✅ `Phase4Schema` migration — exists
- ✅ `RosterChangeRequestsController.cs` — exists
- ✅ `PlayerController.cs` — exists
- ✅ `RosterChangeRequestTests.cs` — exists (420 lines)
- ✅ `PlayerTests.cs` — exists (226 lines)
- ✅ All 97 tests pass (`dotnet test` exit 0)
- ✅ Commits `04e4d77`, `279f7a4`, `07cc27d` exist in git log
