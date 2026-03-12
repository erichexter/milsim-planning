---
phase: 1
slug: foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| **Full suite** | `dotnet test && pnpm test --run` |
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
| 1-01-01 | 01 | 1 | AUTH-01..06, AUTHZ-01..06 | unit | `dotnet test --filter Category=Unit` | MISSING W0 | pending |
| 1-01-02 | 01 | 1 | AUTH-01..06 | integration | `dotnet test --filter Category=Integration` | MISSING W0 | pending |
| 1-02-01 | 02 | 2 | AUTH-02,03,04,05,06 | integration | `dotnet test --filter Category=Integration` | MISSING W0 | pending |
| 1-02-02 | 02 | 2 | AUTH-01 | integration | `dotnet test --filter Category=Integration` | MISSING W0 | pending |
| 1-03-01 | 03 | 2 | AUTHZ-01..06 | integration | `dotnet test --filter Category=Integration` | MISSING W0 | pending |
| 1-03-02 | 03 | 2 | AUTHZ-06 | integration | `dotnet test --filter Category=Integration` | MISSING W0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `src/MilsimPlanning.Api.Tests/Auth/AuthTests.cs` — stubs for AUTH-02..06
- [ ] `src/MilsimPlanning.Api.Tests/Auth/InvitationTests.cs` — stubs for AUTH-01
- [ ] `src/MilsimPlanning.Api.Tests/Auth/MagicLinkTests.cs` — stubs for AUTH-03
- [ ] `src/MilsimPlanning.Api.Tests/Authorization/RbacTests.cs` — stubs for AUTHZ-01..05
- [ ] `src/MilsimPlanning.Api.Tests/Authorization/ScopeGuardTests.cs` — stubs for AUTHZ-06
- [ ] `src/MilsimPlanning.Api.Tests/Helpers/TestDbContext.cs` — Testcontainers setup helpers

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
