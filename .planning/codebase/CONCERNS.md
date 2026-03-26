---
focus: concerns
generated: 2026-03-25
---

# Concerns & Risks

## Summary

The milsim-planning codebase is generally well-structured with deliberate security controls, but carries several meaningful risks: a hardcoded JWT fallback secret that surfaces in production if environment configuration is incomplete, an in-process notification queue that loses all pending jobs on container restart, and duplicated raw `fetch` calls in two frontend components that bypass the shared API client. Test coverage is good for the backend (integration tests via Testcontainers) but frontend tests are primarily smoke tests with limited assertion depth.

---

## Security Considerations

### Hardcoded JWT Fallback Secret
- **Risk:** If `Jwt:Secret` is not set in the environment, the application falls back to `"dev-placeholder-secret-32-chars!!"` and still starts. A container deployed without this secret set will issue valid tokens signed with the public placeholder, enabling token forgery.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Program.cs:37`, `milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs:58`
- **Current mitigation:** None. The fallback is silent — no warning is logged at startup.
- **Recommendation:** Throw `InvalidOperationException` on startup if `Jwt:Secret` is absent or contains "placeholder". Log a clear startup error. Never default to a working placeholder in production paths.

### DevUploadController Has No Auth Guard and No Environment Check
- **Risk:** `DevUploadController` exposes `PUT /api/dev/upload/{*key}` and `GET /api/dev/upload/{*key}` with no `[Authorize]` attribute and no environment condition. The controller is always registered (`LocalFileService` is always added via `AddSingleton`). If a production deployment accidentally sets `R2:AccountId` to a placeholder value, `DevUploadController` becomes publicly accessible — anyone can PUT arbitrary files to the server's disk.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Controllers/DevUploadController.cs`, `milsim-platform/src/MilsimPlanning.Api/Program.cs:79-87`
- **Current mitigation:** The storage backend switch (local vs. R2) is based on `R2:AccountId` content heuristics (checking for "placeholder" or "your-"). This is fragile.
- **Recommendation:** Register `DevUploadController` only when `IWebHostEnvironment.IsDevelopment()` is true, or add `[Authorize(Policy = "RequireSystemAdmin")]` and an explicit development environment guard.

### Notification Blast: No Rate Limiting or Recipient Count Cap
- **Risk:** Any `faction_commander` can send a notification blast to all event players. There is no per-event cooldown, no daily send limit, and no cap on blast frequency. A compromised commander account could trigger hundreds of Resend API calls, incurring cost and potential spam complaints.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Controllers/NotificationBlastsController.cs:29-62`
- **Current mitigation:** Resend API key limits apply externally but are not enforced in-app.
- **Recommendation:** Add a per-event cooldown (e.g., minimum 5 minutes between blasts) enforced at the controller or service level.

### Magic Link Token Cleanup — No Periodic Purge
- **Risk:** `MagicLinkToken` rows are written to the database on every link request but never purged. Expired tokens accumulate indefinitely in the `MagicLinkTokens` table. This is a data retention concern and will degrade index performance over time.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Data/Entities/MagicLinkToken.cs`, `milsim-platform/src/MilsimPlanning.Api/Services/MagicLinkService.cs`
- **Current mitigation:** None.
- **Recommendation:** Add a scheduled cleanup job (or a DELETE WHERE ExpiresAt < NOW() on each verify call) to remove expired tokens.

### Email HTML Not Sanitized in Notification Blasts
- **Risk:** `BuildBlastHtml` in `NotificationWorker` does a naive `body.Replace("\n", "<br>")` but does not HTML-encode the `subject` or `body` values. If a commander includes `<script>` or phishing content, it is sent verbatim in the HTML email.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Infrastructure/BackgroundJobs/NotificationWorker.cs:58-61`
- **Current mitigation:** Only `faction_commander` accounts can send blasts, which limits exposure to internal misuse.
- **Recommendation:** HTML-encode `subject` and `body` before embedding in the HTML template, or use a proper templating library.

### JWT Stored in localStorage
- **Risk:** The JWT is stored in `localStorage` (`milsim_token`), making it accessible to any JavaScript running on the page. An XSS vulnerability in any dependency could exfiltrate tokens.
- **Files:** `web/src/lib/auth.ts`
- **Current mitigation:** No known XSS vectors currently, but the surface area grows with every npm dependency update.
- **Recommendation:** Consider `HttpOnly` cookie-based token storage as a future improvement. Document the trade-off explicitly.

---

## Tech Debt

### Duplicated Raw `fetch` Calls Bypass the Shared API Client
- **Issue:** Two components use inline `fetch` calls that manually reconstruct `VITE_API_URL` and auth headers, duplicating the logic in `web/src/lib/api.ts`. These calls will silently fail to redirect on 401 and do not benefit from shared error handling.
- **Files:**
  - `web/src/components/hierarchy/SquadCell.tsx:34-35`
  - `web/src/pages/events/NotificationBlastPage.tsx:42-43`
- **Impact:** Inconsistent 401 handling; future auth changes must be updated in three places.
- **Fix approach:** Replace both raw `fetch` calls with the corresponding `api.*` methods from `web/src/lib/api.ts`.

### Large-Batch Roster Invitations Are Synchronous (No True Async Path)
- **Issue:** `RosterService.SendInvitationsAsync` has a comment noting that large batches (>20) should use a background queue but instead runs the same synchronous-await loop. The `else` branch is dead code — both paths are identical.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Services/RosterService.cs:243-255`
- **Impact:** Importing a 200-player roster blocks the HTTP request thread while sending 200 sequential Resend API calls. This increases timeout risk and degrades server responsiveness.
- **Fix approach:** Queue invitation emails via `INotificationQueue` using a new `RosterInviteJob` type, consistent with how blast notifications work.

### `EventService.ListEventsAsync` Sorts by `Id` (Timestamp Proxy)
- **Issue:** Events are sorted by `OrderByDescending(e => e.Id)` because there is no `CreatedAt` column. The comment acknowledges this: `// newest first (no CreatedAt in Phase 2 model)`.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Services/EventService.cs:64`
- **Impact:** Sort order is undefined if multiple events share sequential GUIDs (UUID v4 is random — sorting by UUID is semantically meaningless). The "newest first" intent is not reliably satisfied.
- **Fix approach:** Add a `CreatedAt` column to the `Event` entity in the next schema migration and update the sort.

### `AssertCommanderAccess` Is Duplicated in Three Services
- **Issue:** The same private `AssertCommanderAccess(Faction faction)` method is copy-pasted verbatim in `EventService`, `HierarchyService`, and `ContentService`.
- **Files:**
  - `milsim-platform/src/MilsimPlanning.Api/Services/EventService.cs:195-199`
  - `milsim-platform/src/MilsimPlanning.Api/Services/HierarchyService.cs:284-288`
  - `milsim-platform/src/MilsimPlanning.Api/Services/ContentService.cs:206-210`
- **Impact:** Any change to the access check (e.g., allowing system admins) must be made in all three places.
- **Fix approach:** Move to a `ScopeGuard.AssertCommanderAccess(Faction, ICurrentUser)` static method alongside the existing `AssertEventAccess`.

### In-Process Notification Queue Is Not Durable
- **Issue:** `NotificationQueue` uses a bounded in-memory `Channel<NotificationJob>` with capacity 500. All queued notification jobs are lost on container restart, crash, or deployment update.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Infrastructure/BackgroundJobs/NotificationQueue.cs`
- **Impact:** Email notifications (blast, squad assignment, roster decisions) silently drop if the container is recycled mid-processing.
- **Fix approach:** For the current scale (small events), this is acceptable. At larger scale, migrate to a durable queue (Azure Storage Queue, Azure Service Bus, or Hangfire with PostgreSQL backend).

### `FluentValidation.AspNetCore` Is Registered But Minimally Used
- **Issue:** `FluentValidation.AspNetCore` is listed in `MilsimPlanning.Api.csproj` but validation is performed via inline checks in controllers and services rather than via FluentValidation validators. The package is used only for its `ValidationException` type in `ContentService` and `FileService`.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/MilsimPlanning.Api.csproj:17`, `milsim-platform/src/MilsimPlanning.Api/Services/FileService.cs:41`
- **Impact:** Inconsistent validation approach; `ValidationException` from FluentValidation is used as a signal type rather than for its intended pipeline integration.
- **Fix approach:** Either adopt FluentValidation request validators consistently or replace the `ValidationException` usage with a purpose-built domain exception and remove the package.

---

## Performance Bottlenecks

### `CurrentUserService.LoadEventIds` Is Synchronous
- **Issue:** `CurrentUserService.LoadEventIds()` is a synchronous database call (`.ToHashSet()` not `.ToHashSetAsync()`), called via a lazy property accessor. Entity Framework Core synchronous calls block a thread pool thread.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Services/CurrentUserService.cs:57-63`
- **Impact:** Minor for current load but blocks a thread for the duration of each DB round-trip. At higher concurrency, this reduces throughput.
- **Fix approach:** Refactor `EventMembershipIds` to an async initialization pattern using `Task<IReadOnlySet<Guid>>` or initialize lazily on first async service method call.

### `BulkAssignAsync` Executes N Sequential DB Saves
- **Issue:** `HierarchyService.BulkAssignAsync` calls `AssignSquadAsync` or `AssignToPlatoonAsync` in a `foreach` loop. Each call does multiple includes, an IDOR check query, `SaveChangesAsync`, and potentially a notification queue write — one round-trip set per player.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Services/HierarchyService.cs:191-220`
- **Impact:** Assigning 100 players to a squad generates 100+ `SaveChangesAsync` calls. Acceptable for current event sizes but will degrade noticeably at scale.
- **Fix approach:** Batch all player updates and call `SaveChangesAsync` once after the loop.

---

## Fragile Areas

### `GetEventAsync` Applies Commander-Only Scope on a Player-Accessible Route
- **Issue:** `EventsController.GetById` (`GET /api/events/{id}`) is used as the `CreatedAtAction` target (called by tests and for response linking) and as a helper route. However, `EventService.GetEventAsync` calls `AssertCommanderAccess`, making the route accessible only to the event's commander — not to players who are members of the event.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Services/EventService.cs:71-81`, `milsim-platform/src/MilsimPlanning.Api/Controllers/EventsController.cs:40-46`
- **Impact:** Players cannot fetch event details via the events endpoint. Any future feature that needs players to load event metadata will be blocked by this access check.
- **Fix approach:** Separate commander-write scope from general event read access. Use `ScopeGuard.AssertEventAccess` for GET by ID and only `AssertCommanderAccess` for write operations.

### Production Seed Skipped if Any User Exists — Role Creation Is Not Gated
- **Issue:** `ProductionSeedService` skips the initial user creation if `userCount > 0`, but it always runs the role-creation loop. If roles were already created, this is harmless. However, if deployment starts and `Seed:AdminEmail` / `Seed:AdminPassword` are not set, the service logs a warning and exits without creating any user — silently leaving the system with no admin account on first deploy.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Data/ProductionSeedService.cs:43-50`
- **Impact:** First-time deployment with missing seed config produces a running system with no login credentials.
- **Fix approach:** Log an error (not just a warning) and consider failing the startup healthcheck when seed credentials are absent and no users exist.

### `DevSeedService` Backfill Runs on Every Startup
- **Issue:** `DevSeedService.SeedAsync` calls two backfill methods (`BackfillCommanderMembershipsAsync`, `BackfillPlayerMembershipsAsync`) on every development startup. Each loads potentially all `EventPlayer` rows to check for unlinked players. As data grows in dev, this startup query becomes expensive.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Data/DevSeedService.cs:53-61`
- **Impact:** Slow dev startup as the database grows. Not a production concern.
- **Fix approach:** Move backfill logic to a one-time migration or add a version flag to skip if already run.

---

## Test Coverage Gaps

### No Frontend Tests for Auth Flow Pages
- **Untested areas:** `web/src/pages/auth/LoginPage.tsx`, `web/src/pages/auth/PasswordResetRequestPage.tsx`, `web/src/pages/auth/PasswordResetConfirmPage.tsx`
- **Files:** `web/src/__tests__/` (MagicLinkConfirmPage is tested; login/password-reset pages are not)
- **Risk:** Regressions in login form submission, error display, or redirect logic go undetected.
- **Priority:** Medium

### No Backend Tests for Profile Endpoints
- **Untested area:** `ProfileController` and profile update logic
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Controllers/ProfileController.cs` (no corresponding test file)
- **Risk:** Profile read/update changes could silently break without test coverage.
- **Priority:** Low

### No Tests for `DevUploadController`
- **Untested area:** The local file upload/download proxy
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Controllers/DevUploadController.cs`
- **Risk:** Minimal production risk since this controller is dev-only, but path traversal in the `key` parameter is untested.
- **Priority:** Low

### Frontend Tests Are Primarily Render/Smoke Tests
- **Issue:** Most frontend test files render a component and assert basic visibility (`toBeInTheDocument`) rather than testing user interactions or state transitions.
- **Files:** `web/src/__tests__/`, `web/src/tests/`
- **Risk:** Interaction regressions (form submission, drag-and-drop in `HierarchyBuilder`, multi-step upload flows) are not covered.
- **Priority:** Medium

---

## Missing Critical Features

### No Token Revocation
- **Problem:** JWTs are 7-day tokens with no server-side revocation mechanism. Calling `POST /api/auth/logout` is a no-op on the server side — the comment says "JWT is stateless; client discards token". A stolen token remains valid for up to 7 days.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs:52-53`
- **Blocks:** Secure logout, role change propagation (a user demoted from commander keeps commander permissions until their token expires).
- **Note:** Magic link verification does call `UpdateSecurityStampAsync` which would invalidate cookie sessions but has no effect on existing JWTs.

### No Input Length Validation on Free-Text Fields
- **Problem:** Fields like `NotificationBlast.Subject`, `NotificationBlast.Body`, `InfoSection.Title`, `InfoSection.BodyMarkdown`, and `RosterChangeRequest.Note` have no server-side length limits enforced in the API layer. The database columns may accept up to PostgreSQL text limits.
- **Files:**
  - `milsim-platform/src/MilsimPlanning.Api/Controllers/NotificationBlastsController.cs:33`
  - `milsim-platform/src/MilsimPlanning.Api/Models/Content/CreateInfoSectionRequest.cs`
- **Impact:** Large payloads could cause slow DB writes, large email sends, or memory pressure in the notification worker.

---

## Dependencies at Risk

### `Resend` SDK Version 0.2.2 Is Very Early
- **Risk:** `Resend` version `0.2.2` is a pre-1.0 SDK. Pre-1.0 packages commonly have breaking API changes between minor versions.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/MilsimPlanning.Api.csproj:25`
- **Impact:** Future Resend SDK updates may require migration effort.
- **Migration plan:** Pin to a specific minor version and monitor Resend's changelog.

### `System.IdentityModel.Tokens.Jwt` Version `8.*` While ASP.NET Core Is `net10.0`
- **Risk:** The project targets `net10.0` and uses ASP.NET Core 10.x packages, but `System.IdentityModel.Tokens.Jwt` is pinned to version `8.*`. This creates a version family mismatch. Microsoft.IdentityModel libraries should generally track with the ASP.NET Core version to avoid conflicts.
- **Files:** `milsim-platform/src/MilsimPlanning.Api/MilsimPlanning.Api.csproj:27`
- **Impact:** Potential compatibility issues or security patches not received if Microsoft releases JWT library fixes in v9/v10 only.
- **Migration plan:** Update to `System.IdentityModel.Tokens.Jwt` version `9.*` or `10.*` to align with the runtime.

---

## Scaling Limits

### In-Memory Notification Queue Capacity: 500 Jobs
- **Current capacity:** 500 pending jobs in-memory, bounded `Channel` with `FullMode = Wait`.
- **Limit:** A large batch send (e.g., blast to 2000 players = 20 chunk-100 batches = 20 jobs) fills a meaningful portion of the queue. If the worker is slow (Resend rate limits), the queue backs up and HTTP requests enqueuing new blasts will block.
- **Scaling path:** Replace `Channel<T>` with a durable external queue when event sizes exceed ~200 players or when multiple simultaneous events are running.

---

*Concerns audit: 2026-03-25*
