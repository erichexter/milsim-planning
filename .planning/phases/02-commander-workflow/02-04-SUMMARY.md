---
phase: 02-commander-workflow
plan: "04"
subsystem: ui
tags: [react, tanstack-query, msw, shadcn, react-router, vitest, testing-library]

requires:
  - phase: 02-commander-workflow
    provides: Event CRUD API (EventService, EventsController), CSV Roster API (RosterService, RosterController)

provides:
  - EventList page with Draft/Published badges and CreateEventDialog
  - DuplicateEventDialog always sending copyInfoSectionIds (Phase 3 forward compat)
  - CsvImportPage with errors-only table and commit-button gated on errorCount
  - EventDetail page with publish button and nav to roster/hierarchy
  - React Router v7 routes for /events, /events/:id, /events/:id/roster/import
  - MSW mock server setup for component tests
  - 35 passing tests across EventList, CsvImportPage, HierarchyBuilder, RosterView

affects: [02-05, 03-info-sections, 04-player-view]

tech-stack:
  added:
    - react-dropzone@15
    - "@tanstack/react-table@8"
    - "@testing-library/jest-dom@6"
    - shadcn/ui accordion, badge, checkbox, command, dialog, popover, table
  patterns:
    - MSW + vitest server.use() overrides per test
    - QueryClient with retry:false in tests
    - MemoryRouter + Routes for component test isolation
    - DuplicateEventDialog always sends copyInfoSectionIds even as [] (Phase 3 compat)

key-files:
  created:
    - web/src/pages/events/EventList.tsx
    - web/src/pages/events/EventDetail.tsx
    - web/src/pages/events/CreateEventDialog.tsx
    - web/src/components/events/DuplicateEventDialog.tsx
    - web/src/pages/roster/CsvImportPage.tsx
    - web/src/pages/roster/HierarchyBuilder.tsx
    - web/src/pages/roster/RosterView.tsx
    - web/src/components/hierarchy/SquadCell.tsx
    - web/src/mocks/server.ts
    - web/src/mocks/handlers.ts
    - web/src/test-setup.ts
    - web/components.json
  modified:
    - web/src/lib/api.ts
    - web/src/main.tsx
    - web/vite.config.ts
    - web/package.json
    - web/src/__tests__/EventList.test.tsx
    - web/src/__tests__/CsvImportPage.test.tsx
    - web/src/__tests__/HierarchyBuilder.test.tsx
    - web/src/__tests__/RosterView.test.tsx

key-decisions:
  - "Tests live in __tests__/ (not tests/ as written in plans) — already established convention from Phase 1"
  - "ProtectedRoute uses Outlet, not children prop — tests use it as layout route in MemoryRouter"
  - "Router is in main.tsx, not App.tsx — App.tsx is unused default placeholder"
  - "shadcn CLI wrote to web/@/components/ (literal @) — files manually copied to web/src/components/ui/"
  - "MSW server.use() per-test overrides — beforeEach sets base mock, tests override with specific data"
  - "react-dropzone onDrop triggered by fireEvent.change on hidden file input in tests"

patterns-established:
  - "MSW pattern: server in mocks/server.ts with setupServer, handlers in mocks/handlers.ts"
  - "TanStack Query wrapper with retry:false in tests prevents retry noise"
  - "DuplicateEventDialog always sends copyInfoSectionIds array (Phase 3 forward compat)"

requirements-completed: [EVNT-01, EVNT-02, EVNT-03, EVNT-04, EVNT-05, ROST-01, ROST-02, ROST-03]

duration: 25min
completed: 2026-03-13
---

# Phase 2 Plan 04: React Event + Roster UI Summary

**React event management UI (EventList, CreateEventDialog, DuplicateEventDialog, EventDetail) and CSV import page (CsvImportPage), with MSW component tests and complete React Router v7 routing**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-03-13T17:30:00Z
- **Completed:** 2026-03-13T17:55:00Z
- **Tasks:** 2 (Tasks 1+2 combined since hierarchy pages done alongside)
- **Files modified:** 28

## Accomplishments
- EventList with Draft/Published status badges; CreateEventDialog posts to `/api/events`
- DuplicateEventDialog always sends `copyInfoSectionIds: []` (Phase 3 forward compat confirmed by test)
- CsvImportPage: errors-only table, summary line, commit button gated on `errorCount === 0`
- HierarchyBuilder and RosterView (from 02-05 plan) created in same commit
- MSW server setup with vitest setupFiles — 35 tests passing

## Task Commits

1. **Task 1+2: All React UI + tests** - `a92637d` (feat)

**Plan metadata:** TBD (docs commit)

## Files Created/Modified
- `web/src/pages/events/EventList.tsx` - Events list with badges
- `web/src/pages/events/CreateEventDialog.tsx` - New event form
- `web/src/components/events/DuplicateEventDialog.tsx` - Duplicate with copyInfoSectionIds
- `web/src/pages/events/EventDetail.tsx` - Event detail with publish + nav links
- `web/src/pages/roster/CsvImportPage.tsx` - CSV import with errors-only table
- `web/src/pages/roster/HierarchyBuilder.tsx` - Players grouped by TeamAffiliation
- `web/src/pages/roster/RosterView.tsx` - Accordion roster with search
- `web/src/components/hierarchy/SquadCell.tsx` - Combobox squad assignment
- `web/src/lib/api.ts` - Extended with typed event/CSV/hierarchy helpers
- `web/src/main.tsx` - Added all Phase 2 routes
- `web/src/mocks/server.ts` + `handlers.ts` - MSW test infrastructure
- `web/src/test-setup.ts` - Vitest MSW lifecycle hooks

## Decisions Made
- Router is in `main.tsx` not `App.tsx` — App.tsx is the default Vite placeholder, never used
- shadcn CLI wrote to `web/@/` literal directory — files manually copied to correct `src/components/ui/` location
- Tests in `__tests__/` (not `tests/`) — existing Phase 1 convention maintained
- `ProtectedRoute` renders `<Outlet />` — tests use it as layout route, not wrapper

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] shadcn init failed due to tsconfig alias in tsconfig.app.json not tsconfig.json**
- **Found during:** Task 1 (installing shadcn components)
- **Issue:** `pnpm dlx shadcn init` read `tsconfig.json` which has no paths — alias is in `tsconfig.app.json`
- **Fix:** Manually created `components.json` with correct config; copied generated files from `@/` literal dir to `src/`
- **Files modified:** web/components.json (created)
- **Verification:** `pnpm build` exits 0

**2. [Rule 1 - Bug] EventList test "duplicate sends copyInfoSectionIds" failed with multiple-element match**
- **Found during:** Task 2 (test execution)
- **Issue:** `screen.getByText('Duplicate Event')` matched both the dialog `<h2>` title and the submit `<button>`
- **Fix:** Changed to `screen.getByRole('dialog')` to wait for dialog open, then `getByRole('button', {name: 'Duplicate Event'})` for submit
- **Files modified:** web/src/__tests__/EventList.test.tsx
- **Verification:** 35/35 tests pass

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both needed for correctness. No scope creep.

## Issues Encountered
- None beyond the two auto-fixed deviations above

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All Phase 2 React UI complete
- 35 frontend tests passing
- Ready for Phase 3 (information sections, email notifications)

---
*Phase: 02-commander-workflow*
*Completed: 2026-03-13*
