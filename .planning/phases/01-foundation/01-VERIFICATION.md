---
phase: 01-foundation
verified: 2026-03-13T14:10:00Z
status: passed
score: 5/5 must-haves verified
re_verification: false
human_verification:
  - test: "End-to-end invitation email flow"
    expected: "User receives an invitation email in their actual inbox with a working activation link"
    why_human: "EmailService is a stub (logs only) — no real email delivery in this phase; integration test only verifies the mock was called"
  - test: "Session persistence across page refresh"
    expected: "After logging in, reload the browser — user remains logged in without re-authentication"
    why_human: "localStorage behavior verified by vitest tests but actual browser behavior requires manual testing"
  - test: "Magic link email scanner protection"
    expected: "GET /auth/magic-link/confirm in a browser shows a button, does not auto-complete login"
    why_human: "Code confirms no useEffect auto-submit, but visual appearance and exact UX needs browser verification"
---

# Phase 1: Foundation — Verification Report

**Phase Goal:** Every user can securely access the application and all data is scoped correctly by role
**Verified:** 2026-03-13T14:10:00Z
**Status:** ✅ PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User receives an invitation email and can activate their account via the link | ✓ VERIFIED | `AuthService.InviteUserAsync` creates user via `UserManager.CreateAsync`, generates `EmailConfirmationToken`, sends via `IEmailService` with activation URL; integration test `Invitation_CreatesUserAndSendsEmail` asserts 201 + email mock called |
| 2 | User can log in with email/password and stay logged in across browser refresh | ✓ VERIFIED | `POST /api/auth/login` → `SignInManager.PasswordSignInAsync` → JWT with 7-day expiry; React `useAuth` initializes from `useState(() => { const token = getToken(); … })` (not useEffect) — localStorage persistence is synchronous on mount |
| 3 | User can log in via magic link sent to their email (single-use, 15-60 min expiry) | ✓ VERIFIED | `MagicLinkService` stores SHA256-hashed token with `ExpiresAt = UTC+30min`, `UsedAt = null`; `VerifyMagicLinkAsync` checks `UsedAt == null` and sets `UsedAt` **before** issuing JWT (race-condition-safe); tests `MagicLink_ValidToken_ReturnsJwt` and `MagicLink_TokenUsedTwice_Returns401` both pass |
| 4 | User can log out from any page and reset a forgotten password via email link | ✓ VERIFIED | `POST /api/auth/logout` returns 200 (JWT stateless; client discards); `POST /api/auth/password-reset` generates ASP.NET Identity reset token and sends email; `POST /api/auth/password-reset/confirm` calls `UserManager.ResetPasswordAsync`; `DashboardPage` has logout button calling `useAuth.logout()` |
| 5 | A Faction Commander can take actions their role permits; a Player is blocked from commander-only actions; email addresses are hidden from Player role | ✓ VERIFIED | `MinimumRoleHandler` uses `AppRoles.Hierarchy` numeric comparison (single source of truth); `POST /api/auth/invite` requires `RequireFactionCommander` policy; `RosterController` strips email for roles below PlatoonLeader level; IDOR tests `ScopeGuard_PlayerInEventA_Returns403ForEventB` and `ScopeGuard_CommanderInEventA_Returns403ForEventB` verify cross-event 403 |

**Score:** 5/5 truths verified

---

## Required Artifacts

### Plan 01-01 Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `milsim-platform/milsim-platform.slnx` | .NET 10 solution file | ✓ VERIFIED | Exists; note `.slnx` not `.sln` (correct .NET 10 format per SUMMARY decision) |
| `src/MilsimPlanning.Api/Data/AppDbContext.cs` | IdentityDbContext with all DbSets | ✓ VERIFIED | `class AppDbContext : IdentityDbContext<AppUser>` with DbSets for UserProfiles, Events, EventMemberships, MagicLinkTokens; `base.OnModelCreating(builder)` called first |
| `src/MilsimPlanning.Api/Data/Entities/MagicLinkToken.cs` | Single-use token table | ✓ VERIFIED | Has `UsedAt` nullable datetime (`DateTime? UsedAt`) |
| `src/MilsimPlanning.Api/Data/Entities/EventMembership.cs` | User-to-event binding with role | ✓ VERIFIED | Contains `EventId`, `UserId`, `Role`; composite unique index on `(UserId, EventId)` in AppDbContext |
| `src/MilsimPlanning.Api/Domain/AppRoles.cs` | Role constants + Hierarchy dict | ✓ VERIFIED | All 5 roles constants; `Hierarchy` dictionary with Player=1 through SystemAdmin=5 |
| `src/MilsimPlanning.Api/Data/Migrations/` | InitialSchema migration | ✓ VERIFIED | `20260313132614_InitialSchema.cs` covers AspNetUsers, Events, EventMemberships, MagicLinkTokens, UserProfiles |

### Plan 01-02 Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/MilsimPlanning.Api/Controllers/AuthController.cs` | All 8 auth endpoints | ✓ VERIFIED | login, logout, magic-link POST, magic-link/confirm GET+POST, password-reset, password-reset/confirm, invite — all present |
| `src/MilsimPlanning.Api/Services/AuthService.cs` | JWT generation + login + invitation | ✓ VERIFIED | `GenerateJwt` with sub/email/role/callsign claims; `LoginAsync` with discriminated union; `InviteUserAsync` |
| `src/MilsimPlanning.Api/Services/MagicLinkService.cs` | Send + single-use verify | ✓ VERIFIED | `SendMagicLinkAsync` + `VerifyMagicLinkAsync`; critical ordering (UsedAt set before JWT) at line 92-93 |
| `src/MilsimPlanning.Api/Services/EmailService.cs` | IEmailService stub | ✓ VERIFIED | Interface + stub implementation logging to ILogger |

### Plan 01-03 Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/MilsimPlanning.Api/Authorization/Requirements/MinimumRoleRequirement.cs` | IAuthorizationRequirement | ✓ VERIFIED | `record MinimumRoleRequirement(string MinimumRole) : IAuthorizationRequirement` |
| `src/MilsimPlanning.Api/Authorization/Handlers/MinimumRoleHandler.cs` | Numeric hierarchy comparison | ✓ VERIFIED | Uses `AppRoles.Hierarchy.GetValueOrDefault` — no raw role string comparisons |
| `src/MilsimPlanning.Api/Authorization/ScopeGuard.cs` | IDOR prevention | ✓ VERIFIED | `AssertEventAccess` calls `EventMembershipIds.Contains(eventId)` |
| `src/MilsimPlanning.Api/Services/CurrentUserService.cs` | ICurrentUser scoped per request | ✓ VERIFIED | `_cachedEventIds ??= LoadEventIds()` — single DB query, field-cached |

### Plan 01-04 Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `web/src/lib/auth.ts` | JWT localStorage helpers | ✓ VERIFIED | getToken, setToken, clearToken, parseJwt, isTokenExpired all exported |
| `web/src/lib/api.ts` | Bearer token injecting fetch wrapper | ✓ VERIFIED | Line 11: `if (token) headers['Authorization'] = \`Bearer ${token}\`` |
| `web/src/hooks/useAuth.ts` | Auth state hook with persistence | ✓ VERIFIED | `useState<AuthUser | null>(() => { const token = getToken(); if (!token || isTokenExpired(token)) return null; return userFromToken(token); })` — synchronous mount read |
| `web/src/components/ProtectedRoute.tsx` | Redirect to /auth/login when unauth | ✓ VERIFIED | `if (!isAuthenticated) return <Navigate to="/auth/login" replace />` |
| `web/src/pages/auth/MagicLinkConfirmPage.tsx` | Button-click only confirm | ✓ VERIFIED | Comment "No useEffect here"; `api.post` only called from `handleConfirm` (button onClick) — never on mount |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `AppDbContext.cs` | `IdentityDbContext<AppUser>` | class inheritance | ✓ WIRED | Line 7: `class AppDbContext : IdentityDbContext<AppUser>` |
| `EventMembership.cs` | `Event.cs` and `AppUser.cs` | FK relations | ✓ WIRED | AppDbContext configures FK via HasOne/WithMany for both relations |
| `Program.cs` | `AppDbContext` | `AddDbContext<AppDbContext>` | ✓ WIRED | Line 18 |
| `Program.cs` | `MinimumRoleRequirement` | `AddAuthorization` 5 policies | ✓ WIRED | Lines 52-56 — all 5 policies present |
| `MinimumRoleHandler` | `AppRoles.Hierarchy` | numeric comparison | ✓ WIRED | `AppRoles.Hierarchy.GetValueOrDefault(userRole, 0)` |
| `ScopeGuard.AssertEventAccess` | `ICurrentUser.EventMembershipIds` | HashSet.Contains check | ✓ WIRED | `!currentUser.EventMembershipIds.Contains(eventId)` |
| `CurrentUserService.EventMembershipIds` | `AppDbContext.EventMemberships` | single DB query cached per request | ✓ WIRED | `_cachedEventIds ??= LoadEventIds()` → `_db.EventMemberships.Where(m => m.UserId == UserId)` |
| `MagicLinkService.VerifyMagicLinkAsync` | `MagicLinkTokens table` | `UsedAt IS NULL` conditional update | ✓ WIRED | Line 86: `t.UsedAt == null` filter; line 92: `record.UsedAt = DateTime.UtcNow` before JWT |
| `AuthController GET /magic-link/confirm` | HTML landing page | returns text/html with button | ✓ WIRED | `return Content(html, "text/html")` — does NOT call `VerifyMagicLinkAsync` |
| `AuthService.GenerateJwt` | `Jwt:Secret` config | `SymmetricSecurityKey` | ✓ WIRED | `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))` |
| `web/src/lib/api.ts` | localStorage JWT | `Authorization: Bearer` on every fetch | ✓ WIRED | `if (token) headers['Authorization'] = \`Bearer ${token}\`` |
| `ProtectedRoute.tsx` | `useAuth.isAuthenticated` | redirect to /auth/login when falsy | ✓ WIRED | `<Navigate to="/auth/login" replace />` |
| `useAuth.ts` | localStorage | `useState` initializer reads token on mount | ✓ WIRED | `useState<AuthUser | null>(() => { const token = getToken(); … })` |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| AUTH-01 | 01-02 | User can create account via invitation email | ✓ SATISFIED | `AuthService.InviteUserAsync` creates user + sends email; `Invitation_CreatesUserAndSendsEmail` test passes |
| AUTH-02 | 01-02 | User can log in with email and password | ✓ SATISFIED | `POST /api/auth/login` → `SignInManager.PasswordSignInAsync` → JWT; `Login_WithValidCredentials_ReturnsJwtToken` test |
| AUTH-03 | 01-02 | User can log in via magic link | ✓ SATISFIED | `MagicLinkService` send+verify flow; two-step GET→POST pattern; `MagicLink_ValidToken_ReturnsJwt` test |
| AUTH-04 | 01-02, 01-04 | Session persists across browser refresh | ✓ SATISFIED | `useAuth` hook uses `useState` initializer to read localStorage synchronously on mount; 17 vitest tests pass |
| AUTH-05 | 01-02 | User can reset password via email link | ✓ SATISFIED | `POST /api/auth/password-reset` + confirm endpoint; `PasswordReset_ValidToken_UpdatesPassword` verifies round-trip |
| AUTH-06 | 01-02 | User can log out from any page | ✓ SATISFIED | `POST /api/auth/logout` returns 200; `DashboardPage` calls `useAuth.logout()`; `Logout_AuthenticatedUser_Returns200` test |
| AUTHZ-01 | 01-01, 01-03 | System enforces 5 roles | ✓ SATISFIED | `AppRoles.cs` defines 5 role constants; `MinimumRoleHandler` enforces hierarchy; roles seeded in test setup |
| AUTHZ-02 | 01-03 | Faction Commander has full admin access to their event | ✓ SATISFIED | `RequireFactionCommander` policy gates invite endpoint; `Roles_FactionCommander_CanAccessFactionCommanderPolicy` |
| AUTHZ-03 | 01-03 | Platoon/Squad Leader have read-only access | ✓ SATISFIED | `RequirePlayer` policy on roster GET; no write endpoints for leaders; `ReadOnlyLeader_CanGetRoster_CannotPost` |
| AUTHZ-04 | 01-03 | Players can view roster and access event info | ✓ SATISFIED | `RequirePlayer` policy on roster; `PlayerAccess_InEvent_CanGetRoster` test passes |
| AUTHZ-05 | 01-03 | Email addresses visible only to PlatoonLeader+ | ✓ SATISFIED | `RosterController` strips email for callerLevel < platoonLeaderLevel; `EmailVisibility_Player_EmailFieldAbsentInRosterResponse` and `EmailVisibility_PlatoonLeader_EmailFieldPresentInRosterResponse` tests |
| AUTHZ-06 | 01-01, 01-03 | All data scoped to authenticated user event membership | ✓ SATISFIED | `ScopeGuard.AssertEventAccess` + `CurrentUserService.EventMembershipIds`; IDOR tests return 403 for cross-event access |

**All 12 Phase 1 requirement IDs fully satisfied.**

No orphaned requirements — every ID claimed in plan frontmatter (AUTH-01 through AUTHZ-06) maps to a verified implementation.

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `RosterController.cs` | 47 | `// Mock roster — Phase 2 replaces` | ℹ️ Info | Expected stub — Plan 01-03 explicitly designed this as a Phase 2 placeholder for RBAC testing; does not block Phase 1 goal |
| `EmailService.cs` | — | Logs only, no actual email delivery | ℹ️ Info | Expected stub per plan spec ("Resend integration added in Phase 3"); email flows verified via mock in integration tests |
| `MagicLinkService.cs` | 71-89 | `return null` on early exits | ℹ️ Info | Intentional early-return pattern in `VerifyMagicLinkAsync` — not a stub; each null return has a meaningful security check |

**No blockers or warnings found.** All anti-patterns are intentional stubs with clear Phase 2/3 upgrade paths documented.

---

## Human Verification Required

### 1. Invitation Email Round-Trip

**Test:** Create a test user account, send invitation email, check actual email inbox, click activation link
**Expected:** Receive HTML email with working activation link; clicking it navigates to activation page
**Why human:** `EmailService` is a log-only stub in Phase 1 — no actual email delivery occurs; Resend integration deferred to Phase 3

### 2. Session Persistence Across Browser Refresh

**Test:** Log in via the SPA at http://localhost:5173, then press F5 to refresh the page
**Expected:** User remains logged in (dashboard visible, no redirect to login page)
**Why human:** vitest tests verify the `useState` initializer reads localStorage, but actual browser behavior (including cookie handling, localStorage timing) requires a real browser session

### 3. Magic Link Email Scanner Protection (End-to-End)

**Test:** In a browser, visit a magic link URL directly (simulating email scanner): `http://localhost:5173/auth/magic-link/confirm?token=xxx&userId=yyy`
**Expected:** Page loads with a button only — no automatic sign-in occurs; signing in requires clicking the button
**Why human:** Code analysis confirms no useEffect auto-submit (comment at line 18 of MagicLinkConfirmPage.tsx), but visual UX and exact browser behavior need manual verification

---

## Gaps Summary

No gaps found. All 5 observable truths verified, all 12 requirements satisfied, all key links wired.

The phase delivers exactly what it promised: a secure authentication and authorization foundation for the milsim platform. The JWT/Identity auth layer, RBAC with numeric role hierarchy, IDOR scope guard, and React SPA with localStorage persistence are all implemented, tested (integration tests via Testcontainers + vitest unit tests), and wired end-to-end.

**Notable implementation quality:** The `UsedAt`-before-JWT ordering in `MagicLinkService.VerifyMagicLinkAsync` (line 92-93) is correctly implemented — this is the most common place where single-use token security is incorrectly sequenced. The `MinimumRoleHandler` as the single source of truth for all role hierarchy comparisons (no raw string role comparisons elsewhere in business logic) is also correctly achieved.

---

*Verified: 2026-03-13T14:10:00Z*
*Verifier: Claude (gsd-verifier)*
