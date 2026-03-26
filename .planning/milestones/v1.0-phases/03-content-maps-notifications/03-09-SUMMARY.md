---
phase: 03-content-maps-notifications
plan: "09"
subsystem: testing
tags: [cont, maps, notf, integration-tests, aspnet]
requires:
  - phase: 03-08
    provides: Deterministic notification test auth pattern and hosted-worker harness isolation
provides:
  - Shared deterministic integration auth handler for API test suites
  - CONT and MAPS test harnesses decoupled from runtime JWT environment values
  - Green CONT, MAPS, and NOTF regression coverage after harness rewiring
affects: [03-verification, phase-03-gap-closure, phase-04-readiness]
tech-stack:
  added: []
  patterns: [header-driven integration auth scheme override, shared test-client identity helper]
key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api.Tests/Fixtures/IntegrationTestAuthHandler.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs
key-decisions:
  - "CONT and MAPS integration harnesses now use explicit test auth scheme defaults with deterministic role/user headers instead of bearer JWT generation."
  - "Shared client header application is centralized in IntegrationTestAuthHandler.ApplyTestIdentity to keep harness contracts aligned across suites."
patterns-established:
  - "Integration suites that validate authorization should override DefaultAuthenticateScheme/DefaultChallengeScheme to IntegrationTestAuthHandler.SchemeName."
  - "Commander/player test clients should set X-Test-UserId and X-Test-Role through shared helper methods instead of inline header duplication."
requirements-completed: [CONT-01, CONT-02, CONT-03, CONT-04, CONT-05, MAPS-01, MAPS-02, MAPS-03, MAPS-04, MAPS-05, NOTF-01, NOTF-02, NOTF-03, NOTF-04, NOTF-05]
duration: 5 min
completed: 2026-03-13
---

# Phase 03 Plan 09: CONT/MAPS Auth Harness Gap Closure Summary

**CONT and MAPS integration tests now run end-to-end through deterministic test authentication headers, and combined CONT+MAPS+NOTF regressions pass without JWT environment coupling.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-13T22:12:03Z
- **Completed:** 2026-03-13T22:17:50Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- Added `IntegrationTestAuthHandler` in shared fixtures with deterministic scheme/header constants and emitted `NameIdentifier`/`sub`/`Role` claims.
- Rewired `ContentTestsBase` and `MapResourceTestsBase` to register test auth defaults and set commander/player identity via `X-Test-UserId` + `X-Test-Role` headers.
- Removed bearer-token generation dependency from CONT/MAPS integration clients while keeping role seeding, event membership seeding, and service mocks unchanged.
- Validated regression closure with passing runs for `Category~CONT|Category~MAPS` and `Category~CONT|Category~MAPS|Category~NOTF`, then verified `Category~NOTF` remains green.

## Task Commits

Each task was committed atomically:

1. **Task 1: Extract shared deterministic integration test-auth contract** - `31bfbfa` (feat)
2. **Task 2: Rewire CONT and MAPS base fixtures to shared test-auth scheme** - `53c9593` (fix)
3. **Task 3: Prove full Phase 03 non-regression after auth harness changes** - `cf2cc11` (refactor)

## Files Created/Modified
- `milsim-platform/src/MilsimPlanning.Api.Tests/Fixtures/IntegrationTestAuthHandler.cs` - shared deterministic auth scheme, header contract, claims emission, and client identity helper.
- `milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs` - content harness auth scheme override and deterministic commander/player header setup.
- `milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs` - maps harness auth scheme override and deterministic commander/player header setup.

## Decisions Made
- Standardized integration authorization for CONT/MAPS on test-scheme headers to eliminate environment-sensitive JWT validation from harness bootstrapping.
- Centralized header assignment in shared fixture helper to avoid drift between category test bases.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed disallowed MIME test to assert real validation path**
- **Found during:** Task 2 (Rewire CONT and MAPS base fixtures to shared test-auth scheme)
- **Issue:** `GetUploadUrl_DisallowedMimeType_Returns400` expected `400` but mocked file service always returned upload URL, producing `200` once auth boundary was unblocked.
- **Fix:** Configured test-specific `IFileService.GenerateUploadUrl` mock to throw `ValidationException` for `application/x-msdownload` so endpoint validation behavior is exercised correctly.
- **Files modified:** `milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs`
- **Verification:** `dotnet test milsim-platform/milsim-platform.slnx --filter "Category~CONT|Category~MAPS"`
- **Committed in:** `53c9593`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Bug fix was required to make intended CONT assertions execute correctly after auth harness stabilization; no production code changes.

## Issues Encountered
- A parallel verification attempt caused a transient build lock (`CS2012` on `MilsimPlanning.Api.dll`); rerunning `Category~NOTF` sequentially passed cleanly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Verification Truth #1 and #2 for Phase 03 are now closed by passing CONT and MAPS category runs without 401 pre-assertion failures.
- NOTF regression remains green after introducing shared CONT/MAPS auth harness plumbing, so Phase 03 test coverage is stable for phase transition.

---
*Phase: 03-content-maps-notifications*
*Completed: 2026-03-13*

## Self-Check: PASSED
