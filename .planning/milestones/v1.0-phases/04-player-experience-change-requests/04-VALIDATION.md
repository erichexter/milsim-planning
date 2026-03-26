---
phase: 4
slug: player-experience-change-requests
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-15
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework (API)** | xUnit + Testcontainers (PostgreSQL) — existing, no install needed |
| **Framework (web)** | Vitest + Testing Library — existing, no install needed |
| **API config file** | `milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` |
| **Web config file** | `web/vite.config.ts` |
| **Quick run (API)** | `dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj --filter "Category=RCHG OR Category=PLAY"` |
| **Quick run (web)** | `pnpm --prefix web test --run` |
| **Full suite command** | `dotnet test milsim-platform/milsim-platform.slnx && pnpm --prefix web test --run` |
| **Estimated runtime** | ~90 seconds (API ~60s, web ~10s) |

---

## Sampling Rate

- **After every task commit:** Run quick run commands for modified layer (API or web)
- **After every plan wave:** Run full suite command
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | RCHG-01,02,03,04,05 | integration | `dotnet test ... --filter "Category=RCHG"` | ❌ W0 | ⬜ pending |
| 04-01-02 | 01 | 1 | PLAY-01,06 | integration | `dotnet test ... --filter "Category=PLAY"` | ❌ W0 | ⬜ pending |
| 04-02-01 | 02 | 2 | PLAY-02,03,04,05 | component | `pnpm --prefix web test --run` | ❌ W0 | ⬜ pending |
| 04-02-02 | 02 | 2 | RCHG-01,02,03 | component | `pnpm --prefix web test --run` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/ChangeRequests/ChangeRequestTests.cs` — stubs for RCHG-01 through RCHG-05
- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/Player/PlayerTests.cs` — stubs for PLAY-01, PLAY-06
- [ ] `web/src/tests/PlayerEventView.test.tsx` — stubs for PLAY-02,03,04,05 component tests
- [ ] `web/src/tests/ChangeRequestForm.test.tsx` — stubs for RCHG-01,02,03 UI tests

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Bottom tab bar renders correctly on mobile viewport | PLAY-05 | Visual layout check | Open `http://localhost:5173/events/:id/player` on mobile or Chrome DevTools mobile emulator; verify tab bar is at bottom, tabs are ≥44px touch targets |
| Assignment card callsign is visually prominent | PLAY-06 | Visual check | Confirm orange monospace `[CALLSIGN]` renders above the fold on mobile viewport |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
