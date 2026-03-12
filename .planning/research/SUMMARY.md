# Project Research Summary

**Project:** Airsoft / MilSim Event Planning & Roster Management Platform
**Domain:** Event management / hierarchical roster coordination web app
**Researched:** 2026-03-12
**Confidence:** HIGH

## Executive Summary

This is a structured event coordination and roster management platform purpose-built for the airsoft/milsim community — a domain with no existing purpose-built tooling. Commanders currently cobble together 4–6 separate tools (registration systems, Squarespace, Discord, Google Drive, and email) to manage what this platform handles in one place: publishing a complete event briefing with structured unit assignments, downloadable maps, and guaranteed player notification. The recommended approach is a Next.js 15 (App Router) full-stack monolith backed by PostgreSQL and Drizzle ORM, with better-auth for RBAC, Resend for transactional email, and UploadThing for file storage. This is a proven, greenfield-optimized stack that keeps operational complexity minimal while covering every feature requirement.

The core architectural pattern is a layered monolith: thin route handlers delegate to a service layer containing all business logic, which accesses the database and external services through wrapper libraries. RBAC is enforced at two levels — role-level middleware AND object-level scope guards (event/faction ownership checks per query). CSV import is a two-phase operation (validate-then-commit). Files are stored privately in object storage with pre-signed URL access. Events follow a state machine (Draft → Published → Archived) where publishing and notifying are separate, explicit actions. These patterns are well-documented and essential to get right from the start — none are optional polish.

The primary risks cluster around three areas: authorization (IDOR vulnerabilities and scattered role checks are OWASP #1 and must be solved architecturally, not patched), email delivery (synchronous bulk email at 800+ recipients times out and corrupts event state — must be async from day one), and CSV data integrity (partial imports and silent column-mapping errors destroy commander trust — two-phase preview is mandatory). Every one of these failure modes is recoverable before launch only if addressed in the correct phase. The recommended build order from ARCHITECTURE.md — Foundation → Core Data Model → Roster/Hierarchy → Content/Files → Publish/Notifications → Player Experience — directly maps to a 6-phase roadmap and should be followed as specified.

---

## Key Findings

### Recommended Stack

The stack is well-established and optimized for this project's scale (~800 users, 8 events/year, small team). Next.js 15 with App Router provides the full-stack framework, React Server Components for fast page loads, and Route Handlers as REST API endpoints — eliminating the need for a separate backend service. PostgreSQL with Drizzle ORM is the correct choice for the Faction → Platoon → Squad → Player hierarchy: relational data with JSON column flexibility and full-text search, with a faster and lighter runtime than Prisma. better-auth replaces Lucia (archived) and is preferable to Clerk (per-user cost at 800 users) — it provides magic link, email/password, and a purpose-built RBAC `admin` plugin with `createAccessControl()` for defining the 5-role custom hierarchy. See [STACK.md](./STACK.md) for full alternatives analysis.

**Core technologies:**
- **Next.js 15 (App Router)**: Full-stack framework — RSC for performance, Route Handlers for REST API, built-in TypeScript
- **PostgreSQL 16 + Drizzle ORM 0.38+**: Relational DB + SQL-first ORM — optimal for hierarchy, serverless-ready, zero-dependency
- **better-auth 1.x**: Auth + RBAC — magic link plugin, admin plugin with custom role hierarchy, Drizzle adapter, self-hosted
- **Tailwind CSS 4 + shadcn/ui**: Styling — mobile-first utility CSS; owns the component code (Radix primitives, accessible)
- **Resend 4.x + react-email**: Transactional email — best DX, React templates, 3k/month free tier, batch send API
- **UploadThing 7.x**: File uploads — auth on your server, bandwidth on theirs, type-safe file router, Next.js native
- **Zod 4 + React Hook Form 7**: Validation + forms — Zod 4 stable March 2026; shared schemas across API and client
- **TanStack Query 5**: Client-side server-state — optimistic updates, cache invalidation for roster tables
- **Papa Parse 5**: CSV parsing — browser + Node, header-name parsing, handles malformed rows

**What NOT to use:** Lucia Auth (archived), Next.js Pages Router (legacy), moment.js, Formik, GraphQL, tRPC, Socket.io, Redux/Zustand for server state. See STACK.md for full rationale.

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

The recommended architecture is a layered monolith: a Next.js 15 full-stack app serving both UI (React Server Components) and API (Route Handlers), backed by a single PostgreSQL instance and private object storage. The service layer holds all business logic and is testable independent of HTTP. External integrations (email, storage) are abstracted behind thin library wrappers so providers can be swapped without touching service code. At the target scale (800 users, 8 events/year), no queue, cache, or horizontal scaling is needed — but the notification blast (800 emails) is the first scaling boundary and must be async from the start. See [ARCHITECTURE.md](./ARCHITECTURE.md) for full data model, data flow diagrams, and anti-patterns.

**Major components:**
1. **Next.js App Router (UI + API)** — React Server Components for pages, Route Handlers as REST endpoints, Turbopack dev server
2. **Auth + RBAC layer** — better-auth session management + custom `createAccessControl()` RBAC, short-lived JWTs + refresh, object-level scope guards per DB query
3. **Service layer** — EventSvc, RosterSvc, HierarchySvc, FileSvc, NotificationSvc — all business logic, orchestrates DB + storage + email
4. **PostgreSQL + Drizzle ORM** — relational schema: users → factions → events → platoons → squads → event_roster; UUIDs on all public-facing IDs
5. **UploadThing / private object storage** — files in private bucket; pre-signed URL access only; metadata in DB; never proxied through API
6. **Resend + react-email** — transactional email with React templates; event publish notification is the critical path

**Key patterns to follow:**
- **Flat RBAC + scope guards**: Role enum defines hierarchy level; every mutation checks BOTH role AND resource ownership
- **Two-phase CSV import**: Validate all rows first → show preview → commander confirms → single DB transaction with upsert-by-email
- **Pre-signed URL file access**: API generates short-lived signed URLs; file bytes never flow through the application server
- **Publish-gated event state machine**: `draft → published → archived`; publish and notify are separate, explicit actions
- **Service layer boundary**: Route handlers are glue only; zero business logic in routes

### Critical Pitfalls

The full list of 16 pitfalls is in [PITFALLS.md](./PITFALLS.md). These 7 are critical — getting any one wrong requires expensive recovery and can destroy commander trust before the platform establishes credibility.

1. **Object-level authorization bypass (IDOR)** — Role middleware is not enough; every DB query must scope to the authenticated user's event/faction. Scope guard belongs in the service layer. Write integration tests proving User A cannot read User B's resources. (Phase 1 — foundational)
2. **Scattered role string comparisons** — Define a single `createAccessControl()` permission matrix at startup. All permission checks go through one `can(user, action, resource)` function. Never `if (role === 'faction_commander')` in business logic. (Phase 1 — foundational)
3. **Synchronous bulk email on publish** — 800 API calls in a request handler times out, causes partial sends, and triggers duplicate notifications on retry. Use Resend's batch send API; queue the job; return `202 Accepted`; show delivery progress in UI. (Phase 5 — email notifications)
4. **CSV import with no validation preview** — Partial imports leaving the DB in an inconsistent state destroy trust. Two-phase import is mandatory: validate all rows → preview with error list → commander confirms → single transaction with upsert-by-email. (Phase 3 — roster import)
5. **Files served without authentication** — Public S3 buckets expose operational security materials to anyone who guesses a URL. All files in private buckets; access only via authenticated API that generates 15-minute pre-signed URLs; UUIDs as filenames. (Phase 4 — file upload)
6. **Magic link tokens that are long-lived or reusable** — Tokens must be single-use (invalidated on first use), 15–60 minute expiry, 32+ bytes entropy. Use a two-step confirm page to mitigate email security scanners that auto-click links. (Phase 1 — auth)
7. **Irreversible event publish that triggers immediate notification** — Separate "Publish" (makes event visible, no email) from "Send Notifications" (explicit action). Provide draft preview. Allow updating published content without re-notifying. (Phase 5 — event state machine)

---

## Implications for Roadmap

The ARCHITECTURE.md build order is the correct phase structure. It reflects hard dependencies — each layer must be stable before the next. The 6 phases map directly to architectural layers and feature dependencies identified in FEATURES.md.

### Phase 1: Foundation — Auth, RBAC & Database Schema
**Rationale:** Auth gates every other route. Schema contracts must be stable before services are built against them. RBAC must be architecturally correct from the start — retrofitting scope guards after the fact is high-risk. This phase has the highest concentration of security pitfalls.
**Delivers:** Working auth system (email/password + magic link), 5-role RBAC with scope guards, full database schema with migrations, UUID primary keys on all tables.
**Addresses:** Auth feature, RBAC feature (from FEATURES.md table stakes)
**Avoids:** IDOR authorization bypass (Pitfall 1), scattered role comparisons (Pitfall 2), long-lived magic link tokens (Pitfall 6)
**Stack:** Next.js 15, PostgreSQL + Drizzle ORM, better-auth (magicLink + admin plugins), Zod 4, Resend (for magic link email only)

### Phase 2: Core Data Model — Events, Factions & User Management
**Rationale:** Events are the root object — nothing else can be created without them. Faction and user management are prerequisites for assigning commanders and enabling scoped access. This is a thin CRUD layer once auth and schema are in place.
**Delivers:** Event CRUD (draft state only), faction management, admin user management, event-scoped access working correctly.
**Addresses:** Event creation & management feature (from FEATURES.md table stakes)
**Avoids:** Single-admin-role anti-pattern (Pitfall 5 in PITFALLS.md), event ID enumeration via sequential IDs
**Stack:** Next.js Route Handlers, Drizzle ORM, TanStack Query (client-side)

### Phase 3: Roster Import & Hierarchy Builder
**Rationale:** Players cannot be assigned to units until they're imported. Hierarchy assignment cannot be validated until roster players exist. This is the most complex commander workflow and the highest-risk phase for data integrity bugs.
**Delivers:** Two-phase CSV import with per-row validation preview, player account activation via magic link, platoon/squad CRUD, player assignment to squads (nullable squad_id in event_roster).
**Addresses:** CSV roster import, hierarchy builder, player assignment (FEATURES.md P1)
**Avoids:** CSV partial import / no preview (Pitfall 4), CSV column-position assumption (Pitfall 9), re-import overwriting manual assignments (Pitfall 10), mass assignment via CSV data fields (security pitfall)
**Stack:** Papa Parse (CSV), Zod (row validation), React Hook Form (import confirmation UI), Drizzle transactions (upsert-by-email)
**Research flag:** Drag-and-drop hierarchy builder with keyboard/touch fallback (Pitfall 14) — assess DnD library options during phase planning

### Phase 4: Content & File Management
**Rationale:** Event content (briefing sections, maps, documents) must exist before publishing is meaningful. File upload architecture must be correct before any files are stored — retrofitting private storage is high-cost.
**Delivers:** Custom information sections (markdown + attachments, sort order), private file upload pipeline (pre-signed PUT → confirm flow), document and map file hosting, external map link storage, markdown rendering with XSS sanitization.
**Addresses:** Custom information sections, document/map file upload & download (FEATURES.md P1); section ordering (P2 differentiator — low effort, include here)
**Avoids:** Public S3 bucket exposure (Pitfall 5), markdown XSS in briefing sections (Pitfall 12), file size limits not at HTTP layer (Pitfall 13), serving files from webroot
**Stack:** UploadThing (file router), react-markdown + remark-gfm + DOMPurify (markdown rendering + sanitization), nuqs (section sort state)

### Phase 5: Publish, Notifications & Change Requests
**Rationale:** Publishing is the platform's "moment of value" — the culmination of all prior phases. Must be implemented correctly (state machine, async notifications) because errors here affect 800 players simultaneously. Roster change requests require auth + RBAC + roster to already exist.
**Delivers:** Event state machine (draft → published → archived), async email notification blast to all roster players, roster change request workflow (submit → approve/deny → notify), email deliverability setup (SPF/DKIM/DMARC).
**Addresses:** Event publish + notification blast, roster change request workflow (FEATURES.md P1)
**Avoids:** Synchronous bulk email (Pitfall 3), irreversible publish without draft state (Pitfall 7), notification blast reputation damage (Pitfall 11), publish and notify as single action (UX pitfall)
**Stack:** Resend batch send API, react-email (notification templates), event status field + state transition guards
**Research flag:** Resend batch API limits and rate throttling for 800-recipient events — verify during phase planning

### Phase 6: Player Experience & Mobile Polish
**Rationale:** Player-facing views can only be built once roster data, hierarchy, content, and notifications are all working. This phase is the final delivery surface — it's what every player sees. Mobile quality is non-negotiable (commanders and players are in the field).
**Delivers:** Player event dashboard (assignment + unit + docs + info in one view), event detail page (sections, files, hierarchy display), mobile-optimized layouts (44px touch targets, responsive), offline-capable document download flow (`Content-Disposition: attachment`), player change request status visibility.
**Addresses:** Player-facing event dashboard, responsive mobile UI (FEATURES.md P1); callsign-prominent UI (P2 differentiator — include here as low-effort)
**Avoids:** Touch targets too small for mobile (Pitfall 8), hierarchy rebuilt on every page load instead of flat join (Architecture anti-pattern 4), no search/filter on 800-player roster table
**Stack:** React Server Components (server-rendered player views), TanStack Query (client-side optimistic updates), shadcn/ui (accessible mobile components), date-fns (event countdown)

### Phase Ordering Rationale

- **Auth before everything**: Every route requires authenticated user context; RBAC must be architecturally sound before any feature data is added
- **Schema before services**: Drizzle schema migrations are the contract; services coded against unstable schema produce expensive rework
- **Roster before hierarchy assignment**: `event_roster` rows must exist before `squad_id` can be populated; import creates the users
- **Content before publish**: Publishing an empty event with no sections, no maps, and no roster is useless; publish is only meaningful once content exists
- **Notifications before player experience**: Players need to know the event exists before they visit the dashboard; the notification is the entry point
- **Player experience last**: It's the most visible layer but entirely dependent on all prior phases being functional

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 3 (Hierarchy Builder):** Drag-and-drop library selection with keyboard/touch fallback requirement — options like dnd-kit, @dnd-kit/core, or react-beautiful-dnd (deprecated) need evaluation; Pitfall 14 is real
- **Phase 5 (Notifications):** Resend batch send API limits, rate throttling, and subdomain reputation isolation strategy — verify current limits before designing the notification dispatch architecture

Phases with standard, well-documented patterns (skip research-phase):
- **Phase 1 (Auth):** better-auth magic link + admin plugin is well-documented with Drizzle adapter; implementation is mechanical
- **Phase 2 (Core CRUD):** Standard Next.js Route Handler + Drizzle CRUD — well-documented, no research needed
- **Phase 4 (Files):** UploadThing + Next.js App Router is documented and supported; pre-signed URL pattern is canonical S3 architecture
- **Phase 6 (Player UI):** React Server Components + shadcn/ui + Tailwind mobile-first — established patterns with official docs

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Core stack (Next.js 15, PostgreSQL, Drizzle, better-auth, Resend, UploadThing) verified via official docs and live sites. Version compatibility matrix confirmed. Zod 4 stable confirmed March 2026. |
| Features | HIGH | Derived directly from PROJECT.md requirements + comparative analysis of TeamSnap, RunSignup, and MilSim West current state. Feature dependency map is sound. |
| Architecture | HIGH | Patterns (RBAC + scope guards, pre-signed URLs, two-phase CSV, state machine, service layer) are well-established for this class of application. No single authoritative source needed — these are canonical. |
| Pitfalls | HIGH | Critical pitfalls sourced from OWASP official docs (Cheat Sheets), Postmark best practices, and Google web.dev. Domain-specific pitfalls (CSV column order, re-import behavior) are grounded in direct PROJECT.md analysis. |

**Overall confidence:** HIGH

### Gaps to Address

- **Drag-and-drop library selection** (Phase 3): The hierarchy builder UX requires DnD with touch/keyboard fallback. React Beautiful DnD is deprecated; dnd-kit is the likely replacement but needs evaluation during Phase 3 planning. Fallback: select-and-confirm UI is the safer primary interaction.
- **Resend batch limits** (Phase 5): The notification blast for 800 players needs verification against Resend's current batch send API limits and per-second rate caps. Plan for queuing even if Resend handles the batch — prevents timeout on the publish endpoint. Verify current limits at implementation time.
- **Multi-faction data model** (deferred to v2): The current schema supports a single faction per event. If multi-faction events (OPFOR + BLUFOR) become a requirement, the event_roster and hierarchy tables need significant additions. Flagged as v2+ in FEATURES.md — don't let v1 implementation lock this out unnecessarily.
- **Email deliverability setup timing**: SPF/DKIM/DMARC DNS records must be configured before the first production notification blast. This is a deployment prerequisite, not a code task — ensure it's in the launch checklist.

---

## Sources

### Primary (HIGH confidence)
- `https://nextjs.org/blog/next-15` — Next.js 15 release notes; App Router, Turbopack stable, React 19 compatibility
- `https://orm.drizzle.team/docs/overview` — Drizzle ORM v1 RC; serverless-ready, zero-dependency confirmed
- `https://better-auth.com/docs/introduction` — better-auth 1.x; magic link plugin, admin RBAC plugin, Drizzle adapter
- `https://better-auth.com/docs/plugins/magic-link` — magic-link plugin configuration
- `https://better-auth.com/docs/plugins/admin` — admin plugin with `createAccessControl()`, custom roles
- `https://resend.com/` — Resend SDK 4.x; React Email integration, pricing, batch send
- `https://uploadthing.com/` — UploadThing 7.x; auth model, file router, Next.js integration
- `https://ui.shadcn.com/docs` — shadcn/ui; Tailwind v4 support confirmed
- `https://zod.dev/` — Zod 4 stable (March 2026)
- `https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html` — OWASP Authorization; IDOR prevention
- `https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html` — OWASP File Upload; private storage requirement
- `https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html` — OWASP Input Validation; server-side validation
- `https://cheatsheetseries.owasp.org/cheatsheets/Mass_Assignment_Cheat_Sheet.html` — OWASP Mass Assignment; CSV field allowlist
- `https://postmarkapp.com/guides/transactional-email-best-practices` — Postmark 2026; email deliverability, SPF/DKIM/DMARC
- `https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html` — AWS S3; pre-signed URL pattern
- `https://auth0.com/blog/refresh-tokens-what-are-they-and-when-to-use-them/` — Auth0; JWT + refresh token strategy
- `https://web.dev/articles/responsive-web-design-basics` — Google web.dev; mobile touch targets, responsive design
- `milsimwest.com` — MilSim West; current state of milsim event info distribution (direct observation)
- `teamsnap.com` — TeamSnap; sports team management feature comparison
- `info.runsignup.com` — RunSignup; event participant management feature comparison
- PROJECT.md requirements — primary source of feature scope (HIGH confidence — direct spec)

### Secondary (MEDIUM confidence)
- TanStack Query v5 patterns — training data; version verified via official site
- date-fns v3, Papa Parse v5, nuqs v2 — training data; version numbers from official sites

---
*Research completed: 2026-03-12*
*Ready for roadmap: yes*
