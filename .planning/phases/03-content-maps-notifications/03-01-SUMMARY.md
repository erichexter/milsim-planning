---
phase: 03-content-maps-notifications
plan: "01"
subsystem: database
tags: [ef-core, migrations, testcontainers, cloudflare-r2, aws-sdk, vitest, xunit]

# Dependency graph
requires:
  - phase: 02-commander-workflow
    provides: "AppDbContext, Event entity, Phase2Schema migration, Testcontainers fixture, MSW test infrastructure"
provides:
  - "InfoSection, InfoSectionAttachment, MapResource, NotificationBlast EF entities"
  - "Phase3Schema EF migration with 4 new tables + composite indexes"
  - "FileService with GenerateUploadUrl (15-min pre-signed PUT) and GenerateDownloadUrl (1-hr pre-signed GET)"
  - "IAmazonS3 singleton wired to Cloudflare R2 (ForcePathStyle=true)"
  - "7 test stub files in RED/trivial-pass state for Phase 3 API plans"
affects: [03-02-content-api, 03-03-maps-api, 03-04-notifications, 03-05-frontend-ui]

# Tech tracking
tech-stack:
  added: [AWSSDK.S3 4.0.19, AWSSDK.Extensions.NETCore.Setup 4.0.3.25]
  patterns:
    - "Pre-signed URL generation via IAmazonS3.GetPreSignedURL with ForcePathStyle=true for R2 compatibility"
    - "R2Key path pattern: events/{eventId}/resources/{resourceId}/files/{uploadId}/{fileName}"
    - "Config hierarchy separator: R2__AccountId env var maps to R2:AccountId in IConfiguration"
    - "Test base classes (ContentTestsBase, MapResourceTestsBase, NotificationTestsBase) follow HierarchyTestsBase pattern"
    - "React test stubs: import server from mocks/server + beforeAll/afterEach/afterAll lifecycle"

key-files:
  created:
    - milsim-platform/src/MilsimPlanning.Api/Data/Entities/InfoSection.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/Entities/InfoSectionAttachment.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/Entities/MapResource.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/Entities/NotificationBlast.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/Migrations/20260313182212_Phase3Schema.cs
    - milsim-platform/src/MilsimPlanning.Api/Services/FileService.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs
    - milsim-platform/src/MilsimPlanning.Api.Tests/Notifications/NotificationTests.cs
    - web/src/tests/BriefingPage.test.tsx
    - web/src/tests/SectionEditor.test.tsx
    - web/src/tests/MapResourcesPage.test.tsx
    - web/src/tests/NotificationBlastPage.test.tsx
  modified:
    - milsim-platform/src/MilsimPlanning.Api/Data/Entities/Event.cs
    - milsim-platform/src/MilsimPlanning.Api/Data/AppDbContext.cs
    - milsim-platform/src/MilsimPlanning.Api/Program.cs
    - milsim-platform/src/MilsimPlanning.Api/MilsimPlanning.Api.csproj
    - milsim-platform/src/MilsimPlanning.Api/appsettings.Development.json
    - milsim-platform/src/MilsimPlanning.Api/Data/Migrations/AppDbContextModelSnapshot.cs

key-decisions:
  - "AWSSDK v4 (4.0.19) used instead of v3 — latest stable release; GetPreSignedURL/ForcePathStyle/HttpVerb all present in v4 API"
  - "React test stubs import server from mocks but do NOT import component files — components created in Plan 03-05"
  - "API test stubs use Assert.True(true) trivial pass pattern matching Phase 2 HierarchyTests precedent"

patterns-established:
  - "ContentTestsBase/MapResourceTestsBase/NotificationTestsBase: per-feature base class with seeded commander+player+event"
  - "IFileService scoped, IAmazonS3 singleton — S3 client is thread-safe and expensive to construct"
  - "AllowedMimeTypes whitelist in FileService is the single source of truth for file type enforcement"

requirements-completed: [CONT-01, CONT-02, CONT-03, CONT-04, CONT-05, MAPS-01, MAPS-02, MAPS-03, MAPS-04, MAPS-05, NOTF-01, NOTF-02, NOTF-03, NOTF-04, NOTF-05]

# Metrics
duration: 7min
completed: 2026-03-13
---

# Phase 3 Plan 01: Wave 0 Foundation Summary

**4 EF entities + Phase3Schema migration + FileService (Cloudflare R2 pre-signed URLs) + 7 xUnit/Vitest test stubs in trivial-pass state**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-13T18:18:29Z
- **Completed:** 2026-03-13T18:25:48Z
- **Tasks:** 3 completed
- **Files modified:** 20

## Accomplishments

- 4 Phase 3 EF entities created (InfoSection, InfoSectionAttachment, MapResource, NotificationBlast) with correct field specs
- Phase3Schema EF migration generated and verified as latest in migration list
- AppDbContext updated with 4 new DbSets and composite indexes on (EventId, Order) for InfoSections and MapResources
- FileService implemented with MIME whitelist, 15-min upload URL, 1-hour download URL, wired to IAmazonS3 singleton (ForcePathStyle=true for R2)
- 7 test stub files created: 3 xUnit API stubs + 4 Vitest React stubs — all in trivial-pass state ready for Plan 03-02 through 03-05 implementation

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 test stubs** - `7769c47` (test)
2. **Task 2: EF Core entities + migration** - `539da03` (feat)
3. **Task 3: FileService + IAmazonS3 DI** - `d9059a8` (feat)

**Plan metadata:** (docs: complete plan — committed after summary)

## Files Created/Modified

### Created
- `milsim-platform/src/MilsimPlanning.Api/Data/Entities/InfoSection.cs` - InfoSection entity
- `milsim-platform/src/MilsimPlanning.Api/Data/Entities/InfoSectionAttachment.cs` - Attachment entity with R2 fields
- `milsim-platform/src/MilsimPlanning.Api/Data/Entities/MapResource.cs` - Map resource entity (external URL or R2 file)
- `milsim-platform/src/MilsimPlanning.Api/Data/Entities/NotificationBlast.cs` - Blast record entity
- `milsim-platform/src/MilsimPlanning.Api/Data/Migrations/20260313182212_Phase3Schema.cs` - EF migration
- `milsim-platform/src/MilsimPlanning.Api/Services/FileService.cs` - R2 pre-signed URL generation
- `milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs` - 10 test stubs (CONT_Sections, CONT_Attachments, CONT_Reorder)
- `milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs` - 7 test stubs (MAPS_Resources, MAPS_Files)
- `milsim-platform/src/MilsimPlanning.Api.Tests/Notifications/NotificationTests.cs` - 5 test stubs (NOTF_Blast, NOTF_Squad)
- `web/src/tests/BriefingPage.test.tsx` - React stubs for BriefingPage
- `web/src/tests/SectionEditor.test.tsx` - React stubs for SectionEditor
- `web/src/tests/MapResourcesPage.test.tsx` - React stubs for MapResourcesPage
- `web/src/tests/NotificationBlastPage.test.tsx` - React stubs for NotificationBlastPage

### Modified
- `milsim-platform/src/MilsimPlanning.Api/Data/Entities/Event.cs` - Added 3 Phase 3 navigation properties
- `milsim-platform/src/MilsimPlanning.Api/Data/AppDbContext.cs` - 4 new DbSets + Phase 3 composite indexes
- `milsim-platform/src/MilsimPlanning.Api/Program.cs` - IAmazonS3 singleton + IFileService scoped registration
- `milsim-platform/src/MilsimPlanning.Api/MilsimPlanning.Api.csproj` - AWSSDK.S3 + AWSSDK.Extensions.NETCore.Setup packages
- `milsim-platform/src/MilsimPlanning.Api/appsettings.Development.json` - Placeholder R2 config values

## Decisions Made

- **AWSSDK v4 over v3**: Latest stable version (4.0.19) installed. All required APIs (`GetPreSignedURL`, `ForcePathStyle`, `HttpVerb`) confirmed present in v4. No API differences observed.
- **React stubs do NOT import component files**: Components (BriefingPage, SectionEditor, etc.) don't exist until Plan 03-05. Stubs establish MSW lifecycle and describe blocks only.
- **IFileService scoped, IAmazonS3 singleton**: S3 client is thread-safe and expensive to construct; singleton is correct. FileService depends on IConfiguration which is a singleton so scoped registration works cleanly.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - `dotnet build` succeeded on first attempt. AWS SDK v4 API is backward-compatible with the plan's v3-style usage patterns.

## User Setup Required

**External services require manual configuration.** Two services need credentials before Phase 3 APIs will function:

### Cloudflare R2 (File Storage)
- `R2__AccountId` — Cloudflare Dashboard → R2 → Overview (Account ID shown on page)
- `R2__AccessKeyId` — Cloudflare Dashboard → R2 → Manage R2 API Tokens → Create API Token
- `R2__SecretAccessKey` — Cloudflare Dashboard → R2 → Manage R2 API Tokens → Create API Token
- `R2__BucketName` — Create a **PRIVATE** bucket (NOT public) in Cloudflare R2 dashboard

### Resend (Email Delivery — Phase 3 wires real email)
- `Resend__ApiKey` — Resend Dashboard → API Keys → Create API Key (Full Access)
- `Resend__FromAddress` — Resend Dashboard → Domains → Verified domain (e.g. `noreply@yourdomain.com`)

Placeholder values are in `appsettings.Development.json`; replace with real values before running locally.

## Next Phase Readiness

- Phase 3 database schema is ready: Phase3Schema migration can be applied via `dotnet ef database update`
- FileService is ready for use by Plans 03-02 (Content API) and 03-03 (Maps API)
- Test stubs are ready: Plans 03-02 through 03-04 can fill in the `Assert.True(true)` bodies with real assertions
- Wave 2 plans (03-02, 03-03, 03-04) have no file conflicts with this Wave 1 plan

---
*Phase: 03-content-maps-notifications*
*Completed: 2026-03-13*

## Self-Check: PASSED

All 13 key files found on disk. All 3 task commits verified in git history (7769c47, 539da03, d9059a8).
