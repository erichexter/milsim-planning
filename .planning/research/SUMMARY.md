# Project Research Summary

**Project:** Airsoft / MilSim Event Planning & Roster Management Platform
**Domain:** Event management / hierarchical roster coordination web app
**Researched:** 2026-03-12
**Confidence:** HIGH

## Executive Summary

This is a structured event coordination and roster management platform purpose-built for the airsoft/milsim community — a domain with no existing purpose-built tooling. Commanders currently cobble together 4–6 separate tools (registration systems, Squarespace, Discord, Google Drive, and email) to manage what this platform handles in one place: publishing a complete event briefing with structured unit assignments, downloadable maps, and guaranteed player notification. The stack is locked: **C# .NET 10 / ASP.NET Core** REST API backend + **React 19 / Vite 6** SPA frontend + **PostgreSQL** database. This is a proven, production-ready combination that delivers excellent type safety, strong ORM tooling (EF Core 10 + Npgsql), and first-class auth via ASP.NET Core Identity with a hand-rolled magic-link flow.

The core architectural pattern is a layered monolith: thin ASP.NET Core controllers delegate to a fat service layer containing all business logic, which accesses the database via EF Core and external services (Cloudflare R2, Resend, background workers) through wrapper infrastructure. RBAC is enforced at two levels — ASP.NET Core Authorization Policies for role-level checks AND service-layer scope guards (event/faction ownership checks per query). CSV import uses CsvHelper with a two-phase validate-then-commit pattern. Files are stored privately in Cloudflare R2 with pre-signed URL access. Events follow a state machine (Draft → Published → Archived) where publishing and notifying are separate, explicit actions. These patterns are critical to get right from the start — none are optional polish.

The primary risks cluster around three areas: authorization (IDOR vulnerabilities and scattered role checks are OWASP #1 and must be solved architecturally, not patched), email delivery (synchronous bulk email at 800+ recipients times out and corrupts event state — the built-in `BackgroundService + Channel<T>` pattern handles this and must be in from day one), and CSV data integrity (partial imports and silent column-mapping errors destroy commander trust — two-phase preview is mandatory). The recommended build order from ARCHITECTURE.md — Foundation → Core Data Model → Roster/Hierarchy → Content/Files → Publish/Notifications → Player Experience — directly maps to a 6-phase roadmap and should be followed as specified.

---

## Key Findings

### Recommended Stack

The stack is locked to C# .NET 10 / ASP.NET Core API + React/Vite SPA + PostgreSQL. All versions verified against live NuGet and npm registries as of 2026-03-12. The API is a single deployable ASP.NET Core Web API project using EF Core 10 + Npgsql 10.0.1 for the ORM, ASP.NET Core Identity for user/role management, and a hand-rolled magic-link flow using Identity's built-in `GenerateUserTokenAsync` / `VerifyUserTokenAsync` infrastructure. The frontend is a Vite 6 + React 19 SPA deployed as static files, using React Router v7 for client-side routing, TanStack Query v5 for server state, React Hook Form v7 + Zod v3 for forms, and shadcn/ui (Vite-native) for components. See [STACK.md](./STACK.md) for full alternatives analysis, package manifest, and solution structure.

**Core technologies:**
- **ASP.NET Core (.NET 10)**: REST API host — Minimal API + Controllers; thin controller layer, fat service layer
- **EF Core 10 + Npgsql 10.0.1**: ORM + PostgreSQL provider — LINQ queries, code-first migrations via `dotnet ef`, Identity integration
- **ASP.NET Core Identity**: User/role management — `UserManager<T>`, `SignInManager<T>`, token providers for magic link
- **JWT Bearer (10.0.x)**: Auth tokens for SPA clients — stateless, horizontally scalable
- **CsvHelper 33.x**: CSV parsing — strongly-typed `GetRecords<T>()`, header-name parsing, RFC 4180 compliant
- **AWSSDK.S3 3.7.x + Cloudflare R2**: File storage — S3-compatible, no egress fees, pre-signed PUT/GET URLs
- **Resend .NET SDK**: Transactional email — `IResend` DI interface, 3k/month free tier
- **BackgroundService + System.Threading.Channels**: Async notification queue — built-in, no Hangfire needed at this scale
- **FluentValidation 11.x**: Business rule validation — CSV row validation with per-row error collection
- **React 19 + Vite 6 + TypeScript 5.7+**: SPA frontend — fast dev server, Rollup production build, static deploy
- **React Router v7**: Client-side routing — declarative SPA mode, `createBrowserRouter`
- **TanStack Query v5**: Server state — caching, background refetch, optimistic updates for roster tables
- **React Hook Form v7 + Zod v3**: Forms + validation — minimal re-renders; Zod v3 (not v4 — ecosystem resolvers on v3)
- **shadcn/ui (Vite-native) + Tailwind CSS 4**: Component library + styling — accessible Radix primitives, owned in your codebase

**What NOT to use:** Duende IdentityServer (paid license, overkill), OpenIddict (OAuth2 overkill for single-tenant SPA), NHibernate (legacy ORM), Newtonsoft.Json (replaced by System.Text.Json), Redux (server state belongs in TanStack Query), Formik (slower than React Hook Form), Axios (not needed with TanStack Query + native fetch). See STACK.md for full rationale.

### Expected Features

The platform's entire value proposition is replacing the current 4–6-tool workflow with a single URL where commanders publish a complete event briefing and every player receives it without anything falling through the cracks. All 12 P1 features below must ship in v1 — none are optional. See [FEATURES.md](./FEATURES.md) for dependency map and competitor analysis.

**Must have (table stakes) — all 12 required for v1:**
- **Event creation & management** — root object; everything else hangs from it
- **CSV roster import** — primary onboarding path; 600+ players cannot be entered manually
- **Platoon/squad hierarchy builder** — the central commander workflow; flat lists break at this scale
- **Player assignment to unit** — players must know exactly which unit they're in
- **Role-based access control (5 roles)** — required to safely scope actions at each hierarchy level
- **Email/password + magic link auth** — magic link reduces abandonment for one-time logins
- **Custom information sections (markdown + attachments)** — the briefing content delivery mechanism
- **Document/map file upload and download** — offline map access is field-critical
- **Event publish + email notification blast** — the moment of delivery; makes it a publishing tool
- **Roster change request workflow** — structured feedback loop; prevents Discord chaos
- **Player-facing event dashboard** — assignment + docs + info in one mobile-first view
- **Responsive mobile UI** — players access from phones in the field; non-negotiable

**Should have (differentiators — v1.x, after first event validation):**
- **Targeted sub-unit notifications** — per-platoon email targeting; validate need before building
- **Information section ordering** — drag-to-reorder; easy refinement once enough sections exist
- **Event cloning / template reuse** — amortizes setup after 2nd–3rd event
- **Callsign-prominent UI polish** — airsoft identity is callsign-first, not legal name
- **Faction-scoped multi-event history view** — meaningful only after 3+ events

**Defer (v2+):**
- Multi-faction event support (complex data model; validate single-faction first)
- KMZ in-browser preview (high effort; download is sufficient for v1)
- Player event history / statistics (out of scope for logistics tool)
- PWA / "Add to Home Screen" manifest (low effort but only worthwhile at regular usage)
- API integration with registration platforms (CSV works; ROI unproven until platform is proven)

**Anti-features (deliberately excluded from v1):** In-app messaging/chat, interactive embedded maps, registration/ticket sales, real-time game tracking, OAuth/social login, attendance check-in, bulk email marketing, native mobile app.

### Architecture Approach

The recommended architecture is a layered monolith: an ASP.NET Core Web API serving all business logic and data access, paired with a separately deployed React/Vite SPA as a CDN-hosted static site. The API follows a thin-controller / fat-service pattern — controllers parse HTTP and return responses, services own all business logic and are independently testable via xUnit + Testcontainers. External integrations (Resend email, Cloudflare R2 storage) are abstracted behind infrastructure interfaces so providers can be swapped without touching service code. At the target scale (800 users, 8 events/year), no external queue or horizontal scaling is needed — but the notification blast is the first scaling boundary and uses a `BackgroundService + Channel<T>` queue from day one. See [ARCHITECTURE.md](./ARCHITECTURE.md) for full data model, data flow diagrams, and anti-patterns.

**Major components:**
1. **ASP.NET Core Web API (Controllers + Services)** — thin HTTP layer + fat service layer; all business logic in services, independently testable
2. **ASP.NET Core Identity + JWT Bearer** — `UserManager<T>` for user/role management; hand-rolled magic-link using `GenerateUserTokenAsync`; JWT access tokens + refresh strategy
3. **EF Core 10 + Npgsql 10.0.1 (AppDbContext extends IdentityDbContext)** — relational schema: users → factions → events → platoons → squads → event_roster; single migration workflow covers both Identity and application tables
4. **Cloudflare R2 via AWSSDK.S3** — private bucket; pre-signed PUT URLs for upload, 24h pre-signed GET URLs for download; file bytes never flow through the API server
5. **BackgroundService + Channel&lt;T&gt; (NotificationWorker)** — in-process async queue for email blasts; upgrade to Hangfire if restart-survival is needed
6. **React 19 / Vite 6 SPA** — CDN-deployed static site; React Router v7 for routing; TanStack Query v5 for server state; shadcn/ui for accessible components; cookie-based or bearer token auth

**Key patterns to follow:**
- **ASP.NET Core Authorization Policies + service-layer scope guards**: `[Authorize(Policy = "CommanderOrAbove")]` for role checks; `GetEventForCommanderAsync(eventId, userId)` scope guard in service for resource ownership
- **Two-phase CSV import (CsvHelper)**: Validate all rows first with FluentValidation → return preview with errors → commander confirms → single EF Core transaction with upsert-by-email
- **Pre-signed URL file access (R2)**: API generates short-lived signed URLs via `GetPreSignedURL`; file bytes never flow through the application server
- **Publish-gated event state machine**: `draft → published → archived`; publish and notify are separate, explicit actions
- **Thin controllers / fat services**: Route handlers are glue only; zero business logic in controllers; services are testable via `Microsoft.AspNetCore.Mvc.Testing` + Testcontainers

### Critical Pitfalls

The full list of 16 pitfalls is in [PITFALLS.md](./PITFALLS.md). These 7 are critical — getting any one wrong requires expensive recovery and can destroy commander trust before the platform establishes credibility.

1. **Object-level authorization bypass (IDOR)** — `[Authorize]` middleware is not enough; every DB query must scope to the authenticated user's event/faction via service-layer scope guards (e.g., `WHERE event_id = ? AND faction_id = ?`). Write integration tests proving User A cannot read User B's resources. (Phase 1 — foundational)
2. **Scattered role string comparisons** — Define Authorization Policies in `Program.cs` once (`AddAuthorization`). All permission checks use `[Authorize(Policy = "...")]` or `IAuthorizationService.AuthorizeAsync`. Never raw role string comparisons in service logic. (Phase 1 — foundational)
3. **Synchronous bulk email on publish** — 800 Resend API calls in a request handler will time out and cause partial sends. Use `BackgroundService + Channel<T>` queue (built-in); enqueue on publish; return `202 Accepted`; show delivery progress in UI. (Phase 5 — email notifications)
4. **CSV import with no validation preview** — Partial imports leaving the DB in an inconsistent state destroy trust. Two-phase import is mandatory: CsvHelper parses + FluentValidation validates all rows → preview with error list → commander confirms → single EF Core transaction with upsert-by-email. (Phase 3 — roster import)
5. **Files served without authentication** — Public R2 buckets expose operational security materials to anyone. All files in private buckets; access only via authenticated API endpoint that calls `GetPreSignedURL`; UUIDs as storage keys (`FileRecord.StorageKey`). (Phase 4 — file upload)
6. **Magic link tokens that are long-lived or reusable** — Use `UserManager.GenerateUserTokenAsync` (15-min TTL) + store token hash in `magic_link_tokens` table; invalidate on first use via `used_at`; call `UpdateSecurityStampAsync` after successful auth. Two-step confirm page mitigates email security scanners. (Phase 1 — auth)
7. **Irreversible event publish that triggers immediate notification** — Separate "Publish" (status → published, no email) from "Send Notifications" (explicit action, enqueues blast). Provide draft preview. Allow updating published content without re-notifying. (Phase 5 — event state machine)

---

## Implications for Roadmap

The ARCHITECTURE.md build order is the correct phase structure. It reflects hard dependencies — each layer must be stable before the next. The 6 phases map directly to architectural layers and feature dependencies identified in FEATURES.md.

### Phase 1: Foundation — Auth, RBAC & Database Schema
**Rationale:** Auth gates every other route. EF Core schema contracts must be stable (migrations committed) before services are built against them. RBAC must be architecturally correct from the start — retrofitting scope guards after the fact is high-risk. This phase has the highest concentration of security pitfalls.
**Delivers:** Working auth system (email/password + hand-rolled magic-link via `UserManager`), 5-role RBAC with ASP.NET Core Authorization Policies + service-layer scope guards, full EF Core schema with migrations (`AppDbContext extends IdentityDbContext<ApplicationUser>`), UUID primary keys on all public-facing tables.
**Addresses:** Auth feature, RBAC feature (from FEATURES.md table stakes)
**Avoids:** IDOR authorization bypass (Pitfall 1), scattered role comparisons (Pitfall 2), long-lived/reusable magic link tokens (Pitfall 6)
**Stack:** ASP.NET Core (.NET 10), EF Core 10 + Npgsql 10.0.1, ASP.NET Core Identity, JWT Bearer, Resend (magic link email only), xUnit + Testcontainers

### Phase 2: Core Data Model — Events, Factions & User Management
**Rationale:** Events are the root object — nothing else can be created without them. Faction and user management are prerequisites for assigning commanders and enabling scoped access. This is a thin CRUD layer once auth and schema are in place.
**Delivers:** Event CRUD (draft state only), faction management, admin user management, event-scoped access working correctly via scope guards.
**Addresses:** Event creation & management feature (from FEATURES.md table stakes)
**Avoids:** Single-admin-role anti-pattern, event ID enumeration via sequential IDs (UUIDs enforced from Phase 1)
**Stack:** ASP.NET Core Controllers, EF Core (LINQ queries + migrations), TanStack Query v5 (frontend), React Router v7

### Phase 3: Roster Import & Hierarchy Builder
**Rationale:** Players cannot be assigned to units until they're imported. Hierarchy assignment cannot be validated until roster players exist. This is the most complex commander workflow and the highest-risk phase for data integrity bugs.
**Delivers:** Two-phase CSV import with per-row validation preview (CsvHelper + FluentValidation), player account activation via magic link, platoon/squad CRUD, player assignment to squads (nullable `squad_id` in `event_roster`).
**Addresses:** CSV roster import, hierarchy builder, player assignment (FEATURES.md P1)
**Avoids:** CSV partial import / no preview (Pitfall 4), CSV column-position assumption (Pitfall 9 — CsvHelper parses by header name), re-import overwriting manual assignments (Pitfall 10), mass assignment via CSV data fields (role is never set from CSV)
**Stack:** CsvHelper 33.x, FluentValidation 11.x, EF Core transactions (upsert-by-email), React Hook Form + Zod (import confirmation UI)
**Research flag:** Drag-and-drop hierarchy builder with keyboard/touch fallback (Pitfall 14) — assess DnD library options (dnd-kit) during phase planning; select-and-confirm is the safe primary interaction

### Phase 4: Content & File Management
**Rationale:** Event content (briefing sections, maps, documents) must exist before publishing is meaningful. File upload architecture must be correct before any files are stored — retrofitting private storage is high-cost.
**Delivers:** Custom information sections (markdown + attachments, sort order), private file upload pipeline (pre-signed PUT → confirm flow via `AWSSDK.S3` + Cloudflare R2), document and map file hosting, external map link storage, markdown rendering with XSS sanitization.
**Addresses:** Custom information sections, document/map file upload & download (FEATURES.md P1); section ordering (P2 differentiator — low effort, include here)
**Avoids:** Public R2 bucket exposure (Pitfall 5), markdown XSS in briefing sections (Pitfall 12 — DOMPurify on frontend), file size limits not at HTTP layer (Pitfall 13 — configure in ASP.NET Core `RequestSizeLimitAttribute` + Kestrel), serving files without auth
**Stack:** AWSSDK.S3 3.7.x + Cloudflare R2, `GetPreSignedURL` (PUT for upload, GET 24h TTL for download), react-markdown + remark-gfm + DOMPurify, react-dropzone

### Phase 5: Publish, Notifications & Change Requests
**Rationale:** Publishing is the platform's "moment of value" — the culmination of all prior phases. Must be implemented correctly (state machine, async notifications) because errors here affect 800 players simultaneously. Roster change requests require auth + RBAC + roster to already exist.
**Delivers:** Event state machine (draft → published → archived), async email notification blast via `NotificationWorker` (`BackgroundService + Channel<NotificationJob>`), roster change request workflow (submit → approve/deny → notify outcome), email deliverability setup (SPF/DKIM/DMARC).
**Addresses:** Event publish + notification blast, roster change request workflow (FEATURES.md P1)
**Avoids:** Synchronous bulk email (Pitfall 3 — `BackgroundService + Channel<T>` queue), irreversible publish without draft state (Pitfall 7), notification blast reputation damage (Pitfall 11), publish and notify as single action (UX pitfall)
**Stack:** Resend .NET SDK (`IResend.EmailSendAsync`), `BackgroundService + Channel<NotificationJob>`, event `status` field with transition guards; upgrade path to Hangfire 1.8.23 + `Hangfire.PostgreSql` if restart-survival is needed
**Research flag:** Resend free tier limits (3k/month) and per-request throughput for 800-recipient events — verify at implementation time; Resend sends one API call per recipient in basic mode (batch endpoint is REST-only, not SDK-level)

### Phase 6: Player Experience & Mobile Polish
**Rationale:** Player-facing views can only be built once roster data, hierarchy, content, and notifications are all working. This phase is the final delivery surface — it's what every player sees. Mobile quality is non-negotiable (commanders and players are in the field).
**Delivers:** Player event dashboard (assignment + unit + docs + info in one view), event detail page (sections, files, hierarchy display), mobile-optimized layouts (44px touch targets, responsive), offline-capable document download flow (`Content-Disposition: attachment` header from R2 pre-signed GET URL), player change request status visibility.
**Addresses:** Player-facing event dashboard, responsive mobile UI (FEATURES.md P1); callsign-prominent UI (P2 differentiator — include here as low-effort)
**Avoids:** Touch targets too small for mobile (Pitfall 8), hierarchy rebuilt on every page load instead of flat JOIN (Architecture anti-pattern 4 — single EF Core query with `.Include()` across platoons/squads, build tree in service layer), no search/filter on 800-player roster table
**Stack:** React 19 + React Router v7, TanStack Query v5 (client-side caching + optimistic updates), shadcn/ui (accessible mobile components), Tailwind CSS 4 (mobile-first utilities), date-fns (event countdown)

### Phase Ordering Rationale

- **Auth before everything**: Every ASP.NET Core route requires authenticated user context via `[Authorize]`; RBAC policies must be registered in `Program.cs` before any controller uses them
- **EF Core schema before services**: Migrations are the contract; services coded against unstable schema produce expensive rework; commit all migrations before writing service layer
- **Roster before hierarchy assignment**: `event_roster` rows must exist before `squad_id` can be populated; CsvHelper import creates the users
- **Content before publish**: Publishing an empty event with no sections, no maps, and no roster is useless; publish is only meaningful once content exists
- **Notifications before player experience**: Players need to know the event exists before they visit the dashboard; the `NotificationWorker` email is the entry point
- **Player experience last**: It's the most visible layer but entirely dependent on all prior phases being functional and the SPA having well-typed API responses to consume

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 3 (Hierarchy Builder):** Drag-and-drop library selection with keyboard/touch fallback requirement — dnd-kit is the leading option for React; Pitfall 14 is real; verify touch support on target devices before committing
- **Phase 5 (Notifications):** Resend .NET SDK sends one API call per recipient; verify whether Resend's batch REST endpoint has a .NET SDK wrapper or requires raw `HttpClient` calls; also confirm 3k/month free tier covers event blasts before first production use

Phases with standard, well-documented patterns (skip research-phase):
- **Phase 1 (Auth):** ASP.NET Core Identity magic-link pattern is fully documented in official MS docs; `GenerateUserTokenAsync` + `VerifyUserTokenAsync` is the canonical implementation
- **Phase 2 (Core CRUD):** Standard ASP.NET Core Controller + EF Core CRUD — well-documented, no research needed
- **Phase 4 (Files):** AWSSDK.S3 + Cloudflare R2 pre-signed URL pattern is canonical S3 architecture; `ForcePathStyle = true` + custom `ServiceURL` is the R2 configuration pattern
- **Phase 6 (Player UI):** React 19 + shadcn/ui Vite-native + Tailwind 4 — established patterns with official docs; no novel integration risks

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All core packages verified against live NuGet and npm registries as of 2026-03-12. Npgsql 10.0.1 (released 2026-03-12) confirmed as the .NET 10 PostgreSQL provider. shadcn/ui Vite-native init confirmed. React Router v7.13.1 current. |
| Features | HIGH | Derived directly from PROJECT.md requirements + comparative analysis of TeamSnap, RunSignup, and MilSim West current state. Feature dependency map is sound and validated against architecture constraints. |
| Architecture | HIGH | Patterns (ASP.NET Core Authorization + scope guards, pre-signed URLs, two-phase CSV, state machine, thin controllers/fat services) are canonical for this class of application and well-documented in official MS docs. |
| Pitfalls | HIGH | Critical pitfalls sourced from OWASP official docs (Cheat Sheets), Postmark best practices, and Google web.dev. Domain-specific pitfalls (CSV column order, re-import behavior) are grounded in direct PROJECT.md analysis. |

**Overall confidence:** HIGH

### Gaps to Address

- **Drag-and-drop library selection** (Phase 3): The hierarchy builder UX requires DnD with touch/keyboard fallback. dnd-kit is the current leading React DnD library (react-beautiful-dnd is deprecated), but touch support on mobile commanders' tablets needs verification during Phase 3 planning. Fallback: select-and-confirm dropdown UI is the safer primary interaction and must exist regardless.
- **Resend batch throughput** (Phase 5): The .NET Resend SDK calls `EmailSendAsync` per message. For 800 recipients the `BackgroundService + Channel<T>` queue handles this safely, but verify Resend's per-second rate limits and whether the REST batch endpoint (`/emails/batch`) has SDK support or requires raw `HttpClient`. If rate-limited, add delay between sends in `NotificationWorker`.
- **CORS configuration for R2 pre-signed PUT** (Phase 4): Browser-side direct-to-R2 uploads via pre-signed PUT URLs require correct CORS headers on the R2 bucket (`AllowedOrigins`, `AllowedMethods: PUT`). This is a deployment configuration step, not a code step — document in the launch checklist and verify in dev against local Vite dev server origin.
- **Multi-faction data model** (deferred to v2): The current schema supports a single faction per event. If multi-faction events (OPFOR + BLUFOR) become a requirement, the `event_roster` and hierarchy tables need significant additions. Flagged as v2+ in FEATURES.md — ensure v1 EF Core entities don't hard-block this with overly tight constraints.
- **Email deliverability setup timing**: SPF/DKIM/DMARC DNS records must be configured before the first production notification blast. This is a deployment prerequisite, not a code task — include in the launch checklist alongside R2 CORS config.

---

## Sources

### Primary (HIGH confidence)
- `https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.1` — Npgsql 10.0.1 for .NET 10; released 2026-03-12
- `https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization` — ASP.NET Core Identity for SPAs; cookie vs token modes; .NET 10 docs
- `https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity` — `UserManager.GenerateUserTokenAsync` magic-link token pattern
- `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services` — `BackgroundService` + `Channel<T>` queue pattern; .NET 10 docs
- `https://joshclose.github.io/CsvHelper/` — CsvHelper; RFC 4180 compliant, strongly-typed `GetRecords<T>()`, header-name parsing
- `https://resend.com/docs/send-with-dotnet` — Resend .NET SDK; DI setup, `IResend` interface, `EmailSendAsync`
- `https://learn.microsoft.com/en-us/ef/core/providers/` — EF Core providers; Npgsql listed as supported provider
- `https://www.hangfire.io/` — Hangfire 1.8.23 (upgrade path); PostgreSQL storage; .NET compatibility
- `https://ui.shadcn.com/docs/installation/vite` — shadcn/ui Vite-native init (`pnpm dlx shadcn@latest init -t vite`) confirmed
- `https://reactrouter.com/home` — React Router v7.13.1; three modes (Declarative, Data, Framework); declarative = SPA
- `https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html` — OWASP Authorization; IDOR prevention
- `https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html` — OWASP File Upload; private storage requirement
- `https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html` — OWASP Input Validation; server-side validation
- `https://cheatsheetseries.owasp.org/cheatsheets/Mass_Assignment_Cheat_Sheet.html` — OWASP Mass Assignment; CSV field allowlist
- `https://postmarkapp.com/guides/transactional-email-best-practices` — Postmark 2026; email deliverability, SPF/DKIM/DMARC
- `https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html` — AWS S3; pre-signed URL pattern (applies to R2)
- `https://auth0.com/blog/refresh-tokens-what-are-they-and-when-to-use-them/` — JWT + refresh token strategy
- `https://web.dev/articles/responsive-web-design-basics` — Google web.dev; mobile touch targets, responsive design
- `milsimwest.com` — MilSim West; current state of milsim event info distribution (direct observation)
- `teamsnap.com` — TeamSnap; sports team management feature comparison
- `info.runsignup.com` — RunSignup; event participant management feature comparison
- PROJECT.md requirements — primary source of feature scope (HIGH confidence — direct spec)

### Secondary (MEDIUM confidence)
- `https://www.npgsql.org/efcore/index.html` — Npgsql EF Core provider docs; EF 9 config shown; v10 follows same API
- Cloudflare R2 `.NET` docs (training data + community) — R2 S3-compatible endpoint config (`ServiceURL` + `ForcePathStyle = true`) is well-documented in community despite official page 404
- Zod v3 vs v4 ecosystem state — `@hookform/resolvers` primarily supports v3; Zod v4 is a separate `zod/v4` package; using v3 is correct for current ecosystem
- TanStack Router v1 evaluation — excellent type-safe routing; React Router v7 wins on ecosystem breadth for this app

---
*Research completed: 2026-03-12*
*Ready for roadmap: yes*
