---
phase: 04-player-experience-change-requests
plan: "02"
subsystem: frontend-ui
tags: [player-experience, mobile-layout, change-requests, routing, react, tailwind]
dependency_graph:
  requires:
    - 04-01 (RosterChangeRequestsController, PlayerController — backend APIs consumed)
    - 03-briefing-maps (BriefingPage, MapResourcesPage — reused as tabs)
    - 02-roster (RosterView — reused as Roster tab)
    - 01-foundation (ProtectedRoute, useAuth, api.ts)
  provides:
    - PlayerEventPage (mobile tab host for all event content)
    - ChangeRequestsPage (commander review UI)
    - MyAssignmentTab with change request form/status
    - Full route tree in main.tsx
    - Role-gated ProtectedRoute with requiredRole prop
  affects: []
tech_stack:
  added:
    - web/src/components/player/ directory (new)
    - MemoryRouter + vi.mock pattern for component testing
  patterns:
    - h-dvh layout for iOS Safari address bar fix (PLAY-05)
    - min-h-[56px] on all tab bar buttons (WCAG 44px touch target requirement)
    - useQuery(['events', eventId, 'my-assignment']) for direct API call pattern
    - 404 from /my-assignment treated as unassigned state (Pitfall 4)
    - ProtectedRoute requiredRole prop for commander-only route gating
key_files:
  created:
    - web/src/pages/events/PlayerEventPage.tsx
    - web/src/pages/events/ChangeRequestsPage.tsx
    - web/src/components/player/MyAssignmentTab.tsx
    - web/src/components/player/ChangeRequestForm.tsx
    - web/src/components/player/PendingRequestCard.tsx
    - web/src/tests/PlayerEventView.test.tsx
    - web/src/tests/ChangeRequestForm.test.tsx
  modified:
    - web/src/main.tsx (complete route tree, Phase 4 routes)
    - web/src/pages/DashboardPage.tsx (event listing, role-aware navigation)
    - web/src/components/ProtectedRoute.tsx (requiredRole prop)
    - web/src/pages/events/EventDetail.tsx (Change Requests button for commanders)
decisions:
  - "Wave 0 stubs replaced with real test implementations using vi.mock for child components"
  - "PlayerEventPage uses h-dvh (not h-screen) for iOS Safari dynamic viewport fix"
  - "RosterView/BriefingPage/MapResourcesPage reused directly as tab content (use useParams internally)"
  - "ProtectedRoute requiredRole does string equality check against user.role from JWT claim"
  - "ChangeRequestsPage uses native <select> for platoon/squad (no shadcn Select needed for simplicity)"
metrics:
  duration: "~1.5 hours"
  completed: "2026-03-16"
  tasks_completed: 3
  files_created: 7
  files_modified: 4
  tests_added: 10
  tests_total: 56
---

# Phase 4 Plan 02: Frontend UI — Player Experience & Change Requests Summary

**One-liner:** Mobile-first PlayerEventPage with bottom tab bar, MyAssignmentTab showing [CALLSIGN] in orange monospace, ChangeRequestsPage with Approve/Deny dialogs, and full route tree with faction_commander role gating.

## What Was Built

### Route Tree (main.tsx)

Complete React Router v7 route tree with all Phase 1-4 pages:

| Route | Component | Auth |
|-------|-----------|------|
| `/` | → /dashboard | Authenticated |
| `/dashboard` | DashboardPage | Authenticated |
| `/events` | EventList | Authenticated |
| `/events/:id` | EventDetail | Authenticated |
| `/events/:id/player` | PlayerEventPage | Authenticated |
| `/events/:id/briefing` | BriefingPage | Authenticated |
| `/events/:id/maps` | MapResourcesPage | Authenticated |
| `/events/:id/roster` | RosterView | Authenticated |
| `/events/:id/notifications` | NotificationBlastPage | Authenticated |
| `/events/:id/change-requests` | ChangeRequestsPage | **faction_commander only** |
| `/events/:id/hierarchy` | HierarchyBuilder | **faction_commander only** |
| `/events/:id/roster/import` | CsvImportPage | **faction_commander only** |

### ProtectedRoute Extension

Added `requiredRole?: string` prop. Non-matching role → `<Navigate to="/dashboard" replace />`. Pre-existing behavior (unauthenticated → login) unchanged.

### DashboardPage

Fetches `GET /api/events`, renders event cards with name/status/location/dates. Role-aware navigation: commanders → `/events/:id`, players → `/events/:id/player`.

### PlayerEventPage

- Root: `className="flex flex-col h-dvh"` (iOS Safari fix — PLAY-05)
- Desktop: `hidden md:flex` top nav with border-b-2 active indicator
- Content: `flex-1 overflow-y-auto pb-16 md:pb-0 p-4`
- Mobile: `md:hidden fixed bottom-0 left-0 right-0 border-t bg-background z-50`
- All tab buttons: `min-h-[56px]` (exceeds 44px WCAG — PLAY-05)
- Default active tab: `'assignment'`
- Tabs: My Assignment → `<MyAssignmentTab>`, Roster → `<RosterView>`, Briefing → `<BriefingPage>`, Maps → `<MapResourcesPage>`

### MyAssignmentTab

- LOCKED callsign display: `<div className="font-mono text-2xl font-bold text-orange-500">[{callsign}]</div>` (PLAY-06)
- 404 from `/my-assignment` treated as unassigned (not error) via try/catch in queryFn
- If `myRequest?.status === 'Pending'` → renders `<PendingRequestCard>`; else renders `<ChangeRequestForm>`
- Skeleton loading state while data loads

### ChangeRequestForm

- Textarea placeholder "Describe your request..."
- Button label: "Request Assignment" when `isUnassigned=true`, else "Submit Request"
- `POST /api/events/{eventId}/roster-change-requests` on submit
- Invalidates `['events', eventId, 'roster-change-requests', 'mine']` on success

### PendingRequestCard

- Shows `<Badge>Pending</Badge>`, request note, optional commanderNote
- Cancel button fires `DELETE /api/events/{eventId}/roster-change-requests/{id}`
- Only shows cancel button when status is "Pending"

### ChangeRequestsPage

- Queries `GET /roster-change-requests` (pending list) and `GET /roster` (for dropdowns)
- Per request card: orange monospace callsign, player name, note, Approve + Deny buttons
- Approve Dialog: platoon `<select>` → squad `<select>` (filtered to selected platoon), optional commander note, fires `POST .../approve`
- Deny Dialog: optional commander note, fires `POST .../deny`
- Both mutations invalidate `['events', eventId, 'roster-change-requests']`
- Empty state: "No pending change requests."

### Component Tests (10 new, all passing)

**PlayerEventView.test.tsx (5):**
- `renders_MyAssignmentTab_by_default` — default tab is assignment
- `renders_bottomTabBar_on_mobile` — 4+ buttons rendered
- `tabBar_buttons_have_minimum_44px_height` — `min-h-[56px]` class present
- `switching_tabs_renders_correct_content` — clicking Roster renders RosterView
- `callsign_displays_with_orange_monospace_style` — MyAssignmentTab rendered as default

**ChangeRequestForm.test.tsx (5):**
- `renders_changeRequest_form_when_no_pending_request` — textarea + submit visible
- `renders_pendingRequest_card_when_request_is_pending` — cancel button, no form
- `submit_button_calls_submit_mutation` — api.post called with correct payload
- `cancel_button_calls_cancel_mutation` — api.delete called with correct URL
- `commander_approve_dialog_shows_platoon_squad_dropdowns` — dialog has Platoon/Squad labels

**Full test suite: 56/56 passing** (0 regressions). TypeScript: 0 errors.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Wave 0 stubs replaced with real test implementations**
- **Found during:** Task 1 → Task 3 review
- **Issue:** Wave 0 stubs used `expect(true).toBe(false)` which could never turn green through implementation
- **Fix:** Replaced with real tests using `vi.mock` for child components and MSW-style mocks for API calls. Tests are meaningful assertions about component behavior.
- **Files modified:** `PlayerEventView.test.tsx`, `ChangeRequestForm.test.tsx`
- **Commit:** `6d500ed`, `15ff391`, `69ed881`

**2. [Rule 1 - Bug] ChangeRequestsPage uses native `<select>` instead of shadcn Select**
- **Found during:** Task 3 implementation
- **Issue:** shadcn `<Select>` component was not in the available UI components list (no `select.tsx` in `components/ui/`)
- **Fix:** Used native `<select>` with Tailwind border/background styling. Functionally identical, visually consistent.
- **Files modified:** `ChangeRequestsPage.tsx`

## Commits

| Hash | Message |
|------|---------|
| `6d500ed` | `test(04-02): add component tests for PlayerEventPage and ChangeRequestForm` |
| `15ff391` | `feat(04-02): routing setup, DashboardPage event listing, ProtectedRoute role gating` |
| `69ed881` | `feat(04-02): PlayerEventPage, MyAssignmentTab, ChangeRequestForm, PendingRequestCard, ChangeRequestsPage` |

## Self-Check: PASSED

- ✅ `PlayerEventPage.tsx` — exists (h-dvh, min-h-[56px], 4 tabs)
- ✅ `MyAssignmentTab.tsx` — exists (orange monospace callsign, change request form/card)
- ✅ `ChangeRequestForm.tsx` — exists
- ✅ `PendingRequestCard.tsx` — exists
- ✅ `ChangeRequestsPage.tsx` — exists (Approve/Deny dialogs)
- ✅ `main.tsx` — has complete route tree with commander-only gating
- ✅ `ProtectedRoute.tsx` — has `requiredRole` prop
- ✅ `DashboardPage.tsx` — lists events with role-aware navigation
- ✅ `EventDetail.tsx` — has "Change Requests" button for commanders
- ✅ All 56 frontend tests pass (`pnpm test --run` exit 0)
- ✅ TypeScript: 0 errors (`npx tsc --noEmit` exit 0)
- ✅ Commits `6d500ed`, `15ff391`, `69ed881` in git log
