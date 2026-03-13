---
phase: 03-content-maps-notifications
plan: "03"
subsystem: api
tags: [maps, cloudflare-r2, aspnet-core, integration-tests, authorization]

# Dependency graph
requires:
  - phase: 03-content-maps-notifications-01
    provides: "MapResource entity, FileService pre-signed URL generation, MAPS test scaffold"
  - phase: 03-content-maps-notifications-02
    provides: "InfoSections authorization and controller/service patterns"
provides:
  - "MapResourceService implementing external link CRUD and file upload/download orchestration"
  - "MapResourcesController endpoints for MAPS-01..05 with player/commander policy boundaries"
  - "MAPS integration tests replacing stubs with endpoint assertions"
affects: [03-04-notifications, 03-05-frontend-ui, player-map-download-flow]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Map files remain private in storage; download URL generated only by on-demand endpoint"
    - "Map resource list projection excludes private storage keys and signed URLs"
    - "Commander write actions enforce ScopeGuard + commander ownership check in service layer"

key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Models/Maps/CreateExternalMapLinkRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Maps/CreateMapFileRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Maps/ConfirmMapFileRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Services/MapResourceService.cs
    - milsim-platform/src/MilsimPlanning.Api/Controllers/MapResourcesController.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs

key-decisions:
  - "GET /map-resources list omits both R2Key and pre-signed URLs; clients use dedicated download-url endpoint"
  - "Map file upload endpoint uses caller-provided map resource ID so confirm/download operate on stable resource identity"

patterns-established:
  - "MapResourcesController mirrors InfoSectionsController catch/response pattern for 400/404/403 behavior"
  - "MapResourceService stores only R2Key and metadata; pre-signed GET URL is generated on request"

requirements-completed: [MAPS-01, MAPS-02, MAPS-03, MAPS-04, MAPS-05]

# Metrics
duration: 26 min
completed: 2026-03-13
---

# Phase 3 Plan 03: Maps API Summary

**Map resources API now supports external map links plus private file upload/download flow with authenticated, on-demand pre-signed access URLs.**

## Performance

- **Duration:** 26 min
- **Started:** 2026-03-13T18:30:00Z
- **Completed:** 2026-03-13T18:56:00Z
- **Tasks:** 2 completed
- **Files modified:** 6

## Accomplishments

- Implemented `IMapResourceService`/`MapResourceService` covering external link creation, upload URL generation, upload confirmation, ordered listing, deletion, and download URL generation.
- Added `MapResourcesController` with 6 endpoints and policy boundaries (`RequireFactionCommander` for writes, `RequirePlayer` for reads/download).
- Replaced all 7 MAPS xUnit stubs with real API assertions including list privacy checks and MIME-rejection behavior.

## Task Commits

Each task was committed atomically:

1. **Task 1: MapResourceService — external links + file upload/download + CRUD** - `5a977c8` (feat)
2. **Task 2: MapResourcesController + real integration tests replacing MAPS stubs** - `9db63c4` (feat)

**Plan metadata:** pending docs commit

## Files Created/Modified

- `milsim-platform/src/MilsimPlanning.Api/Models/Maps/CreateExternalMapLinkRequest.cs` - request contract for external map links
- `milsim-platform/src/MilsimPlanning.Api/Models/Maps/CreateMapFileRequest.cs` - request contract for map file upload URL requests
- `milsim-platform/src/MilsimPlanning.Api/Models/Maps/ConfirmMapFileRequest.cs` - request contract for confirming uploaded map files
- `milsim-platform/src/MilsimPlanning.Api/Services/MapResourceService.cs` - map resource domain/service logic and access enforcement
- `milsim-platform/src/MilsimPlanning.Api/Controllers/MapResourcesController.cs` - MAPS REST endpoints and HTTP status handling
- `milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs` - 7 integration tests with real assertions

## Decisions Made

- Used list projection with `IsFile` flag so API can indicate file-vs-link resources without exposing `R2Key`.
- Enforced upload confirmation key match (`ConfirmMapFileRequest.R2Key` must match pending generated key) to prevent mismatched file confirmations.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Integration test execution requires Docker/Testcontainers; environment did not have Docker available (`npipe://./pipe/docker_engine` unreachable), so runtime test pass/fail could not be validated in this session.

## User Setup Required

None - no new external service configuration added by this plan.

## Next Phase Readiness

- Map endpoints and contracts are available for UI integration in Plan 03-05.
- Notification plan (03-04) can reference the same event access and policy patterns.
- Re-run MAPS integration suite in Docker-enabled environment to fully validate runtime behavior.

---
*Phase: 03-content-maps-notifications*
*Completed: 2026-03-13*

## Self-Check: PASSED

- Found `.planning/phases/03-content-maps-notifications/03-03-SUMMARY.md` on disk.
- Verified task commits `5a977c8` and `9db63c4` exist in git history.
