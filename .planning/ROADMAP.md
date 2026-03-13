# Roadmap: Airsoft Event Planning Application

## Overview

Four phases that build from identity to delivery: first, a secure auth and RBAC foundation that gates every feature; then the core commander workflow where events are created, rosters imported, and squads organized; then the content and notification layer that makes publishing meaningful; finally, the player-facing surface and change-request loop that closes the feedback cycle. At the end of Phase 4, a commander can run an entire event briefing from one URL and every player can find their assignment on a mobile phone.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Foundation** - Secure authentication, 5-role RBAC, and full database schema
- [ ] **Phase 2: Commander Workflow** - Event management, CSV roster import, and platoon/squad hierarchy
- [ ] **Phase 3: Content, Maps & Notifications** - Event briefing content, file storage, and email delivery
- [ ] **Phase 4: Player Experience & Change Requests** - Player dashboard, mobile UI, and roster change workflow

## Phase Details

### Phase 1: Foundation
**Goal**: Every user can securely access the application and all data is scoped correctly by role
**Depends on**: Nothing (first phase)
**Requirements**: AUTH-01, AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, AUTHZ-01, AUTHZ-02, AUTHZ-03, AUTHZ-04, AUTHZ-05, AUTHZ-06
**Success Criteria** (what must be TRUE):
  1. User receives an invitation email and can activate their account via the link
  2. User can log in with email/password and stay logged in across browser refresh
  3. User can log in via magic link sent to their email (single-use, 15-60 min expiry)
  4. User can log out from any page and reset a forgotten password via email link
  5. A Faction Commander can take actions their role permits; a Player is blocked from commander-only actions; email addresses are hidden from Player role
**Plans**: 4 plans

Plans:
- [ ] 01-01-PLAN.md — Solution scaffold, EF Core entity model + migration, authorization policy registration (API project only)
- [ ] 01-02-PLAN.md — Test project stubs (PostgreSqlFixture, AuthTests, AuthorizationTests) + auth endpoints (login, magic link, password reset, logout, invite) + JWT service
- [ ] 01-03-PLAN.md — RBAC handlers + IDOR scope guard: MinimumRoleHandler, ScopeGuard, CurrentUserService, AUTHZ integration tests (wave 3, parallel with 01-04)
- [ ] 01-04-PLAN.md — React SPA foundation: Vite scaffold, auth.ts, api.ts, useAuth hook, ProtectedRoute, all 5 auth pages (wave 3, parallel with 01-03)

### Phase 2: Commander Workflow
**Goal**: A Faction Commander can create an event, import their player roster via CSV, and organize players into platoons and squads
**Depends on**: Phase 1
**Requirements**: EVNT-01, EVNT-02, EVNT-03, EVNT-04, EVNT-05, EVNT-06, ROST-01, ROST-02, ROST-03, ROST-04, ROST-05, ROST-06, HIER-01, HIER-02, HIER-03, HIER-04, HIER-05, HIER-06
**Success Criteria** (what must be TRUE):
  1. Commander can create a new event (or duplicate an existing one) and see it in their event list
  2. Commander can upload a CSV, see a per-row validation preview before committing, and have players upserted correctly (no duplicates on re-import)
  3. Imported players who have no account receive an invitation email automatically
  4. Commander can create platoons and squads, assign players to squads, and move players between squads
  5. Full faction roster (names, callsigns, assignments) is visible to all faction members
**Plans**: 5 plans

Plans:
- [ ] 02-01-PLAN.md — Phase 2 entity model (Event/Faction/Platoon/Squad/EventPlayer) + EF migration + Wave 0 test stubs (7 files)
- [ ] 02-02-PLAN.md — Event CRUD API (create, list, publish, duplicate) + integration tests (wave 1, parallel with 02-03)
- [ ] 02-03-PLAN.md — CSV roster import API (validate + commit endpoints, CsvHelper pipeline, invite trigger) + integration tests (wave 1, parallel with 02-02)
- [ ] 02-04-PLAN.md — React UI: EventList, CreateEventDialog, DuplicateEventDialog, EventDetail, CsvImportPage (wave 2)
- [ ] 02-05-PLAN.md — Hierarchy API (platoon/squad CRUD, player assignment, roster tree) + React HierarchyBuilder + RosterView (wave 2, parallel with 02-04)

### Phase 3: Content, Maps & Notifications
**Goal**: A published event contains a complete briefing — information sections, downloadable files, and map resources — and the commander can notify all participants by email
**Depends on**: Phase 2
**Requirements**: CONT-01, CONT-02, CONT-03, CONT-04, CONT-05, MAPS-01, MAPS-02, MAPS-03, MAPS-04, MAPS-05, NOTF-01, NOTF-02, NOTF-03, NOTF-04, NOTF-05
**Success Criteria** (what must be TRUE):
  1. Commander can create, edit, reorder, and delete markdown information sections with file attachments
  2. Commander can add external map links (with setup instructions) and upload downloadable map files (PDF, JPEG, PNG, KMZ)
  3. Uploaded files are accessible only via authenticated time-limited download links (never public URLs)
  4. Commander can send a notification blast to all event participants; the blast is processed asynchronously and does not block the UI
  5. Squad-assignment-change emails and roster-change-decision emails are sent automatically via transactional provider
**Plans**: TBD

Plans:
- [ ] 03-01: Information sections (markdown editor, attachments, reordering)
- [ ] 03-02: Map resources (external links and private file upload)
- [ ] 03-03: Notification blast, assignment-change emails, and async email pipeline

### Phase 4: Player Experience & Change Requests
**Goal**: Every player can find their assignment, access all event materials on a mobile phone, and submit roster change requests that commanders can act on
**Depends on**: Phase 3
**Requirements**: PLAY-01, PLAY-02, PLAY-03, PLAY-04, PLAY-05, PLAY-06, RCHG-01, RCHG-02, RCHG-03, RCHG-04, RCHG-05
**Success Criteria** (what must be TRUE):
  1. Player sees their squad and platoon assignment prominently (callsign displayed) on their event dashboard
  2. Player can browse the full faction roster (names, callsigns, team affiliations, assignments) and access all information sections
  3. Player can download maps and documents for offline use from a mobile phone (44px touch targets, responsive layout)
  4. Player can submit a roster change request and receive an email when it is approved or denied
  5. Commander can view all pending change requests, approve or deny each, and the roster updates automatically on approval
**Plans**: TBD

Plans:
- [ ] 04-01: Player event dashboard and roster view (mobile-first, callsign-prominent)
- [ ] 04-02: Roster change request workflow (submit, review, approve/deny, notify)

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 0/4 | Planned | - |
| 2. Commander Workflow | 0/5 | Planned | - |
| 3. Content, Maps & Notifications | 0/3 | Not started | - |
| 4. Player Experience & Change Requests | 0/2 | Not started | - |
