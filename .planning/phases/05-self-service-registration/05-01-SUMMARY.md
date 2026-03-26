---
phase: 05-self-service-registration
plan: 01
subsystem: auth
tags: [aspnet-core, identity, jwt, postgresql, integration-tests]

# Dependency graph
requires:
  - phase: 04-player-experience-change-requests
    provides: AuthService, AuthController, AppUser, UserProfile, GenerateJwt
provides:
  - POST /api/auth/register endpoint returning JWT + userId + email + displayName + role
  - RegisterAsync service method assigning faction_commander role
  - RegisterResponse record with 5 fields
  - RegisterRequest model with validation annotations
  - 5 integration tests for AC-1 through AC-5
affects:
  - 05-02 (frontend registration form calls this endpoint)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Duplicate-email detection via sentinel string DUPLICATE_EMAIL in InvalidOperationException"
    - "Self-registered users get faction_commander role automatically"
    - "UserProfile.Callsign set to empty string for self-registration (NOT NULL column)"

key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Models/Responses/RegisterResponse.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Requests/RegisterRequest.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs
    - milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs

key-decisions:
  - "Self-registered users assigned faction_commander role (D-04) — they are commanders not players"
  - "EmailConfirmed=true on registration — no activation token flow (D-03)"
  - "Duplicate email detection via sentinel DUPLICATE_EMAIL string to distinguish 409 from other Identity errors"
  - "Callsign set to empty string for self-registration since column is NOT NULL in database"

patterns-established:
  - "RegisterAsync follows InviteUserAsync pattern: CreateAsync -> UserProfile -> UpdateAsync -> AddToRoleAsync -> GenerateJwt"

requirements-completed: [AC-1, AC-2, AC-3, AC-4, AC-5]

# Metrics
duration: 8min
completed: 2026-03-26
---

# Phase 5 Plan 01: Backend Registration Endpoint Summary

**POST /api/auth/register endpoint with JWT response, faction_commander auto-assignment, and 5 integration tests covering AC-1 through AC-5**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-26T17:40:09Z
- **Completed:** 2026-03-26T17:48:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- RegisterAsync service method creates user with EmailConfirmed=true, empty Callsign, faction_commander role, returns JWT
- POST /api/auth/register endpoint returns 200 + JWT on success, 409 on duplicate email, 400 on validation failure
- 5 integration tests pass: valid registration, missing displayName, short password, duplicate email, role verification
- Full test suite passes with no regressions (113 total, up from 108)

## Task Commits

Each task was committed atomically:

1. **Task 1: Backend registration endpoint + service + response model** - `0cdf4c3` (feat)
2. **Task 2: Integration tests for AC-1 through AC-5** - `a7d899f` (feat)

## Files Created/Modified

- `milsim-platform/src/MilsimPlanning.Api/Models/Responses/RegisterResponse.cs` - Record with Token, UserId, Email, DisplayName, Role
- `milsim-platform/src/MilsimPlanning.Api/Models/Requests/RegisterRequest.cs` - Record with Required/EmailAddress/MinLength(6) validation
- `milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs` - Added RegisterAsync method
- `milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs` - Added POST /api/auth/register endpoint
- `milsim-platform/src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` - Added 5 Auth_Register integration tests

## Decisions Made

- Self-registered users get faction_commander role automatically (as specified in D-04): commanders register themselves, not players
- EmailConfirmed=true on registration — no activation token needed (D-03)
- Duplicate email detection uses sentinel string "DUPLICATE_EMAIL" to distinguish 409 scenario from other Identity errors (e.g., password policy failures)
- Callsign is set to empty string since the column is NOT NULL in the database schema

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created RegisterRequest.cs in worktree (was untracked in main repo)**
- **Found during:** Task 2 (integration tests)
- **Issue:** RegisterRequest.cs was untracked in the main repo and absent from the worktree, causing build error CS0246
- **Fix:** Created the file in the worktree with the exact content from the plan's interface specification
- **Files modified:** milsim-platform/src/MilsimPlanning.Api/Models/Requests/RegisterRequest.cs
- **Verification:** Build succeeded, all 5 tests passed
- **Committed in:** a7d899f (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Auto-fix was necessary — the file was already designed (shown in plan interfaces) but missing from worktree. No scope creep.

## Issues Encountered

- RegisterRequest.cs was untracked in the main project repo and absent from the worktree — created from the plan's interface specification. This was a worktree isolation issue, not a logic problem.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Backend registration endpoint fully functional and tested
- Plan 02 (frontend registration form) can proceed immediately — calls POST /api/auth/register

---
*Phase: 05-self-service-registration*
*Completed: 2026-03-26*
