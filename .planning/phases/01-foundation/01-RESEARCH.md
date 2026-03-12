# Phase 1: Foundation — Research

**Researched:** 2026-03-12
**Domain:** Authentication (email/password + magic link), RBAC (5-role hierarchy + scope guards), PostgreSQL schema bootstrapping
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| AUTH-01 | User can create an account via invitation email sent when imported via CSV | Covered: better-auth `admin.createUser()` creates the account; `magicLink` plugin sends the invitation link; Resend delivers the email |
| AUTH-02 | User can log in with email and password | Covered: better-auth `emailAndPassword` built-in; `signIn.email()` client API |
| AUTH-03 | User can log in via magic link sent to their email | Covered: better-auth `magicLink` plugin; `signIn.magicLink()` + `sendMagicLink` callback wired to Resend |
| AUTH-04 | User session persists across browser refresh | Covered: better-auth uses HTTP-only cookies with configurable TTL; `useSession()` hook rehydrates from cookie on reload |
| AUTH-05 | User can reset their password via email link | Covered: better-auth `emailAndPassword.sendResetPassword` callback + `requestPasswordReset()` / `resetPassword()` client APIs |
| AUTH-06 | User can log out from any page | Covered: better-auth `signOut()` client API; clears session cookie universally |
| AUTHZ-01 | System enforces five roles: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player | Covered: better-auth admin plugin `createAccessControl()` + `ac.newRole()` for 5 custom roles; stored in `user.role` field |
| AUTHZ-02 | Faction Commander has full administrative access to their event | Covered: RBAC permission matrix grants Commander all event-scoped actions; scope guard enforces `event.factionId === user.factionId` |
| AUTHZ-03 | Platoon Leader and Squad Leader have read-only access to roster and event information | Covered: RBAC permission matrix restricts write actions to Commander and above; read-only roles explicitly defined |
| AUTHZ-04 | Players can view roster, access event information, and submit roster change requests | Covered: Player role defined with view + request-submit permissions in the `createAccessControl()` matrix |
| AUTHZ-05 | Email addresses are visible only to leadership roles (Platoon Leader and above) | Covered: Column-level scoping in service layer — email field omitted from Player-role query projections |
| AUTHZ-06 | All data is scoped to the authenticated user event membership (no cross-event leakage) | Covered: Scope guard pattern — every query joins through `event_roster` to enforce membership; integration tests verify isolation |
</phase_requirements>

---

## Summary

Phase 1 establishes the security foundation that every subsequent phase depends on. It has three deliverables: (1) a working PostgreSQL schema with migrations for all tables, (2) a complete auth system covering email/password login, magic link login, session persistence, logout, and password reset, and (3) a 5-role RBAC system with both role-level permission checks and object-level event/faction scope guards. Getting any one of these wrong requires expensive, risky retrofits after data has accumulated.

The chosen stack (better-auth 1.x + Drizzle ORM 0.38+ + PostgreSQL 16 + Resend) is a tight integration with official docs available for every seam. The key implementation insight is that **invitation flow is not a custom system** — it reuses the magic link plugin: `auth.api.createUser()` (admin plugin) creates the account in a known-inactive state, then `auth.api.signInMagicLink()` (magic link plugin) sends the activation link. The user is not asked to set a password until after account activation; they arrive on a "set your password" page post magic-link auth.

The two highest-risk implementation decisions are the RBAC permission matrix and the scope guard. Both must be defined as shared modules in `lib/auth/` that every service imports — never inline `if (role === '...')` comparisons. This is the architectural boundary that prevents the most expensive class of security bugs in this application.

**Primary recommendation:** Build the Drizzle schema and run `npx auth@latest generate` first (schema contract before service code), then wire better-auth with all four plugins (emailAndPassword + magicLink + admin + `createAccessControl()`), then implement scope guards as pure functions tested independently of HTTP.

---

## Standard Stack

### Core (Phase 1 subset)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `better-auth` | 1.x | Auth + RBAC | Purpose-built magic link plugin, admin plugin with `createAccessControl()`, Drizzle adapter; self-hosted, no per-user billing |
| `drizzle-orm` | 0.38+ (v1 RC) | ORM + migrations | SQL-first, zero-dependency, serverless-ready; `drizzle-kit generate/migrate` handles schema evolution |
| `postgres` (postgres.js) | 3.x | DB driver | Better TypeScript support than `pg`; use with `drizzle-orm/postgres-js` |
| `resend` | 4.x | Transactional email | Required for magic link delivery + invitation emails; React Email templates |
| `react-email` + `@react-email/components` | 3.x / latest | Email template authoring | Write invitation/magic-link emails in React; eliminates table-based HTML |
| `zod` | 4.x | Schema validation | Stable March 2026; use for all API input validation, shared with client |
| `next` | 15.x (App Router) | Full-stack framework | Route Handler at `app/api/auth/[...all]/route.ts` via `toNextJsHandler(auth)` |

### Supporting (Phase 1)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `@hookform/resolvers` | 3.x | Zod + RHF bridge | Login / forgot-password forms |
| `react-hook-form` | 7.x | Form state | Auth forms (sign in, request reset, magic link request) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `better-auth` | Clerk | Clerk is excellent DX but $0.02+/MAU at 800 users; no data ownership |
| `better-auth` | Auth.js (NextAuth v5) | Auth.js magic link less mature; `better-auth` has purpose-built plugin |
| `better-auth` | Lucia Auth | Lucia archived late 2024 — do not use |
| `drizzle-orm` | Prisma | Prisma has better beginner docs but heavier runtime; Drizzle faster, lighter |
| `resend` | SendGrid | SendGrid works but worse DX, legacy UX; Resend has React Email integration |

**Installation:**
```bash
npm install better-auth drizzle-orm postgres resend react-email @react-email/components zod react-hook-form @hookform/resolvers
npm install -D drizzle-kit
```

---

## Architecture Patterns

### Recommended Project Structure (Phase 1 scope)
```
src/
├── app/
│   ├── api/
│   │   └── auth/
│   │       └── [...all]/
│   │           └── route.ts        # toNextJsHandler(auth) — catch-all
│   ├── (auth)/
│   │   ├── login/page.tsx
│   │   ├── magic-link/page.tsx     # "Send me a link" form
│   │   ├── magic-link/verify/page.tsx  # Two-step confirm (see Pitfall 6)
│   │   ├── forgot-password/page.tsx
│   │   └── reset-password/page.tsx
│   └── (app)/
│       └── layout.tsx              # Auth gate — redirect if no session
├── lib/
│   ├── auth/
│   │   ├── auth.ts                 # betterAuth() instance (server only)
│   │   ├── auth-client.ts          # createAuthClient() (client only)
│   │   └── permissions.ts          # createAccessControl() + 5 roles
│   ├── db/
│   │   ├── client.ts               # drizzle(postgres(...)) instance
│   │   └── schema/
│   │       ├── auth.ts             # better-auth generated tables
│   │       ├── users.ts            # extended user fields (callsign, etc.)
│   │       ├── events.ts
│   │       ├── factions.ts
│   │       └── index.ts            # barrel export
│   └── email/
│       └── client.ts               # Resend instance
├── emails/
│   ├── InvitationEmail.tsx
│   ├── MagicLinkEmail.tsx
│   └── PasswordResetEmail.tsx
└── middleware.ts                   # Next.js middleware for auth guard
```

### Pattern 1: better-auth Server Configuration
**What:** Single `auth.ts` file wires all plugins; exported as `auth`. Route handler at `app/api/auth/[...all]/route.ts` delegates to `toNextJsHandler(auth)`.
**When to use:** Always — all auth operations go through this instance.
**Example:**
```typescript
// Source: https://www.better-auth.com/docs/installation
// lib/auth/auth.ts
import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { magicLink } from "better-auth/plugins";
import { admin } from "better-auth/plugins";
import { db } from "@/lib/db/client";
import { ac, factionCommander, platoonLeader, squadLeader, player, systemAdmin } from "./permissions";
import { resend } from "@/lib/email/client";

export const auth = betterAuth({
  database: drizzleAdapter(db, { provider: "pg" }),
  emailAndPassword: {
    enabled: true,
    sendResetPassword: async ({ user, url }) => {
      await resend.emails.send({
        from: "MilSim Platform <noreply@yourdomain.com>",
        to: user.email,
        subject: "Reset your password",
        react: PasswordResetEmail({ url }),
      });
    },
  },
  plugins: [
    magicLink({
      expiresIn: 900, // 15 minutes (900 seconds) — within the 15-60 min spec
      sendMagicLink: async ({ email, url }) => {
        await resend.emails.send({
          from: "MilSim Platform <noreply@yourdomain.com>",
          to: email,
          subject: "Your sign-in link",
          react: MagicLinkEmail({ url }),
        });
      },
    }),
    admin({
      ac,
      roles: { systemAdmin, factionCommander, platoonLeader, squadLeader, player },
      defaultRole: "player",
    }),
  ],
});

// app/api/auth/[...all]/route.ts
import { auth } from "@/lib/auth/auth";
import { toNextJsHandler } from "better-auth/next-js";
export const { POST, GET } = toNextJsHandler(auth);
```

### Pattern 2: RBAC Permission Matrix with `createAccessControl()`
**What:** Define all 5 roles and their resource-action permissions in one canonical file. All permission checks use `auth.api.userHasPermission()` or the client-side `authClient.admin.hasPermission()`. Never raw string comparisons.
**When to use:** Every authorization decision in service layer code.
**Example:**
```typescript
// Source: https://www.better-auth.com/docs/plugins/admin#create-access-control
// lib/auth/permissions.ts
import { createAccessControl } from "better-auth/plugins/access";

export const statement = {
  event:   ["create", "read", "update", "delete", "publish"],
  roster:  ["import", "read", "assign", "update"],
  member:  ["read", "read-email", "invite"],
  section: ["create", "read", "update", "delete"],
  file:    ["upload", "read", "delete"],
  request: ["submit", "read", "approve", "deny"],
} as const;

export const ac = createAccessControl(statement);

export const player = ac.newRole({
  event:   ["read"],
  roster:  ["read"],
  member:  ["read"],          // NO read-email
  section: ["read"],
  file:    ["read"],
  request: ["submit", "read"],
});

export const squadLeader = ac.newRole({
  ...player.statements,
  // Squad leaders have read-only + all player permissions
});

export const platoonLeader = ac.newRole({
  ...squadLeader.statements,
  member: ["read", "read-email"],  // Can see email addresses
});

export const factionCommander = ac.newRole({
  event:   ["create", "read", "update", "delete", "publish"],
  roster:  ["import", "read", "assign", "update"],
  member:  ["read", "read-email", "invite"],
  section: ["create", "read", "update", "delete"],
  file:    ["upload", "read", "delete"],
  request: ["read", "approve", "deny"],
});

export const systemAdmin = ac.newRole({
  ...factionCommander.statements,
  // Full access — inherit all from commander, add system-level
});
```

### Pattern 3: Object-Level Scope Guard
**What:** Every data mutation and sensitive read checks BOTH the role permission AND resource ownership (event belongs to user's faction, user is a member of that event). This is a pure function in the service layer, not in route handlers.
**When to use:** Every service function that accesses event-scoped data.
**Example:**
```typescript
// lib/auth/scope-guards.ts
import { db } from "@/lib/db/client";
import { events, eventRoster } from "@/lib/db/schema";
import { eq, and } from "drizzle-orm";

export class ForbiddenError extends Error {
  constructor(message = "Forbidden") { super(message); this.name = "ForbiddenError"; }
}

/**
 * Verifies the user is a member of the event (via event_roster).
 * Throws ForbiddenError if not. Returns the roster row on success.
 */
export async function assertEventMembership(userId: string, eventId: string) {
  const row = await db.query.eventRoster.findFirst({
    where: and(
      eq(eventRoster.userId, userId),
      eq(eventRoster.eventId, eventId)
    ),
  });
  if (!row) throw new ForbiddenError("User is not a member of this event");
  return row;
}

/**
 * Verifies the authenticated user owns (commands) this event via faction.
 * Use for mutations that require Faction Commander role.
 */
export async function assertEventOwnership(userId: string, eventId: string) {
  const event = await db.query.events.findFirst({
    where: eq(events.id, eventId),
    with: { faction: true },
  });
  if (!event) throw new ForbiddenError("Event not found");
  if (event.faction.createdBy !== userId) throw new ForbiddenError("Not your event");
  return event;
}
```

### Pattern 4: Invitation Flow (AUTH-01)
**What:** Account creation via admin plugin + magic link activation. Accounts are created in an inactive/no-password state; activation happens via magic link on first login.
**When to use:** CSV import triggers invitation (Phase 2/3); the same pattern should be established here for manual invitation in Phase 1.
**Example:**
```typescript
// services/invitation.service.ts
// Source: https://www.better-auth.com/docs/plugins/admin#create-user
import { auth } from "@/lib/auth/auth";
import { resend } from "@/lib/email/client";

export async function inviteUser(email: string, name: string, role: string) {
  // Step 1: Create the user account (no password — activation via magic link)
  const { user } = await auth.api.createUser({
    body: { email, name, role, password: crypto.randomUUID() }, // random pw, never used
  });

  // Step 2: Send a magic link as the "invitation" — user activates on click
  await auth.api.signInMagicLink({
    body: {
      email,
      callbackURL: "/dashboard",    // where to land after activation
      newUserCallbackURL: "/welcome", // first-time user welcome flow
    },
  });

  return user;
}
```

### Pattern 5: Two-Step Magic Link Confirm (Pitfall 6 mitigation)
**What:** Magic link URL redirects to a server-rendered page showing "Click to sign in" — the actual auth action fires only on that button click, not on URL load. Prevents email security scanners from consuming the token.
**When to use:** Always — wire this into the `callbackURL` for magic links.
**Example:**
```typescript
// app/(auth)/magic-link/verify/page.tsx
// Token arrives in URL: /magic-link/verify?token=...
// This page shows a button. The button calls authClient.magicLink.verify()
// Source: https://www.better-auth.com/docs/plugins/magic-link#verify-magic-link

"use client";
import { authClient } from "@/lib/auth/auth-client";
import { useSearchParams, useRouter } from "next/navigation";

export default function MagicLinkVerifyPage() {
  const params = useSearchParams();
  const router = useRouter();
  const token = params.get("token");

  async function handleConfirm() {
    const { error } = await authClient.magicLink.verify({
      query: { token: token!, callbackURL: "/dashboard" },
    });
    if (error) router.push(`/login?error=${error.message}`);
  }

  return (
    <button onClick={handleConfirm}>Click to sign in</button>
  );
}
```

### Pattern 6: Next.js Middleware Auth Guard
**What:** `middleware.ts` at the root uses better-auth session check to protect all `(app)` routes. Unauthenticated requests redirect to `/login`.
**When to use:** Route-level protection for the entire authenticated app shell.
**Example:**
```typescript
// middleware.ts
import { auth } from "@/lib/auth/auth";
import { NextRequest, NextResponse } from "next/server";

export async function middleware(request: NextRequest) {
  const session = await auth.api.getSession({ headers: request.headers });
  const isAuthRoute = request.nextUrl.pathname.startsWith("/login") ||
                      request.nextUrl.pathname.startsWith("/magic-link") ||
                      request.nextUrl.pathname.startsWith("/forgot-password") ||
                      request.nextUrl.pathname.startsWith("/reset-password");

  if (!session && !isAuthRoute) {
    return NextResponse.redirect(new URL("/login", request.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico).*)"],
};
```

### Anti-Patterns to Avoid
- **Inline role comparisons:** `if (session.user.role === 'faction_commander')` scattered in route handlers — put ALL permission logic in `permissions.ts` + service layer scope guards
- **JWT role trust:** Never read `user.role` from a long-lived JWT without re-checking against DB for sensitive mutations. better-auth sessions re-validate on `getSession()`.
- **Skipping the two-step magic link confirm page:** Email security scanners will consume single-use tokens if the URL itself is the auth action
- **Public better-auth schema tables with sequential IDs:** better-auth generates UUIDs by default — verify `user.id` is UUID in generated schema; do NOT add sequential integer IDs to public-facing resources
- **`npx auth generate` schema conflicts:** Run `npx auth@latest generate` AFTER adding all plugins to `auth.ts`, not before — the generated schema includes plugin-added fields (role, banned, banReason, banExpires, impersonatedBy)

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Magic link token generation + storage + expiry + invalidation | Custom `magic_link_tokens` table + cron cleanup | `better-auth` `magicLink` plugin | Token entropy, hashing, single-use enforcement, expiry cleanup all handled; Pitfall 6 requires 32+ bytes entropy and scanner mitigation — non-trivial |
| Session management (cookie, refresh, CSRF) | Custom JWT + cookie logic | `better-auth` sessions | HTTP-only cookie, SameSite=Lax, CSRF handled by default; short-lived access + automatic refresh |
| Password hashing | Custom bcrypt/argon2 integration | `better-auth` built-in (`scrypt`) | OWASP-compliant by default; override via `password.hash` option if Argon2 required |
| RBAC permission evaluation | `if/else` chains | `createAccessControl()` + `ac.newRole()` | Type-safe, auditable, hierarchically composable; avoids Pitfall 2 |
| Admin user creation | Custom signup endpoint | `auth.api.createUser()` (admin plugin) | Sets role atomically, skips public signup flow |
| Schema generation for auth tables | Manual SQL migrations | `npx auth@latest generate` + `drizzle-kit migrate` | Plugin fields (role, banned, impersonatedBy) added automatically |
| Drizzle schema for app tables | Anything not Drizzle | `pgTable()` in `lib/db/schema/` | Schema-as-code with type inference; `drizzle-kit generate` produces migration SQL |

**Key insight:** The hardest part of auth is not the happy path — it's the edge cases (scanner-consumed tokens, partial reuse, role desync). These are exactly what better-auth's plugins handle. Reproducing them correctly from scratch under time pressure is high-risk.

---

## Common Pitfalls

### Pitfall 1: Object-Level Authorization Bypass (IDOR)
**What goes wrong:** Role middleware says "user is a Platoon Leader" but a Platoon Leader in Event A can GET roster data from Event B by changing the `eventId` URL parameter. This is OWASP #1.
**Why it happens:** Scope check added to middleware ("is this a valid user?") but not to the service query ("does this event belong to this user's faction?").
**How to avoid:** Every data query scopes to the authenticated user's event membership via `assertEventMembership()` or `assertEventOwnership()`. Write integration tests that prove User A cannot read User B's event data.
**Warning signs:** API accepts bare `eventId` params without cross-referencing `event_roster` or faction ownership.

### Pitfall 2: Scattered Role String Comparisons
**What goes wrong:** `if (session.user.role === 'faction_commander')` in 20 places. Adding a "Co-Commander" role requires touching all 20. One is missed; privilege escalation exists.
**Why it happens:** Simple and fast to write initially.
**How to avoid:** `permissions.ts` is the ONLY place roles are defined. Service layer uses `auth.api.userHasPermission()`. Zero `role ===` comparisons outside `permissions.ts`.
**Warning signs:** Grep for `role ===` in `services/` or `app/` — should return zero matches.

### Pitfall 3: Magic Link Token Consumed by Email Security Scanner
**What goes wrong:** `expiresIn` set to 15 minutes. Corporate email gateway auto-clicks every link in email. Token consumed before player opens it. Player sees "invalid link" error.
**Why it happens:** Single-use token + direct auth on URL load = scanner breaks it.
**How to avoid:** Use the two-step confirm page pattern (Pattern 5 above). Magic link URL goes to a page showing a "Sign in" button. Token is NOT redeemed on page load — only on button click.
**Warning signs:** `callbackURL` in `signIn.magicLink()` points directly to `/dashboard` instead of `/magic-link/verify?token=...`

### Pitfall 4: Schema Generated Before All Plugins Are Added
**What goes wrong:** `npx auth@latest generate` run before `admin` plugin is added to `auth.ts`. Schema lacks `role`, `banned`, `banReason`, `banExpires`, `impersonatedBy` columns. Later migration adds columns to a populated table — higher-risk operation.
**Why it happens:** Developer wires email/password first, adds admin plugin later.
**How to avoid:** Add ALL plugins to `auth.ts` before running `generate` for the first time. Run `generate` once, review the output, then `drizzle-kit migrate`.
**Warning signs:** `user` table exists but missing `role` column; `session` table missing `impersonatedBy`.

### Pitfall 5: Email Address Visible to Players via API Response
**What goes wrong:** AUTHZ-05 requires email hidden from Player role. API returns full user object including email to all roster queries. Player calls `GET /api/roster` and sees all email addresses.
**Why it happens:** Default Drizzle query returns all columns; no projection applied.
**How to avoid:** Service layer MUST check the requesting user's role and apply different projections: `{ name, callsign, teamAffiliation }` for Player; `{ name, callsign, email, teamAffiliation }` for Platoon Leader and above. Test with a Player-role session that roster queries do NOT contain email fields.
**Warning signs:** Roster service returns `db.query.users.findMany()` without column selection.

### Pitfall 6: `better-auth` Schema Mismatch When Using `usePlural`
**What goes wrong:** App uses plural table names (`users`, `sessions`) but Drizzle adapter defaults to singular (`user`, `session`). Queries fail silently or throw at runtime.
**Why it happens:** Next.js convention often uses plural; better-auth uses singular by default.
**How to avoid:** Either pass `usePlural: true` to `drizzleAdapter(db, { provider: "pg", usePlural: true })` OR keep singular names throughout. Pick one convention and apply consistently BEFORE any data exists.
**Warning signs:** Auth queries return "table not found" errors or null sessions.

---

## Code Examples

Verified from official better-auth docs (https://www.better-auth.com):

### Auth Handler (Next.js App Router)
```typescript
// Source: https://www.better-auth.com/docs/installation#mount-handler
// app/api/auth/[...all]/route.ts
import { auth } from "@/lib/auth/auth";
import { toNextJsHandler } from "better-auth/next-js";
export const { POST, GET } = toNextJsHandler(auth);
```

### Email/Password Sign In (Client)
```typescript
// Source: https://www.better-auth.com/docs/authentication/email-password#sign-in
import { authClient } from "@/lib/auth/auth-client";

const { data, error } = await authClient.signIn.email({
  email: "user@example.com",
  password: "password1234",
  rememberMe: true,          // session survives browser close (AUTH-04)
  callbackURL: "/dashboard",
});
```

### Magic Link Request (Client)
```typescript
// Source: https://www.better-auth.com/docs/plugins/magic-link#sign-in-with-magic-link
import { authClient } from "@/lib/auth/auth-client";

const { data, error } = await authClient.signIn.magicLink({
  email: "user@example.com",
  callbackURL: "/magic-link/verify",  // two-step confirm page (NOT /dashboard)
  newUserCallbackURL: "/welcome",
});
```

### Magic Link Verify (Two-Step Confirm, Client)
```typescript
// Source: https://www.better-auth.com/docs/plugins/magic-link#verify-magic-link
const { data, error } = await authClient.magicLink.verify({
  query: {
    token: tokenFromURL,
    callbackURL: "/dashboard",
  },
});
```

### Sign Out (Client)
```typescript
// Source: https://www.better-auth.com/docs/authentication/email-password#sign-out
await authClient.signOut({
  fetchOptions: {
    onSuccess: () => router.push("/login"),
  },
});
```

### Password Reset Request (Client)
```typescript
// Source: https://www.better-auth.com/docs/authentication/email-password#request-password-reset
const { data, error } = await authClient.requestPasswordReset({
  email: "user@example.com",
  redirectTo: "/reset-password",
});
```

### Password Reset Submit (Client)
```typescript
// Source: https://www.better-auth.com/docs/authentication/email-password#request-password-reset
const token = new URLSearchParams(window.location.search).get("token");
const { data, error } = await authClient.resetPassword({
  newPassword: "newpassword1234",
  token: token!,
});
```

### Admin Create User (Server — for invitation flow)
```typescript
// Source: https://www.better-auth.com/docs/plugins/admin#create-user
const newUser = await auth.api.createUser({
  body: {
    email: "player@example.com",
    name: "Call Sign Actual",
    role: "player",
    password: crypto.randomUUID(), // placeholder — user will use magic link
  },
});
```

### Set User Role (Server — for admin operations)
```typescript
// Source: https://www.better-auth.com/docs/plugins/admin#set-user-role
const data = await auth.api.setRole({
  body: {
    userId: "user-uuid",
    role: "faction_commander",
  },
  headers: await headers(),
});
```

### Drizzle Adapter Configuration
```typescript
// Source: https://www.better-auth.com/docs/adapters/drizzle
import { betterAuth } from "better-auth";
import { drizzleAdapter } from "better-auth/adapters/drizzle";
import { db } from "@/lib/db/client";
import * as schema from "@/lib/db/schema";

export const auth = betterAuth({
  database: drizzleAdapter(db, {
    provider: "pg",
    schema: { ...schema },
    // If using plural table names:
    // usePlural: true,
  }),
  // ...plugins
});
```

### Core Schema Tables (Application, Non-Auth)
```typescript
// lib/db/schema/factions.ts
import { pgTable, uuid, text, timestamp } from "drizzle-orm/pg-core";

export const factions = pgTable("factions", {
  id:          uuid("id").primaryKey().defaultRandom(),
  name:        text("name").notNull(),
  createdBy:   uuid("created_by").notNull(), // → users.id (better-auth managed)
  createdAt:   timestamp("created_at").notNull().defaultNow(),
});

// lib/db/schema/events.ts  
export const events = pgTable("events", {
  id:          uuid("id").primaryKey().defaultRandom(),
  factionId:   uuid("faction_id").notNull().references(() => factions.id),
  name:        text("name").notNull(),
  status:      text("status").notNull().default("draft"), // draft | published | archived
  createdBy:   uuid("created_by").notNull(),
  createdAt:   timestamp("created_at").notNull().defaultNow(),
});

// lib/db/schema/eventRoster.ts — join between users and events (AUTHZ-06)
export const eventRoster = pgTable("event_roster", {
  id:            uuid("id").primaryKey().defaultRandom(),
  eventId:       uuid("event_id").notNull().references(() => events.id),
  userId:        uuid("user_id").notNull(), // → better-auth user.id
  squadId:       uuid("squad_id"),          // nullable until assigned
  callsign:      text("callsign"),
  teamAffiliation: text("team_affiliation"),
  importedAt:    timestamp("imported_at").notNull().defaultNow(),
});
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Lucia Auth | better-auth 1.x | Late 2024 (Lucia archived) | Lucia is unmaintained; better-auth is the community's recommended replacement |
| NextAuth v4 | better-auth (or Auth.js v5 beta) | 2024 | NextAuth v4 is legacy; Auth.js v5 still beta as of early 2026 |
| next-auth magic link | better-auth `magicLink` plugin | 2024–2025 | Purpose-built plugin with configurable expiry, single-use enforcement |
| Zod 3 | Zod 4 | March 2026 stable | Zod 4 has smaller bundle, faster parsing; `@hookform/resolvers` 3.9+ required |
| Prisma for auth schema | `npx auth@latest generate` + `drizzle-kit migrate` | 2024–2025 | better-auth CLI generates Drizzle schema directly; no manual migration authoring for auth tables |

**Deprecated/outdated:**
- **Lucia Auth:** Archived — author recommends migrating to better-auth. Do not start any new project on Lucia.
- **NextAuth v4:** Legacy. Auth.js v5 exists but is still in beta. better-auth is stable.
- **`next-auth` Pages Router patterns:** App Router route handler is the correct pattern for Next.js 15.

---

## Open Questions

1. **Invitation email sender domain**
   - What we know: Resend requires a verified sending domain; `noreply@yourdomain.com` must be configured before any email sends
   - What's unclear: What domain the project will use for production sending; whether a custom subdomain (`auth.yourdomain.com`) is desired for isolation
   - Recommendation: Use `noreply@yourdomain.com` as placeholder in code; flag DNS verification (SPF/DKIM/DMARC) as a Wave 0 environment task before any email-sending test can run end-to-end

2. **better-auth schema table naming convention**
   - What we know: better-auth defaults to singular (`user`, `session`, `account`, `verification`); `usePlural: true` option available
   - What's unclear: Whether the project wants all app tables in plural or singular form — must be decided before the first migration
   - Recommendation: Pick singular throughout (matching better-auth defaults) to avoid needing `usePlural` remapping; document the convention in `CLAUDE.md`

3. **User additional fields (callsign, etc.)**
   - What we know: better-auth `user` table can be extended with `additionalFields`; the project needs `callsign` and `displayName` on users
   - What's unclear: Whether to use better-auth `additionalFields` config or a separate `user_profiles` table joined to better-auth's `user`
   - Recommendation: Use a separate `user_profiles` table keyed on `user.id` — keeps better-auth schema clean and avoids re-running `auth generate` when profile fields change

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Vitest 2.x (co-located with Next.js App Router; faster than Jest for ESM) |
| Config file | `vitest.config.ts` — Wave 0 gap if not present |
| Quick run command | `npx vitest run --reporter=verbose tests/unit` |
| Full suite command | `npx vitest run --reporter=verbose` |
| E2E / integration | Playwright 1.x for auth flows that require a real browser + cookie |
| E2E run command | `npx playwright test tests/e2e/auth` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | Notes |
|--------|----------|-----------|-------------------|-------|
| AUTH-01 | Invited user receives email + activates via magic link | E2E | `npx playwright test tests/e2e/auth/invitation.spec.ts` | Requires Resend dev/test mode or email interceptor (Mailpit) |
| AUTH-02 | Email/password sign in returns a valid session | Integration | `npx vitest run tests/integration/auth/email-login.test.ts` | Mock Drizzle adapter; verify session cookie set |
| AUTH-03 | Magic link request sends email; verify URL redeems token; token rejected on reuse | Integration | `npx vitest run tests/integration/auth/magic-link.test.ts` | Verify `allowedAttempts=1` enforced (second use returns error) |
| AUTH-04 | Session rehydrates from cookie on fresh request | Integration | `npx vitest run tests/integration/auth/session-persistence.test.ts` | `auth.api.getSession()` with saved cookie headers |
| AUTH-05 | Password reset email sent; token resets password; old password rejected | Integration | `npx vitest run tests/integration/auth/password-reset.test.ts` | |
| AUTH-06 | Sign out clears session; subsequent protected route returns 401 | Integration | `npx vitest run tests/integration/auth/sign-out.test.ts` | |
| AUTHZ-01 | All 5 roles exist in permission matrix; `checkRolePermission()` returns expected results | Unit | `npx vitest run tests/unit/auth/permissions.test.ts` | Pure function test — no DB |
| AUTHZ-02 | Faction Commander can invoke `event:publish`; Player cannot | Unit | `npx vitest run tests/unit/auth/permissions.test.ts` | `ac.newRole()` composition test |
| AUTHZ-03 | Platoon Leader and Squad Leader pass `event:read`; fail `event:publish` and `roster:import` | Unit | `npx vitest run tests/unit/auth/permissions.test.ts` | |
| AUTHZ-04 | Player passes `request:submit`; fails `request:approve` | Unit | `npx vitest run tests/unit/auth/permissions.test.ts` | |
| AUTHZ-05 | Player-role roster query does NOT return email field; Platoon Leader query DOES | Integration | `npx vitest run tests/integration/auth/email-visibility.test.ts` | Scope-level projection test |
| AUTHZ-06 | User A (Event 1) cannot access roster row from Event 2 | Integration | `npx vitest run tests/integration/auth/scope-isolation.test.ts` | **Critical IDOR test** — most important test in this phase |

### Critical Test: IDOR Scope Isolation (AUTHZ-06)
```typescript
// tests/integration/auth/scope-isolation.test.ts
// Verify User A cannot read User B's event data
it("blocks cross-event roster access", async () => {
  const userA = await createTestUser({ role: "player", eventId: "event-1" });
  const userB = await createTestUser({ role: "player", eventId: "event-2" });
  const rosterRowB = await createTestRosterRow({ eventId: "event-2", userId: userB.id });

  // User A attempts to read a roster row from Event 2 — must throw ForbiddenError
  await expect(
    rosterService.getRosterRow(userA.session, rosterRowB.id)
  ).rejects.toThrow("ForbiddenError");
});
```

### Sampling Rate
- **Per task commit:** `npx vitest run tests/unit` (< 5 seconds)
- **Per wave merge:** `npx vitest run && npx playwright test tests/e2e/auth` (full suite)
- **Phase gate:** Full suite green (unit + integration + e2e auth flows) before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `vitest.config.ts` — Vitest configuration for Next.js App Router (needs `@vitejs/plugin-react`)
- [ ] `playwright.config.ts` — Playwright config with `baseURL: 'http://localhost:3000'` and auth fixtures
- [ ] `tests/unit/auth/permissions.test.ts` — covers AUTHZ-01 through AUTHZ-04
- [ ] `tests/integration/auth/scope-isolation.test.ts` — covers AUTHZ-06 (IDOR critical test)
- [ ] `tests/integration/auth/email-visibility.test.ts` — covers AUTHZ-05
- [ ] `tests/integration/auth/magic-link.test.ts` — covers AUTH-03 (single-use enforcement)
- [ ] `tests/e2e/auth/invitation.spec.ts` — covers AUTH-01 (end-to-end invitation flow)
- [ ] `tests/helpers/test-db.ts` — shared test database helpers (createTestUser, createTestRosterRow)
- [ ] Email interceptor setup (Mailpit or Resend test mode) — required for AUTH-01, AUTH-03, AUTH-05 integration tests
- [ ] Framework install: `npm install -D vitest @vitejs/plugin-react @playwright/test` if not present

---

## Sources

### Primary (HIGH confidence)
- `https://www.better-auth.com/docs/plugins/magic-link` — magic link plugin API; `expiresIn`, `allowedAttempts`, `sendMagicLink` callback; `signIn.magicLink()`, `magicLink.verify()` client APIs — verified 2026-03-12
- `https://www.better-auth.com/docs/plugins/admin` — admin plugin; `createAccessControl()`, `ac.newRole()`, `createUser()`, `setRole()`, `userHasPermission()`, schema fields (role, banned, impersonatedBy) — verified 2026-03-12
- `https://www.better-auth.com/docs/authentication/email-password` — `signUp.email()`, `signIn.email()`, `signOut()`, `requestPasswordReset()`, `resetPassword()`, `sendResetPassword` callback — verified 2026-03-12
- `https://www.better-auth.com/docs/installation` — `toNextJsHandler(auth)`, Drizzle adapter config, `createAuthClient()` — verified 2026-03-12
- `https://www.better-auth.com/docs/adapters/drizzle` — `drizzleAdapter(db, { provider: "pg" })`, `usePlural`, `npx auth@latest generate` + `drizzle-kit migrate` workflow — verified 2026-03-12
- OWASP Authorization Cheat Sheet — `https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html` — IDOR prevention patterns (HIGH confidence)
- `.planning/research/STACK.md` — stack decisions and version compatibility matrix
- `.planning/research/ARCHITECTURE.md` — Flat RBAC + scope guard pattern; data model; service layer boundary
- `.planning/research/PITFALLS.md` — Pitfalls 1 (IDOR), 2 (role strings), 6 (magic link tokens) — all Phase 1-relevant

### Secondary (MEDIUM confidence)
- Vitest 2.x + Playwright 1.x for testing — patterns from training data; version numbers from official sites (vitest.dev, playwright.dev); no Context7 query performed but both are stable, well-documented frameworks

### Tertiary (LOW confidence)
- None — all Phase 1 claims are verified from official better-auth docs or OWASP

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all library APIs verified against live official better-auth docs (2026-03-12)
- Architecture patterns: HIGH — scope guard, RBAC matrix, two-step magic link from OWASP + official better-auth docs
- Pitfalls: HIGH — sourced from OWASP official cheatsheets + confirmed via better-auth plugin behavior from official docs
- Code examples: HIGH — taken verbatim or adapted directly from official better-auth docs fetched 2026-03-12

**Research date:** 2026-03-12
**Valid until:** 2026-04-12 (better-auth is actively developed; check for 1.x minor releases before implementation)
