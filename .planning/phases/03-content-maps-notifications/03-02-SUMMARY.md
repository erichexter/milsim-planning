---
phase: 03-content-maps-notifications
plan: "02"
subsystem: api
tags: [aspnet-core, ef-core, xunit, testcontainers, content-service, info-sections]

# Dependency graph
requires:
  - phase: 03-content-maps-notifications
    provides: "Phase3Schema entities (InfoSection, InfoSectionAttachment) and FileService pre-signed URL generation"
provides:
  - "InfoSectionsController endpoints for section CRUD, reorder, upload-url, confirm, and download-url"
  - "ContentService implementation for CONT-01..05 plus attachment persistence"
  - "Event duplication now clones selected info sections and their attachments"
  - "CONT integration test stubs replaced with real endpoint assertions"
affects: [03-03-maps-api, 03-04-notifications, 03-05-frontend-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ScopeGuard.AssertEventAccess as the first line in each controller action"
    - "Write endpoints require RequireFactionCommander while read endpoints use RequirePlayer"
    - "Attachment download URLs are generated on demand from R2Key and never stored"

key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Controllers/InfoSectionsController.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Content/CreateInfoSectionRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Content/UpdateInfoSectionRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Content/ReorderSectionsRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Content/UploadUrlRequest.cs
    - milsim-platform/src/MilsimPlanning.Api/Models/Content/ConfirmAttachmentRequest.cs
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Services/ContentService.cs
    - milsim-platform/src/MilsimPlanning.Api/Services/EventService.cs
    - milsim-platform/src/MilsimPlanning.Api/Program.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs

key-decisions:
  - "Kept upload URL generation at IFileService boundary and mocked IFileService in integration tests (not IAmazonS3)"
  - "Implemented attachment download as on-demand URL generation using stored R2Key to avoid stale persisted URLs"
  - "Used full order reassignment loop (0..N) in ContentService.ReorderInfoSectionsAsync"

patterns-established:
  - "Content API surface follows event-scoped route prefix: /api/events/{eventId}/info-sections"
  - "Two-step attachment flow: GET upload-url then POST confirm"

requirements-completed: [CONT-01, CONT-02, CONT-03, CONT-04, CONT-05]

# Metrics
duration: 12 min
completed: 2026-03-13
---

# Phase 3 Plan 02: Content API Summary

**Info section CRUD, reorder, and attachment upload/download endpoints are implemented with event-scoped authorization and backed by real CONT integration tests.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-13T19:09:00Z
- **Completed:** 2026-03-13T19:20:51Z
- **Tasks:** 2 completed
- **Files modified:** 10

## Accomplishments

- Implemented `ContentService` methods for section create/update/delete, full reorder persistence, upload URL validation, and attachment confirmation storage.
- Implemented `InfoSectionsController` with all planned routes, policy boundaries (`RequireFactionCommander` for writes, `RequirePlayer` for reads), and on-demand attachment download URL responses.
- Replaced the `DuplicateEventAsync` info section placeholder with live cloning logic for selected sections and their attachments.
- Replaced CONT test stubs with real integration assertions for sections, attachments, and reorder behavior.

## Task Commits

Each task was committed atomically:

1. **Task 1: ContentService — InfoSection CRUD + reorder + attachment upload flow** - `accd316` (feat)
2. **Task 2: InfoSectionsController + real integration tests replacing CONT stubs** - `31c3685` (feat)

**Plan metadata:** pending (docs commit created after summary/state updates)

## Files Created/Modified

- `milsim-platform/src/MilsimPlanning.Api/Services/ContentService.cs` - Content domain service with CONT-01..05 behaviors
- `milsim-platform/src/MilsimPlanning.Api/Controllers/InfoSectionsController.cs` - REST API endpoints for info sections and attachments
- `milsim-platform/src/MilsimPlanning.Api/Services/EventService.cs` - Event duplication now copies selected info sections
- `milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs` - 10 real CONT integration tests
- `milsim-platform/src/MilsimPlanning.Api/Program.cs` - `IContentService` DI registration

## Decisions Made

- Mocked `IFileService` in tests to isolate controller/service behavior from Cloudflare R2 infrastructure concerns.
- Kept `download-url` generation in controller/service runtime path, storing only `R2Key` in DB for secure, time-limited access.
- Preserved event-scoped access checks with `ScopeGuard.AssertEventAccess` on every endpoint.

## Deviations from Plan

None - plan executed exactly as written.

## Authentication Gates

None.

## Issues Encountered

- CONT integration tests require Docker for Testcontainers PostgreSQL. `dotnet test` could not complete in this environment because Docker daemon was unavailable (`npipe://./pipe/docker_engine` timeout).

## User Setup Required

None - no additional external service setup required for this plan.

## Next Phase Readiness

- Content API contracts required by Plan 03-05 UI are in place.
- Ready for `03-03-PLAN.md` (map resources API) with the same FileService and policy patterns.

---
*Phase: 03-content-maps-notifications*
*Completed: 2026-03-13*

## Self-Check: PASSED

Summary file exists and task commits `accd316` and `31c3685` are present in git history.
