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
| **Framework** | Vitest (unit/integration) + Playwright (E2E) |
| **Config file** | vitest.config.ts / playwright.config.ts (none — Wave 0 installs) |
| **Quick run command** | `npx vitest run --reporter=verbose` |
| **Full suite command** | `npx vitest run && npx playwright test` |
| **Estimated runtime** | ~30 seconds (unit), ~120 seconds (full with Playwright) |

---

## Sampling Rate

- **After every task commit:** Run `npx vitest run --reporter=verbose`
- **After every plan wave:** Run `npx vitest run && npx playwright test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds (unit), 120 seconds (full)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | AUTH-01..06, AUTHZ-01..06 | unit | `npx vitest run src/lib/auth` | MISSING W0 | pending |
| 1-01-02 | 01 | 1 | AUTH-01..06 | integration | `npx vitest run tests/auth` | MISSING W0 | pending |
| 1-02-01 | 02 | 1 | AUTH-02,03 | E2E | `npx playwright test auth` | MISSING W0 | pending |
| 1-02-02 | 02 | 1 | AUTH-01,04,05,06 | E2E | `npx playwright test auth` | MISSING W0 | pending |
| 1-03-01 | 03 | 2 | AUTHZ-01..06 | unit | `npx vitest run src/lib/permissions` | MISSING W0 | pending |
| 1-03-02 | 03 | 2 | AUTHZ-02..06 | integration | `npx vitest run tests/rbac` | MISSING W0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `tests/auth/email-password.test.ts` — stubs for AUTH-02, AUTH-04, AUTH-05, AUTH-06
- [ ] `tests/auth/magic-link.test.ts` — stubs for AUTH-03
- [ ] `tests/auth/invitation.test.ts` — stubs for AUTH-01
- [ ] `tests/rbac/permissions.test.ts` — stubs for AUTHZ-01..06
- [ ] `tests/rbac/scope-guards.test.ts` — stubs for AUTHZ-06
- [ ] `tests/e2e/auth.spec.ts` — Playwright stubs for full auth flows
- [ ] `vitest.config.ts` — Vitest configuration
- [ ] `playwright.config.ts` — Playwright configuration

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Invitation email received in inbox | AUTH-01 | Email delivery cannot be automated in CI | Use Resend test mode; check dashboard for delivery |
| Magic link email received | AUTH-03 | Email delivery | Use Resend test mode; verify single-use invalidation |
| Email scanner safety (two-step confirm) | AUTH-03 | Cannot simulate scanner bot in Playwright | Verify /magic-link/verify shows button before redeeming token |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s (unit), 120s (E2E)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
