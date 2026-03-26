---
phase: 03-content-maps-notifications
plan: "05"
subsystem: ui
tags: [react, dnd-kit, react-markdown, maps, notifications, vitest, msw]

# Dependency graph
requires:
  - phase: 03-content-maps-notifications-02
    provides: "Info sections and attachment endpoints used by BriefingPage"
  - phase: 03-content-maps-notifications-03
    provides: "Map resource upload/download endpoints used by MapResourcesPage"
  - phase: 03-content-maps-notifications-04
    provides: "Notification blast endpoints and 202 queue contract"
provides:
  - "Briefing UI with sortable section cards, markdown edit/preview, and attachment upload/download"
  - "Map resources UI with external-link cards and on-demand file downloads"
  - "Commander notification blast form with queue toast and blast history table"
  - "Real component tests replacing four phase test stubs"
affects: [phase-04-player-experience, commander-content-workflow, qa-frontend-regression]

# Tech tracking
tech-stack:
  added: [@dnd-kit/core, @dnd-kit/sortable, @dnd-kit/utilities, react-markdown, remark-gfm]
  patterns:
    - "Drag activation bound to grip button via setActivatorNodeRef to avoid accidental drag on text interaction"
    - "R2 uploads use two-step presign PUT + confirm with Content-Type on browser PUT"
    - "Map and attachment downloads request pre-signed URLs only when user clicks Download"

key-files:
  created:
    - web/src/pages/events/BriefingPage.tsx
    - web/src/pages/events/MapResourcesPage.tsx
    - web/src/pages/events/NotificationBlastPage.tsx
    - web/src/components/content/SectionList.tsx
    - web/src/components/content/SortableSectionCard.tsx
    - web/src/components/content/SectionEditor.tsx
    - web/src/components/content/SectionAttachments.tsx
    - web/src/components/content/UploadZone.tsx
    - web/src/components/content/MapResourceCard.tsx
  modified:
    - web/src/main.tsx
    - web/src/lib/api.ts
    - web/src/tests/BriefingPage.test.tsx
    - web/src/tests/SectionEditor.test.tsx
    - web/src/tests/MapResourcesPage.test.tsx
    - web/src/tests/NotificationBlastPage.test.tsx
    - web/package.json
    - web/pnpm-lock.yaml

key-decisions:
  - "Implemented drag handle activation only on the grip button to preserve editor click usability while keeping reordering available."
  - "Used click-time download-url API calls for map files and section attachments instead of prefetching signed URLs."
  - "Notification queued toast is shown only when POST /notification-blasts returns HTTP 202 Accepted."

patterns-established:
  - "Phase 3 content pages consume typed helpers in web/src/lib/api.ts for endpoint consistency"
  - "Role-sensitive pages hide commander-only actions while preserving read-only player access"

requirements-completed: [CONT-01, CONT-02, CONT-03, CONT-04, CONT-05, MAPS-01, MAPS-02, MAPS-03, MAPS-04, MAPS-05, NOTF-01, NOTF-05]

# Metrics
duration: 7 min
completed: 2026-03-13
---

# Phase 3 Plan 05: Content Maps & Notifications UI Summary

**Commander-facing briefing, map-resource, and notification-blast pages now ship with sortable markdown sections, secure on-demand file downloads, and real component coverage for all four Phase 3 UI test stubs.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-13T19:43:43Z
- **Completed:** 2026-03-13T19:50:54Z
- **Tasks:** 2 completed
- **Files modified:** 17

## Accomplishments

- Implemented `BriefingPage` and content components (`SectionList`, `SortableSectionCard`, `SectionEditor`, `SectionAttachments`, `UploadZone`) with drag-and-drop reorder, markdown preview, and attachment upload/download flow.
- Implemented `MapResourcesPage` and `MapResourceCard` with commander create/delete controls, map file upload flow, and per-click download-url retrieval.
- Implemented `NotificationBlastPage` commander workflow with subject/body validation, `202 Accepted` queue behavior, success toast, and blast history refresh.
- Replaced the four Plan 03-05 frontend test stubs with real MSW-backed component tests.

## Task Commits

Each task was committed atomically:

1. **Task 1: Install packages + BriefingPage and content editor/upload/DnD components** - `ec2c825` (feat)
2. **Task 2: MapResourcesPage + NotificationBlastPage + component tests** - `2adcaa0` (feat)

**Additional fix:** `6168626` (fix) — enforce `202` status gating before showing queued notification toast.

## Files Created/Modified

- `web/src/pages/events/BriefingPage.tsx` - briefing route and info-section query integration
- `web/src/components/content/SectionList.tsx` - sortable section orchestration and add-section inline editor
- `web/src/components/content/SortableSectionCard.tsx` - dnd-kit sortable card with grip-only activator
- `web/src/components/content/SectionEditor.tsx` - title + markdown edit/preview with save validation
- `web/src/components/content/SectionAttachments.tsx` - presigned upload/confirm and download-url flow
- `web/src/pages/events/MapResourcesPage.tsx` - external link/file resource management UI
- `web/src/components/content/MapResourceCard.tsx` - resource rendering, download-url fetch, commander delete
- `web/src/pages/events/NotificationBlastPage.tsx` - send form, 202 queue toast, blast history table
- `web/src/tests/BriefingPage.test.tsx` - real section rendering and commander action coverage
- `web/src/tests/SectionEditor.test.tsx` - tab switching, save disabled/enabled checks, markdown preview assertions
- `web/src/tests/MapResourcesPage.test.tsx` - external link/file card assertions with download button
- `web/src/tests/NotificationBlastPage.test.tsx` - form controls, disabled send state, history rendering

## Decisions Made

- Kept drag listeners off the card root and attached them to the grip icon only to prevent accidental drags while editing titles/body markdown.
- Used backend-generated pre-signed download URLs only on click for both section attachments and map files to preserve private storage behavior.
- Added explicit `fetch` status handling in notification send flow so the UI toast semantics match backend queue contract (`202 Accepted`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Queue toast tied explicitly to HTTP 202 response**
- **Found during:** Task 2 verification
- **Issue:** Notification send path initially showed success toast for any 2xx response rather than explicitly enforcing the documented `202 Accepted` queue contract.
- **Fix:** Reworked send handler to inspect `response.status === 202` before showing queue toast and resetting form.
- **Files modified:** `web/src/pages/events/NotificationBlastPage.tsx`
- **Verification:** `pnpm --prefix web build` and `pnpm --prefix web test --run` both pass.
- **Committed in:** `6168626`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Deviation tightened behavior to match plan contract; no scope creep.

## Issues Encountered

- Verification query "no `expect(true).toBe(true)` anywhere under `web/src/tests`" surfaced pre-existing placeholder tests in files outside this plan's target set. Logged to `.planning/phases/03-content-maps-notifications/deferred-items.md` and left unchanged per scope boundary.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 3 now has complete frontend coverage for briefing, maps, and notification blast flows wired to backend endpoints.
- Ready for next plan/phase activities focused on polish, UX hardening, or cross-phase integration validation.

## Self-Check: PASSED

- Found `.planning/phases/03-content-maps-notifications/03-05-SUMMARY.md` on disk.
- Verified task commits `ec2c825`, `2adcaa0`, and `6168626` exist in git history.
