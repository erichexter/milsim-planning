# Phase 1: Self-Service Registration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves how decisions were made.

**Date:** 2026-03-25
**Phase:** 01-foundation (self-service registration feature)
**Mode:** Auto — all decisions derived from PRD-REGISTRATION.md, no interactive questions
**Areas discussed:** Backend Registration API, Frontend RegisterPage, Auth Guard, Routing, LoginPage Update, Tests

---

## Mode: PRD Auto-Derive

| Area | Decision Source | Selected |
|------|----------------|----------|
| Backend endpoint | PRD §"Backend: POST /api/auth/register" | ✓ POST /api/auth/register, AllowAnonymous |
| Request model | PRD §"Backend" | ✓ RegisterRequest { displayName, email, password } |
| Service pattern | PRD "Follow InviteUserAsync pattern but skip email confirmation" | ✓ RegisterAsync(), immediately active |
| Default role | PRD "Assign faction_commander role to all self-registered users" | ✓ faction_commander |
| Response shape | PRD "Success 200: { token, userId, email, displayName, role }" | ✓ New RegisterResponse record |
| Error codes | PRD "400 validation, 409 duplicate email" | ✓ 400 + 409 |
| Frontend page | PRD §"Frontend: RegisterPage.tsx" | ✓ /auth/register, match LoginPage exactly |
| Form fields | PRD "Display Name, Email, Password, Confirm Password" | ✓ 4 fields with Zod confirm validation |
| Success flow | PRD "store JWT, redirect to /dashboard" | ✓ login() + navigate('/dashboard') |
| Error display | PRD "On 409: specific message; On 400: API error" | ✓ inline error, not toast |
| Password mismatch | PRD "client-side error without API call" | ✓ Zod refine, no submit |
| Auth guard | PRD "Auth guard on /auth/register (redirect if authenticated)" | ✓ Inline in RegisterPage using useAuth |
| Login link | PRD "Don't have an account? Create one" link below form | ✓ CardFooter of LoginPage |
| Route | PRD §"Files to Modify: web/src/main.tsx" | ✓ Public route in main.tsx |
| Tests | PRD §"Files to Modify: AuthTests.cs: add tests for AC-1 through AC-5" | ✓ Backend tests |

---

## Claude's Discretion

- FluentValidation vs DataAnnotations for RegisterRequest — follow LoginRequest.cs convention
- Duplicate email detection approach — pre-check or catch Identity errors, either acceptable
- Exact error field name in 400 response — match existing `{ error: "..." }` shape

## Deferred Ideas

None — PRD scope was tight and complete.
