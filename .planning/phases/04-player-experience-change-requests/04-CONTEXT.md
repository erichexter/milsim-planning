# Phase 4: Player Experience & Change Requests - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Every player can find their assignment, access all event materials on a mobile phone, and submit roster change requests that commanders can act on. Specifically:
- Player event dashboard (assignment-first, mobile-first)
- Player access to roster, briefing sections, and maps
- Roster change request submit/cancel flow (player side)
- Roster change request review/approve/deny flow (commander side)
- Automatic roster update on approval + email notification (RCHG-05 + RCHG-04)
- Full app routing and navigation structure (App.tsx currently has no routes)

</domain>

<decisions>
## Implementation Decisions

### Player Event Dashboard Layout
- Separate route `/events/:id/player` — commanders go to `/events/:id`, players go to `/events/:id/player`
- Assignment card is above the fold: full chain displayed — Faction > Platoon > Squad > Callsign
- Callsign continues the existing orange monospace prominent style (`[CALLSIGN]`) established in RosterView
- Unassigned state: prominent "Unassigned" banner with a "Request Assignment" CTA leading to change request form
- The assignment card contains a "Request Change" button (always visible for assigned players too)

### Roster Change Request Form
- Free-text note only — player writes what they want in natural language
- One open request per player per event — if pending, player sees the request status and can cancel; cannot submit another until resolved
- Post-submit state: player sees their pending request with a cancel button
- CTA lives inside the assignment card (not a separate tab or section)

### Commander Change Request Review UI
- Dedicated page per event: `/events/:id/change-requests`
- Linked from EventDetail alongside existing Import / Hierarchy / Roster / View Roster buttons
- List of pending requests with inline Approve / Deny actions
- On Approve: commander selects target platoon + squad from dropdowns; API updates EventPlayer assignment immediately (RCHG-05 auto-update)
- On Deny: no note required (commander note is optional for both approve and deny)
- Notification email is sent for both outcomes (leverages existing RosterChangeDecisionJob pipeline)

### Player Navigation & Routing
- App.tsx needs a full routing setup (currently a placeholder stub)
- Player event view uses a **bottom tab bar on mobile, top nav / sidebar on desktop**
  - Tabs: My Assignment | Roster | Briefing | Maps
  - My Assignment tab is default when entering `/events/:id/player`
- Dashboard (`/dashboard`) lists the events the logged-in player is enrolled in; tap one → `/events/:id/player`
- Commander-only routes (hierarchy builder, CSV import, change requests page) are strictly hidden from players via ProtectedRoute role gating — no disabled links shown

### Claude's Discretion
- Loading skeleton / shimmer implementation
- Exact spacing, typography, and visual design within the mobile-first constraint
- Tab bar component implementation (shadcn doesn't have one — can use custom or radix Tabs)
- Error state handling for failed API calls
- Pending request count badge on the commander's Change Requests button (if easy to add)

</decisions>

<specifics>
## Specific Ideas

- Callsign display style already established: `[CALLSIGN]` in orange monospace — keep consistent on player dashboard
- Players access the app from phones during live events — the assignment card must be immediately readable without scrolling
- The "Request Change" button on the assignment card is the primary entry point for RCHG-01; it should be accessible even when assigned (player may want a squad move)

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RosterView.tsx`: Already renders full hierarchy with callsign-prominent style, accordion, search — player roster tab can reuse this directly
- `EventDetail.tsx`: Commander event hub with button row — add "Change Requests" button here following existing pattern
- `DashboardPage.tsx`: Stub exists at `/dashboard` — extend to list enrolled events for the player role
- `RosterChangeDecisionsController.cs`: Already handles the notification side of approve/deny decisions; Phase 4 adds the CRUD layer (submit, list, approve, deny) on top
- `HierarchyController.cs GET /api/events/{id}/roster`: Already returns `RosterHierarchyDto` with platoons/squads/players — used for squad dropdown in approval form
- `shadcn/ui`: Card, Button, Badge, Input, Dialog, Accordion all available; Tabs available via radix (no bottom tab bar component exists — custom needed for mobile)
- `useQuery` / `useMutation` (TanStack Query): established pattern for all data fetching

### Established Patterns
- `ProtectedRoute`: role-based route gating — extend to gate `/events/:id/player` and `/events/:id/change-requests`
- `IntegrationTestAuthHandler.ApplyTestIdentity`: deterministic test auth — all new integration tests must use this
- `ScopeGuard.AssertEventAccess`: first line of every service method with eventId — new RosterChangeRequest service follows same contract
- `INotificationQueue.EnqueueAsync`: used by existing controllers for email dispatch — approval/denial uses same queue

### Integration Points
- New `RosterChangeRequests` entity needed (DB migration): fields — Id, EventId, EventPlayerId, Note, Status (Pending/Approved/Denied), CreatedAt, ResolvedAt
- New API routes needed:
  - `POST /api/events/{eventId}/roster-change-requests` — player submits
  - `GET /api/events/{eventId}/roster-change-requests` — commander lists pending
  - `GET /api/events/{eventId}/roster-change-requests/mine` — player views own request
  - `DELETE /api/events/{eventId}/roster-change-requests/{id}` — player cancels
  - `POST /api/events/{eventId}/roster-change-requests/{id}/approve` — commander approves (body: platoonId, squadId, optional note)
  - `POST /api/events/{eventId}/roster-change-requests/{id}/deny` — commander denies (body: optional note)
- Approval endpoint updates `EventPlayer.PlatoonId` / `EventPlayer.SquadId` and enqueues `RosterChangeDecisionJob`
- App.tsx routing setup connects all existing page components (EventList, EventDetail, BriefingPage, MapResourcesPage, etc.) that are currently unreachable

</code_context>

<deferred>
## Deferred Ideas

- Pending request count badge on commander dashboard / EventDetail — noted as "easy to add" but not required
- Push notifications or in-app notification center — v2 (NOTF-V2-02)
- Multi-faction events (BLUFOR/OPFOR) — v2 (MFAC-01)

</deferred>

---

*Phase: 04-player-experience-change-requests*
*Context gathered: 2026-03-15*
