# Phase 5: Self-Service Registration - Research

**Researched:** 2026-03-26
**Domain:** ASP.NET Core Identity registration + React form page
**Confidence:** HIGH

## Summary

Phase 5 adds self-service registration to an existing auth system. The codebase is highly established — all patterns, components, and test infrastructure are already in place and well-understood. This is a narrow, additive change: one backend method, one backend endpoint, one frontend page, two small modifications to existing files, and five new integration tests.

The key design tension identified during research is the response shape mismatch. The PRD specifies `{ token, userId, email, displayName, role }` on success, but the existing `AuthResponse` record is `(string Token, int ExpiresIn)`. This is a real decision with two viable paths. Research recommends a dedicated `RegisterResponse` record rather than extending `AuthResponse`, so the two endpoints stay independently typed and `AuthResponse` continues to serve login and magic link unchanged.

The frontend auth guard pattern (D-14) has a subtlety: `useAuth` derives state from localStorage token, not from a React context. The hook uses `useState` initialised from `getToken()`, meaning `isAuthenticated` is available synchronously on render. A `useEffect` redirect is therefore safe and the correct pattern — no loading state needed.

**Primary recommendation:** Follow `InviteUserAsync` as the backend template (minus email sending, plus `EmailConfirmed = true` and role `faction_commander`). Model `RegisterPage.tsx` exactly on `LoginPage.tsx`. Add a dedicated `RegisterResponse` record for the richer success payload.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** POST /api/auth/register — [AllowAnonymous], follows InviteUserAsync pattern in AuthService
- **D-02:** Request model: `{ displayName, email, password }` — RegisterRequest.cs already exists with correct annotations
- **D-03:** User created immediately active (EmailConfirmed = true, no activation token)
- **D-04:** Assign `faction_commander` role to all self-registered users
- **D-05:** Success 200: `{ token, userId, email, displayName, role: "faction_commander" }`
- **D-06:** 400 on missing displayName, invalid email format, or password < 6 chars
- **D-07:** 409 on duplicate email
- **D-08:** Route: `/auth/register` — public route (no ProtectedRoute wrapper), added to main.tsx
- **D-09:** Fields: Display Name, Email, Password, Confirm Password — matching LoginPage.tsx structure exactly (Card + CardHeader + CardContent + CardFooter, react-hook-form + zod)
- **D-10:** Password confirm is client-side only via zod `.refine()` — no API call on mismatch (AC-10)
- **D-11:** On success: call `login(token)` from useAuth, then `navigate('/dashboard')`
- **D-12:** On 409: show "An account with this email already exists" (inline or toast — Claude's discretion, match LoginPage pattern)
- **D-13:** On 400: show specific error message from API response (Claude's discretion on placement)
- **D-14:** Auth guard: inline `useEffect` in RegisterPage — if authenticated, redirect to `/dashboard` immediately (AC-9)
- **D-15:** Add "Don't have an account? Create one" link to `/auth/register` in CardFooter — placement and exact wording at Claude's discretion, consistent with existing footer links
- **D-16:** Backend integration tests for AC-1 through AC-5 added to AuthTests.cs
- **D-17:** Follow existing test patterns (WebApplicationFactory, shared fixture, seeded test data)

### Claude's Discretion

- Exact error display style (inline vs toast) for server errors — match whatever feels consistent with the existing auth pages
- "Create an account" link position within CardFooter
- Auth guard implementation approach (useEffect vs loader) in RegisterPage

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AC-1 | Valid registration returns 200 + JWT | AuthService.GenerateJwt + new RegisterAsync pattern confirmed |
| AC-2 | Missing displayName returns 400 | RegisterRequest.cs [Required] attribute handles automatically via model binding |
| AC-3 | Password < 6 chars returns 400 | RegisterRequest.cs [MinLength(6)] handles automatically via model binding |
| AC-4 | Duplicate email returns 409 | UserManager.CreateAsync fails if email taken; controller maps to 409 |
| AC-5 | Self-registered user has role faction_commander | AddToRoleAsync("faction_commander") confirmed pattern from InviteUserAsync |
| AC-6 | /auth/register renders with all 4 fields | LoginPage.tsx template provides exact structure; add DisplayName + ConfirmPassword |
| AC-7 | Successful registration redirects to /dashboard | login(token) + navigate('/dashboard') — exact pattern from LoginPage |
| AC-8 | "Create an account" link visible on login page | CardFooter in LoginPage.tsx already has Link components; add one more |
| AC-9 | Authenticated users visiting /auth/register redirected to /dashboard | useAuth().isAuthenticated is synchronous from localStorage; useEffect redirect is safe |
| AC-10 | Password mismatch shows client-side error without API call | zod .refine() on schema level, before handleSubmit fires |
</phase_requirements>

## Standard Stack

### Core (already installed — no new dependencies required)

| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| ASP.NET Core Identity | .NET 10 | User creation, password hashing, role assignment | `UserManager<AppUser>` already wired |
| react-hook-form | existing | Form state, submission, error tracking | Used in LoginPage.tsx |
| @hookform/resolvers/zod | existing | Connects zod schema to react-hook-form | Used in LoginPage.tsx |
| zod | existing | Schema validation + `.refine()` for password confirm | Used in LoginPage.tsx |
| react-router | existing | Navigation after success, `useNavigate` | Used in LoginPage.tsx |
| sonner | existing | `toast.error()` for unexpected server errors | Used in LoginPage.tsx |

**No new packages needed.** This phase is purely additive using existing libraries.

### New file to create (backend)

`RegisterResponse.cs` — a new response record `(string Token, string UserId, string Email, string DisplayName, string Role)` in `MilsimPlanning.Api/Models/Responses/`. Do NOT reuse `AuthResponse`; it only carries `(Token, ExpiresIn)` and the register response requires four additional fields.

## Architecture Patterns

### Backend: RegisterAsync Method (follow InviteUserAsync)

The `InviteUserAsync` pattern in `AuthService.cs` is the direct template. Key differences for `RegisterAsync`:

- Accept `RegisterRequest` (already created) instead of `InviteUserRequest`
- Set `EmailConfirmed = true` (not false as in invite)
- Use the actual password from the request (not a generated temp password)
- Call `UserManager.CreateAsync(user, request.Password)` directly
- Create `UserProfile` with `DisplayName = request.DisplayName` (no callsign field on self-register)
- Call `AddToRoleAsync(user, "faction_commander")` — hardcoded, no role selection
- Skip all email sending
- Return a `RegisterResponse` (not `AppUser`)

**Error mapping in controller:**
- `UserManager.CreateAsync` fails with `DuplicateUserName` or `DuplicateEmail` errors → return 409
- Model binding validation failures ([Required], [MinLength(6)], [EmailAddress]) → automatic 400 from ASP.NET Core model state
- No `ArgumentException` needed — Identity errors are sufficient

### Backend: Controller Endpoint

Follow the `Login` endpoint pattern — `[HttpPost("register")]`, `[AllowAnonymous]`, delegate to `_authService.RegisterAsync(request)`, return `Ok(response)`.

The controller should inspect `UserManager` result errors for duplicate email detection. Identity returns `IdentityErrorDescriber.DuplicateEmail` (`code: "DuplicateEmail"`) or `DuplicateUserName` when email is already taken:

```csharp
// Source: existing AuthService.cs InviteUserAsync pattern (lines 93-95)
var createResult = await _userManager.CreateAsync(user, request.Password);
if (!createResult.Succeeded)
{
    bool isDuplicate = createResult.Errors.Any(e =>
        e.Code is "DuplicateEmail" or "DuplicateUserName");
    if (isDuplicate)
        return Conflict(new { error = "An account with this email already exists" });
    return BadRequest(new { error = string.Join("; ", createResult.Errors.Select(e => e.Description)) });
}
```

Recommendation: keep this logic in the controller (not AuthService) to keep the service return type clean. Alternatively, throw a custom exception from the service (existing `InvalidOperationException` pattern) and catch in controller — either works, use whichever feels cleaner.

### Frontend: RegisterPage Structure

The `LoginPage.tsx` is the exact template. Structure to replicate:

```
div.flex.min-h-screen.items-center.justify-center.p-4
  Card.w-full.max-w-sm
    CardHeader > CardTitle "Create Account"
    CardContent
      form onSubmit={handleSubmit(onSubmit)} className="space-y-4"
        // Display Name field (new)
        // Email field (copy from LoginPage)
        // Password field (copy from LoginPage, change autoComplete to "new-password")
        // Confirm Password field (new)
        // Submit button
    CardFooter className="flex flex-col gap-2 text-sm"
      // Back to sign in link
```

### Frontend: Zod Schema with Password Confirm

```typescript
// .refine() runs client-side only — no API call on mismatch (AC-10)
const schema = z.object({
  displayName: z.string().min(1, 'Display name is required'),
  email: z.string().email('Invalid email address'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
  confirmPassword: z.string().min(1, 'Please confirm your password'),
}).refine((data) => data.password === data.confirmPassword, {
  message: "Passwords do not match",
  path: ["confirmPassword"],
});
```

### Frontend: Auth Guard (D-14, AC-9)

`useAuth` initialises `user` state synchronously from localStorage via `useState(() => { ... })`. `isAuthenticated` is available on first render — no async loading state. A simple `useEffect` redirect is the correct pattern:

```typescript
// Source: useAuth.ts lines 27-31 — useState init is synchronous
const { isAuthenticated } = useAuth();
const navigate = useNavigate();

useEffect(() => {
  if (isAuthenticated) navigate('/dashboard', { replace: true });
}, [isAuthenticated, navigate]);
```

No loading spinner needed. The redirect fires synchronously before the user sees the form.

### Frontend: Error Handling Pattern

The `api.ts` error throwing pattern (lines 23-24) is:
```typescript
throw Object.assign(new Error(error.error ?? 'API error'), { status: response.status });
```

So in `onSubmit`, check `(err as any).status`:

```typescript
const onSubmit = async (data: RegisterForm) => {
  try {
    const result = await api.post<RegisterResponse>('/auth/register', {
      displayName: data.displayName,
      email: data.email,
      password: data.password,
    });
    login(result.token);
    navigate('/dashboard');
  } catch (err) {
    if ((err as any).status === 409) {
      // Inline error on email field (matches LoginPage inline error style)
      setError('email', { message: 'An account with this email already exists' });
    } else {
      toast.error((err as Error).message ?? 'Registration failed');
    }
  }
};
```

Recommendation for D-12/D-13: Use `setError('email', {...})` for 409 (shows inline under the email field, consistent with zod inline errors) and `toast.error()` for unexpected 400/500 errors. This is the closest match to LoginPage's style which also uses inline `errors.field.message` for field errors.

Note: `useForm` must destructure `setError` from the hook call:
```typescript
const { register, handleSubmit, formState: { errors, isSubmitting }, setError } = useForm<RegisterForm>(...);
```

### Frontend: Route Registration

Add to `main.tsx` public auth routes block (lines 31-35):
```typescript
{ path: '/auth/register', element: <RegisterPage /> },
```

No import of `ProtectedRoute` needed — public routes have no wrapper.

### Frontend: api.ts Function

Add to the `api` object in `web/src/lib/api.ts`:
```typescript
registerUser: (req: { displayName: string; email: string; password: string }) =>
  request<RegisterResponse>('/auth/register', { method: 'POST', body: JSON.stringify(req) }),
```

And add the `RegisterResponse` interface at the bottom of the file alongside other interfaces:
```typescript
export interface RegisterResponse {
  token: string;
  userId: string;
  email: string;
  displayName: string;
  role: string;
}
```

Alternatively, the page can call `api.post<RegisterResponse>(...)` directly (as LoginPage does for login) without adding a named method. Either approach is consistent with existing usage. Using a named method on the `api` object is slightly more explicit.

### Recommended Project Structure

No new directories needed. Files to create:

```
milsim-platform/src/MilsimPlanning.Api/Models/Responses/
└── RegisterResponse.cs           (new — register-specific response shape)

web/src/pages/auth/
└── RegisterPage.tsx              (new — mirrors LoginPage.tsx)
```

Files to modify:

```
milsim-platform/src/MilsimPlanning.Api/Services/AuthService.cs   (add RegisterAsync)
milsim-platform/src/MilsimPlanning.Api/Controllers/AuthController.cs  (add Register endpoint)
milsim-platform/src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs   (add 5 tests)
web/src/lib/api.ts               (add registerUser + RegisterResponse interface)
web/src/pages/auth/LoginPage.tsx  (add "Create an account" link in CardFooter)
web/src/main.tsx                  (add /auth/register route)
```

`RegisterRequest.cs` already exists — do not recreate.

### Anti-Patterns to Avoid

- **Reusing AuthResponse for register:** `AuthResponse` is `(Token, ExpiresIn)`. The register success shape needs `userId`, `email`, `displayName`, `role`. Adding optional fields to `AuthResponse` would break the clean record type and confuse the login/magic-link flow. Create a separate `RegisterResponse`.
- **EmailConfirmed = false:** The invite flow sets this to false and sends an activation email. Register must set `EmailConfirmed = true` so the user can sign in immediately with `PasswordSignInAsync` (which respects the confirmed email state).
- **Wrapping /auth/register in ProtectedRoute:** Auth pages are public in this codebase. The guard is implemented inside the page via `useEffect`, not at the router level. Consistent with how LoginPage handles the case (LoginPage redirects authenticated users via the ProtectedRoute catching them before they navigate to /auth/login, but RegisterPage does it inline).
- **Password confirm sent to API:** D-10 is explicit — `confirmPassword` is zod-only. Strip it from the API call payload.
- **Using `<Navigate>` render instead of `useEffect` for auth guard:** `<Navigate>` on render would cause a flash if isAuthenticated is true, and could conflict with React Router's rendering. The `useEffect` approach is cleaner for this use case.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Password hashing | Custom hashing | `UserManager.CreateAsync(user, password)` | Identity handles bcrypt, salt, policy |
| Email uniqueness check | Manual DB query | UserManager returns `DuplicateEmail` error | Atomic check-and-create, no race condition |
| JWT generation | Custom JWT builder | `AuthService.GenerateJwt()` already exists | Reuse — it already handles claims, expiry, signing |
| Form validation | Custom validators | zod schema + react-hook-form | Established pattern; handles async, touch, submit states |
| Token storage | Custom storage | `login(token)` from useAuth + existing auth.ts | Stores in localStorage with correct key; sets React state |

## Common Pitfalls

### Pitfall 1: Identity Password Policy vs. MinLength(6)

**What goes wrong:** ASP.NET Core Identity has a default password policy (`RequireDigit`, `RequireUppercase`, `RequireLowercase`, `RequireNonAlphanumeric`, all true, minimum length 6). A password like "abcdef" satisfies `[MinLength(6)]` at model binding but fails Identity's complexity requirements. The `UserManager.CreateAsync` call would return errors.

**Why it happens:** Model binding validates the data annotation; Identity validates its own policy separately.

**How to avoid:** Check `Program.cs` identity options to understand what password policy is actually configured. If the policy requires uppercase/digits/symbols, the 400 response for simple passwords will come from Identity (not model binding), which is fine — it returns a descriptive error. Ensure the controller handles non-duplicate `CreateAsync` failures with a 400 response.

**Warning signs:** Tests for AC-3 ("password < 6 chars returns 400") pass, but a test with "abcdef" (6 chars, no complexity) might fail unexpectedly with a 400 that has a different error message than expected.

### Pitfall 2: UserProfile Navigation Property

**What goes wrong:** `AppUser.Profile` is a required navigation property. If `RegisterAsync` creates the user but fails to create/attach the `UserProfile`, subsequent profile reads will throw or return null.

**Why it happens:** `InviteUserAsync` creates the profile by setting `user.Profile = new UserProfile { ... }` then calling `UpdateAsync`. This pattern must be replicated exactly.

**How to avoid:** Follow `InviteUserAsync` lines 98-106 exactly — set `user.Profile` then call `_userManager.UpdateAsync(user)`. For self-registration, there is no callsign (not in RegisterRequest), so `Callsign` can be null or empty string — check what the `UserProfile` entity allows.

**Warning signs:** Null reference exceptions on profile access after registration; login works but `/profile` endpoint fails.

### Pitfall 3: api.ts 401 Handling Intercepts Register Call

**What goes wrong:** The `api.ts` request function (line 18-22) redirects to `/auth/login` and returns `undefined` on any 401 response. Register should never return 401, so this is not a concern for the happy path. However, if the register endpoint is accidentally decorated with `[Authorize]` instead of `[AllowAnonymous]`, the frontend will silently redirect instead of showing an error.

**Why it happens:** Copy-paste from a protected endpoint.

**How to avoid:** Verify `[AllowAnonymous]` is on the Register endpoint before testing. Confirmed by D-01.

### Pitfall 4: confirmPassword Included in API Payload

**What goes wrong:** If `RegisterPage` passes the full form data object to `api.post('/auth/register', data)`, it includes `confirmPassword` in the request body. The backend's `RegisterRequest` doesn't have a `confirmPassword` field, which causes no error (ASP.NET Core ignores unknown properties) — but it's a leak of unintended data.

**Why it happens:** LoginPage passes `data` directly. RegisterPage would do the same.

**How to avoid:** Explicitly destructure: `api.post('/auth/register', { displayName: data.displayName, email: data.email, password: data.password })`.

## Code Examples

### AuthService.RegisterAsync (template from InviteUserAsync)

```csharp
// Source: AuthService.cs lines 81-120 (InviteUserAsync pattern)
public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
{
    var user = new AppUser
    {
        UserName = request.Email,
        Email = request.Email,
        EmailConfirmed = true  // immediately active, no invite flow
    };

    var createResult = await _userManager.CreateAsync(user, request.Password);
    if (!createResult.Succeeded)
    {
        bool isDuplicate = createResult.Errors.Any(e =>
            e.Code is "DuplicateEmail" or "DuplicateUserName");
        throw isDuplicate
            ? new DuplicateEmailException()                   // custom or use InvalidOperationException with code
            : new InvalidOperationException(
                string.Join("; ", createResult.Errors.Select(e => e.Description)));
    }

    user.Profile = new UserProfile
    {
        UserId = user.Id,
        Callsign = null,            // not collected on self-registration
        DisplayName = request.DisplayName,
        User = user
    };
    await _userManager.UpdateAsync(user);
    await _userManager.AddToRoleAsync(user, "faction_commander");

    var token = GenerateJwt(user, "faction_commander");
    return new RegisterResponse(token, user.Id, user.Email!, request.DisplayName, "faction_commander");
}
```

Alternative: Return result data from service and let controller map errors from IdentityResult directly (no exceptions). Either approach works; the exception approach matches the existing `InviteUserAsync` pattern more closely.

### AuthController Register Endpoint

```csharp
// Source: AuthController.cs login/invite patterns (lines 37-48, 136-141)
[HttpPost("register")]
[AllowAnonymous]
public async Task<IActionResult> Register(RegisterRequest request)
{
    try
    {
        var response = await _authService.RegisterAsync(request);
        return Ok(response);
    }
    catch (DuplicateEmailException)
    {
        return Conflict(new { error = "An account with this email already exists" });
    }
}
```

### Integration Test Structure for AC-1 to AC-5

```csharp
// Source: AuthTests.cs test pattern (lines 109-130)
[Fact]
[Trait("Category", "Auth_Register")]
public async Task Register_WithValidData_Returns200AndJwt()
{
    var email = $"register-valid-{Guid.NewGuid():N}@test.com";
    var response = await _client.PostAsJsonAsync("/api/auth/register", new
    {
        displayName = "Test User",
        email,
        password = "TestPass123!"
    });
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    body.GetProperty("role").GetString().Should().Be("faction_commander");  // AC-5
}
```

## State of the Art

This phase uses no new libraries — research focused on verifying existing patterns.

| Area | Current Pattern | Notes |
|------|----------------|-------|
| Identity duplicate detection | Check `IdentityResult.Errors[].Code` for `"DuplicateEmail"` | Identity 2.x+ stable |
| Form validation | zod `.refine()` for cross-field rules | Established in this codebase |
| Auth guard on public route | `useEffect` + `useNavigate` | Synchronous because useAuth reads localStorage in useState init |

## Open Questions

1. **UserProfile.Callsign nullability**
   - What we know: `InviteUserAsync` always sets a callsign. Self-registration has no callsign field.
   - What's unclear: Whether `UserProfile.Callsign` is nullable in the DB schema (the entity shows `string?` in `useAuth.ts` line 9, suggesting null is valid).
   - Recommendation: Assume nullable (consistent with how `useAuth` handles it); set to `null` in `RegisterAsync`. Planner should verify the `UserProfile` entity definition.

2. **Identity password policy configuration**
   - What we know: Default Identity policy requires uppercase, digit, and non-alphanumeric characters.
   - What's unclear: Whether `Program.cs` relaxes these requirements (common for MVP projects).
   - Recommendation: Planner should read `Program.cs` identity configuration. If policy is strict, the 6-char min from model binding is redundant but harmless. Test AC-3 with a truly invalid password like "abc" (< 6 chars) which fails both checks.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Backend build + tests | Yes | 10.0.201 | — |
| Node.js | Frontend build | Yes | v24.14.0 | — |
| npm | Frontend packages | Yes | 11.9.0 | — |
| Docker | Testcontainers (PostgreSQL) | Unknown | — | Must verify before running tests |

**Missing dependencies with no fallback:**
- Docker: Required by `PostgreSqlFixture` (Testcontainers.PostgreSql). Tests will fail if Docker is not running. Planner should include a step verifying Docker availability before running integration tests.

**Missing dependencies with fallback:**
- None.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (existing, MilsimPlanning.Api.Tests project) |
| Config file | None — uses WebApplicationFactory + Testcontainers |
| Quick run command | `dotnet test --filter "Category=Auth_Register" milsim-platform/src/MilsimPlanning.Api.Tests/` |
| Full suite command | `dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AC-1 | Valid registration returns 200 + JWT | Integration | `dotnet test --filter "Category=Auth_Register"` | Will exist after Wave 1 |
| AC-2 | Missing displayName returns 400 | Integration | `dotnet test --filter "Category=Auth_Register"` | Will exist after Wave 1 |
| AC-3 | Password < 6 chars returns 400 | Integration | `dotnet test --filter "Category=Auth_Register"` | Will exist after Wave 1 |
| AC-4 | Duplicate email returns 409 | Integration | `dotnet test --filter "Category=Auth_Register"` | Will exist after Wave 1 |
| AC-5 | Self-registered user has faction_commander role | Integration | `dotnet test --filter "Category=Auth_Register"` | Will exist after Wave 1 |
| AC-6 | RegisterPage renders with 4 fields | Manual / visual | n/a (no test framework for React pages in this project) | n/a |
| AC-7 | Successful registration redirects to /dashboard | Manual / smoke | n/a | n/a |
| AC-8 | "Create an account" link visible on login page | Manual / visual | n/a | n/a |
| AC-9 | Authenticated users redirected from /auth/register | Manual / smoke | n/a | n/a |
| AC-10 | Password mismatch shows client-side error | Manual / smoke | n/a | n/a |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "Category=Auth_Register" milsim-platform/src/MilsimPlanning.Api.Tests/`
- **Per wave merge:** `dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/`
- **Phase gate:** Full test suite green + manual smoke of AC-6 through AC-10 before `/gsd:verify-work`

### Wave 0 Gaps

None — existing test infrastructure covers all backend test requirements. `AuthTests.cs` already uses the shared `PostgreSqlFixture`, xUnit `IClassFixture`, and `WebApplicationFactory` pattern. New tests for AC-1 through AC-5 are additions to this existing file.

Frontend acceptance criteria (AC-6 through AC-10) have no automated test coverage in this project — manual smoke testing at phase gate.

## Sources

### Primary (HIGH confidence)

All findings are derived directly from the project codebase. No external documentation was required — the existing patterns are the authoritative source.

- `AuthController.cs` — endpoint patterns, `[AllowAnonymous]`, error shapes
- `AuthService.cs` — `InviteUserAsync` as template for `RegisterAsync`
- `AuthTests.cs` — test infrastructure, fixture usage, trait patterns
- `LoginPage.tsx` — UI template (Card structure, zod schema, react-hook-form, error display)
- `web/src/lib/api.ts` — error throwing convention (`.status` on Error), `api.post<T>()` usage
- `web/src/hooks/useAuth.ts` — `login(token)` function, synchronous `isAuthenticated` from localStorage
- `RegisterRequest.cs` — already exists with correct annotations
- `AuthResponse.cs` — confirms new `RegisterResponse` record is needed

### Secondary (MEDIUM confidence)

- ASP.NET Core Identity `IdentityResult.Errors[].Code` values (`"DuplicateEmail"`, `"DuplicateUserName"`) — well-known constants, consistent across Identity 2.x

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries verified by reading actual project code
- Architecture patterns: HIGH — derived from existing production code in the same codebase
- Pitfalls: HIGH for P1/P2/P3 (codebase-specific), MEDIUM for P4 (general ASP.NET Core knowledge)
- Test patterns: HIGH — test file read directly

**Research date:** 2026-03-26
**Valid until:** 2026-04-26 (stable codebase; only changes if Phase 5 implementation itself changes auth infrastructure)
