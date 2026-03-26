---
phase: 05-self-service-registration
verified: 2026-03-26T18:30:00Z
status: human_needed
score: 10/10 must-haves verified
re_verification: false
human_verification:
  - test: "Visit /auth/login — verify 'Don't have an account? Create one' link is visible in the CardFooter"
    expected: "Link is rendered below the form and navigates to /auth/register when clicked"
    why_human: "Link present in JSX (confirmed by grep), but visual rendering and click navigation require a browser"
  - test: "Visit /auth/register — verify all 4 fields render: Display Name, Email, Password, Confirm Password"
    expected: "Card page renders with 4 labeled inputs and a 'Create Account' submit button"
    why_human: "JSX confirmed substantive, but visual completeness requires browser (AC-6)"
  - test: "Enter mismatched passwords and submit — verify 'Passwords do not match' error appears without any network request"
    expected: "Error appears under Confirm Password field; DevTools Network tab shows no POST to /api/auth/register"
    why_human: "zod .refine() logic confirmed in code; browser + DevTools needed to verify no API call fires (AC-10)"
  - test: "Submit valid registration data — verify redirect to /dashboard occurs after success"
    expected: "Browser navigates to /dashboard; user is authenticated"
    why_human: "login(result.token) + navigate('/dashboard') confirmed in code; actual redirect requires running app (AC-7)"
  - test: "While authenticated, navigate directly to /auth/register — verify redirect to /dashboard"
    expected: "Browser immediately redirects to /dashboard without showing the registration form"
    why_human: "useEffect auth guard confirmed in code; redirect behavior requires a running auth session (AC-9)"
---

# Phase 5: Self-Service Registration Verification Report

**Phase Goal:** Add self-service registration so new users can create accounts and get immediate access as faction_commander without waiting for an invite.
**Verified:** 2026-03-26T18:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths — Plan 01 (Backend, AC-1 through AC-5)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | POST /api/auth/register with valid data returns 200 + JWT + userId + email + displayName + role | VERIFIED | `Register_WithValidData_Returns200AndJwt` passes; response asserts all 5 fields + JWT structure |
| 2 | POST /api/auth/register with missing displayName returns 400 | VERIFIED | `Register_MissingDisplayName_Returns400` passes; `[Required]` on `DisplayName` in RegisterRequest triggers model state validation |
| 3 | POST /api/auth/register with password < 6 chars returns 400 | VERIFIED | `Register_ShortPassword_Returns400` passes; `[MinLength(6)]` on `Password` in RegisterRequest |
| 4 | POST /api/auth/register with duplicate email returns 409 | VERIFIED | `Register_DuplicateEmail_Returns409` passes; sentinel string "DUPLICATE_EMAIL" in AuthService maps to Conflict(409) in controller |
| 5 | Self-registered user has role faction_commander in the database | VERIFIED | `Register_SelfRegisteredUser_HasFactionCommanderRole` passes; `AddToRoleAsync(user, "faction_commander")` confirmed in AuthService |

### Observable Truths — Plan 02 (Frontend, AC-6 through AC-10)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 6 | /auth/register renders a form with Display Name, Email, Password, and Confirm Password fields | VERIFIED (code); NEEDS HUMAN (visual) | RegisterPage.tsx line 75-120 contains all 4 labeled inputs; TypeScript compiles clean |
| 7 | Submitting valid registration redirects to /dashboard | VERIFIED (code); NEEDS HUMAN (runtime) | `login(result.token); navigate('/dashboard')` at lines 53-54 of RegisterPage.tsx |
| 8 | LoginPage shows a 'Create an account' link to /auth/register | VERIFIED (code); NEEDS HUMAN (visual) | `<Link to="/auth/register">Don't have an account? Create one</Link>` at lines 83-84 of LoginPage.tsx |
| 9 | Authenticated users visiting /auth/register are redirected to /dashboard | VERIFIED (code); NEEDS HUMAN (runtime) | `useEffect(() => { if (isAuthenticated) navigate('/dashboard', { replace: true }); }, ...)` at lines 33-35 |
| 10 | Mismatched passwords show a client-side error without making an API call | VERIFIED (code); NEEDS HUMAN (runtime) | `schema.refine(data => data.password === data.confirmPassword, ...)` at lines 21-24; zod fires before `onSubmit` is called |

**Score:** 10/10 truths verified at code level; 5/10 require human confirmation for browser behavior

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `milsim-platform/src/MilsimPlanning.Api/Models/Responses/RegisterResponse.cs` | Register-specific response record | VERIFIED | `public record RegisterResponse(string Token, string UserId, string Email, string DisplayName, string Role)` — 5-field record, correct namespace |
| `milsim-platform/src/MilsimPlanning.Api/Models/Requests/RegisterRequest.cs` | Request model with validation | VERIFIED | `[Required]`, `[EmailAddress]`, `[MinLength(6)]` on all 3 fields |
| `milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs` | RegisterAsync method | VERIFIED | `public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)` at line 81; full implementation with EmailConfirmed=true, Callsign="", faction_commander role, GenerateJwt |
| `milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs` | Register endpoint | VERIFIED | `[HttpPost("register")]` + `[AllowAnonymous]` at lines 51-52; calls `_authService.RegisterAsync`; returns Ok/Conflict/BadRequest |
| `milsim-platform/src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` | 5 integration tests for AC-1 through AC-5 | VERIFIED | 5 `[Trait("Category", "Auth_Register")]` methods present; all 5 pass (`dotnet test --filter "Category=Auth_Register"`) |
| `web/src/pages/auth/RegisterPage.tsx` | Registration form page (min 60 lines) | VERIFIED | 135 lines; exports `RegisterPage` function; 4-field form with zod validation, auth guard, error handling |
| `web/src/lib/api.ts` | RegisterResponse interface | VERIFIED | `export interface RegisterResponse` at line 271 with token, userId, email, displayName, role fields |
| `web/src/main.tsx` | /auth/register route | VERIFIED | `{ path: '/auth/register', element: <RegisterPage /> }` at line 37; `RegisterPage` imported at line 13 |
| `web/src/pages/auth/LoginPage.tsx` | Create an account link | VERIFIED | `<Link to="/auth/register">Don't have an account? Create one</Link>` at lines 83-84 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| AuthController.cs | AuthService.RegisterAsync | method call | WIRED | `var response = await _authService.RegisterAsync(request)` at line 57 |
| AuthService.RegisterAsync | UserManager.CreateAsync | Identity user creation | WIRED | `await _userManager.CreateAsync(user, request.Password)` at line 90 |
| AuthService.RegisterAsync | AddToRoleAsync("faction_commander") | role assignment | WIRED | `await _userManager.AddToRoleAsync(user, "faction_commander")` at line 113 |
| RegisterPage.tsx | /api/auth/register | api.post call | WIRED | `api.post<RegisterResponse>('/auth/register', {...})` at line 47 |
| RegisterPage.tsx | useAuth.login | login(token) after success | WIRED | `login(result.token)` at line 53 |
| RegisterPage.tsx | /dashboard | navigate after login | WIRED | `navigate('/dashboard')` at line 54 |
| LoginPage.tsx | /auth/register | Link component | WIRED | `<Link to="/auth/register">` at line 83 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| RegisterPage.tsx | `result` (RegisterResponse) | `api.post('/auth/register', ...)` → backend controller → `AuthService.RegisterAsync` → `_userManager.CreateAsync` + `AddToRoleAsync` + `GenerateJwt` | Yes — real DB write, real JWT | FLOWING |
| AuthController Register | `response` (RegisterResponse) | `_authService.RegisterAsync(request)` → real Identity user creation in PostgreSQL | Yes — Integration tests confirm real DB writes (AC-5 test queries DB directly) | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| 5 Auth_Register integration tests pass | `dotnet test --filter "Category=Auth_Register"` | Failed: 0, Passed: 5, Total: 5 | PASS |
| Full backend test suite (no regressions) | `dotnet test MilsimPlanning.Api.Tests.csproj` | Failed: 0, Passed: 113, Total: 113 | PASS |
| TypeScript compiles without errors | `pnpm exec tsc --noEmit` | Exit code 0, no output | PASS |

### Requirements Coverage

All requirement IDs are sourced from `PRD-REGISTRATION.md` (acceptance criteria AC-1 through AC-10).

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| AC-1 | 05-01 | Valid registration returns 200 + JWT | SATISFIED | `Register_WithValidData_Returns200AndJwt` passes; asserts token, userId, email, displayName, role="faction_commander" |
| AC-2 | 05-01 | Missing displayName returns 400 | SATISFIED | `Register_MissingDisplayName_Returns400` passes; `[Required]` on DisplayName |
| AC-3 | 05-01 | Password < 6 chars returns 400 | SATISFIED | `Register_ShortPassword_Returns400` passes; `[MinLength(6)]` on Password |
| AC-4 | 05-01 | Duplicate email returns 409 | SATISFIED | `Register_DuplicateEmail_Returns409` passes; error body contains "email already exists" |
| AC-5 | 05-01 | Self-registered user has role faction_commander | SATISFIED | `Register_SelfRegisteredUser_HasFactionCommanderRole` passes; queries DB via UserManager |
| AC-6 | 05-02 | /auth/register renders with all 4 fields | SATISFIED (code) / NEEDS HUMAN (visual) | RegisterPage.tsx lines 74-120 contain Display Name, Email, Password, Confirm Password inputs |
| AC-7 | 05-02 | Successful registration redirects to /dashboard | SATISFIED (code) / NEEDS HUMAN (runtime) | `login(result.token); navigate('/dashboard')` confirmed at lines 53-54 |
| AC-8 | 05-02 | "Create an account" link visible on login page | SATISFIED (code) / NEEDS HUMAN (visual) | `<Link to="/auth/register">Don't have an account? Create one</Link>` confirmed in LoginPage.tsx |
| AC-9 | 05-02 | Authenticated users visiting /auth/register redirected to /dashboard | SATISFIED (code) / NEEDS HUMAN (runtime) | `useEffect(() => { if (isAuthenticated) navigate('/dashboard', { replace: true }); })` confirmed |
| AC-10 | 05-02 | Password mismatch shows client-side error without API call | SATISFIED (code) / NEEDS HUMAN (runtime) | zod `.refine()` on schema blocks `onSubmit`; no API call path exists before validation passes |

**Orphaned requirements:** None. All 10 ACs from PRD-REGISTRATION.md are claimed by plans 05-01 and 05-02.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs` | 58 | `"dev-placeholder-secret-32-chars!!"` | Info | Dev config fallback for JWT secret; intentional for local development; not a stub |

No blockers or warnings found. The only flagged string is an intentional dev-environment JWT secret fallback that is not stub behavior.

### SUMMARY.md Commit Hash Discrepancy

The 05-01-SUMMARY.md documents commit hashes `0cdf4c3` and `a7d899f`. The actual repository contains commits `51c1195` (tests) and `cd6fb28` (docs summary), with `8de1331` being the Plan 02 frontend commit. This is a documentation error in the SUMMARY — the code artifacts are real, present, and passing. No functional impact.

### Human Verification Required

The following 5 items require a running browser session to confirm AC-6 through AC-10. All code paths are verified; only runtime/visual confirmation remains.

#### 1. "Create an account" link visible on login page (AC-8)

**Test:** Start frontend (`cd web && pnpm dev`), visit `http://localhost:5173/auth/login`
**Expected:** "Don't have an account? Create one" link is visible below the login form in the CardFooter; clicking it navigates to `/auth/register`
**Why human:** Link present in JSX (line 83 of LoginPage.tsx), but visual rendering and click navigation require a browser

#### 2. Registration form renders all 4 fields (AC-6)

**Test:** Visit `http://localhost:5173/auth/register`
**Expected:** Card page shows 4 labeled inputs (Display Name, Email, Password, Confirm Password) and a "Create Account" submit button
**Why human:** JSX contains all 4 fields (confirmed), but visual completeness and labeling require browser

#### 3. Password mismatch shows client-side error without API call (AC-10)

**Test:** On `/auth/register`, enter any email, enter "password1" in Password and "password2" in Confirm Password, click Create Account
**Expected:** "Passwords do not match" error appears under the Confirm Password field; DevTools Network tab shows no POST request was made
**Why human:** zod `.refine()` fires before `onSubmit` (confirmed in code), but no-network-call behavior requires DevTools observation

#### 4. Successful registration redirects to /dashboard (AC-7)

**Test:** On `/auth/register`, submit with Display Name "Test User", a unique email, password "TestPass123!" (confirmed match)
**Expected:** Browser navigates to `/dashboard`; user is logged in (JWT stored)
**Why human:** `login(result.token)` + `navigate('/dashboard')` confirmed in code; end-to-end redirect requires running backend and frontend

#### 5. Authenticated users redirected away from /auth/register (AC-9)

**Test:** After logging in, navigate directly to `http://localhost:5173/auth/register`
**Expected:** Browser immediately redirects to `/dashboard` without displaying the registration form
**Why human:** `useEffect` auth guard confirmed (lines 33-35 of RegisterPage.tsx), but redirect behavior requires an active auth session

### Gaps Summary

No gaps. All 10 acceptance criteria have implementation evidence in the codebase:

- **AC-1 through AC-5:** Proven by 5 passing integration tests that execute against a real PostgreSQL database
- **AC-6 through AC-10:** Proven by substantive code in RegisterPage.tsx, confirmed key links in main.tsx and LoginPage.tsx, and a clean TypeScript build

The only outstanding items are browser-level confirmations of the 5 frontend ACs (AC-6 through AC-10). The code is complete, wired, and type-safe.

---

_Verified: 2026-03-26T18:30:00Z_
_Verifier: Claude (gsd-verifier)_
