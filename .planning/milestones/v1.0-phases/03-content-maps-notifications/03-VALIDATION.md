---
phase: 3
slug: content-maps-notifications
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-13
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9+ (API) + Vitest (React) |
| **Config file** | `milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` |
| **Quick run command** | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=CONT|Category=MAPS|Category=NOTF"` |
| **Full suite command** | `dotnet test milsim-platform/milsim-platform.sln && pnpm --prefix web test --run` |
| **Estimated runtime** | ~35 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test milsim-platform/milsim-platform.sln --filter "Category=CONT|Category=MAPS|Category=NOTF"`
- **After every plan wave:** Run `dotnet test milsim-platform/milsim-platform.sln && pnpm --prefix web test --run`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 35 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | CONT-01..05 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=CONT_Sections"` | ❌ Wave 0 | ⬜ pending |
| 03-01-02 | 01 | 1 | CONT-03 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=CONT_Attachments"` | ❌ Wave 0 | ⬜ pending |
| 03-01-03 | 01 | 1 | CONT-04 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=CONT_Reorder"` | ❌ Wave 0 | ⬜ pending |
| 03-02-01 | 02 | 1 | MAPS-01..05 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=MAPS_Resources"` | ❌ Wave 0 | ⬜ pending |
| 03-02-02 | 02 | 1 | MAPS-03..05 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=MAPS_Files"` | ❌ Wave 0 | ⬜ pending |
| 03-03-01 | 03 | 1 | NOTF-01..05 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=NOTF_Blast|Category=NOTF_Squad"` | ❌ Wave 0 | ⬜ pending |
| 03-03-02 | 03 | 1 | NOTF-02 | Integration | `dotnet test milsim-platform/milsim-platform.sln --filter "Category=NOTF_Squad"` | ❌ Wave 0 | ⬜ pending |
| 03-04-01 | 04 | 2 | CONT-01..05 | Component | `pnpm --prefix web test --run -- --filter=BriefingPage` | ❌ Wave 0 | ⬜ pending |
| 03-04-02 | 04 | 2 | CONT-03..04 | Component | `pnpm --prefix web test --run -- --filter=SectionEditor` | ❌ Wave 0 | ⬜ pending |
| 03-05-01 | 05 | 2 | MAPS-01..05 | Component | `pnpm --prefix web test --run -- --filter=MapResourcesPage` | ❌ Wave 0 | ⬜ pending |
| 03-05-02 | 05 | 2 | NOTF-01 | Component | `pnpm --prefix web test --run -- --filter=NotificationBlastPage` | ❌ Wave 0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All test files must be created before their corresponding implementation tasks run. The test project from Phase 1/2 provides the infrastructure (PostgreSqlFixture, TestAuthHandler, WebApplicationFactory, Moq, FluentAssertions).

**API test stubs (xUnit + Testcontainers):**
- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs` — stubs for CONT-01..05 (CONT_Sections, CONT_Attachments, CONT_Reorder)
- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs` — stubs for MAPS-01..05 (MAPS_Resources, MAPS_Files)
- [ ] `milsim-platform/src/MilsimPlanning.Api.Tests/Notifications/NotificationTests.cs` — stubs for NOTF-01..05 (NOTF_Blast, NOTF_Squad)

**React component test stubs (Vitest + MSW):**
- [ ] `web/src/tests/BriefingPage.test.tsx` — stubs for info section CRUD + DnD reorder (CONT-01..05)
- [ ] `web/src/tests/SectionEditor.test.tsx` — stubs for markdown edit/preview toggle + title validation (CONT-02)
- [ ] `web/src/tests/MapResourcesPage.test.tsx` — stubs for external links + file upload zone (MAPS-01..05)
- [ ] `web/src/tests/NotificationBlastPage.test.tsx` — stubs for blast form + send log table (NOTF-01, NOTF-05)

*Wave 0 is the first task in Plan 03-01.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Drag-and-drop section reorder feel | CONT-04 | Visual/interaction UX — drag animation, ghost card, drop placement | Open an event briefing page, drag a section by its grip handle, confirm smooth animation and correct final order |
| File download triggers browser save dialog | CONT-03, MAPS-04 | Browser download UX — requires real pre-signed URL + real R2 bucket | Click a file attachment name, confirm browser opens a download save dialog (not a blank tab) |
| Markdown preview renders GFM correctly | CONT-02, MAPS-02 | Visual rendering — tables, strikethrough, task lists | Edit a section, enter a GFM table, switch to Preview tab, confirm table renders with borders |
| Notification blast toast appears + UI unblocked | NOTF-01 | UI responsiveness — toast + immediate return without progress spinner | Send a blast, confirm toast appears instantly and form remains usable before emails complete |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 35s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
