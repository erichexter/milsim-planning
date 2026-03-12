# Architecture Research

**Domain:** Event management / roster management platform (airsoft/milsim)
**Researched:** 2026-03-12
**Confidence:** HIGH — patterns are well-established for this class of application at this scale

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                       CLIENT LAYER                               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Static SPA (Vite/React)  — CDN-hosted                   │   │
│  │  Mobile-first responsive UI                              │   │
│  │  Auth state (JWT/session token)                          │   │
│  └──────────────────────┬───────────────────────────────────┘   │
└─────────────────────────┼───────────────────────────────────────┘
                          │ HTTPS  (REST API)
┌─────────────────────────┼───────────────────────────────────────┐
│                    API SERVER LAYER                              │
│  ┌──────────────────────┼───────────────────────────────────┐   │
│  │              API Server (Node/Hono or Express)            │   │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────────────┐  │   │
│  │  │   Auth     │  │   RBAC     │  │  Route Handlers    │  │   │
│  │  │ Middleware │  │ Middleware │  │  (resource routers)│  │   │
│  │  └────────────┘  └────────────┘  └────────────────────┘  │   │
│  │  ┌────────────────────────────────────────────────────┐   │   │
│  │  │           Service Layer (business logic)           │   │   │
│  │  │  EventSvc  RosterSvc  HierarchySvc  FileSvc  ...   │   │   │
│  │  └────────────────────────────────────────────────────┘   │   │
│  └────────────────────────────────────────────────────────┘   │
└──────┬───────────────────┬──────────────┬───────────────────────┘
       │                   │              │
┌──────┴──────┐  ┌─────────┴──────┐  ┌───┴──────────────────────┐
│  Database   │  │  File Storage  │  │  Email Service           │
│  (Postgres) │  │  (S3/R2/equiv) │  │  (SendGrid or equiv)     │
└─────────────┘  └────────────────┘  └──────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Notes |
|-----------|----------------|-------|
| Static SPA | All UI rendering, client-side routing, file download links | CDN-hosted; no SSR needed at this scale |
| API Server | Auth, authorization, all business logic, DB access, queue dispatch | Single deployable unit; no microservices needed |
| Auth Middleware | Validate JWT / magic-link token; attach user + role to request context | Runs on every authenticated route |
| RBAC Middleware | Check role against required permission for route; enforce org-scoped access | Role hierarchy: Admin > Commander > Platoon Leader > Squad Leader > Player |
| Route Handlers | Thin HTTP layer — parse request, call service, return response | No business logic here |
| Service Layer | Enforce business rules, orchestrate DB + storage + email | Testable independent of HTTP |
| Database (Postgres) | Persistent store: users, events, factions, hierarchy, roster, requests | Relational model fits hierarchy well |
| File Storage | Documents, maps (PDF/PNG/KMZ), and CSV uploads | Object storage, not DB blobs |
| Email Service | Transactional email (publish notifications, magic links, request updates) | External provider; API calls from service layer |

---

## Recommended Project Structure

### Backend (API Server)

```
api/
├── src/
│   ├── index.ts                # Server entry point, middleware wiring
│   ├── config.ts               # Env vars, validated at startup
│   ├── db/
│   │   ├── client.ts           # DB connection (Drizzle/Prisma instance)
│   │   ├── schema/             # Table definitions (one file per domain)
│   │   │   ├── users.ts
│   │   │   ├── events.ts
│   │   │   ├── factions.ts
│   │   │   ├── hierarchy.ts    # platoons, squads
│   │   │   ├── roster.ts       # event_player, assignments
│   │   │   └── files.ts
│   │   └── migrations/         # SQL migration files
│   ├── middleware/
│   │   ├── auth.ts             # JWT validation, session resolution
│   │   └── rbac.ts             # Role / permission enforcement
│   ├── routes/
│   │   ├── auth.ts             # login, magic-link, logout
│   │   ├── events.ts           # CRUD + publish
│   │   ├── factions.ts         # faction management
│   │   ├── hierarchy.ts        # platoon / squad CRUD
│   │   ├── roster.ts           # CSV import, assignments, change requests
│   │   ├── files.ts            # upload, download URL generation
│   │   └── users.ts            # user management (admin)
│   ├── services/
│   │   ├── auth.service.ts     # Token creation, magic-link generation
│   │   ├── event.service.ts    # Event business logic + publish orchestration
│   │   ├── roster.service.ts   # CSV parse, player upsert, assignment logic
│   │   ├── hierarchy.service.ts# Platoon/squad CRUD + cascade rules
│   │   ├── file.service.ts     # Storage pre-signed URL generation
│   │   ├── email.service.ts    # Template rendering + send calls
│   │   └── notification.service.ts # Batch notification dispatch
│   ├── lib/
│   │   ├── csv.ts              # CSV parse utilities
│   │   ├── storage.ts          # S3/R2 client wrapper
│   │   └── email.ts            # SendGrid/Resend client wrapper
│   └── types/
│       └── index.ts            # Shared TypeScript interfaces
└── package.json
```

### Frontend (Static SPA)

```
web/
├── src/
│   ├── main.tsx                # App entry, router setup
│   ├── lib/
│   │   ├── api.ts              # Typed API client (fetch wrapper)
│   │   ├── auth.ts             # Auth state, token storage
│   │   └── permissions.ts      # Client-side role checks (display only)
│   ├── pages/
│   │   ├── auth/               # Login, magic-link landing
│   │   ├── events/             # Event list, event detail, event editor
│   │   ├── roster/             # Roster view, import flow, assignments
│   │   ├── hierarchy/          # Platoon/squad builder
│   │   ├── files/              # Document/map upload and listing
│   │   └── admin/              # User management (Admin role only)
│   ├── components/
│   │   ├── ui/                 # Generic reusable components
│   │   ├── roster/             # Roster-specific components
│   │   ├── hierarchy/          # Tree/hierarchy display components
│   │   └── files/              # File upload, download card components
│   └── hooks/
│       ├── useAuth.ts
│       ├── useEvent.ts
│       └── useRoster.ts
└── package.json
```

### Structure Rationale

- **`routes/` (thin) + `services/` (fat):** Routes handle HTTP; services own business logic. Keeps logic testable without a running HTTP server.
- **`db/schema/` per domain:** One schema file per concept avoids merge conflicts and makes domain boundaries visible.
- **`lib/` wraps externals:** Storage, email, CSV parsing isolated here so swapping providers doesn't touch services.
- **`pages/` per feature area:** Collocates route, data fetching, and view logic. Avoids deeply nested component trees for a small team.

---

## Architectural Patterns

### Pattern 1: Flat RBAC with Scope Guards

**What:** Users have a single role (enum), and every API operation checks two things: (1) does the role permit this action? and (2) does the user's organization/event scope include this resource?

**When to use:** When role hierarchy is fixed and known upfront. Simpler than a full permissions table — no ACL rows to maintain.

**Trade-offs:**
- ✅ Simple to reason about, easy to audit
- ✅ Permissions are code, not DB rows — no sync bugs
- ❌ Adding a 6th role requires a code deploy, not a config change
- ❌ Cross-faction commander delegation (not in scope for v1) requires rethinking

**Example:**
```typescript
// middleware/rbac.ts
const ROLE_HIERARCHY = {
  system_admin:      5,
  faction_commander: 4,
  platoon_leader:    3,
  squad_leader:      2,
  player:            1,
};

export function requireRole(minimum: keyof typeof ROLE_HIERARCHY) {
  return (req, res, next) => {
    const userLevel = ROLE_HIERARCHY[req.user.role] ?? 0;
    const minLevel  = ROLE_HIERARCHY[minimum];
    if (userLevel < minLevel) return res.status(403).json({ error: 'Forbidden' });
    next();
  };
}

// Scope guard — verify event/faction belongs to requesting user's context
export async function assertEventScope(userId: string, eventId: string) {
  const event = await db.query.events.findFirst({ where: eq(events.id, eventId) });
  if (!event || event.factionId !== (await getFactionForUser(userId))) {
    throw new ForbiddenError('Event not in your scope');
  }
}
```

---

### Pattern 2: Service-Orchestrated CSV Import Pipeline

**What:** CSV import is a multi-step process handled entirely in the service layer. The route handler receives the file, the service validates → upserts users → creates roster rows → returns a structured result.

**When to use:** Any import that must be idempotent, produce per-row errors, and not partially commit.

**Trade-offs:**
- ✅ Re-import is safe (upsert on email/callsign)
- ✅ Per-row validation errors returned to UI before DB commit
- ✅ No background job needed at this scale (800 players, few seconds to process)
- ❌ Large CSV (>5k rows) would need async processing — not a concern here

**Example:**
```typescript
// services/roster.service.ts
export async function importRoster(eventId: string, csvBuffer: Buffer) {
  const rows = parseCSV(csvBuffer); // lib/csv.ts
  const errors: RowError[] = [];
  const players: Player[] = [];

  for (const [i, row] of rows.entries()) {
    const result = validateRow(row);
    if (!result.ok) { errors.push({ row: i + 1, ...result.error }); continue; }
    players.push(result.data);
  }

  if (errors.length > 0) return { ok: false, errors }; // reject entire import

  await db.transaction(async (tx) => {
    for (const player of players) {
      const user = await upsertUser(tx, player);
      await upsertEventRoster(tx, { eventId, userId: user.id, ...player });
    }
  });

  return { ok: true, imported: players.length };
}
```

---

### Pattern 3: Pre-Signed URL File Access

**What:** Files are stored in object storage (S3/R2). The API never proxies file bytes — it generates time-limited pre-signed URLs. The client downloads directly from storage.

**When to use:** Any file storage scenario. Critical for large files (maps, PDFs) and for not bottlenecking the API server during event-day traffic spikes.

**Trade-offs:**
- ✅ API server never touches file bytes — zero I/O load for downloads
- ✅ Pre-signed URL expiry enforces access control without token validation on storage tier
- ✅ Direct CDN/edge download speed for players in the field
- ❌ Pre-signed URL expiry means "permanent download links" aren't possible — use generous TTL (24h) for player downloads
- ❌ Requires client to request fresh URL after expiry

**Example:**
```typescript
// lib/storage.ts
export async function getDownloadUrl(key: string, ttlSeconds = 86400): Promise<string> {
  const command = new GetObjectCommand({ Bucket: BUCKET, Key: key });
  return getSignedUrl(s3Client, command, { expiresIn: ttlSeconds });
}

// routes/files.ts
router.get('/:fileId/download', requireRole('player'), async (req, res) => {
  const file = await db.query.files.findFirst({ where: eq(files.id, req.params.fileId) });
  if (!file) return res.status(404).json({ error: 'Not found' });
  const url = await getDownloadUrl(file.storageKey);
  res.json({ url }); // client redirects to this URL
});
```

---

### Pattern 4: Publish-Gated Event State Machine

**What:** Events move through a defined state machine: `draft → published → archived`. Transitions are explicit API actions, not just field updates.

**When to use:** Any resource where state changes trigger side effects (notifications). Guards against partial publishes and accidental re-notification.

**Trade-offs:**
- ✅ Notification blast is tied to state transition, not a field update — no double-send
- ✅ Players see consistent event state (either published or not)
- ✅ Archived events remain accessible but clearly closed
- ❌ Requires UI to drive explicit publish action (not auto-save)

**State transitions:**
```
draft ──publish──► published ──archive──► archived
  ▲                    │
  └─── (unpublish) ────┘   (only before notification sent)
```

---

## Data Flow

### Request Flow (Standard API Call)

```
Browser (SPA)
    │ HTTPS POST /api/events/:id/publish
    ▼
Auth Middleware
    │ Validates JWT → attaches req.user {id, role, factionId}
    ▼
RBAC Middleware
    │ requireRole('faction_commander') → passes
    ▼
Route Handler (routes/events.ts)
    │ Extracts params, calls service
    ▼
Event Service (services/event.service.ts)
    │ assertEventScope(userId, eventId)
    │ Validates state transition (draft → published)
    │ DB transaction: update event.status
    │ Calls notification.service.ts
    ▼
Notification Service
    │ Queries all roster players for event
    │ Calls email.service.ts for each player (or batch)
    ▼
Email Service (lib/email.ts)
    │ POST to SendGrid API
    ▼
Route Handler
    │ Returns 200 { status: 'published', notified: N }
    ▼
Browser updates UI state
```

### CSV Import Flow

```
Commander selects CSV file
    │
    ▼
SPA: POST /api/events/:id/roster/import  (multipart/form-data)
    │
    ▼
API: multer/busboy receives file buffer
    │
    ▼
RosterService.importRoster(eventId, buffer)
    ├── Parse CSV rows
    ├── Validate each row (required fields, email format)
    ├── If errors → return { ok: false, errors[] } → SPA shows per-row errors
    └── If clean → DB transaction:
            ├── upsertUser (by email)        ← creates account if new
            ├── upsertEventRoster row        ← idempotent re-import
            └── No assignment yet            ← hierarchy step is separate
    │
    ▼
SPA shows import summary; commander proceeds to hierarchy builder
```

### File Upload Flow

```
Commander selects file
    │
    ▼
SPA: POST /api/files/upload-url  { fileName, mimeType, eventId }
    │
    ▼
FileService: generates S3 pre-signed PUT URL + creates DB record (pending)
    │
    ▼
SPA: PUT [pre-signed URL]  (file bytes go directly to S3, not API)
    │
    ▼
SPA: POST /api/files/:id/confirm  (after upload completes)
    │
    ▼
API: marks DB record as confirmed
    │
    ▼
File is now available; download URLs generated on demand
```

### Authentication Flow — Magic Link

```
User enters email
    │
    ▼
POST /api/auth/magic-link  { email }
    │
    ▼
AuthService: generate signed token (JWT, 15min TTL) → send via email
    │
    ▼
User clicks link in email → GET /auth/verify?token=...
    │
    ▼
API validates token → creates session JWT (7d TTL) → sets cookie / returns token
    │
    ▼
SPA stores session, routes to dashboard
```

---

## Data Model (Key Relationships)

```
users
  id, email, callsign, display_name, role, created_at

factions
  id, name, created_by (→ users), created_at

events
  id, faction_id (→ factions), name, date, status (draft|published|archived)
  description_md, created_by (→ users)

platoons
  id, event_id (→ events), name, leader_id (→ users, nullable)

squads
  id, platoon_id (→ platoons), name, leader_id (→ users, nullable)

event_roster
  id, event_id (→ events), user_id (→ users),
  squad_id (→ squads, nullable),   ← assignment
  team_affiliation, callsign_override, imported_at

roster_change_requests
  id, event_roster_id (→ event_roster), requested_by (→ users),
  change_type, new_value, status (pending|approved|denied), resolved_by

event_sections                      ← custom info blocks
  id, event_id, title, body_md, sort_order

files
  id, event_id, uploaded_by, storage_key, file_name, mime_type,
  file_type (document|map), status (pending|confirmed), size_bytes

magic_link_tokens
  id, user_id, token_hash, expires_at, used_at
```

**Key design notes:**
- `event_roster` is the join between a user and an event — all hierarchy assignment flows through it
- Players are created at import time; their account is activated when they first log in (magic link)
- `squad_id` on `event_roster` is nullable — player is imported but unassigned until commander assigns
- Sections and files are both event-scoped; section `sort_order` enables drag-to-reorder

---

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 0–800 users, 8 events/year (current) | Monolith API server + single Postgres instance + S3/R2. No queue, no cache needed. Simple deploy works. |
| 800–5k users, more frequent events | Add read replica for Postgres. CDN cache for event detail page (short TTL). Rate-limit notification endpoint. |
| 5k+ users | Consider job queue for notification blasts (avoid request timeout on 5k emails). Horizontal API scaling behind load balancer. |

### Scaling Priorities (current target: ~800 users)

1. **First bottleneck: notification blast** — Publishing an event triggers N email API calls. At 800 players this is still fast enough to do synchronously (1–2s); above ~2k players, move to a background job queue (BullMQ or similar).
2. **Second bottleneck: file downloads during event day** — Mitigated by pre-signed URL pattern (API never proxies bytes). Storage tier handles load independently.
3. **Non-issue at this scale:** Database connection pooling (PgBouncer), caching, CDN for API responses.

---

## Anti-Patterns

### Anti-Pattern 1: Storing Files in the Database

**What people do:** Store PDF/image binary as a `BYTEA` column in Postgres.
**Why it's wrong:** Bloats database, makes backups huge, and means every file download eats a DB connection and API server memory. The API becomes the bottleneck during event-day downloads.
**Do this instead:** Object storage (S3/R2) + pre-signed URLs. DB stores only the metadata (key, name, size, type).

---

### Anti-Pattern 2: Encoding Role in JWT and Trusting It Forever

**What people do:** Put `role: 'faction_commander'` in the JWT and never re-validate against the DB.
**Why it's wrong:** If an admin demotes a user, their existing token still grants commander privileges until expiry. Especially dangerous for a platform where commanders can affect 800 players' event assignments.
**Do this instead:** Keep JWT TTL short (15–60 minutes) with a refresh token strategy, OR re-fetch role from DB on sensitive operations (promote/demote, publish). For this scale a short-lived access token + refresh is sufficient.

---

### Anti-Pattern 3: Business Logic in Route Handlers

**What people do:** Write all logic (validation, DB queries, email sending) directly in `router.post('/events', async (req, res) => { ... })`.
**Why it's wrong:** Untestable without a running HTTP server. Logic gets duplicated across routes. Hard to enforce consistent error handling.
**Do this instead:** Route handlers are glue only — parse params, call service, return response. Services contain all business logic and are tested independently.

---

### Anti-Pattern 4: Rebuilding Hierarchy Every Page Load

**What people do:** For each page load of the event roster view, recursively query all platoons → squads → players from scratch.
**Why it's wrong:** N+1 query pattern; slow with 800 players and multiple hierarchy levels.
**Do this instead:** Query the flat `event_roster` table with a single JOIN across platoons and squads. Build the hierarchy tree in the service layer (or frontend) from flat data. One query, fast at any realistic size.

---

### Anti-Pattern 5: One "Admin" Role with No Scope

**What people do:** Create a single "admin" role that can edit any event, any faction, any roster — no tenancy checks.
**Why it's wrong:** A Faction Commander for Red Faction can accidentally (or maliciously) edit Blue Faction's event.
**Do this instead:** Every mutation checks resource scope (`event.factionId === user.factionId`) in addition to role level. Scope guard is a service-layer responsibility, not just a route guard.

---

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Email (SendGrid/Resend/Postmark) | REST API call from `lib/email.ts`; wrap in thin client | Abstract provider behind interface so swapping is one-file change |
| Object Storage (S3/R2/Backblaze) | AWS SDK v3 (`@aws-sdk/client-s3`) for all providers | R2 is S3-compatible; minimal lock-in |
| External registration system | CSV export → manual upload → import pipeline | No API integration for v1; all data enters through `POST /roster/import` |
| External map platforms (CalTopo, Google MyMaps) | URL links stored in `files` table with `file_type = 'map_link'` | No embed; just hyperlinks |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| SPA ↔ API | REST over HTTPS, JSON responses | No WebSockets needed; no real-time requirements |
| API Routes ↔ Services | Direct function calls (same process) | No message bus needed at monolith scale |
| Services ↔ DB | ORM queries (Drizzle or Prisma) | No raw SQL except complex hierarchy queries |
| Services ↔ Storage | `lib/storage.ts` wrapper (pre-signed URLs) | File bytes never flow through API process |
| Services ↔ Email | `lib/email.ts` wrapper | Synchronous at current scale; async queue added when needed |

---

## Suggested Build Order

This sequence reflects hard dependencies — each layer must be stable before the next.

```
Phase 1: Foundation
├── Database schema + migrations (all tables)
├── Auth system (email/password + magic link)
└── RBAC middleware (role enforcement + scope guards)
        ↓ (auth gates everything else)
Phase 2: Core Data Model
├── User management (admin CRUD)
├── Event CRUD (draft state only)
└── Faction CRUD
        ↓ (events must exist before hierarchy or roster)
Phase 3: Roster + Hierarchy
├── CSV import pipeline
├── Player account activation (first magic link login)
├── Platoon + squad CRUD
└── Player assignment to squads
        ↓ (roster must be populated before publish is meaningful)
Phase 4: Content + Files
├── Event sections (markdown + sort order)
├── File upload pipeline (pre-signed URL flow)
└── Map resource management
        ↓ (content must exist before publish notifies anyone)
Phase 5: Publish + Notifications
├── Event state machine (draft → published → archived)
├── Email notification blast
└── Roster change request flow
        ↓
Phase 6: Player Experience
├── Player dashboard (assignment view)
├── Event detail page (sections, files, hierarchy)
└── Mobile optimization + offline download flow
```

**Dependency rationale:**
- Auth before everything — every route needs it
- Schema before code — services depend on stable table contracts
- CSV import before hierarchy assignment — can't assign unimported players
- Content/files before publish — publish with empty event is useless
- Notification before player experience — players need to know the event exists

---

## Sources

- Pattern analysis based on well-established REST API architecture for multi-tenant SaaS platforms (HIGH confidence — no single authoritative source; pattern is standard)
- Pre-signed URL pattern: AWS S3 documentation — https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html (HIGH confidence)
- JWT + refresh token best practices: https://auth0.com/blog/refresh-tokens-what-are-they-and-when-to-use-them/ (HIGH confidence)
- Event state machine pattern: standard practice in booking/event software (HIGH confidence — common domain knowledge)
- CSV upsert import pattern: common in workforce/roster management tooling (HIGH confidence — common domain knowledge)
- Note: External search was unavailable (no Brave API key); findings are based on established architectural patterns for this class of application. Core patterns (RBAC, pre-signed URLs, service layer separation, state machines) are stable and well-documented.

---
*Architecture research for: Airsoft/milsim event planning platform*
*Researched: 2026-03-12*
