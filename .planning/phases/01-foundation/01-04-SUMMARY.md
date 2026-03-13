---
phase: 01-foundation
plan: 04
subsystem: ui
tags: [react, vite, typescript, vitest, react-router, tanstack-query, react-hook-form, zod, shadcn, tailwind, jwt, localStorage]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: Auth API endpoints (login, magic-link GET/POST, password-reset, logout) with JWT token response
provides:
  - Vite 8 React TypeScript SPA in web/
  - auth.ts: JWT localStorage helpers (getToken/setToken/clearToken/parseJwt/isTokenExpired)
  - api.ts: typed fetch wrapper with automatic Bearer token injection
  - useAuth.ts: React hook with localStorage persistence on mount (AUTH-04)
  - ProtectedRoute.tsx: redirects unauthenticated users to /auth/login
  - All 5 auth pages: LoginPage, MagicLinkRequestPage, MagicLinkConfirmPage (button-click only), PasswordResetRequestPage, PasswordResetConfirmPage
  - DashboardPage: authenticated placeholder
  - React Router routes wiring all auth paths and protected dashboard
  - 17 passing vitest tests covering auth helpers, API client, useAuth hook, ProtectedRoute, MagicLinkConfirmPage
affects:
  - all future phases (SPA is the user-facing surface; auth wiring used throughout)

# Tech tracking
tech-stack:
  added:
    - vite 8.0.0 + @vitejs/plugin-react 6.0.1
    - react 19.2.4 + react-dom 19.2.4
    - react-router 7.13.1
    - '@tanstack/react-query 5.90.21'
    - react-hook-form 7.71.2
    - zod 4.3.6 + @hookform/resolvers 5.2.2
    - sonner 2.0.7 (toasts)
    - tailwindcss 4.2.1 + @tailwindcss/vite 4.2.1
    - class-variance-authority + clsx + tailwind-merge (shadcn/ui primitives)
    - '@radix-ui/react-label + @radix-ui/react-slot'
    - lucide-react 0.577.0
    - vitest 4.1.0 + @testing-library/react + @testing-library/user-event + happy-dom + msw
  patterns:
    - localStorage JWT persistence via useState initializer (not useEffect) — AUTH-04
    - Bearer token injection in api.ts request function
    - MagicLinkConfirmPage button-click-only confirm (no useEffect — email scanner protection)
    - ProtectedRoute with Navigate+replace to /auth/login
    - TDD for auth layer: RED failing tests → GREEN implementation → build verification

key-files:
  created:
    - web/package.json
    - web/vite.config.ts
    - web/tsconfig.json
    - web/tsconfig.app.json
    - web/tsconfig.node.json
    - web/index.html
    - web/src/index.css
    - web/src/lib/auth.ts
    - web/src/lib/api.ts
    - web/src/lib/utils.ts
    - web/src/hooks/useAuth.ts
    - web/src/components/ProtectedRoute.tsx
    - web/src/components/ui/button.tsx
    - web/src/components/ui/input.tsx
    - web/src/components/ui/label.tsx
    - web/src/components/ui/card.tsx
    - web/src/pages/auth/LoginPage.tsx
    - web/src/pages/auth/MagicLinkRequestPage.tsx
    - web/src/pages/auth/MagicLinkConfirmPage.tsx
    - web/src/pages/auth/PasswordResetRequestPage.tsx
    - web/src/pages/auth/PasswordResetConfirmPage.tsx
    - web/src/pages/DashboardPage.tsx
    - web/src/__tests__/auth.test.ts
    - web/src/__tests__/api.test.ts
    - web/src/__tests__/useAuth.test.tsx
    - web/src/__tests__/ProtectedRoute.test.tsx
    - web/src/__tests__/MagicLinkConfirmPage.test.tsx
  modified:
    - web/src/main.tsx (replaced default Vite template with full router setup)
    - web/pnpm-lock.yaml

key-decisions:
  - "shadcn/ui components scaffolded manually (not via pnpm dlx shadcn init) — shadcn@latest init is interactive and cannot run non-interactively in this environment; components created directly from shadcn source"
  - "vitest `/// <reference types=\"vitest\" />` added to vite.config.ts header; vitest/config added to tsconfig.node.json types array — required for `test` field in vite.config.ts to type-check"
  - "test mock reset with mockFetch.mockReset() in beforeEach — happy-dom shares vi.fn() state across tests; explicit reset required to prevent cross-test auth header bleed"
  - "Tailwind CSS v4 with @tailwindcss/vite plugin — single `@import tailwindcss` in index.css, no tailwind.config.js needed"

patterns-established:
  - "useAuth localStorage persistence: useState initializer reads getToken() synchronously on mount — guarantees session survives page refresh without useEffect flicker"
  - "MagicLinkConfirmPage: never call api.post in useEffect/onMount — always require explicit button click (email scanner protection established in Plan 01-02)"
  - "api.ts request function: single place for Bearer injection, error handling, JSON parsing"
  - "TDD for React hooks/components: RED test suite committed, GREEN implementation makes all pass, build verification final gate"

requirements-completed:
  - AUTH-04

# Metrics
duration: 9min
completed: 2026-03-13
---

# Phase 1 Plan 04: React SPA Foundation Summary

**Vite 8 React TypeScript SPA with localStorage JWT persistence (AUTH-04), typed API client with Bearer injection, ProtectedRoute, 5 auth pages (including button-click magic link confirm for email scanner protection), and 17 passing vitest tests**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-13T13:44:35Z
- **Completed:** 2026-03-13T13:54:00Z
- **Tasks:** 2
- **Files modified:** 27 (26 new + 1 modified)

## Accomplishments

- Vite 8 + React 19 + TypeScript SPA scaffolded in `web/` with pnpm, Tailwind CSS v4, React Router, TanStack Query, React Hook Form + Zod, Sonner toasts, and shadcn/ui components
- Auth layer: `auth.ts` (localStorage helpers), `api.ts` (typed fetch with Bearer injection), `useAuth.ts` (hook with localStorage persistence via useState initializer on mount — AUTH-04)
- All 5 auth pages with correct security properties: MagicLinkConfirmPage shows button only — no useEffect auto-submit (maintains email scanner protection from Plan 01-02)
- ProtectedRoute redirects unauthenticated users to `/auth/login`; React Router wires all auth paths and protected `/dashboard`
- 17 vitest tests covering all behaviors specified in the plan's `<behavior>` sections — all passing

## Task Commits

Each task was committed atomically:

1. **Task 1: Vite scaffold, auth helpers, API client, and useAuth hook** - `d775cc3` (feat)
2. **Task 2: ProtectedRoute, auth pages, and router wiring** - `be2f75c` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `web/src/lib/auth.ts` — JWT localStorage helpers (getToken/setToken/clearToken/parseJwt/isTokenExpired)
- `web/src/lib/api.ts` — typed fetch wrapper with Authorization: Bearer injection
- `web/src/lib/utils.ts` — shadcn/ui cn() utility
- `web/src/hooks/useAuth.ts` — React auth hook with localStorage persistence (useState initializer)
- `web/src/components/ProtectedRoute.tsx` — Navigate to /auth/login when !isAuthenticated
- `web/src/components/ui/button.tsx` — shadcn Button component
- `web/src/components/ui/input.tsx` — shadcn Input component
- `web/src/components/ui/label.tsx` — shadcn Label component
- `web/src/components/ui/card.tsx` — shadcn Card component
- `web/src/pages/auth/LoginPage.tsx` — email/password form → calls /auth/login → login(token)
- `web/src/pages/auth/MagicLinkRequestPage.tsx` — email form → calls /auth/magic-link
- `web/src/pages/auth/MagicLinkConfirmPage.tsx` — button-click confirm → /auth/magic-link/confirm (NO auto-submit)
- `web/src/pages/auth/PasswordResetRequestPage.tsx` — email form → /auth/password-reset
- `web/src/pages/auth/PasswordResetConfirmPage.tsx` — new password form with match validation → /auth/password-reset/confirm
- `web/src/pages/DashboardPage.tsx` — authenticated placeholder with logout
- `web/src/main.tsx` — React Router + QueryClient + Toaster setup
- `web/src/__tests__/auth.test.ts` — 6 tests for auth helpers
- `web/src/__tests__/api.test.ts` — 3 tests for API client Bearer injection
- `web/src/__tests__/useAuth.test.tsx` — 4 tests for useAuth hook persistence
- `web/src/__tests__/ProtectedRoute.test.tsx` — 2 tests for redirect and render
- `web/src/__tests__/MagicLinkConfirmPage.test.tsx` — 2 tests for email scanner protection
- `web/vite.config.ts` — Vite config with Tailwind, path alias, test config, dev proxy
- `web/tsconfig.app.json` — TypeScript app config with @/* path alias
- `web/tsconfig.node.json` — Node config with vitest/config type
- `web/package.json` — full dependency set

## Decisions Made

- **shadcn/ui scaffolded manually**: `pnpm dlx shadcn@latest init` is interactive and cannot be run non-interactively. Components created directly from shadcn source code — functionally identical to the CLI output.
- **vitest type reference in tsconfig.node.json**: The `test` field in vite.config.ts requires `vitest/config` in `types[]` of the tsconfig that covers vite.config.ts (tsconfig.node.json). Adding `/// <reference types="vitest" />` alone was insufficient.
- **mockFetch.mockReset() in beforeEach**: vitest's `vi.fn()` accumulates calls across tests in the same describe block. Without explicit reset, the second api test read the Authorization header from the first test's call — cross-test bleed.
- **Tailwind CSS v4**: Uses `@import "tailwindcss"` with `@tailwindcss/vite` plugin — no tailwind.config.js or postcss.config.js needed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] vitest `test` field not recognized in vite.config.ts**
- **Found during:** Task 1 (build verification)
- **Issue:** `tsc -b` reported `Object literal may only specify known properties, and 'test' does not exist in type 'UserConfigExport'` — vitest types not loaded for vite.config.ts
- **Fix:** Added `/// <reference types="vitest" />` to vite.config.ts AND added `"vitest/config"` to `types[]` array in `tsconfig.node.json` (which covers vite.config.ts)
- **Files modified:** web/vite.config.ts, web/tsconfig.node.json
- **Verification:** `pnpm build` exits 0 with no TypeScript errors
- **Committed in:** d775cc3 (Task 1 commit)

**2. [Rule 1 - Bug] Unused import caused TypeScript build error**
- **Found during:** Task 1 (build verification)
- **Issue:** `clearToken` unused in useAuth.test.tsx; `Outlet` unused in ProtectedRoute.test.tsx — TS6133 errors with `noUnusedLocals: true`
- **Fix:** Removed unused imports from test files
- **Files modified:** web/src/__tests__/useAuth.test.tsx, web/src/__tests__/ProtectedRoute.test.tsx
- **Verification:** `pnpm build` exits 0
- **Committed in:** d775cc3, be2f75c (respective task commits)

**3. [Rule 1 - Bug] vi.fn() mock accumulated calls across tests causing cross-test bleed**
- **Found during:** Task 1 (test suite run)
- **Issue:** api.test.ts second test read Authorization header from first test's fetch call (`mockFetch.mock.calls[0]` referenced stale call)
- **Fix:** Added `mockFetch.mockReset()` in `beforeEach` to clear accumulated mock call history
- **Files modified:** web/src/__tests__/api.test.ts
- **Verification:** All 13 tests pass consistently
- **Committed in:** d775cc3 (Task 1 commit)

---

**Total deviations:** 3 auto-fixed (3 bugs — TypeScript build errors and test isolation)
**Impact on plan:** All auto-fixes required for correctness. No scope creep.

## Issues Encountered

None — all issues were auto-fixed inline (see Deviations above).

## User Setup Required

None - no external service configuration required. Run `cd web && pnpm dev` to start the dev server after ensuring the .NET API is running at `http://localhost:5000`.

## Next Phase Readiness

- Phase 1 complete: .NET 10 API (solution scaffold, auth endpoints, RBAC) + React SPA (auth UI, JWT state, protected routing) — all 4 plans executed
- SPA connects to auth endpoints from Plan 01-02 via the `/api` proxy → `http://localhost:5000`
- AUTH-04 (session persistence) implemented and verified by test
- Ready for Phase 2: commander workflow features

## Self-Check: PASSED

All key files found on disk. Task commits d775cc3 and be2f75c verified in git log.

---
*Phase: 01-foundation*
*Completed: 2026-03-13*
