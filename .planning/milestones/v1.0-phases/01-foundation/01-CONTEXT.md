# Phase 1: Self-Service User Registration - Context

**Gathered:** 2026-03-25
**Status:** Ready for planning
**Source:** PRD-REGISTRATION.md (full context — no discussion questions needed)

<domain>
## Phase Boundary

Add self-service registration so new users can create accounts without waiting for an invite. A new POST /api/auth/register endpoint and /auth/register frontend page. Authenticated users redirected away from the register page. Login page gains a "Create an account" link. All self-registered users receive the faction_commander role by default.

**Not in scope:** Email verification, invite flow changes, role selection during registration.

</domain>

<decisions>
## Implementation Decisions

### Backend: Registration Endpoint

- **D-01:** Add `POST /api/auth/register` to `AuthController.cs` with `[AllowAnonymous]` — matches existing pattern of auth endpoints
- **D-02:** Create new `RegisterRequest.cs` record in `Models/Requests/` with fields: `DisplayName` (required, non-empty), `Email` (valid format + unique), `Password` (min 6 chars)
- **D-03:** Add `RegisterAsync()` to `AuthService.cs` — follow `InviteUserAsync` pattern but: skip email confirmation, set `EmailConfirmed = true`, no activation token, no email sent
- **D-04:** User is immediately active — no pending/activation state
- **D-05:** All self-registered users assigned role `faction_commander` (no choice, no other role)
- **D-06:** Success response: `{ token, userId, email, displayName, role: "faction_commander" }` — create new `RegisterResponse` record (differs from `AuthResponse` which only has Token + ExpiresIn)
- **D-07:** Error responses: `400` for validation failures (missing displayName, invalid email, short password), `409` for duplicate email
- **D-08:** 409 duplicate email: detect via `UserManager.FindByEmailAsync` before creation OR catch the identity error — return `409 Conflict` with `{ error: "An account with this email already exists" }`

### Frontend: RegisterPage.tsx

- **D-09:** New page at `web/src/pages/auth/RegisterPage.tsx`
- **D-10:** Match `LoginPage.tsx` styling exactly — same Card layout, same spacing, same button style
- **D-11:** Four fields: Display Name, Email, Password, Confirm Password (in that order)
- **D-12:** Zod schema includes password confirmation match validation: `z.object({ displayName, email, password, confirmPassword }).refine(data => data.password === data.confirmPassword, { message: "Passwords do not match", path: ["confirmPassword"] })`
- **D-13:** Password mismatch shown as client-side error — no API call made
- **D-14:** On success: store JWT via `login()` from `useAuth`, redirect to `/dashboard`
- **D-15:** On 409: display `"An account with this email already exists"` (specific message, not toast)
- **D-16:** On 400: display specific error from API response body
- **D-17:** Add `registerUser()` function to `web/src/lib/api.ts`

### Auth Guard on /auth/register

- **D-18:** Authenticated users visiting `/auth/register` are redirected to `/dashboard`
- **D-19:** Implement inline in `RegisterPage.tsx` using `useAuth().isAuthenticated` + early return `<Navigate to="/dashboard" replace />` — no new route-level component needed (single page, not a pattern to generalize)

### Routing

- **D-20:** Add `/auth/register` as a public route in `web/src/main.tsx` — alongside existing `/auth/login`, `/auth/magic-link`, etc.
- **D-21:** Route element: `<RegisterPage />` — no wrapper needed beyond auth guard inside component

### LoginPage Update

- **D-22:** Add `"Don't have an account? Create one"` link to `/auth/register` in `CardFooter` of `LoginPage.tsx` — alongside existing magic link and forgot password links

### Tests

- **D-23:** Add backend tests to `AuthTests.cs` covering AC-1 through AC-5: valid registration returns 200 + JWT, missing displayName → 400, password < 6 chars → 400, duplicate email → 409, self-registered user has role faction_commander

### Claude's Discretion

- FluentValidation vs DataAnnotations for `RegisterRequest` validation — follow whichever pattern `LoginRequest.cs` uses (convention consistency)
- Whether to use `UserManager.FindByEmailAsync` pre-check for duplicate or catch Identity errors — either is fine, prefer the cleaner approach
- Exact error field name in 400 response — match existing error response shape (e.g., `{ error: "..." }`)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### PRD (primary spec)
- `PRD-REGISTRATION.md` — Full feature spec, acceptance criteria AC-1 through AC-10, files to create/modify

### Existing auth implementation (pattern source)
- `milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs` — existing auth endpoint patterns, [AllowAnonymous] usage
- `milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs` — InviteUserAsync pattern to follow for RegisterAsync, GenerateJwt signature
- `milsim-platform/src/MilsimPlanning.Api/Models/Requests/InviteUserRequest.cs` — existing request model pattern (DataAnnotations)
- `milsim-platform/src/MilsimPlanning.Api/Models/Responses/AuthResponse.cs` — existing auth response shape (Token, ExpiresIn) — note: new RegisterResponse will be different

### Frontend pattern source
- `web/src/pages/auth/LoginPage.tsx` — styling template to match exactly; CardFooter link placement
- `web/src/main.tsx` — where to add /auth/register public route
- `web/src/lib/api.ts` — where to add registerUser() function
- `web/src/components/ProtectedRoute.tsx` — auth guard pattern (inverse needed for register page)
- `web/src/hooks/useAuth.ts` — login() method + isAuthenticated used by RegisterPage

### Codebase conventions
- `.planning/codebase/CONVENTIONS.md` — naming, component design, form patterns
- `.planning/codebase/STACK.md` — react-hook-form + Zod validation pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Card, CardHeader, CardTitle, CardContent, CardFooter` (shadcn/ui): LoginPage already uses these — RegisterPage must use the same
- `Input, Label, Button` (shadcn/ui): same form components as LoginPage
- `AuthService.GenerateJwt(user, role)`: reusable — RegisterAsync calls this after user creation
- `UserManager<AppUser>` / `SignInManager<AppUser>`: already injected into AuthService
- `useAuth()` hook — exposes `login(token)` for storing JWT and `isAuthenticated` for the guard
- `api.ts` typed fetch wrapper — registerUser() follows same pattern as other mutation calls

### Established Patterns
- `react-hook-form` + `zodResolver` for all forms — RegisterPage must use same
- `[AllowAnonymous]` on public auth endpoints — register is public, same pattern
- `UserProfile` entity created alongside `AppUser` in InviteUserAsync — RegisterAsync must do the same (DisplayName stored in UserProfile)
- `AddToRoleAsync(user, "faction_commander")` — exact call needed in RegisterAsync
- Error pattern in controllers: `catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }) }` — register can throw on duplicate or use pre-check

### Integration Points
- `AuthController.cs` — add Register action method
- `AuthService.cs` — add RegisterAsync method
- `main.tsx` — add `/auth/register` public route
- `LoginPage.tsx` — add "Create an account" link in CardFooter

</code_context>

<specifics>
## Specific Ideas

- PRD explicitly says: "Styling: match LoginPage.tsx exactly" — not "similar to", but exact match
- PRD explicitly says: "Follow InviteUserAsync pattern but skip email confirmation"
- PRD explicitly says: password confirmation mismatch must show client-side error WITHOUT API call
- AC-9: Authenticated users visiting /auth/register must redirect to /dashboard — enforce this

</specifics>

<deferred>
## Deferred Ideas

None — PRD scope is tight and well-defined. Discussion stayed within phase boundary.

</deferred>

---

*Phase: 01-foundation (registration feature)*
*Context gathered: 2026-03-25*
