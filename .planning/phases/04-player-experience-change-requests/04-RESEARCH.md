# Phase 4: Player Experience & Change Requests - Research

**Researched:** 2026-03-15
**Domain:** React Router v7 · Mobile bottom tab bar · ASP.NET Core EF Core entity design · TanStack Query · IntegrationTestAuthHandler pattern · shadcn/ui Tabs
**Confidence:** HIGH — all findings derived from verified prior-phase patterns, official docs patterns established in Phases 1–3, and the CONTEXT.md locked decisions

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Player Event Dashboard Layout:**
- Separate route `/events/:id/player` — commanders go to `/events/:id`, players go to `/events/:id/player`
- Assignment card is above the fold: full chain displayed — Faction > Platoon > Squad > Callsign
- Callsign continues the existing orange monospace prominent style (`[CALLSIGN]`) established in RosterView
- Unassigned state: prominent "Unassigned" banner with a "Request Assignment" CTA leading to change request form
- The assignment card contains a "Request Change" button (always visible for assigned players too)

**Roster Change Request Form:**
- Free-text note only — player writes what they want in natural language
- One open request per player per event — if pending, player sees the request status and can cancel; cannot submit another until resolved
- Post-submit state: player sees their pending request with a cancel button
- CTA lives inside the assignment card (not a separate tab or section)

**Commander Change Request Review UI:**
- Dedicated page per event: `/events/:id/change-requests`
- Linked from EventDetail alongside existing Import / Hierarchy / Roster / View Roster buttons
- List of pending requests with inline Approve / Deny actions
- On Approve: commander selects target platoon + squad from dropdowns; API updates EventPlayer assignment immediately (RCHG-05 auto-update)
- On Deny: no note required (commander note is optional for both approve and deny)
- Notification email is sent for both outcomes (leverages existing RosterChangeDecisionJob pipeline)

**Player Navigation & Routing:**
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

### Deferred Ideas (OUT OF SCOPE)
- Pending request count badge on commander dashboard / EventDetail — noted as "easy to add" but not required
- Push notifications or in-app notification center — v2 (NOTF-V2-02)
- Multi-faction events (BLUFOR/OPFOR) — v2 (MFAC-01)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| PLAY-01 | Player can view their squad and platoon assignment for an event | GET `/api/events/{id}/roster-change-requests/mine` + GET `/api/events/{id}/my-assignment` endpoint returns EventPlayer with PlatoonId/SquadId/Callsign |
| PLAY-02 | Player can view the full faction roster (names, callsigns, team affiliations, assignments) | Reuse existing `GET /api/events/{id}/roster` → `RosterHierarchyDto`; reuse `RosterView.tsx` component directly |
| PLAY-03 | Player can access all published event information sections | Player role already has `RequirePlayer` access to `GET /events/{id}/info-sections`; BriefingPage.tsx already exists |
| PLAY-04 | Player can download maps and documents from an event | Player role already has `RequirePlayer` access to download URLs; MapResourcesPage.tsx already exists |
| PLAY-05 | Player-facing UI is fully functional on mobile phones (responsive, 44px touch targets) | Bottom tab bar on mobile; min-h-[44px] / min-w-[44px] on all interactive elements; Tailwind responsive breakpoints |
| PLAY-06 | Callsign is displayed prominently in all roster views | `[CALLSIGN]` orange monospace style — already in RosterView, extend to assignment card |
| RCHG-01 | Player can submit a roster change request | POST `/api/events/{eventId}/roster-change-requests`; free-text note; one-pending-at-a-time guard |
| RCHG-02 | Faction Commander can view all pending roster change requests for an event | GET `/api/events/{eventId}/roster-change-requests`; commander-only; status filter = Pending |
| RCHG-03 | Faction Commander can approve or deny a roster change request | POST `…/{id}/approve` (body: platoonId, squadId, optional note); POST `…/{id}/deny` (body: optional note) |
| RCHG-04 | Player receives an email notification when their request is approved or denied | `RosterChangeDecisionJob` pipeline already exists from Phase 3 (03-06); approval/denial enqueues it |
| RCHG-05 | Roster is updated automatically when a change request is approved | Approve handler updates `EventPlayer.PlatoonId`/`SquadId` in same DB transaction as setting Status=Approved |
</phase_requirements>

---

## Summary

Phase 4 has two primary work streams: (1) a new `RosterChangeRequest` entity + full CRUD API with business rules, and (2) a complete React routing/navigation setup that exposes player-facing views through a mobile-first bottom tab bar. The backend work is straightforward EF Core entity design following the same delta-migration pattern established in Phases 2 and 3. The API endpoints follow the exact service layer pattern with `ScopeGuard.AssertEventAccess` as first line and `IntegrationTestAuthHandler` for test auth.

The frontend work is the heavier lift: App.tsx is currently a Vite stub with no routes. Phase 4 must wire all existing pages plus the three new ones (`PlayerEventPage`, `DashboardPage` extension, `ChangeRequestsPage`) through React Router v7. The bottom tab bar on mobile is the only net-new UI component with no direct shadcn equivalent — it should be built as a custom component using radix `Tabs` primitives (already available for Tabs in shadcn) with responsive CSS hiding it above the `md` breakpoint.

The approval flow is the most complex API endpoint: it must atomically update `EventPlayer.PlatoonId`/`SquadId` AND set `RosterChangeRequest.Status = Approved` AND enqueue the `RosterChangeDecisionJob` — all within a single `SaveChangesAsync` + enqueue call. The notification enqueue happens after the DB save succeeds, mirroring the squad-change pattern from Phase 3.

**Primary recommendation:** Plan 04-01 handles the Wave 0 test stubs + EF entity + routing setup + player dashboard view. Plan 04-02 handles the full RCHG CRUD API + commander review UI. No new npm/NuGet packages needed beyond what's already installed.

---

## Standard Stack

### Core (Phase 4 uses existing stack — no new packages required)

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| React Router v7 | 7.x | Route definitions in `main.tsx`; `useParams`, `useNavigate`, `Outlet` | Already installed |
| TanStack Query | 5.x | `useQuery` / `useMutation` for all API calls | Already installed |
| shadcn/ui Tabs | radix-based | Foundation for bottom tab bar component | Already installed (used in Phase 3 SectionEditor) |
| Tailwind CSS v4 | 4.x | Responsive breakpoints (`md:`, `lg:`), touch target sizing (`min-h-[44px]`) | Already installed |
| xUnit + Testcontainers | 2.9+ | API integration tests with PostgreSQL | Already installed |
| `IntegrationTestAuthHandler` | local | Deterministic test auth for commander/player roles | Created in Phase 3 Plan 09 |

### No New Packages Needed

All Phase 4 requirements are served by the existing stack. No additional npm or NuGet packages required.

> **Confidence:** HIGH — all packages verified present in prior phase summaries

---

## Architecture Patterns

### New EF Core Entity: RosterChangeRequest

```csharp
// Source: CONTEXT.md Integration Points, established Phase 2/3 entity pattern
// Data/Entities/RosterChangeRequest.cs

public class RosterChangeRequest
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid EventPlayerId { get; set; }
    public string Note { get; set; } = null!;          // free-text; player writes natural language
    public RosterChangeStatus Status { get; set; } = RosterChangeStatus.Pending;
    public string? CommanderNote { get; set; }          // optional; set on approve or deny
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }           // null until approved or denied

    // Navigation properties
    public Event Event { get; set; } = null!;
    public EventPlayer EventPlayer { get; set; } = null!;
}

public enum RosterChangeStatus
{
    Pending,
    Approved,
    Denied
}
```

**AppDbContext additions:**
```csharp
public DbSet<RosterChangeRequest> RosterChangeRequests => Set<RosterChangeRequest>();

// In OnModelCreating:
builder.Entity<RosterChangeRequest>()
    .HasIndex(r => new { r.EventPlayerId, r.Status });  // fast lookup for "player's pending request"

builder.Entity<RosterChangeRequest>()
    .HasIndex(r => new { r.EventId, r.Status });        // fast lookup for "all pending for event"
```

**Migration:** Single `Phase4Schema` migration following Phase 2/3 delta pattern.

---

### Pattern 1: New API Endpoints — RosterChangeRequestsController

**Route base:** `[Route("api/events/{eventId:guid}/roster-change-requests")]`

All endpoints follow: `ScopeGuard.AssertEventAccess(_currentUser, eventId)` as first line.

#### Player Endpoints

```csharp
// Source: CONTEXT.md Integration Points + Phase 2/3 controller pattern

// POST — player submits change request
[HttpPost]
[Authorize(Policy = "RequirePlayer")]
public async Task<IActionResult> Submit(Guid eventId, SubmitChangeRequestDto request)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    // Business rule: one pending request per player per event
    var existingPending = await _db.RosterChangeRequests
        .AnyAsync(r => r.EventPlayer.EventId == eventId
                    && r.EventPlayer.UserId == _currentUser.UserId
                    && r.Status == RosterChangeStatus.Pending);
    if (existingPending)
        return Conflict(new { error = "You already have a pending change request for this event." });

    // Find the EventPlayer for this user/event
    var eventPlayer = await _db.EventPlayers
        .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);
    if (eventPlayer is null)
        return NotFound();

    var changeRequest = new RosterChangeRequest
    {
        EventId = eventId,
        EventPlayerId = eventPlayer.Id,
        Note = request.Note,
        CreatedAt = DateTime.UtcNow
    };
    _db.RosterChangeRequests.Add(changeRequest);
    await _db.SaveChangesAsync();

    return Created($"api/events/{eventId}/roster-change-requests/{changeRequest.Id}",
        new { id = changeRequest.Id, status = changeRequest.Status });
}

// GET mine — player views own request
[HttpGet("mine")]
[Authorize(Policy = "RequirePlayer")]
public async Task<IActionResult> GetMine(Guid eventId)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var request = await _db.RosterChangeRequests
        .Where(r => r.EventPlayer.EventId == eventId
                 && r.EventPlayer.UserId == _currentUser.UserId)
        .OrderByDescending(r => r.CreatedAt)
        .Select(r => new { r.Id, r.Note, r.Status, r.CommanderNote, r.CreatedAt, r.ResolvedAt })
        .FirstOrDefaultAsync();

    return request is null ? NoContent() : Ok(request);
}

// DELETE — player cancels pending request
[HttpDelete("{id:guid}")]
[Authorize(Policy = "RequirePlayer")]
public async Task<IActionResult> Cancel(Guid eventId, Guid id)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var request = await _db.RosterChangeRequests
        .Include(r => r.EventPlayer)
        .FirstOrDefaultAsync(r => r.Id == id
                               && r.EventPlayer.EventId == eventId
                               && r.EventPlayer.UserId == _currentUser.UserId);
    if (request is null) return NotFound();
    if (request.Status != RosterChangeStatus.Pending)
        return UnprocessableEntity(new { error = "Only pending requests can be cancelled." });

    _db.RosterChangeRequests.Remove(request);
    await _db.SaveChangesAsync();
    return NoContent();
}
```

#### Commander Endpoints

```csharp
// GET — commander lists all pending requests for event
[HttpGet]
[Authorize(Policy = "RequireFactionCommander")]
public async Task<IActionResult> ListPending(Guid eventId)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var requests = await _db.RosterChangeRequests
        .Include(r => r.EventPlayer)
        .Where(r => r.EventId == eventId && r.Status == RosterChangeStatus.Pending)
        .OrderBy(r => r.CreatedAt)
        .Select(r => new {
            r.Id,
            r.Note,
            r.CreatedAt,
            Player = new {
                r.EventPlayer.Name,
                r.EventPlayer.Callsign,
                r.EventPlayer.PlatoonId,
                r.EventPlayer.SquadId
            }
        })
        .ToListAsync();

    return Ok(requests);
}

// POST approve — commander approves, updating EventPlayer assignment atomically
[HttpPost("{id:guid}/approve")]
[Authorize(Policy = "RequireFactionCommander")]
public async Task<IActionResult> Approve(Guid eventId, Guid id, ApproveChangeRequestDto request)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var changeRequest = await _db.RosterChangeRequests
        .Include(r => r.EventPlayer)
        .FirstOrDefaultAsync(r => r.Id == id && r.EventId == eventId);
    if (changeRequest is null) return NotFound();
    if (changeRequest.Status != RosterChangeStatus.Pending)
        return UnprocessableEntity(new { error = "Request is no longer pending." });

    // RCHG-05: update EventPlayer assignment atomically
    changeRequest.EventPlayer.PlatoonId = request.PlatoonId;
    changeRequest.EventPlayer.SquadId = request.SquadId;

    // Mark resolved
    changeRequest.Status = RosterChangeStatus.Approved;
    changeRequest.CommanderNote = request.CommanderNote;
    changeRequest.ResolvedAt = DateTime.UtcNow;

    await _db.SaveChangesAsync();  // single transaction: player assignment + request status

    // RCHG-04: enqueue notification AFTER DB save succeeds (mirrors Phase 3 pattern)
    if (changeRequest.EventPlayer.UserId is not null && changeRequest.EventPlayer.Email is not null)
    {
        await _notificationQueue.EnqueueAsync(new RosterChangeDecisionJob(
            RecipientEmail: changeRequest.EventPlayer.Email,
            RecipientName: changeRequest.EventPlayer.Name,
            EventName: /* load from Event nav or separate query */ "",
            Decision: "approved",
            CommanderNote: request.CommanderNote
        ));
    }

    return NoContent();
}

// POST deny — commander denies
[HttpPost("{id:guid}/deny")]
[Authorize(Policy = "RequireFactionCommander")]
public async Task<IActionResult> Deny(Guid eventId, Guid id, DenyChangeRequestDto request)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var changeRequest = await _db.RosterChangeRequests
        .Include(r => r.EventPlayer)
        .FirstOrDefaultAsync(r => r.Id == id && r.EventId == eventId);
    if (changeRequest is null) return NotFound();
    if (changeRequest.Status != RosterChangeStatus.Pending)
        return UnprocessableEntity(new { error = "Request is no longer pending." });

    changeRequest.Status = RosterChangeStatus.Denied;
    changeRequest.CommanderNote = request.CommanderNote;
    changeRequest.ResolvedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    // RCHG-04: notify player
    if (changeRequest.EventPlayer.UserId is not null && changeRequest.EventPlayer.Email is not null)
    {
        await _notificationQueue.EnqueueAsync(new RosterChangeDecisionJob(
            RecipientEmail: changeRequest.EventPlayer.Email,
            RecipientName: changeRequest.EventPlayer.Name,
            EventName: "",
            Decision: "denied",
            CommanderNote: request.CommanderNote
        ));
    }

    return NoContent();
}
```

---

### Pattern 2: Player Assignment Endpoint

**New endpoint needed:** The roster hierarchy (`GET /api/events/{id}/roster`) returns everything but requires a walk of the tree to find the current player. A dedicated "my assignment" endpoint is cleaner for the dashboard:

```csharp
// Route: GET /api/events/{eventId}/my-assignment
// Alternative: add to existing HierarchyController or create PlayerController

[HttpGet("my-assignment")]
[Authorize(Policy = "RequirePlayer")]
public async Task<IActionResult> GetMyAssignment(Guid eventId)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var player = await _db.EventPlayers
        .Include(ep => ep.Squad)
            .ThenInclude(s => s!.Platoon)
        .Include(ep => ep.Platoon)
        .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

    if (player is null) return NotFound();

    return Ok(new {
        player.Id,
        player.Name,
        player.Callsign,
        player.TeamAffiliation,
        Platoon = player.Platoon is null ? null : new { player.Platoon.Id, player.Platoon.Name },
        Squad = player.Squad is null ? null : new { player.Squad.Id, player.Squad.Name },
        IsAssigned = player.SquadId is not null
    });
}
```

> **Where to put it:** Either extend `HierarchyController` with a `/my-assignment` action or add a new `PlayerController`. A new `PlayerController` is cleaner and matches the `PlayerEventPage` domain.

---

### Pattern 3: React Router v7 Full Routing Setup (App.tsx)

**Key finding from Phase 2 summary:** Router is in `main.tsx`, not `App.tsx`. The App.tsx file is the default Vite placeholder and was never used — routes live in `main.tsx`. **Phase 4 adds routes to `main.tsx`.**

```typescript
// Source: Phase 2 02-04-SUMMARY.md decision + React Router v7 docs
// web/src/main.tsx — extend existing route structure

import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';

// Existing pages (already created in Phases 2/3):
import EventList from './pages/events/EventList';
import EventDetail from './pages/events/EventDetail';
import CsvImportPage from './pages/roster/CsvImportPage';
import HierarchyBuilder from './pages/roster/HierarchyBuilder';
import RosterView from './pages/roster/RosterView';
import BriefingPage from './pages/events/BriefingPage';
import MapResourcesPage from './pages/events/MapResourcesPage';
import NotificationBlastPage from './pages/events/NotificationBlastPage';

// New Phase 4 pages:
import DashboardPage from './pages/DashboardPage';
import PlayerEventPage from './pages/events/PlayerEventPage';      // mobile tab bar host
import ChangeRequestsPage from './pages/events/ChangeRequestsPage'; // commander view

// Auth/guard (Phase 1):
import ProtectedRoute from './components/auth/ProtectedRoute';

// In JSX:
<BrowserRouter>
  <Routes>
    {/* Auth routes */}
    <Route path="/login" element={<LoginPage />} />
    <Route path="/register" element={<RegisterPage />} />

    {/* Protected: all roles */}
    <Route element={<ProtectedRoute />}>
      <Route path="/dashboard" element={<DashboardPage />} />

      {/* Player event view — tab bar host */}
      <Route path="/events/:id/player" element={<PlayerEventPage />} />

      {/* Commander routes */}
      <Route path="/events" element={<EventList />} />
      <Route path="/events/:id" element={<EventDetail />} />
    </Route>

    {/* Commander-only routes — hidden from players via ProtectedRoute role */}
    <Route element={<ProtectedRoute requiredRole="faction_commander" />}>
      <Route path="/events/:id/roster/import" element={<CsvImportPage />} />
      <Route path="/events/:id/hierarchy" element={<HierarchyBuilder />} />
      <Route path="/events/:id/change-requests" element={<ChangeRequestsPage />} />
      <Route path="/events/:id/notifications" element={<NotificationBlastPage />} />
    </Route>

    {/* Public-ish player content routes */}
    <Route element={<ProtectedRoute />}>
      <Route path="/events/:id/briefing" element={<BriefingPage />} />
      <Route path="/events/:id/maps" element={<MapResourcesPage />} />
      <Route path="/events/:id/roster" element={<RosterView />} />
    </Route>

    <Route path="/" element={<Navigate to="/dashboard" replace />} />
  </Routes>
</BrowserRouter>
```

> **Note:** Check how `ProtectedRoute` currently accepts `requiredRole` — from Phase 2 summary it uses `Outlet` pattern. May need to extend with optional `requiredRole` prop for commander-only gating.

---

### Pattern 4: PlayerEventPage with Mobile Bottom Tab Bar

**Decision (Claude's discretion):** Use a custom bottom tab bar built on `div` + `button` elements with Tailwind responsive hiding. The shadcn `Tabs` component (radix-based) renders horizontal tabs at the top — for bottom navigation with icon+label, a custom implementation is cleaner.

**Component structure:**

```typescript
// web/src/pages/events/PlayerEventPage.tsx
// Hosts the 4 tabs: My Assignment | Roster | Briefing | Maps
// Bottom bar visible on mobile (< md), top nav on desktop (>= md)

import { useState } from 'react';
import { useParams } from 'react-router-dom';
import MyAssignmentTab from '../../components/player/MyAssignmentTab';
import { RosterView } from '../roster/RosterView';     // reuse existing
import BriefingPage from './BriefingPage';              // reuse existing
import MapResourcesPage from './MapResourcesPage';      // reuse existing

type Tab = 'assignment' | 'roster' | 'briefing' | 'maps';

export default function PlayerEventPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<Tab>('assignment');

  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: 'assignment', label: 'My Assignment', icon: '🎯' },
    { id: 'roster',     label: 'Roster',        icon: '👥' },
    { id: 'briefing',   label: 'Briefing',       icon: '📋' },
    { id: 'maps',       label: 'Maps',           icon: '🗺️' },
  ];

  return (
    <div className="flex flex-col h-dvh">
      {/* Desktop: horizontal top nav (hidden on mobile) */}
      <nav className="hidden md:flex border-b">
        {tabs.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.id
                ? 'border-primary text-primary'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      {/* Tab content — scrollable */}
      <main className="flex-1 overflow-y-auto pb-16 md:pb-0 p-4">
        {activeTab === 'assignment' && <MyAssignmentTab eventId={eventId!} />}
        {activeTab === 'roster'     && <RosterView eventId={eventId!} />}
        {activeTab === 'briefing'   && <BriefingPage eventId={eventId!} />}
        {activeTab === 'maps'       && <MapResourcesPage eventId={eventId!} />}
      </main>

      {/* Mobile: bottom tab bar (hidden on desktop) */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 border-t bg-background z-50">
        <div className="grid grid-cols-4">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`flex flex-col items-center justify-center min-h-[56px] py-1 text-xs gap-1 transition-colors ${
                activeTab === tab.id
                  ? 'text-primary'
                  : 'text-muted-foreground'
              }`}
              aria-current={activeTab === tab.id ? 'page' : undefined}
            >
              <span className="text-lg">{tab.icon}</span>
              <span>{tab.label}</span>
            </button>
          ))}
        </div>
      </nav>
    </div>
  );
}
```

**Key mobile details:**
- `min-h-[56px]` on tab buttons (exceeds 44px WCAG touch target requirement for PLAY-05)
- `pb-16` on content area prevents content from being hidden behind the fixed bottom bar
- `h-dvh` (dynamic viewport height) prevents iOS Safari address bar from clipping content
- `fixed bottom-0` + `z-50` keeps the tab bar above scrollable content on iOS

---

### Pattern 5: MyAssignmentTab — Player Dashboard Card

```typescript
// web/src/components/player/MyAssignmentTab.tsx
// Shows assignment chain + change request form/status

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';

interface AssignmentData {
  id: string;
  name: string;
  callsign: string | null;
  teamAffiliation: string | null;
  platoon: { id: string; name: string } | null;
  squad: { id: string; name: string } | null;
  isAssigned: boolean;
}

interface MyRequestData {
  id: string;
  note: string;
  status: 'Pending' | 'Approved' | 'Denied';
  commanderNote: string | null;
  createdAt: string;
}

export default function MyAssignmentTab({ eventId }: { eventId: string }) {
  const queryClient = useQueryClient();

  const { data: assignment, isLoading: assignmentLoading } = useQuery({
    queryKey: ['events', eventId, 'my-assignment'],
    queryFn: () => api.get<AssignmentData>(`/events/${eventId}/my-assignment`),
  });

  const { data: myRequest } = useQuery({
    queryKey: ['events', eventId, 'roster-change-requests', 'mine'],
    queryFn: () => api.get<MyRequestData | null>(`/events/${eventId}/roster-change-requests/mine`),
  });

  const submitMutation = useMutation({
    mutationFn: (note: string) =>
      api.post(`/events/${eventId}/roster-change-requests`, { note }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events', eventId, 'roster-change-requests', 'mine'] });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (requestId: string) =>
      api.delete(`/events/${eventId}/roster-change-requests/${requestId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['events', eventId, 'roster-change-requests', 'mine'] });
    },
  });

  if (assignmentLoading) return <AssignmentSkeleton />;

  return (
    <div className="space-y-4">
      {/* Assignment card — above the fold */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm text-muted-foreground uppercase tracking-wide">
            Your Assignment
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {assignment?.callsign && (
            <div className="font-mono text-2xl font-bold text-orange-500">
              [{assignment.callsign}]
            </div>
          )}

          {assignment?.isAssigned ? (
            <div className="space-y-1">
              {assignment.platoon && (
                <div className="text-sm">
                  <span className="text-muted-foreground">Platoon: </span>
                  <span className="font-medium">{assignment.platoon.name}</span>
                </div>
              )}
              {assignment.squad && (
                <div className="text-sm">
                  <span className="text-muted-foreground">Squad: </span>
                  <span className="font-medium">{assignment.squad.name}</span>
                </div>
              )}
            </div>
          ) : (
            <Badge variant="destructive" className="text-base px-3 py-1">
              Unassigned
            </Badge>
          )}

          {/* Change request CTA — always visible */}
          {myRequest?.status === 'Pending' ? (
            <PendingRequestCard request={myRequest} onCancel={cancelMutation.mutate} />
          ) : (
            <ChangeRequestForm
              onSubmit={submitMutation.mutateAsync}
              isUnassigned={!assignment?.isAssigned}
            />
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

---

### Pattern 6: ChangeRequestsPage — Commander View

```typescript
// web/src/pages/events/ChangeRequestsPage.tsx

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { api } from '../../lib/api';
import { Button } from '../../components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '../../components/ui/dialog';
// + Select for platoon/squad dropdowns

export default function ChangeRequestsPage() {
  const { id: eventId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const { data: requests } = useQuery({
    queryKey: ['events', eventId, 'roster-change-requests'],
    queryFn: () => api.get(`/events/${eventId}/roster-change-requests`),
  });

  // Load roster hierarchy for approval platoon/squad dropdowns
  const { data: roster } = useQuery({
    queryKey: ['events', eventId, 'roster'],
    queryFn: () => api.get(`/events/${eventId}/roster`),
  });

  const approveMutation = useMutation({
    mutationFn: ({ id, platoonId, squadId, commanderNote }: ApprovePayload) =>
      api.post(`/events/${eventId}/roster-change-requests/${id}/approve`,
        { platoonId, squadId, commanderNote }),
    onSuccess: () => queryClient.invalidateQueries({
      queryKey: ['events', eventId, 'roster-change-requests']
    }),
  });

  const denyMutation = useMutation({
    mutationFn: ({ id, commanderNote }: DenyPayload) =>
      api.post(`/events/${eventId}/roster-change-requests/${id}/deny`, { commanderNote }),
    onSuccess: () => queryClient.invalidateQueries({
      queryKey: ['events', eventId, 'roster-change-requests']
    }),
  });

  // ... render request list with inline approve/deny dialogs
}
```

---

### Pattern 7: Integration Test Pattern for New Endpoints

**Follows `IntegrationTestAuthHandler` established in Phase 3 Plan 09:**

```csharp
// milsim-platform/src/MilsimPlanning.Api.Tests/RosterChangeRequests/RosterChangeRequestTests.cs

// Category traits:
// [Trait("Category", "RCHG_Submit")]   — RCHG-01
// [Trait("Category", "RCHG_Review")]   — RCHG-02
// [Trait("Category", "RCHG_Decision")] — RCHG-03, RCHG-04, RCHG-05
// [Trait("Category", "PLAY_Assignment")] — PLAY-01

public class RosterChangeRequestTestsBase : IAsyncLifetime
{
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected string _commanderUserId = Guid.NewGuid().ToString();
    protected string _playerUserId = Guid.NewGuid().ToString();
    protected Guid _eventId;

    public async Task InitializeAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Deterministic test auth (Phase 3 pattern)
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });

                    // Replace real DB with Testcontainers PostgreSQL
                    // (same PostgreSqlFixture pattern from Phase 2/3)

                    // Mock INotificationQueue to capture enqueued jobs
                    var mockQueue = new Mock<INotificationQueue>();
                    services.AddSingleton(mockQueue.Object);
                    services.AddSingleton(mockQueue);  // for verification in tests
                });
            });

        _commanderClient = factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Add(
            IntegrationTestAuthHandler.UserIdHeader, _commanderUserId);
        _commanderClient.DefaultRequestHeaders.Add(
            IntegrationTestAuthHandler.RoleHeader, "faction_commander");

        _playerClient = factory.CreateClient();
        _playerClient.DefaultRequestHeaders.Add(
            IntegrationTestAuthHandler.UserIdHeader, _playerUserId);
        _playerClient.DefaultRequestHeaders.Add(
            IntegrationTestAuthHandler.RoleHeader, "player");

        // Seed: create event, faction, EventPlayer for player
        // ...
    }
}

// Example test:
[Fact]
[Trait("Category", "RCHG_Submit")]
public async Task SubmitRequest_ValidNote_Returns201()
{
    var body = new { note = "I'd like to move to Alpha Squad please" };
    var response = await _playerClient.PostAsJsonAsync(
        $"/api/events/{_eventId}/roster-change-requests", body);
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}

[Fact]
[Trait("Category", "RCHG_Submit")]
public async Task SubmitRequest_AlreadyPending_Returns409()
{
    // First submit succeeds
    await _playerClient.PostAsJsonAsync(
        $"/api/events/{_eventId}/roster-change-requests",
        new { note = "First request" });

    // Second submit returns 409 Conflict
    var response = await _playerClient.PostAsJsonAsync(
        $"/api/events/{_eventId}/roster-change-requests",
        new { note = "Second request" });
    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}

[Fact]
[Trait("Category", "RCHG_Decision")]
public async Task Approve_UpdatesEventPlayerAssignment()
{
    // Setup: player has a pending request
    var submitResponse = await _playerClient.PostAsJsonAsync(
        $"/api/events/{_eventId}/roster-change-requests",
        new { note = "Move me to Alpha" });
    var submitted = await submitResponse.Content.ReadFromJsonAsync<dynamic>();
    var requestId = submitted!.id;

    // Commander approves with specific platoon/squad
    var approveBody = new { platoonId = _platoonId, squadId = _squadId, commanderNote = (string?)null };
    var approveResponse = await _commanderClient.PostAsJsonAsync(
        $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve", approveBody);
    approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

    // Verify EventPlayer updated in DB
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var player = await db.EventPlayers.FindAsync(_eventPlayerId);
    player!.SquadId.Should().Be(_squadId);
    player.PlatoonId.Should().Be(_platoonId);
}

[Fact]
[Trait("Category", "RCHG_Decision")]
public async Task Approve_EnqueuesRosterChangeDecisionJob()
{
    // ... submit request first
    var mockQueue = _factory.Services.GetRequiredService<Mock<INotificationQueue>>();

    // Commander approves
    await _commanderClient.PostAsJsonAsync(
        $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve",
        new { platoonId = _platoonId, squadId = _squadId });

    // Verify notification enqueued
    mockQueue.Verify(q => q.EnqueueAsync(
        It.Is<RosterChangeDecisionJob>(j => j.Decision == "approved"),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

**Test run command pattern (matching Phase 3 convention):**
```bash
dotnet test milsim-platform/milsim-platform.slnx --filter "Category~RCHG|Category~PLAY"
```

---

### Pattern 8: Player Dashboard Extension

The `DashboardPage` stub exists at `/dashboard`. Phase 4 extends it to list events the player is enrolled in:

```csharp
// New API endpoint needed (PlayerController or extend EventsController):
// GET /api/events?role=player — returns events where current user has EventMembership

// Simpler: filter existing GET /api/events by EventMembership
// EventsController already lists events — extend to include player's enrolled events
```

```typescript
// DashboardPage.tsx extension
// Query: GET /api/events (already exists, returns events for current user's memberships)
// Display: event cards with date, name; tap → navigate to /events/:id/player (for player role)
// Role-aware navigation: commander → /events/:id, player → /events/:id/player
```

---

### Recommended Project Structure (Phase 4 additions)

```
src/MilsimPlanning.Api/
├── Controllers/
│   ├── RosterChangeRequestsController.cs  ← RCHG-01..05 (new)
│   └── PlayerController.cs                ← PLAY-01 /my-assignment (new)
├── Services/
│   └── RosterChangeRequestService.cs      ← business logic (optional extraction)
├── Models/RosterChangeRequests/
│   ├── SubmitChangeRequestDto.cs
│   ├── ApproveChangeRequestDto.cs         ← { PlatoonId, SquadId, CommanderNote? }
│   └── DenyChangeRequestDto.cs            ← { CommanderNote? }
├── Data/Entities/
│   └── RosterChangeRequest.cs             ← new entity
└── Data/Migrations/
    └── Phase4Schema.cs                    ← new migration

milsim-platform/src/MilsimPlanning.Api.Tests/
└── RosterChangeRequests/
    └── RosterChangeRequestTests.cs        ← RCHG + PLAY_Assignment tests

web/src/
├── pages/events/
│   ├── PlayerEventPage.tsx                ← bottom tab bar host (new)
│   └── ChangeRequestsPage.tsx             ← commander view (new)
├── components/player/
│   ├── MyAssignmentTab.tsx                ← assignment card + change request form
│   ├── ChangeRequestForm.tsx              ← free-text note + submit
│   ├── PendingRequestCard.tsx             ← status display + cancel
│   └── BottomTabBar.tsx                   ← mobile nav (custom component)
└── tests/
    ├── PlayerEventPage.test.tsx           ← Wave 0 stubs
    ├── MyAssignmentTab.test.tsx           ← Wave 0 stubs
    └── ChangeRequestsPage.test.tsx        ← Wave 0 stubs
```

### Anti-Patterns to Avoid

- **Letting players reach commander routes:** Use `ProtectedRoute` with `requiredRole` check — don't just hide links; guard the route itself. Players must get 403 if they navigate directly to `/events/:id/change-requests`.
- **Querying roster hierarchy to find player's own assignment:** This walks the full tree (potentially 800 players). Use the dedicated `/my-assignment` endpoint that queries `EventPlayers` directly by `UserId`.
- **Storing callsign without the bracket style:** The `[CALLSIGN]` orange monospace format is locked by CONTEXT.md — apply consistently via a shared `<CallsignBadge>` component.
- **Using `vh` instead of `dvh` for full-screen mobile layout:** iOS Safari's address bar causes `100vh` to clip. Use `h-dvh` (dynamic viewport height) or `min-h-svh` (small viewport height). Tailwind CSS v4 includes `dvh` support.
- **Not clearing `pb-16` on desktop:** The `pb-16` bottom padding that accounts for the fixed tab bar must be conditional (`md:pb-0`) or the desktop layout has unwanted whitespace.
- **Dual-save on approval:** Approval must call `SaveChangesAsync()` ONCE covering both the `EventPlayer` update AND the `RosterChangeRequest.Status` update. Two separate saves risk inconsistency (player moved but request still "Pending").
- **Enqueuing notification before DB save:** Follow Phase 3 pattern — enqueue AFTER `SaveChangesAsync()` succeeds. If DB save fails, no email sent. If enqueue fails after DB save, request is still resolved (acceptable trade-off).
- **Missing `Status != Pending` guard on approve/deny:** A commander could double-approve a request if two commanders act simultaneously. The `UnprocessableEntity` guard prevents this.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Roster tree traversal for player assignment | Custom tree walk | Dedicated `/my-assignment` endpoint with direct EF query | Tree walk = O(n) where n=all players; direct query = O(1) by index |
| Bottom tab bar from scratch | Full custom component | Custom `div`+`button` with Tailwind responsive classes | shadcn Tabs can render at top only; a 30-line custom component is sufficient and avoids pulling in a nav library |
| Platoon/squad dropdowns for approval form | Custom select | shadcn `Select` (already installed) | shadcn Select handles accessible dropdown, keyboard nav, mobile tap |
| Role-aware navigation guards | Manual role checks in each component | `ProtectedRoute` with `requiredRole` prop | Already established pattern in Phase 1; extend with optional prop |
| Change request one-pending-at-a-time | Client-side disable button | Server-side `AnyAsync` guard + 409 response | Client can be bypassed; server guard is the source of truth |

---

## Common Pitfalls

### Pitfall 1: `100vh` Clips on iOS Safari (Critical for PLAY-05)

**What goes wrong:** Player opens the app on an iPhone; the bottom tab bar is partially hidden behind Safari's address bar because `100vh` includes the browser chrome.

**Why it happens:** iOS Safari's dynamic address bar shrinks/grows as user scrolls. `100vh` = full viewport at page load including the browser bar. Content at the bottom gets clipped.

**How to avoid:** Use Tailwind `h-dvh` (dynamic viewport height — Tailwind v4 ships this). The `dvh` unit updates as the browser chrome changes.

```typescript
// WRONG
<div className="h-screen flex flex-col">

// CORRECT
<div className="h-dvh flex flex-col">
```

**Warning signs:** Tab bar looks correct in Chrome DevTools mobile simulation but is clipped on real iOS devices.

---

### Pitfall 2: Bottom Tab Bar Overlaps Scrollable Content

**What goes wrong:** The last item in the content area is hidden behind the fixed bottom tab bar and can't be scrolled into view.

**How to avoid:** Add `pb-16` (or `pb-[56px]` matching the exact tab bar height) to the scrollable content area. Remove the padding on desktop: `pb-16 md:pb-0`.

---

### Pitfall 3: RosterChangeRequest Orphan on EventPlayer Deletion

**What goes wrong:** A player's `EventPlayer` record is deleted (e.g., roster re-import) but their `RosterChangeRequest` records remain with a dangling FK.

**How to avoid:** Configure EF Core cascade delete:

```csharp
// In AppDbContext.OnModelCreating:
builder.Entity<RosterChangeRequest>()
    .HasOne(r => r.EventPlayer)
    .WithMany()
    .OnDelete(DeleteBehavior.Cascade);
```

---

### Pitfall 4: Player Has No EventPlayer Record

**What goes wrong:** A user navigates to `/events/:id/player` but their `EventPlayer` record was deleted or they have an `EventMembership` without a corresponding `EventPlayer`. The `/my-assignment` endpoint returns 404.

**Why it happens:** `EventMembership` (used by `ScopeGuard`) and `EventPlayer` (the roster record) are separate tables. A user can pass the scope guard but have no EventPlayer.

**How to avoid:** On the frontend, handle 404 from `/my-assignment` gracefully — show an "Unassigned" state similar to the explicit `isAssigned: false` case. Don't treat 404 as an error.

---

### Pitfall 5: Approve Sets Wrong PlatoonId

**What goes wrong:** Commander selects a squad but forgets or mismatch the platoon. The `EventPlayer` gets a `SquadId` that belongs to a different `PlatoonId`.

**How to avoid:** In the `Approve` service method, validate that the squad belongs to the specified platoon:

```csharp
var squad = await _db.Squads
    .FirstOrDefaultAsync(s => s.Id == request.SquadId && s.PlatoonId == request.PlatoonId);
if (squad is null)
    return BadRequest(new { error = "Squad does not belong to the specified platoon." });
```

---

### Pitfall 6: main.tsx vs App.tsx Confusion

**What goes wrong:** Developer adds routes to `App.tsx` because the CONTEXT says "App.tsx routing setup." But Phase 2 summary confirmed routes live in `main.tsx` (App.tsx is the unused Vite placeholder).

**How to avoid:** Per Phase 2 02-04-SUMMARY.md: "Router is in main.tsx, not App.tsx — App.tsx is the default Vite placeholder, never used." Add all Phase 4 routes to `main.tsx`.

---

## Code Examples

### EF Core Cascade Delete Configuration

```csharp
// Source: EF Core established pattern (Phase 2 AppDbContext)
builder.Entity<RosterChangeRequest>()
    .HasOne(r => r.EventPlayer)
    .WithMany(ep => ep.ChangeRequests)  // add nav to EventPlayer if desired
    .HasForeignKey(r => r.EventPlayerId)
    .OnDelete(DeleteBehavior.Cascade);

builder.Entity<RosterChangeRequest>()
    .HasOne(r => r.Event)
    .WithMany()
    .HasForeignKey(r => r.EventId)
    .OnDelete(DeleteBehavior.Cascade);
```

### TanStack Query Pattern for Player Dashboard

```typescript
// Source: TanStack Query v5 docs + Phase 2/3 established patterns
// Uses same api.get helper established in Phase 2

const { data: assignment, isLoading, error } = useQuery({
  queryKey: ['events', eventId, 'my-assignment'],
  queryFn: () => api.get<AssignmentData>(`/events/${eventId}/my-assignment`),
  staleTime: 30_000,     // re-fetch every 30s during live event
  retry: 1,              // one retry on network error
});
```

### Mobile Touch Target Enforcement (PLAY-05)

```typescript
// All interactive elements in player UI must meet 44px minimum
// Source: WCAG 2.5.5 + Apple Human Interface Guidelines

// Tab bar buttons
className="min-h-[56px] min-w-full"  // exceeds 44px

// Action buttons (Request Change, Cancel, Submit)
className="min-h-[44px] px-4 w-full"  // full-width on mobile

// Approve / Deny inline buttons in commander view
className="min-h-[44px] px-3"
```

### IntegrationTestAuthHandler Usage (from Phase 3 Plan 09)

```csharp
// Source: Phase 3 03-09-PLAN.md — established test auth pattern
// milsim-platform/src/MilsimPlanning.Api.Tests/Fixtures/IntegrationTestAuthHandler.cs
// (already exists from Phase 3)

// In test base class InitializeAsync:
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
    options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
}).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
    IntegrationTestAuthHandler.SchemeName, _ => { });

// Set headers on clients:
_playerClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.UserIdHeader, _playerUserId);
_playerClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.RoleHeader, "player");

_commanderClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.UserIdHeader, _commanderUserId);
_commanderClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.RoleHeader, "faction_commander");
```

---

## State of the Art

| Old Approach | Current Approach | When Established | Impact |
|--------------|------------------|-----------------|--------|
| `react-beautiful-dnd` | `@dnd-kit` | Phase 3 | Not relevant for Phase 4 (no DnD needed) |
| Hard-coded roles in routes | `ProtectedRoute` with role check | Phase 1 | Extend with `requiredRole` prop for commander-only routes |
| JWT token auth in integration tests | `IntegrationTestAuthHandler` header-based | Phase 3 Plan 09 | All Phase 4 tests use this pattern — no JWT in tests |
| `100vh` for full-screen mobile | `h-dvh` (dynamic viewport height) | CSS standard 2023 | Required for iOS Safari compatibility (PLAY-05) |

**Deprecated in this codebase:**
- `TestAuthHandler` (old name before Phase 3) → use `IntegrationTestAuthHandler` from `Fixtures/`
- JWT bearer token generation in test setup → use X-Test-UserId / X-Test-Role headers

---

## Open Questions

1. **ProtectedRoute `requiredRole` prop extension**
   - What we know: ProtectedRoute uses `Outlet` pattern (Phase 2 summary). Current implementation gates by "authenticated" only.
   - What's unclear: Does it currently accept a `requiredRole` or `minimumRole` prop?
   - Recommendation: The plan implementor should read the actual `ProtectedRoute.tsx` before coding. If no role prop exists, add `requiredRole?: string` that redirects non-matching roles to `/dashboard`.

2. **EventPlayer → ChangeRequests nav property**
   - What we know: Adding `ICollection<RosterChangeRequest> ChangeRequests` to EventPlayer is optional (EF Core can work without it via FK).
   - What's unclear: Whether existing code queries `EventPlayer` in ways that would break if a nav property is added.
   - Recommendation: Add the nav property to EventPlayer entity for future use but keep it as a non-loading collection (no `Include` in existing queries).

3. **DashboardPage: role-aware event navigation**
   - What we know: DashboardPage stub exists. Players navigate to `/events/:id/player`, commanders to `/events/:id`.
   - What's unclear: Whether the current event list API (`GET /api/events`) returns events for both roles or just commander-created events.
   - Recommendation: Implementor checks `EventsController.List` — if it already returns membership-scoped events, the dashboard can reuse it. If it only returns events where user is commander, a new player-specific query is needed.

4. **RosterChangeDecisionJob field naming (EventName)**
   - What we know: `RosterChangeDecisionJob` was defined in Phase 3 (03-06) with fields for RecipientEmail, RecipientName, EventName, Decision, CommanderNote.
   - What's unclear: Whether EventName requires a DB load in the approval endpoint.
   - Recommendation: Load `Event.Name` in the `Approve`/`Deny` handlers via `Include(r => r.Event)` or a separate `_db.Events.FindAsync(eventId)` call before enqueuing.

---

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json` — this section is REQUIRED.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9+ (API) + Vitest (React) |
| Config file | `milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` |
| Quick run command | `dotnet test milsim-platform/milsim-platform.slnx --filter "Category~RCHG\|Category~PLAY"` |
| Full suite command | `dotnet test milsim-platform/milsim-platform.slnx && pnpm --prefix web test --run` |
| Estimated runtime | ~35 seconds (same as Phase 3 — same Testcontainers setup) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PLAY-01 | GET /my-assignment returns player's platoon/squad/callsign | Integration | `dotnet test … --filter "Category=PLAY_Assignment"` | ❌ Wave 0 |
| PLAY-02 | Player can GET roster hierarchy | Integration | `dotnet test … --filter "Category=PLAY_Roster"` | ❌ Wave 0 |
| PLAY-03 | Player can GET info sections | Integration | Already covered by Phase 3 CONT tests | ✅ exists |
| PLAY-04 | Player can GET map download URLs | Integration | Already covered by Phase 3 MAPS tests | ✅ exists |
| PLAY-05 | Mobile UI: 44px touch targets, responsive layout | Component (visual) | `pnpm --prefix web test --run -- --reporter=verbose` + manual | ❌ Wave 0 |
| PLAY-06 | Callsign displayed with `[CALLSIGN]` orange monospace style | Component | `pnpm --prefix web test --run -- --filter=MyAssignmentTab` | ❌ Wave 0 |
| RCHG-01 | Player submits request → 201; duplicate pending → 409 | Integration | `dotnet test … --filter "Category=RCHG_Submit"` | ❌ Wave 0 |
| RCHG-02 | Commander GETs pending requests for event | Integration | `dotnet test … --filter "Category=RCHG_Review"` | ❌ Wave 0 |
| RCHG-03 | Commander approves/denies; player role blocked (403) | Integration | `dotnet test … --filter "Category=RCHG_Decision"` | ❌ Wave 0 |
| RCHG-04 | Approval/denial enqueues RosterChangeDecisionJob | Integration | `dotnet test … --filter "Category=RCHG_Decision"` | ❌ Wave 0 |
| RCHG-05 | EventPlayer.PlatoonId/SquadId updated on approve | Integration | `dotnet test … --filter "Category=RCHG_Decision"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test milsim-platform/milsim-platform.slnx --filter "Category~RCHG|Category~PLAY"`
- **Per wave merge:** `dotnet test milsim-platform/milsim-platform.slnx && pnpm --prefix web test --run`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/RosterChangeRequests/RosterChangeRequestTests.cs` — stubs for RCHG-01..05 + PLAY-01 (categories: RCHG_Submit, RCHG_Review, RCHG_Decision, PLAY_Assignment)
- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/Player/PlayerTests.cs` — optional separate file for PLAY-02 stubs, or combine with above
- [ ] `web/src/tests/PlayerEventPage.test.tsx` — stubs for tab bar render, tab switching, mobile layout
- [ ] `web/src/tests/MyAssignmentTab.test.tsx` — stubs for assignment display, callsign style, change request form
- [ ] `web/src/tests/ChangeRequestsPage.test.tsx` — stubs for pending request list, approve/deny dialogs
- [ ] `Data/Migrations/Phase4Schema.cs` — EF Core migration (created via `dotnet ef migrations add Phase4Schema`, not a hand-written stub)

Framework install: None — xUnit + Testcontainers + Vitest all installed from prior phases.

---

## Sources

### Primary (HIGH confidence)
- Phase 3 `03-09-PLAN.md` — `IntegrationTestAuthHandler` pattern and usage
- Phase 3 `03-06-PLAN.md` — `RosterChangeDecisionJob` contract and enqueue pattern
- Phase 3 `03-04-PLAN.md` — notification enqueue-after-save pattern
- Phase 2 `02-04-SUMMARY.md` — React Router v7 in main.tsx, ProtectedRoute with Outlet, test directory conventions
- Phase 2 `02-05-SUMMARY.md` — EventPlayer entity fields, HierarchyService pattern, ScopeGuard usage
- Phase 2 `02-01-SUMMARY.md` — EventPlayer entity shape (nullable PlatoonId/SquadId, UserId, Email, Callsign)
- Phase 3 `03-RESEARCH.md` — BackgroundService singleton/scope pattern, notification pipeline
- `04-CONTEXT.md` — all locked decisions and integration point specifications
- `.planning/STATE.md` — accumulated project decisions

### Secondary (MEDIUM confidence)
- Tailwind CSS v4 docs — `h-dvh` (dynamic viewport height) for iOS Safari compatibility
- WCAG 2.5.5 — 44px minimum touch target size
- CSS Working Group `dvh` spec — supported in all modern mobile browsers (Chrome 108+, Safari 15.4+, Firefox 101+)

### Tertiary (LOW confidence)
- None — all critical claims grounded in prior-phase established patterns

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages verified present in prior phase summaries
- API architecture: HIGH — directly mirrors Phase 2/3 controller/service patterns with CONTEXT.md specs
- React routing: HIGH — Phase 2 summary explicitly documents main.tsx routing pattern
- Bottom tab bar: HIGH — custom Tailwind approach is the correct call (no library needed)
- Mobile responsiveness: HIGH — dvh/touch target standards are well-established
- Test patterns: HIGH — IntegrationTestAuthHandler pattern documented from Phase 3 Plan 09

**Research date:** 2026-03-15
**Valid until:** 2026-04-15 (stable stack — no fast-moving dependencies)
