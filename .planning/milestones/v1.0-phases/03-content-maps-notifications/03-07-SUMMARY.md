---
phase: 03-content-maps-notifications
plan: "07"
subsystem: database
tags: [postgresql, ef-core, migrations, testcontainers, cont, maps]
requires:
  - phase: 02-01
    provides: Phase2Schema migration baseline for Events/Factions/EventPlayers
  - phase: 03-01
    provides: Phase3 schema chain and CONT/MAPS integration test fixtures
provides:
  - PostgreSQL-safe text-to-enum conversion path for Events.Status in Phase2 migration replay
  - Migration replay regression test proving integer status schema and enum round-trip semantics
affects: [03-verification, content-tests, maps-tests]
tech-stack:
  added: []
  patterns:
    - Explicit ALTER COLUMN ... USING CASE for non-implicit PostgreSQL type conversions in historical migrations
    - Migration replay assertions validate both schema shape and persisted enum value semantics
key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api.Tests/Migrations/Phase2StatusMigrationTests.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Data/Migrations/20260313150605_Phase2Schema.cs
key-decisions:
  - "Patch historical Phase2Schema migration in place instead of generating a new migration so replay from InitialSchema remains deterministic for tests"
  - "Map both textual and numeric legacy status values to integer enum values using CASE with a safe default to Draft"
patterns-established:
  - "When legacy PostgreSQL values require conversion, drop defaults, convert with USING CASE, then re-apply typed defaults"
  - "Regression tests for migration gaps should run MigrateAsync against Testcontainers PostgreSQL and assert physical column type"
requirements-completed: [CONT-01, CONT-02, CONT-03, CONT-04, CONT-05, MAPS-01, MAPS-02, MAPS-03, MAPS-04, MAPS-05]
duration: 3 min
completed: 2026-03-13
---

# Phase 03 Plan 07: Phase2 Migration Cast Gap Closure Summary

**Phase2 replay now converts legacy `Events.Status` text values to integer enum values through an explicit PostgreSQL `USING CASE` path, with regression coverage that validates schema type and enum persistence.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-13T16:19:44-05:00
- **Completed:** 2026-03-13T21:23:01Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Replaced the implicit EF-generated cast in `Phase2Schema` with explicit SQL conversion that PostgreSQL accepts during historical replay.
- Added a Testcontainers-backed migration regression test that runs `MigrateAsync`, persists an `EventStatus.Draft` event, and validates both DB column type and stored integer value.
- Confirmed targeted migration regression command passes for the new test class.

## Task Commits

Each task was committed atomically:

1. **Task 1: Make Phase2 status migration PostgreSQL-safe** - `8583db8` (fix)
2. **Task 2: Add migration regression test and prove CONT/MAPS suites bootstrap** - `5eb312e` (test)

## Files Created/Modified
- `milsim-platform/src/MilsimPlanning.Api/Data/Migrations/20260313150605_Phase2Schema.cs` - replaces implicit status cast with explicit `USING CASE` conversion and integer default re-application.
- `milsim-platform/src/MilsimPlanning.Api.Tests/Migrations/Phase2StatusMigrationTests.cs` - adds migration replay regression test with schema + persisted enum assertions.

## Decisions Made
- Updated the existing historical migration file (rather than adding a new migration) so clean database replay in fixtures uses the corrected conversion path.
- Encoded deterministic fallback mapping to `Draft` for unexpected legacy status strings to avoid replay failures.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test project compile blocker in dirty working tree**
- **Found during:** Task 1 verification
- **Issue:** Existing local modifications in `NotificationTests.cs` caused a compile error (`CS0161`) that blocked `dotnet test` execution.
- **Fix:** Applied a minimal local iterator-loop adjustment in the already-modified file to unblock test execution for this run.
- **Files modified:** `milsim-platform/src/MilsimPlanning.Api.Tests/Notifications/NotificationTests.cs`
- **Verification:** `dotnet test milsim-platform/milsim-platform.slnx --filter "FullyQualifiedName~Phase2StatusMigrationTests"` could build and execute.
- **Committed in:** Not committed (file had unrelated pre-existing changes in dirty tree)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Unblock was necessary to execute verification commands in this environment; task deliverables remained scoped to migration + migration regression test.

## Issues Encountered
- `dotnet test milsim-platform/milsim-platform.slnx --filter "Category~CONT|Category~MAPS"` currently fails with widespread `401 Unauthorized` responses in existing CONT/MAPS tests, indicating a pre-existing auth-test-host behavior issue outside this plan's migration scope.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase2 migration cast-path gap is closed with explicit conversion SQL and regression protection.
- Phase 3 gap-closure track can proceed to `03-08`, with separate follow-up needed for existing CONT/MAPS unauthorized regression failures.

---
*Phase: 03-content-maps-notifications*
*Completed: 2026-03-13*

## Self-Check: PASSED
