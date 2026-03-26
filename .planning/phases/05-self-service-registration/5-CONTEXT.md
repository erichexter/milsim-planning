# Phase 5: Self-Service Registration - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Add self-service registration so new users can create accounts and get immediate access as `faction_commander` without waiting for an admin invite. Delivers: POST /api/auth/register backend endpoint, /auth/register frontend page, "Create an account" link on LoginPage, and auth guard redirecting authenticated users away from /auth/register.

No email verification. No role selection. No invite flow changes.

</domain>

<decisions>
## Implementation Decisions

### Backend endpoint
- **D-01:** POST /api/auth/register — [AllowAnonymous], follows InviteUserAsync pattern in AuthService
- **D-02:** Request model: `{ displayName, email, password }` — RegisterRequest.cs already exists with correct annotations
- **D-03:** User created immediately active (EmailConfirmed = true, no activation token)
- **D-04:** Assign `faction_commander` role to all self-registered users
- **D-05:** Success 200: `{ token, userId, email, displayName, role: "faction_commander" }`
- **D-06:** 400 on missing displayName, invalid email format, or password < 6 chars
- **D-07:** 409 on duplicate email

### Frontend RegisterPage
- **D-08:** Route: `/auth/register` — public route (no ProtectedRoute wrapper), added to main.tsx
- **D-09:** Fields: Display Name, Email, Password, Confirm Password — matching LoginPage.tsx structure exactly (Card + CardHeader + CardContent + CardFooter, react-hook-form + zod)
- **D-10:** Password confirm is client-side only via zod `.refine()` — no API call on mismatch (AC-10)
- **D-11:** On success: call `login(token)` from useAuth, then `navigate('/dashboard')`
- **D-12:** On 409: show "An account with this email already exists" (inline or toast — Claude's discretion, match LoginPage pattern)
- **D-13:** On 400: show specific error message from API response (Claude's discretion on placement)
- **D-14:** Auth guard: inline `useEffect` in RegisterPage — if authenticated, redirect to `/dashboard` immediately (AC-9)

### LoginPage update
- **D-15:** Add "Don't have an account? Create one" link to `/auth/register` in CardFooter — placement and exact wording at Claude's discretion, consistent with existing footer links

### Testing
- **D-16:** Backend integration tests for AC-1 through AC-5 added to AuthTests.cs
- **D-17:** Follow existing test patterns (WebApplicationFactory, shared fixture, seeded test data)

### Claude's Discretion
- Exact error display style (inline vs toast) for server errors — match whatever feels consistent with the existing auth pages
- "Create an account" link position within CardFooter
- Auth guard implementation approach (useEffect vs loader) in RegisterPage

</decisions>

<specifics>
## Specific Ideas

- "Styling: match LoginPage.tsx exactly" — RegisterPage should be visually identical to LoginPage in layout and components
- PRD states success response includes `{ token, userId, email, displayName, role }` — this is richer than the login response shape (`{ token, expiresIn }`); planner should decide whether to extend AuthResponse or use a new RegisterResponse type

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements and PRD
- `PRD-REGISTRATION.md` — Full requirements, acceptance criteria (AC-1 through AC-10), and files to create/modify

### Existing auth patterns to follow
- `milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs` — Endpoint patterns, [AllowAnonymous] usage, error response shape
- `milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs` — InviteUserAsync pattern; RegisterAsync should follow this structure
- `milsim-platform/src/MilsimPlanning.Api/Models/Requests/RegisterRequest.cs` — Already created; verify before creating again
- `web/src/pages/auth/LoginPage.tsx` — UI template to match exactly (Card layout, zod schema, form structure, CardFooter links)
- `web/src/main.tsx` — Route registration; shows ProtectedRoute pattern and where to add /auth/register

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `LoginPage.tsx`: Full template for RegisterPage — same Card/CardHeader/CardContent/CardFooter structure, same react-hook-form + zod pattern, same Button/Input/Label components
- `useAuth` hook: provides `login(token)` for storing JWT after successful registration
- `api.ts`: `api.post<T>()` typed wrapper — use for the register API call; throws Error with `.status` on non-2xx

### Established Patterns
- Auth pages are public routes in main.tsx (no ProtectedRoute wrapper)
- Zod schemas for all form validation, inline error messages via `errors.field.message`
- `toast.error()` from sonner for unexpected/server errors
- Backend: [AllowAnonymous] on auth endpoints, ArgumentException → 400, custom exception → 409

### Integration Points
- `AuthController.cs`: add `RegisterAsync` endpoint
- `AuthService.cs`: add `RegisterAsync()` method
- `main.tsx`: register `/auth/register` route
- `LoginPage.tsx`: add "Create an account" link to CardFooter
- `web/src/lib/api.ts` (or services/api.ts): add `registerUser()` function

### Head Start
- `RegisterRequest.cs` already exists with correct validation attributes — do not recreate

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 05-self-service-registration*
*Context gathered: 2026-03-26*
