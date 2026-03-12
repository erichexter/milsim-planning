---
phase: 1
slug: foundation
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-12
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **API Framework** | xUnit + WebApplicationFactory + Testcontainers.PostgreSql |
| **React Framework** | Vitest + @testing-library/react |
| **Config files** | none — Wave 0 installs both |
| **Quick run (API)** | `dotnet test --filter Category=Unit` |
| **Quick run (React)** | `pnpm test --run` |
| **Full suite** | `dotnet test milsim-platform.sln && cd web && pnpm test --run` |
| **Estimated runtime** | ~20s (unit), ~60s (integration with Testcontainers) |

---

## Sampling Rate

- **After every task commit:** Run quick suite for the layer being modified
- **After every plan wave:** Run full suite (dotnet test + pnpm test --run)
- **Before /gsd-verify-work:** Full suite must be green
- **Max feedback latency:** 20s (unit), 60s (integration)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | AUTHZ-01..06 | unit | `dotnet build src/MilsimPlanning.Api` | Created in 01-01 | pending |
| 1-01-02 | 01 | 1 | AUTHZ-01..06 | unit | `dotnet ef migrations list --project src/MilsimPlanning.Api` | Created in 01-01 | pending |
| 1-02-01 | 02 | 2 | AUTH-01..06 | unit | `dotnet test src/MilsimPlanning.Api.Tests --filter Category!=Integration` | `src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` | pending |
| 1-02-02 | 02 | 2 | AUTH-02,03,04,05,06 | integration | `dotnet test src/MilsimPlanning.Api.Tests --filter Category=Auth_Login` | `src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` | pending |
| 1-02-03 | 02 | 2 | AUTH-01 | integration | `dotnet test src/MilsimPlanning.Api.Tests --filter Category=Auth_Invitation` | `src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` | pending |
| 1-03-01 | 03 | 3 | AUTHZ-01..06 | integration | `dotnet test src/MilsimPlanning.Api.Tests --filter Category=Authz_Roles` | `src/MilsimPlanning.Api.Tests/Authorization/AuthorizationTests.cs` | pending |
| 1-03-02 | 03 | 3 | AUTHZ-06 | integration | `dotnet test src/MilsimPlanning.Api.Tests --filter Category=Authz_IDOR` | `src/MilsimPlanning.Api.Tests/Authorization/AuthorizationTests.cs` | pending |
| 1-04-01 | 04 | 3 | AUTH-04 | build | `cd web && pnpm build` | `web/src/lib/auth.ts` | pending |
| 1-04-02 | 04 | 3 | AUTH-04 | build | `cd web && pnpm build` | `web/src/hooks/useAuth.ts` | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` — stubs for AUTH-01..06 (all auth tests including invitation, magic link, lockout)
- [ ] `src/MilsimPlanning.Api.Tests/Authorization/AuthorizationTests.cs` — stubs for AUTHZ-01..06 (roles, IDOR, email visibility, scope guard)
- [ ] `src/MilsimPlanning.Api.Tests/Fixtures/PostgreSqlFixture.cs` — Testcontainers setup helper

Both files are created in Plan 01-02 Task 1 before any integration tests are written.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Invitation email received in inbox | AUTH-01 | Email delivery to real inbox | Use Resend test mode; check dashboard for delivery receipt |
| Magic link email received and link works | AUTH-03 | Real email delivery | Confirm single-use: visiting URL a second time returns error page |
| Magic link confirm page shows button (not auto-login) | AUTH-03 | Email scanner protection | Verify /auth/magic-link/confirm renders button before token exchange |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 20s (unit), 60s (integration)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
