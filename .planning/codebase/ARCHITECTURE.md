---
focus: arch
generated: 2026-03-25
---

# Architecture

## Summary

Milsim Planning is a two-tier web application: a .NET 9 REST API backend (`milsim-platform/src/MilsimPlanning.Api`) and a React/TypeScript SPA frontend (`web/`). The backend follows a thin Controller → Service → EF Core pattern with JWT-based stateless authentication, a numeric role hierarchy for authorization, and a ScopeGuard pattern for per-event IDOR prevention. The frontend uses TanStack Query for server state management against a single typed `api` client module.

---

## Overall Pattern

**Backend:** Flat Controller → Service → Repository (EF Core DbContext) with no intermediate repository abstraction. Controllers are thin — they validate HTTP inputs, delegate to services, and translate exceptions to HTTP status codes. Services hold all business logic and query EF Core directly.

**Frontend:** Page-per-route component pattern. Pages use TanStack Query (`useQuery`/`useMutation`) to call `src/lib/api.ts`, which is the single centralized typed fetch client. No Redux or Zustand — server state is all TanStack Query, and auth state is `useState` in a hook initialized from `localStorage`.

---

## Backend Layers

### Controllers Layer
- Location: `milsim-platform/src/MilsimPlanning.Api/Controllers/`
- Purpose: HTTP routing, input validation, exception-to-status translation
- Pattern: Each controller is scoped to a domain area. All require `[Authorize]` globally; write endpoints add `[Authorize(Policy = "RequireFactionCommander")]`.
- Exception handling: `KeyNotFoundException` → 404, `ArgumentException` → 400, `ForbiddenException` → 403 (via global middleware in `Program.cs`), `InvalidOperationException` → 409
- Controllers: `AuthController`, `EventsController`, `HierarchyController`, `RosterController`, `RosterChangeRequestsController`, `RosterChangeDecisionsController`, `InfoSectionsController`, `MapResourcesController`, `NotificationBlastsController`, `PlayerController`, `ProfileController`, `DevUploadController`

### Services Layer
- Location: `milsim-platform/src/MilsimPlanning.Api/Services/`
- Purpose: All business logic, data access via EF Core, authorization enforcement
- Pattern: Services are `Scoped` and injected with `AppDbContext` and `ICurrentUser`. They call `AssertCommanderAccess(faction)` (private) for write operations and `ScopeGuard.AssertEventAccess(currentUser, eventId)` (static) for read operations.
- Key services: `EventService`, `HierarchyService`, `RosterService`, `ContentService` (`IContentService`), `MapResourceService` (`IMapResourceService`), `AuthService`, `MagicLinkService`, `CurrentUserService` (`ICurrentUser`), `FileService`/`LocalFileService` (`IFileService`), `EmailService` (`IEmailService`)

### Data Layer
- Location: `milsim-platform/src/MilsimPlanning.Api/Data/`
- ORM: Entity Framework Core with Npgsql (PostgreSQL)
- DbContext: `AppDbContext` extends `IdentityDbContext<AppUser>`, registered as Scoped
- Entities: `milsim-platform/src/MilsimPlanning.Api/Data/Entities/`
- Migrations: `milsim-platform/src/MilsimPlanning.Api/Data/Migrations/` — applied automatically on startup via `db.Database.MigrateAsync()`

### Authorization Layer
- Location: `milsim-platform/src/MilsimPlanning.Api/Authorization/`
- **Role hierarchy** (`AppRoles.cs`): `player(1) < squad_leader(2) < platoon_leader(3) < faction_commander(4) < system_admin(5)` — numeric comparison via `AppRoles.Hierarchy` dictionary
- **Policy handler** (`MinimumRoleHandler.cs`): single `IAuthorizationHandler` for all 5 policies. Reads `ClaimTypes.Role` from JWT, looks up numeric level, succeeds if `userLevel >= minLevel`.
- **Policies** (in `Program.cs`): `RequirePlayer`, `RequireSquadLeader`, `RequirePlatoonLeader`, `RequireFactionCommander`, `RequireSystemAdmin`
- **ScopeGuard** (`ScopeGuard.cs`): Static helper called at the top of service methods accepting `eventId`. Loads `EventMembershipIds` from `ICurrentUser` (cached per request) and throws `ForbiddenException` if user is not a member. `SystemAdmin` bypasses this check.
- **ForbiddenException**: Custom exception in `Authorization/Exceptions/ForbiddenException.cs`, caught by global middleware in `Program.cs` and converted to HTTP 403.

### Domain Layer
- Location: `milsim-platform/src/MilsimPlanning.Api/Domain/`
- Contains: `AppRoles.cs` (role constants + hierarchy dictionary)

### Infrastructure / Background Jobs
- Location: `milsim-platform/src/MilsimPlanning.Api/Infrastructure/BackgroundJobs/`
- Pattern: In-process `Channel<T>`-based queue (`NotificationQueue`) consumed by a singleton `BackgroundService` (`NotificationWorker`). Jobs are fire-and-forget; enqueue returns immediately.
- Job types: `BlastNotificationJob` (bulk email to all event players), `SquadChangeJob` (triggered by squad assignment), `RosterChangeDecisionJob` (triggered by commander approve/deny)
- Email provider: Resend SDK (`IResend`) — batch up to 100 recipients per API call with 200ms throttle between chunks

---

## Data Model

### Core Entities

**Event** (`Event.cs`)
- `Id`, `Name`, `Location`, `Description`, `StartDate`, `EndDate`, `Status` (Draft/Published)
- FK: `FactionId` → `Faction` (1:1 — each event has exactly one faction in v1)
- Nav: `InfoSections`, `MapResources`, `NotificationBlasts`

**Faction** (`Faction.cs`)
- `Id`, `EventId`, `CommanderId` (FK to `AppUser.Id`), `Name`
- Nav: `Platoons`, `Players` (EventPlayer collection)

**Platoon** (`Platoon.cs`)
- `Id`, `FactionId`, `Name`, `Order` (integer for display ordering), `IsCommandElement`
- Nav: `Squads`, `Players`

**Squad** (`Squad.cs`)
- `Id`, `PlatoonId`, `Name`, `Order`
- Nav: `Players`

**EventPlayer** (`EventPlayer.cs`)
- `Id`, `EventId`, `Email` (natural key, lowercase), `Name`, `Callsign`, `TeamAffiliation`, `Role` (free-text label)
- `UserId` — nullable FK to `AppUser.Id`; populated when invite accepted
- `PlatoonId` (nullable), `SquadId` (nullable)
- Unique index: `(EventId, Email)`

**AppUser** (`AppUser.cs`)
- Extends `IdentityUser` (ASP.NET Core Identity)
- Nav: `Profile` (1:1 UserProfile), `EventMemberships`

**EventMembership** (`EventMembership.cs`)
- `UserId`, `EventId`, `Role`, `JoinedAt`
- Unique index: `(UserId, EventId)`
- Dual-purpose: tracks which users can access which events (for ScopeGuard) and their role within that event

**RosterChangeRequest** (`RosterChangeRequest.cs`)
- `Id`, `EventId`, `EventPlayerId`, `Note`, `Status` (Pending/Approved/Denied), `CommanderNote`, `CreatedAt`, `ResolvedAt`
- Cascade deletes from both EventPlayer and Event

**InfoSection** (`InfoSection.cs`), **InfoSectionAttachment** (`InfoSectionAttachment.cs`), **MapResource** (`MapResource.cs`), **NotificationBlast** (`NotificationBlast.cs`): Phase 3 entities for event content.

---

## Authentication & Session Flow

1. User submits email/password to `POST /api/auth/login`
2. `AuthService.LoginAsync` calls ASP.NET Identity's `SignInManager.PasswordSignInAsync` (with lockout)
3. On success, `GenerateJwt` issues an HMAC-SHA256 JWT (7-day expiry) with claims: `sub` (userId), `email`, `ClaimTypes.Role`, `callsign`
4. Frontend stores token in `localStorage` under key `milsim_token`
5. `useAuth` hook initializes from localStorage on mount, parses JWT payload for user info
6. All API calls in `src/lib/api.ts` attach `Authorization: Bearer <token>` header
7. On 401 response, `api.ts` clears token and redirects to `/auth/login`

**Magic Link flow:** `POST /api/auth/magic-link` → `MagicLinkService.SendMagicLinkAsync` → email sent → user clicks GET link (renders HTML form) → user clicks button → `POST /api/auth/magic-link/confirm` → returns JWT (same response shape as password login). The GET/POST split prevents email scanner auto-login.

---

## Authorization Flow (per request)

1. JWT Bearer middleware validates token signature, issuer, audience, expiry
2. `[Authorize(Policy = "RequireFactionCommander")]` triggers `MinimumRoleHandler` — numeric role comparison
3. Service method calls `AssertCommanderAccess(faction)` — checks `faction.CommanderId == currentUser.UserId`
4. For player-accessible endpoints, service calls `ScopeGuard.AssertEventAccess(currentUser, eventId)` — checks `EventMembershipIds.Contains(eventId)` (DB query cached per request in `CurrentUserService`)

---

## File Storage Pattern

Two implementations of `IFileService`:
- **Development** (`LocalFileService`): stores files to `wwwroot/dev-uploads/`, served by `UseStaticFiles()`, returns local file URLs
- **Production** (`FileService`): uses Cloudflare R2 (S3-compatible) via `IAmazonS3`. Issues presigned PUT URLs to client for direct browser uploads; files stored with R2 key. Client confirms upload by calling a `/confirm` endpoint which records the R2 key in the database.

Selection at startup: checks `R2:AccountId` config — if absent/placeholder, uses `LocalFileService`.

---

## Data Flow: Commander Creates Event

1. `POST /api/events` → `EventsController.Create`
2. `EventService.CreateEventAsync` creates `Event` + `Faction` (with commander's userId) in one `SaveChangesAsync`
3. Auto-enrolls commander in `EventMemberships` (second `SaveChangesAsync`)
4. Returns `EventDto` (Id, Name, Location, Description, StartDate, EndDate, Status)

## Data Flow: Notification Blast

1. `POST /api/events/{id}/notification-blasts` → `NotificationBlastsController`
2. Gathers all EventPlayer emails for the event
3. Creates `NotificationBlast` record in DB
4. Enqueues `BlastNotificationJob` to `INotificationQueue` (Channel-based)
5. Returns immediately (recipient count estimate)
6. `NotificationWorker` background service dequeues, calls `IResend.EmailBatchAsync` in chunks of 100, updates `NotificationBlast.RecipientCount`

## Data Flow: Player Squad Assignment

1. `PUT /api/event-players/{id}/squad` → `HierarchyController`
2. `HierarchyService.AssignSquadAsync` verifies squad belongs to same event (IDOR check)
3. Updates `EventPlayer.SquadId` and `EventPlayer.PlatoonId`
4. Enqueues `SquadChangeJob` if player has an email and linked `UserId`
5. `NotificationWorker` sends squad change email via Resend

---

## Frontend Architecture

**Entry:** `web/src/main.tsx` — creates `QueryClient`, wraps router in `QueryClientProvider`, registers all routes

**Routing:** React Router v7 (`createBrowserRouter`) with nested layouts:
- Public routes: `/auth/*` pages (no auth required)
- `ProtectedRoute` (checks `isAuthenticated` → redirect to `/auth/login`)
- `AppLayout` (global header + outlet)
- Commander-only nested `ProtectedRoute` with `requiredRole="faction_commander"` → redirect to `/dashboard`

**State management:**
- Server state: TanStack Query (`useQuery`/`useMutation`) — query keys are `['events']`, `['roster', eventId]`, etc.
- Auth state: `useAuth` hook with local `useState`, persisted to `localStorage`
- No global client state store

**API client:** `web/src/lib/api.ts` — single `api` object with typed methods for every endpoint. Handles `Authorization` header injection, 401 auto-redirect, 204 empty-body handling, and file upload (`upload()` helper for multipart/form-data).

**Type definitions:** All DTO interfaces are defined in `web/src/lib/api.ts` alongside the API methods (co-located for easy maintenance).

---

## Error Handling Strategy

**Backend:**
- Services throw typed exceptions: `KeyNotFoundException` (not found), `ArgumentException` (bad input), `ForbiddenException` (authorization), `InvalidOperationException` (business rule violation)
- Controllers catch these in try/catch and return appropriate HTTP status codes
- `ForbiddenException` also caught by global middleware in `Program.cs` for cases thrown outside try/catch blocks
- Validation errors from Identity operations joined as semicolon-delimited string

**Frontend:**
- `api.ts` throws `Error` objects augmented with `.status` property
- TanStack Query surfaces errors via `isError`/`error` query state
- `sonner` `Toaster` used for user-facing error messages

---

## Cross-Cutting Concerns

**Logging:** ASP.NET Core default `ILogger<T>` — used in `NotificationWorker` for job failures

**Seeding:**
- Development: `DevSeedService.SeedAsync` — creates test users/events
- Production: `ProductionSeedService.SeedAsync` — seeds Identity roles and initial admin; skipped if users already exist

**Migrations:** Applied automatically on startup in all environments via `db.Database.MigrateAsync()`

**CORS:** Single policy scoped to `AppUrl` config value (default: `http://localhost:5173`)

**Swagger/OpenAPI:** Enabled in Development only (`UseSwagger` + `UseSwaggerUI`)
