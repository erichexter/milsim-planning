---
focus: arch
generated: 2026-03-25
---

# Codebase Structure

## Summary

The repository has two independently deployable applications: `milsim-platform/` for the .NET 9 backend API and `web/` for the React/TypeScript SPA. Each is containerized separately via Docker. Planning docs live in `.planning/`, and GSD tooling lives in `.claude/`.

---

## Top-Level Directory Layout

```
milsim-planning/                       # Repo root
├── milsim-platform/                   # Backend (.NET 9)
│   └── src/
│       ├── MilsimPlanning.Api/        # Main API project
│       └── MilsimPlanning.Api.Tests/  # Integration tests (xUnit)
├── web/                               # Frontend (React + Vite)
│   ├── src/                           # Application source
│   ├── public/                        # Static assets served as-is
│   └── @/components/ui/               # shadcn/ui component copies (non-src path)
├── test-data/                         # CSV fixtures for manual/test use
├── .planning/                         # GSD planning docs, phases, todos
│   ├── codebase/                      # Codebase analysis docs (this file)
│   ├── phases/                        # Phase plans (01–04 completed)
│   ├── milestones/
│   ├── todos/
│   └── research/
├── .claude/                           # GSD tooling for Claude
├── .github/workflows/                 # CI pipelines
├── docker-compose.yml                 # Dev orchestration (db, api, web)
├── docker-compose.override.yml        # Local overrides
├── Dockerfile.api                     # API container build
├── Dockerfile.web                     # Web container build
└── PRD-REGISTRATION.md                # Product requirements doc
```

---

## Backend Directory: `milsim-platform/src/MilsimPlanning.Api/`

```
MilsimPlanning.Api/
├── Program.cs                     # Entry point: DI registration, middleware pipeline
├── appsettings.json               # Base config (Jwt, ConnectionStrings, R2, Resend)
├── appsettings.Development.json   # Dev overrides
├── appsettings.Production.json    # Production overrides
├── Controllers/                   # HTTP controllers — thin, delegate to services
│   ├── AuthController.cs          # /api/auth/* — login, magic link, password reset, invite
│   ├── EventsController.cs        # /api/events — CRUD + publish + duplicate
│   ├── HierarchyController.cs     # /api/events/{id}/platoons, /platoons/{id}/squads, /event-players/{id}/*
│   ├── RosterController.cs        # /api/events/{id}/roster — CSV validate/commit
│   ├── RosterChangeRequestsController.cs  # /api/events/{id}/roster-change-requests
│   ├── RosterChangeDecisionsController.cs # /api/events/{id}/roster-change-decisions
│   ├── InfoSectionsController.cs  # /api/events/{id}/info-sections + attachments
│   ├── MapResourcesController.cs  # /api/events/{id}/map-resources
│   ├── NotificationBlastsController.cs  # /api/events/{id}/notification-blasts
│   ├── PlayerController.cs        # /api/events/{id}/player — player overview endpoint
│   ├── ProfileController.cs       # /api/profile — current user profile
│   └── DevUploadController.cs     # /api/dev/upload — local dev file serving only
├── Services/                      # Business logic — one service per domain area
│   ├── AuthService.cs             # JWT generation, login, user invite
│   ├── MagicLinkService.cs        # Magic link token generate/verify
│   ├── EventService.cs            # Event CRUD, publish, duplicate
│   ├── RosterService.cs           # CSV parse/validate/commit, player invite
│   ├── HierarchyService.cs        # Platoon/squad CRUD, player assignment, bulk assign
│   ├── ContentService.cs          # InfoSections CRUD + attachments (implements IContentService)
│   ├── MapResourceService.cs      # Map resource CRUD + file upload (implements IMapResourceService)
│   ├── CurrentUserService.cs      # ICurrentUser — reads JWT claims, caches EventMembershipIds
│   ├── FileService.cs             # IFileService (Cloudflare R2 / production)
│   ├── LocalFileService.cs        # IFileService (local disk / development)
│   ├── EmailService.cs            # IEmailService implementation (Resend)
│   └── IEmailService.cs           # Email service interface
├── Data/                          # EF Core data access
│   ├── AppDbContext.cs            # Single DbContext (extends IdentityDbContext<AppUser>)
│   ├── DevSeedService.cs          # Dev-only seed data
│   ├── ProductionSeedService.cs   # Production bootstrap seed
│   ├── Entities/                  # Entity classes (14 entities)
│   │   ├── AppUser.cs             # Identity user + Profile nav + EventMemberships nav
│   │   ├── UserProfile.cs         # Callsign, DisplayName (1:1 with AppUser)
│   │   ├── Event.cs               # Core event entity (Draft/Published)
│   │   ├── Faction.cs             # Event faction — owns commander + hierarchy
│   │   ├── Platoon.cs             # Ordered platoon within a faction
│   │   ├── Squad.cs               # Ordered squad within a platoon
│   │   ├── EventPlayer.cs         # Roster entry — email natural key, nullable UserId
│   │   ├── EventMembership.cs     # User ↔ Event access record
│   │   ├── MagicLinkToken.cs      # One-time auth tokens
│   │   ├── InfoSection.cs         # Briefing content section with markdown body
│   │   ├── InfoSectionAttachment.cs  # File attachment on an InfoSection
│   │   ├── MapResource.cs         # Map file or external URL
│   │   ├── NotificationBlast.cs   # Sent notification blast record
│   │   └── RosterChangeRequest.cs # Player-submitted change request (Pending/Approved/Denied)
│   └── Migrations/                # EF Core migration files (auto-applied on startup)
├── Models/                        # Request/response DTOs
│   ├── Events/                    # CreateEventRequest, UpdateEventRequest, EventDto, etc.
│   ├── Hierarchy/                 # CreatePlatoonRequest, RosterHierarchyDto, PlatoonDto, etc.
│   ├── Content/                   # InfoSection request/response models
│   ├── Maps/                      # MapResource models
│   ├── Notifications/             # NotificationBlast models
│   ├── RosterChangeRequests/      # SubmitChangeRequestDto, etc.
│   ├── CsvImport/                 # CsvValidationResult, RosterImportRow
│   ├── Requests/                  # Shared request models (LoginRequest, MagicLinkRequest, etc.)
│   └── Responses/                 # Shared response models (AuthResponse)
├── Authorization/                 # Authorization infrastructure
│   ├── ScopeGuard.cs              # Static IDOR guard — call at top of every eventId method
│   ├── AppRoles.cs                # (in Domain/) Role constants + numeric hierarchy
│   ├── Handlers/
│   │   └── MinimumRoleHandler.cs  # Single IAuthorizationHandler for all 5 policies
│   ├── Requirements/
│   │   └── MinimumRoleRequirement.cs  # Holds MinimumRole string
│   └── Exceptions/
│       └── ForbiddenException.cs  # Thrown by ScopeGuard, caught by global middleware
├── Domain/
│   └── AppRoles.cs                # Role constants + Hierarchy dictionary
└── Infrastructure/
    └── BackgroundJobs/
        ├── INotificationQueue.cs  # Channel-based queue interface
        ├── NotificationQueue.cs   # Channel<NotificationJob> implementation (Singleton)
        ├── NotificationWorker.cs  # BackgroundService dequeuing and sending emails
        └── NotificationJob.cs     # Base record; subtypes: BlastNotificationJob, SquadChangeJob, RosterChangeDecisionJob
```

---

## Backend Test Directory: `milsim-platform/src/MilsimPlanning.Api.Tests/`

```
MilsimPlanning.Api.Tests/
├── Fixtures/                      # WebApplicationFactory setup, database fixtures
├── Auth/                          # Auth endpoint tests
├── Authorization/                 # Role/policy handler tests
├── Events/                        # Event CRUD integration tests
├── Hierarchy/                     # Platoon/squad/assignment tests
├── Maps/                          # Map resource tests
├── Notifications/                 # Notification blast tests
├── Content/                       # InfoSection tests
├── Roster/                        # CSV import tests
├── RosterChangeRequests/          # Change request workflow tests
├── Player/                        # Player endpoint tests
└── Migrations/                    # Migration smoke tests
```

---

## Frontend Directory: `web/src/`

```
web/src/
├── main.tsx                       # Entry point — QueryClient, router definition, all route imports
├── index.css                      # Global styles (Tailwind base + custom design tokens)
├── App.tsx                        # Legacy Vite scaffold (not used in routing — superseded by main.tsx)
├── assets/                        # Images (hero.png, logos)
├── lib/
│   ├── api.ts                     # SINGLE API client — all typed fetch calls + all DTO interfaces
│   ├── auth.ts                    # localStorage token helpers (getToken, setToken, clearToken, parseJwt)
│   └── utils.ts                   # Shared utilities (cn() for class merging)
├── hooks/
│   ├── useAuth.ts                 # Auth state hook — reads JWT from localStorage, exposes user/login/logout
│   └── useTheme.ts                # Theme management hook
├── components/
│   ├── AppLayout.tsx              # Outer layout: AppHeader + <Outlet />
│   ├── AppHeader.tsx              # Top navigation bar with user/logout
│   ├── ProtectedRoute.tsx         # Auth guard component (optionally enforces requiredRole)
│   ├── EventBreadcrumb.tsx        # Shared breadcrumb for event pages
│   ├── ui/                        # shadcn/ui primitives (Button, Card, Badge, Dialog, etc.)
│   ├── events/
│   │   └── DuplicateEventDialog.tsx   # Event duplication modal
│   ├── hierarchy/
│   │   └── SquadCell.tsx              # Drag/drop squad assignment cell
│   ├── player/
│   │   ├── ChangeRequestForm.tsx      # Player change request submission form
│   │   ├── PendingRequestCard.tsx     # Displays player's pending request
│   │   └── PlayerOverviewTab.tsx      # Player dashboard tab component
│   └── content/
│       ├── SectionList.tsx            # InfoSection list with reorder
│       ├── SectionEditor.tsx          # InfoSection create/edit form
│       ├── SortableSectionCard.tsx    # Drag/drop section card
│       ├── SectionAttachments.tsx     # Attachment list + upload
│       ├── MapResourceCard.tsx        # Map resource display card
│       └── UploadZone.tsx             # File upload drop zone component
├── pages/
│   ├── DashboardPage.tsx          # Landing page — event list for all roles
│   ├── ProfilePage.tsx            # User profile (callsign, display name)
│   ├── auth/
│   │   ├── LoginPage.tsx          # Email/password login form
│   │   ├── MagicLinkRequestPage.tsx   # Request magic link form
│   │   ├── MagicLinkConfirmPage.tsx   # Token confirmation redirect handler
│   │   ├── PasswordResetRequestPage.tsx
│   │   └── PasswordResetConfirmPage.tsx
│   ├── events/
│   │   ├── EventList.tsx          # Commander event list (legacy — DashboardPage used instead)
│   │   ├── EventDetail.tsx        # Commander event dashboard (tabs: briefing, maps, hierarchy, etc.)
│   │   ├── PlayerEventPage.tsx    # Player-facing event view
│   │   ├── CreateEventDialog.tsx  # Create event modal
│   │   ├── BriefingPage.tsx       # InfoSections viewer/editor
│   │   ├── MapResourcesPage.tsx   # Map resources viewer/editor
│   │   ├── NotificationBlastPage.tsx  # Send notification blasts
│   │   └── ChangeRequestsPage.tsx # Commander: view/approve/deny change requests
│   └── roster/
│       ├── CsvImportPage.tsx      # CSV upload, validate, commit roster
│       ├── HierarchyBuilder.tsx   # Commander drag/drop hierarchy editor
│       └── RosterView.tsx         # Player-accessible roster hierarchy display
├── mocks/
│   ├── server.ts                  # MSW service worker setup for tests
│   └── handlers.ts                # MSW request handlers
├── __tests__/                     # Vitest unit/integration tests
└── tests/                         # Additional test files
```

---

## Key File Locations (Quick Reference)

| Purpose | File |
|---------|------|
| Backend entry / DI / middleware | `milsim-platform/src/MilsimPlanning.Api/Program.cs` |
| Database context + model config | `milsim-platform/src/MilsimPlanning.Api/Data/AppDbContext.cs` |
| All entities | `milsim-platform/src/MilsimPlanning.Api/Data/Entities/` |
| Role hierarchy constants | `milsim-platform/src/MilsimPlanning.Api/Domain/AppRoles.cs` |
| IDOR guard | `milsim-platform/src/MilsimPlanning.Api/Authorization/ScopeGuard.cs` |
| Background job queue | `milsim-platform/src/MilsimPlanning.Api/Infrastructure/BackgroundJobs/NotificationQueue.cs` |
| Frontend entry + route table | `web/src/main.tsx` |
| All API calls + DTO types | `web/src/lib/api.ts` |
| Auth token management | `web/src/lib/auth.ts` |
| Auth state hook | `web/src/hooks/useAuth.ts` |
| Route guard component | `web/src/components/ProtectedRoute.tsx` |
| Dev orchestration | `docker-compose.yml` |

---

## Naming Conventions

**Backend:**
- Controllers: `{Domain}Controller.cs` (PascalCase)
- Services: `{Domain}Service.cs`; interface-backed services use `I{Domain}Service.cs`
- Entities: PascalCase noun (`EventPlayer.cs`, not `EventPlayersEntity.cs`)
- DTOs/Models: `{Action}{Domain}Request.cs` / `{Domain}Dto.cs`
- Namespaces: `MilsimPlanning.Api.{Layer}` mirroring directory structure

**Frontend:**
- Pages: `{Domain}Page.tsx` or `{Domain}Page.tsx`
- Components: PascalCase noun phrase (`HierarchyBuilder.tsx`, `SquadCell.tsx`)
- Hooks: `use{Name}.ts`
- Lib utilities: camelCase file names (`api.ts`, `auth.ts`)

---

## Where to Add New Code

**New API endpoint:**
1. Add controller method to the relevant controller in `milsim-platform/src/MilsimPlanning.Api/Controllers/` or create a new controller
2. Add business logic to the relevant service in `milsim-platform/src/MilsimPlanning.Api/Services/`
3. Add request/response models to `milsim-platform/src/MilsimPlanning.Api/Models/{Domain}/`
4. Add the typed API method to `web/src/lib/api.ts` and export the interface

**New entity:**
1. Add entity class to `milsim-platform/src/MilsimPlanning.Api/Data/Entities/`
2. Register `DbSet<T>` in `AppDbContext.cs`
3. Add EF Core fluent config in `AppDbContext.OnModelCreating` if needed
4. Add EF migration: `dotnet ef migrations add {MigrationName}`

**New frontend page:**
1. Add page component to `web/src/pages/{domain}/`
2. Register route in the router in `web/src/main.tsx`
3. If commander-only, nest under the `requiredRole="faction_commander"` `ProtectedRoute`

**New reusable component:**
- Shared layout/navigation: `web/src/components/`
- Domain-specific: `web/src/components/{domain}/`
- UI primitives (shadcn): `web/src/components/ui/` or `web/@/components/ui/`

**New background job type:**
1. Add a record subtype in `milsim-platform/src/MilsimPlanning.Api/Infrastructure/BackgroundJobs/NotificationJob.cs`
2. Add a `case` branch in `NotificationWorker.ExecuteAsync`
3. Enqueue via `INotificationQueue.EnqueueAsync` from a service

---

## Special Directories

**`web/@/components/ui/`**: shadcn/ui component copies placed outside `src/` — non-standard Vite path. Import with `@/components/ui/...` alias. Do not edit these files; re-generate from shadcn CLI if updates needed.

**`milsim-platform/src/MilsimPlanning.Api/Data/Migrations/`**: EF Core auto-generated migration files. Never hand-edit. Generated: Yes. Committed: Yes.

**`.planning/`**: GSD planning documents — phases, todos, research. Not committed as part of the application build. Contains completed phase plans (`01-foundation` through `04-player-experience-change-requests`).

**`test-data/`**: CSV files for manual roster import testing.
