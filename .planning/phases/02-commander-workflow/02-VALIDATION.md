---
phase: 2
slug: commander-workflow
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-12
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9+ (API) + Vitest (React) |
| **Config file** | `src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` |
| **Quick run command** | `dotnet test milsim-platform.sln --filter "Category=Unit"` |
| **Full suite command** | `dotnet test milsim-platform.sln && pnpm --prefix web test --run` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test milsim-platform.sln --filter "Category=Unit"`
- **After every plan wave:** Run `dotnet test milsim-platform.sln && pnpm --prefix web test --run`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | EVNT-01..06 | Integration | `dotnet test milsim-platform.sln --filter "Category=EVNT_Create|Category=EVNT_List|Category=EVNT_Publish|Category=EVNT_Duplicate"` | ❌ Wave 0 | ⬜ pending |
| 02-01-02 | 01 | 1 | EVNT-01..06 | Integration | `dotnet test milsim-platform.sln --filter "Category=EVNT_Create|Category=EVNT_List|Category=EVNT_Publish|Category=EVNT_Duplicate"` | ❌ Wave 0 | ⬜ pending |
| 02-02-01 | 02 | 1 | ROST-01..06 | Integration | `dotnet test milsim-platform.sln --filter "Category=ROST_Validate|Category=ROST_Commit"` | ❌ Wave 0 | ⬜ pending |
| 02-02-02 | 02 | 1 | ROST-01..06 | Integration | `dotnet test milsim-platform.sln --filter "Category=ROST_Validate|Category=ROST_Commit"` | ❌ Wave 0 | ⬜ pending |
| 02-03-01 | 03 | 2 | HIER-01..06 | Integration | `dotnet test milsim-platform.sln --filter "Category=HIER_Platoon|Category=HIER_Squad|Category=HIER_Assign|Category=HIER_Roster"` | ❌ Wave 0 | ⬜ pending |
| 02-03-02 | 03 | 2 | HIER-01..06 | Integration | `dotnet test milsim-platform.sln --filter "Category=HIER_Platoon|Category=HIER_Squad|Category=HIER_Assign|Category=HIER_Roster"` | ❌ Wave 0 | ⬜ pending |
| 02-04-01 | 04 | 2 | EVNT-01..06 | Component | `pnpm --prefix web test --run -- --filter=EventList` | ❌ Wave 0 | ⬜ pending |
| 02-04-02 | 04 | 2 | ROST-01..06 | Component | `pnpm --prefix web test --run -- --filter=CsvImport` | ❌ Wave 0 | ⬜ pending |
| 02-05-01 | 05 | 3 | HIER-01..06 | Component | `pnpm --prefix web test --run -- --filter=HierarchyBuilder` | ❌ Wave 0 | ⬜ pending |
| 02-05-02 | 05 | 3 | HIER-06 | Component | `pnpm --prefix web test --run -- --filter=RosterView` | ❌ Wave 0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All test files must be created before their corresponding implementation tasks run. The test project from Phase 1 provides the infrastructure (PostgreSqlFixture, TestAuthHandler, WebApplicationFactory).

- [ ] `src/MilsimPlanning.Api.Tests/Events/EventTests.cs` — stubs for EVNT-01..06 (EVNT_Create, EVNT_List, EVNT_Publish, EVNT_Duplicate)
- [ ] `src/MilsimPlanning.Api.Tests/Roster/RosterImportTests.cs` — stubs for ROST-01..06 (ROST_Validate, ROST_Commit)
- [ ] `src/MilsimPlanning.Api.Tests/Hierarchy/HierarchyTests.cs` — stubs for HIER-01..06 (HIER_Platoon, HIER_Squad, HIER_Assign, HIER_Roster)
- [ ] `web/src/tests/EventList.test.tsx` — component stubs for event list + create
- [ ] `web/src/tests/CsvImportPage.test.tsx` — component stubs for CSV upload + error preview
- [ ] `web/src/tests/HierarchyBuilder.test.tsx` — component stubs for inline squad assignment
- [ ] `web/src/tests/RosterView.test.tsx` — component stubs for accordion + callsign search

*Wave 0 is the first task in Plan 02-01.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| CSV drag-and-drop file upload feel | ROST-01 | Visual/interaction UX | Open import page, drag a CSV file onto the upload zone, confirm visual feedback |
| Combobox keyboard navigation in squad cell | HIER-04 | Accessibility interaction | Tab to squad cell, press Enter to open combobox, use arrow keys to select squad, press Enter to confirm |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
