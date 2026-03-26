---
phase: 5
slug: self-service-registration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-26
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing — MilsimPlanning.Api.Tests project) |
| **Config file** | none — uses WebApplicationFactory + Testcontainers |
| **Quick run command** | `dotnet test --filter "Category=Auth_Register" milsim-platform/src/MilsimPlanning.Api.Tests/` |
| **Full suite command** | `dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/` |
| **Estimated runtime** | ~30 seconds (Docker required for Testcontainers) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Auth_Register" milsim-platform/src/MilsimPlanning.Api.Tests/`
- **After every plan wave:** Run `dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/`
- **Before `/gsd:verify-work`:** Full suite must be green + manual smoke of AC-6 through AC-10
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 5-01-01 | 01 | 1 | AC-1..AC-5 | integration | `dotnet test --filter "Category=Auth_Register"` | ❌ W0 | ⬜ pending |
| 5-02-01 | 02 | 2 | AC-6,AC-7,AC-9,AC-10 | manual | n/a | n/a | ⬜ pending |
| 5-02-02 | 02 | 2 | AC-8 | manual | n/a | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements.* `AuthTests.cs` already uses the shared `PostgreSqlFixture`, xUnit `IClassFixture`, and `WebApplicationFactory` pattern. New tests for AC-1 through AC-5 are additions to this existing file — no stubs or new test files needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| RegisterPage renders with 4 fields | AC-6 | No React test framework in project | Load /auth/register, verify Display Name, Email, Password, Confirm Password fields visible |
| Successful registration redirects to /dashboard | AC-7 | No React test framework in project | Register with valid data, confirm redirect to /dashboard |
| "Create an account" link visible on login page | AC-8 | No React test framework in project | Load /auth/login, verify "Create an account" or "Don't have an account" link visible |
| Authenticated users redirected from /auth/register | AC-9 | No React test framework in project | Log in, navigate to /auth/register, confirm redirect to /dashboard |
| Password mismatch shows client-side error | AC-10 | No React test framework in project | Enter mismatched passwords, confirm error shown without network request |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
