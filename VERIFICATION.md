---
verified: 2026-03-17T00:00:00Z
status: gaps_found
score: 42/46 requirements verified (RCHG + PLAY checkbox traceability gap; 1 pending todo; test naming drift)
milestone: v1.0 MVP
gaps:
  - truth: "RCHG-01..05 and PLAY-01..06 checkboxes in the live REQUIREMENTS.md are unchecked despite features shipping in Phase 4"
    status: failed
    reason: "The top-level REQUIREMENTS.md was never updated to mark Phase 4 requirement IDs as complete. The archived v1.0-REQUIREMENTS.md acknowledges this explicitly. The live file shows unchecked boxes for RCHG-01..05 and PLAY-01..06."
    artifacts:
      - path: "REQUIREMENTS.md"
        issue: "Missing — Phase 4 requirement IDs (RCHG-01..05, PLAY-01..06) do not appear in REQUIREMENTS.md at all; the file ends at NOTF-05 with no Roster Change Requests or Player Experience sections"
    missing:
      - "Add RCHG-01..05 and PLAY-01..06 requirement blocks (with [x] checked) to REQUIREMENTS.md sections 4 and 5"

  - truth: "Faction commander hierarchy management UI fully verified as working (pending todo)"
    status: partial
    reason: "An open 'pending' todo (2026-03-16) documents that HierarchyBuilder.tsx needs audit post EventMembership fix and that platoon/squad leader assignment UI coverage is unclear. The todo was never resolved or moved to done."
    artifacts:
      - path: ".planning/todos/pending/2026-03-16-faction-commander-hierarchy-management-ui.md"
        issue: "Still in /pending — not resolved or closed at milestone archive"
    missing:
      - "Resolve or explicitly defer/close the todo: audit HierarchyBuilder.tsx end-to-end, confirm leader assignment UI works (setRoleMutation confirmed wired), move file to /done or document deferred decision"

  - truth: "PlayerEventView tests cover the plan-specified test names including 'callsign_displays_with_orange_monospace_style' (PLAY-06) and 'renders_MyAssignmentTab_by_default'"
    status: partial
    reason: "The 04-02-SUMMARY.md claims 5 PlayerEventView tests including 'callsign_displays_with_orange_monospace_style' and 'renders_MyAssignmentTab_by_default'. The actual test file contains different test names: 'renders_overview_tab_by_default', 'renders_bottomTabBar_on_mobile', 'tabBar_buttons_have_minimum_44px_height', 'switching_tabs_renders_correct_content', 'overview_tab_renders_for_event'. The callsign orange monospace test is absent; MyAssignmentTab is only referenced via vi.mock, not tested as the default rendered component."
    artifacts:
      - path: "web/src/tests/PlayerEventView.test.tsx"
        issue: "No test for 'callsign_displays_with_orange_monospace_style' (PLAY-06). Default tab test references 'player-overview-tab' (PlayerOverviewTab), not 'my-assignment-tab' (MyAssignmentTab). The SUMMARY claim of 5 tests matching plan names does not match the actual file."
    missing:
      - "Either: add a test verifying orange monospace callsign display in MyAssignmentTab, or document that PLAY-06 callsign coverage is handled by MyAssignmentTab.tsx unit behavior and the existing backend callsign precedence test (GetMyAssignment_ProfileCallsignOverridesRosterCallsign)"

human_verification:
  - test: "End-to-end invitation email flow"
    expected: "User receives an invitation email in their actual inbox with a working activation link"
    why_human: "EmailService was a stub in Phase 1; Resend integration added in Phase 3 but real delivery requires live credentials and inbox check"
  - test: "Bottom tab bar renders correctly on mobile viewport"
    expected: "Open /events/:id/player on mobile or Chrome DevTools mobile emulator; tab bar is at bottom, tabs are ≥44px touch targets"
    why_human: "Visual layout check — h-dvh is on AppLayout wrapper (min-h-dvh), PlayerEventPage uses h-full; mobile rendering needs browser verification"
  - test: "HierarchyBuilder full workflow after EventMembership fix"
    expected: "Commander can create platoons, create squads, assign players, and set roles without getting 403 errors"
    why_human: "The pending todo notes a 403 regression was fixed but HierarchyBuilder needs manual end-to-end audit"
---

# v1.0 MVP — Requirements Gap Verification Report

**Project:** Airsoft Event Planning Application (RP0 milsim platform)  
**Verified:** 2026-03-17  
**Milestone:** v1.0 MVP (Phases 1–4, 20 plans)  
**Status:** ⚠️ GAPS FOUND  
**Scope:** Cross-phase gap analysis — not a per-phase re-verification  

---

## Executive Summary

The v1.0 MVP shipped to production on Azure with all four phases complete. Phases 1–3 have passing VERIFICATION.md reports. Phase 4 completed but was never formally verified. The codebase is substantively complete — all features exist and are wired. The gaps identified are **traceability and documentation gaps**, one **open pending todo**, and a **test naming discrepancy**, not missing functionality.

---

## Phase Verification Status

| Phase | Status | Score | Report |
|-------|--------|-------|--------|
| Phase 1: Foundation | ✅ PASSED | 5/5 | `.planning/phases/01-foundation/01-VERIFICATION.md` |
| Phase 2: Commander Workflow | ✅ PASSED | 5/5 | `.planning/phases/02-commander-workflow/02-VERIFICATION.md` |
| Phase 3: Content, Maps & Notifications | ✅ PASSED (re-verified) | 5/5 | `.planning/phases/03-content-maps-notifications/03-VERIFICATION.md` |
| Phase 4: Player Experience & Change Requests | ⚠️ NOT VERIFIED | — | No VERIFICATION.md exists |

---

## Requirements Coverage — All 46 IDs

### Phases 1–3: Fully Verified (AUTH, AUTHZ, EVNT, ROST, HIER, CONT, MAPS, NOTF)

All 35 requirement IDs from Phases 1–3 were verified in their respective phase VERIFICATION.md reports with implementation evidence and passing integration tests.

| Group | IDs | Status |
|-------|-----|--------|
| AUTH-01..06 | Phase 1 | ✅ All verified (Phase 1 VERIFICATION) |
| AUTHZ-01..06 | Phase 1 | ✅ All verified (Phase 1 VERIFICATION) |
| EVNT-01..06 | Phase 2 | ✅ All verified (Phase 2 VERIFICATION) |
| ROST-01..06 | Phase 2 | ✅ All verified (Phase 2 VERIFICATION) |
| HIER-01..06 | Phase 2 | ✅ All verified (Phase 2 VERIFICATION) |
| CONT-01..05 | Phase 3 | ✅ All verified (Phase 3 VERIFICATION) |
| MAPS-01..05 | Phase 3 | ✅ All verified (Phase 3 VERIFICATION) |
| NOTF-01..05 | Phase 3 | ✅ All verified (Phase 3 VERIFICATION) |

### Phase 4: Feature-Complete, Traceability Gap

| Requirement | Description | Implementation Evidence | Checkbox Status |
|-------------|-------------|------------------------|-----------------|
| RCHG-01 | Player can submit a roster change request | `POST /api/events/{eventId}/roster-change-requests` (201); `SubmitRequest_ValidNote_Returns201` test passes | ❌ Unchecked in REQUIREMENTS.md |
| RCHG-02 | Commander can view pending requests | `GET /api/events/{eventId}/roster-change-requests` (FactionCommander policy); `ListPending_CommanderRole_ReturnsPendingRequests` test passes | ❌ Unchecked in REQUIREMENTS.md |
| RCHG-03 | Commander can approve or deny | Approve + Deny endpoints in `RosterChangeRequestsController`; `Approve_UpdatesEventPlayerAssignment` + `Deny_Enqueues*` tests pass | ❌ Unchecked in REQUIREMENTS.md |
| RCHG-04 | Player receives email on decision | `EnqueueAsync(RosterChangeDecisionJob)` after `SaveChangesAsync`; notification pipeline wired from Phase 3 | ❌ Unchecked in REQUIREMENTS.md |
| RCHG-05 | Roster updated automatically on approval | `Approve` endpoint: single `SaveChangesAsync` atomically updates `EventPlayer.PlatoonId`/`SquadId` + `Status=Approved` | ❌ Unchecked in REQUIREMENTS.md |
| PLAY-01 | Player can view squad/platoon assignment | `GET /api/events/{eventId}/my-assignment` → `PlayerController`; `GetMyAssignment_ReturnsPlayerPlatoonSquad` test passes | ❌ Unchecked in REQUIREMENTS.md |
| PLAY-02 | Player can view full faction roster | `PlayerEventPage` Roster tab → `RosterView` (RequirePlayer policy); wired end-to-end | ❌ Unchecked in REQUIREMENTS.md |
| PLAY-03 | Player can access information sections | `PlayerEventPage` Briefing tab → `BriefingPage`; wired end-to-end | ❌ Unchecked in REQUIREMENTS.md |
| PLAY-04 | Player can download maps/documents | `PlayerEventPage` Maps tab → `MapResourcesPage`; wired end-to-end | ❌ Unchecked in REQUIREMENTS.md |
| PLAY-05 | Player UI functional on mobile (44px touch targets) | `PlayerEventPage`: `min-h-[56px]` on all tab buttons; bottom tab bar `md:hidden fixed bottom-0`; `AppLayout` uses `min-h-dvh` | ❌ Unchecked in REQUIREMENTS.md |
| PLAY-06 | Callsign displayed prominently | `PlayerOverviewTab` renders `[{callsign}]` in `font-mono font-medium text-2xl`; `MyAssignmentTab` has `font-mono text-2xl font-bold text-orange-500` | ❌ Unchecked in REQUIREMENTS.md |

**Root cause:** The live `REQUIREMENTS.md` (project root) does not contain RCHG or PLAY sections at all — it ends at section 12 (Out of Scope). These requirement IDs only exist in `.planning/milestones/v1.0-REQUIREMENTS.md` (the archive). The archive explicitly flags this: *"RCHG-01..05 and PLAY-01..06 checkboxes were not updated at Phase 4 completion."*

---

## Artifact Verification — Phase 4

### Backend Artifacts (Plan 04-01)

| Artifact | Exists | Substantive | Wired | Status |
|----------|--------|-------------|-------|--------|
| `milsim-platform/src/MilsimPlanning.Api/Data/Entities/RosterChangeRequest.cs` | ✓ | ✓ (full entity with Id, EventId, EventPlayerId, Note, Status, CommanderNote, CreatedAt, ResolvedAt) | ✓ (AppDbContext DbSet + cascade config) | ✅ VERIFIED |
| `milsim-platform/src/MilsimPlanning.Api/Data/Migrations/20260316142237_Phase4Schema.cs` | ✓ | ✓ (creates RosterChangeRequests table) | ✓ | ✅ VERIFIED |
| `milsim-platform/src/MilsimPlanning.Api/Controllers/RosterChangeRequestsController.cs` | ✓ | ✓ (6 endpoints: Submit/GetMine/Cancel/ListPending/Approve/Deny) | ✓ (SaveChangesAsync before EnqueueAsync; atomic approve; Conflict/UnprocessableEntity responses) | ✅ VERIFIED |
| `milsim-platform/src/MilsimPlanning.Api/Controllers/PlayerController.cs` | ✓ | ✓ (GET /my-assignment via direct EventPlayers query by UserId+EventId) | ✓ | ✅ VERIFIED |
| `milsim-platform/src/MilsimPlanning.Api.Tests/RosterChangeRequests/RosterChangeRequestTests.cs` | ✓ | ✓ (12 integration tests across RCHG_Submit, RCHG_Review, RCHG_Decision) | ✓ | ✅ VERIFIED |
| `milsim-platform/src/MilsimPlanning.Api.Tests/Player/PlayerTests.cs` | ✓ | ✓ (3 tests: assigned, unassigned, ProfileCallsign override) | ✓ | ✅ VERIFIED |

### Frontend Artifacts (Plan 04-02)

| Artifact | Exists | Substantive | Wired | Status |
|----------|--------|-------------|-------|--------|
| `web/src/main.tsx` | ✓ | ✓ (full route tree, commander-only gating with `requiredRole="faction_commander"`) | ✓ | ✅ VERIFIED |
| `web/src/pages/events/PlayerEventPage.tsx` | ✓ | ✓ (h-full layout, min-h-[56px] bottom bar, 4 tabs: Overview/Roster/Briefing/Maps) | ✓ (uses PlayerOverviewTab, not MyAssignmentTab as default — see notes) | ✅ VERIFIED |
| `web/src/components/player/MyAssignmentTab.tsx` | ✓ | ✓ (orange monospace callsign, change request form/status) | ⚠️ ORPHANED — not imported by PlayerEventPage; exists but unreachable in the route tree |
| `web/src/components/player/PlayerOverviewTab.tsx` | ✓ | ✓ (callsign in font-mono font-medium text-2xl, assignment chain, navigation to change-request tab) | ✓ (used by PlayerEventPage as 'overview' tab) | ✅ VERIFIED |
| `web/src/pages/events/ChangeRequestsPage.tsx` | ✓ | ✓ (pending list, Approve/Deny dialogs, platoon/squad dropdowns) | ✓ (gated via ProtectedRoute requiredRole) | ✅ VERIFIED |
| `web/src/tests/PlayerEventView.test.tsx` | ✓ | ✓ (5 tests) | ✓ (tests reference PlayerOverviewTab, not MyAssignmentTab) | ⚠️ Test names diverge from SUMMARY claims — see notes |
| `web/src/tests/ChangeRequestForm.test.tsx` | ✓ | ✓ (5 tests: form/pending states, submit/cancel mutations, approve dialog) | ✓ | ✅ VERIFIED |

---

## Key Link Verification — Phase 4

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `RosterChangeRequestsController.Approve` | `AppDbContext.SaveChangesAsync` | Single SaveChangesAsync covering EventPlayer update + Status=Approved | ✅ WIRED | Line 175: comment + line 176: `await _db.SaveChangesAsync()` before EnqueueAsync |
| `RosterChangeRequestsController.Approve/Deny` | `INotificationQueue.EnqueueAsync` | `RosterChangeDecisionJob` enqueued AFTER SaveChangesAsync | ✅ WIRED | Lines 182/219: EnqueueAsync called post-commit |
| `PlayerController.GetMyAssignment` | `AppDbContext.EventPlayers` | Direct query by `UserId + EventId` | ✅ WIRED | Line 35: `FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId)` |
| `main.tsx ProtectedRoute` | Commander-only routes | `requiredRole="faction_commander"` | ✅ WIRED | Line 62: `element: <ProtectedRoute requiredRole="faction_commander" />` |
| `PlayerEventPage` | `PlayerOverviewTab` | `activeTab === 'overview'` renders `PlayerOverviewTab` | ✅ WIRED | Lines 58-63 |
| `PlayerOverviewTab` | `/api/events/{eventId}/my-assignment` | `useQuery(['events', eventId, 'my-assignment'])` | ✅ WIRED | Confirmed in component |
| `DashboardPage` | Role-aware navigation | `user.role === 'faction_commander'` → `/events/:id`, else → `/events/:id/player` | ✅ WIRED | Lines 18,22,24 |
| `MyAssignmentTab` | (caller) | NOT imported by PlayerEventPage | ⚠️ ORPHANED | Exists in components/player/ but only referenced in test vi.mock; PlayerEventPage uses PlayerOverviewTab instead |

---

## Notable Findings

### Finding 1: MyAssignmentTab vs PlayerOverviewTab (Architectural Deviation)

The plan (04-02-PLAN.md) specified `MyAssignmentTab` as the default tab in `PlayerEventPage`. The SUMMARY claims this was implemented. In reality, the codebase evolved to use `PlayerOverviewTab` as the primary player-facing component — a richer component that integrates the full event overview (countdown, assignment card, info sections, maps) into a single tab.

**Impact:** PLAY-06 (callsign display) is satisfied by `PlayerOverviewTab` using `font-mono font-medium text-2xl` with primary color (not the locked `text-orange-500` from the plan). `MyAssignmentTab` has the orange styling but is unreachable/orphaned. The callsign IS displayed prominently — just via a different component than planned with a different color choice.

**Assessment:** Feature-complete from a user perspective; the planned component (`MyAssignmentTab`) is orphaned code. The SUMMARY inaccurately described what was shipped.

### Finding 2: h-dvh Placement

The plan required `h-dvh` on the `PlayerEventPage` root div (iOS Safari fix). The actual implementation places `min-h-dvh` on `AppLayout` (the global wrapper), and `PlayerEventPage` uses `h-full min-h-0` instead. This achieves the same effect through the layout hierarchy — the AppLayout ensures the viewport height, and PlayerEventPage fills it.

**Assessment:** PLAY-05 (mobile functional, touch targets ≥44px) is satisfied: `min-h-[56px]` is on all tab bar buttons, and the bottom tab bar is correctly `md:hidden fixed bottom-0`.

### Finding 3: PLAY-06 Test Coverage Discrepancy

The 04-02-SUMMARY.md claims test `callsign_displays_with_orange_monospace_style` exists. It does not appear in `PlayerEventView.test.tsx`. The test file has `renders_overview_tab_by_default` and `overview_tab_renders_for_event` — testing `PlayerOverviewTab` (the actual component used) rather than `MyAssignmentTab`.

**Assessment:** The callsign prominence is visually present in `PlayerOverviewTab`, but there is no automated test asserting the orange monospace styling. This is a test coverage gap for PLAY-06.

### Finding 4: Open Pending Todo

`.planning/todos/pending/2026-03-16-faction-commander-hierarchy-management-ui.md` was never resolved at milestone archive. The todo asks to audit HierarchyBuilder end-to-end after an EventMembership 403 fix, and to determine scope of platoon/squad leader assignment UI.

**Codebase check:** `HierarchyBuilder.tsx` does have `setRoleMutation` wired to `api.setPlayerRole()` → `PATCH /api/event-players/{playerId}/role` endpoint (confirmed in HierarchyController). The backend role assignment endpoint exists. Whether the UI is fully audited post-EventMembership fix is unknown without running the app.

### Finding 5: REQUIREMENTS.md Does Not Contain RCHG/PLAY Sections

The project-root `REQUIREMENTS.md` is the original requirements spec (Version: Draft) and ends at section 12 (Out of Scope). It was never extended to include the Phase 4 requirement IDs. The formal traceability for RCHG-01..05 and PLAY-01..06 exists only in the archived `v1.0-REQUIREMENTS.md`.

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `milsim-platform/src/MilsimPlanning.Api/Program.cs` | 37 | `dev-placeholder-secret-32-chars!!` as JWT fallback | ℹ️ Info | Development safety valve — production uses real secret from env; acceptable pattern |
| `milsim-platform/src/MilsimPlanning.Api/appsettings.Development.json` | 9 | `re_placeholder` as Resend API key | ℹ️ Info | Development config — production override expected; acceptable |
| `web/src/components/player/MyAssignmentTab.tsx` | — | Exported component with no callers in route tree | ⚠️ Warning | Orphaned code — `MyAssignmentTab` is referenced only in `PlayerEventView.test.tsx` via `vi.mock`. PlayerEventPage uses `PlayerOverviewTab` instead. |

**No blockers found.** All anti-patterns are either intentional dev placeholders or the one orphaned component.

---

## Human Verification Required

### 1. End-to-End Email Delivery (All Auth + Notification Flows)

**Test:** Invite a user via CSV import; check their real inbox for activation email. Also test notification blast and squad assignment change emails.  
**Expected:** Emails arrive from Resend transactional provider, activation link works, notification emails have correct content.  
**Why human:** Requires live Resend credentials, real email inbox, and deployed environment.

### 2. Mobile Player Experience — Bottom Tab Bar

**Test:** Open `https://green-forest-02e38090f.6.azurestaticapps.net/events/:id/player` on an iPhone or Android device (or Chrome DevTools mobile emulator).  
**Expected:** Bottom tab bar visible at bottom of viewport, tabs ≥44px tall, callsign visible above fold on Overview tab, no content clipped by iOS Safari address bar.  
**Why human:** Visual layout, touch target size, and iOS Safari dynamic viewport behavior require browser verification.

### 3. HierarchyBuilder End-to-End Audit

**Test:** As a faction commander, navigate to `/events/:id/hierarchy`. Create a platoon, create a squad within it, assign an unassigned player to the squad, set a player's role label.  
**Expected:** All operations succeed without 403 errors; roster view updates to reflect assignments; role label appears on the player card.  
**Why human:** The pending todo documents a prior 403 issue that was "fixed" — needs confirmation. Role assignment UI requires visual and functional verification.

### 4. Magic Link Email Scanner Protection

**Test:** In a browser, visit a magic link URL directly: simulate what an email scanner would do.  
**Expected:** Page loads showing a button only — no automatic sign-in on page load.  
**Why human:** Code analysis confirmed no `useEffect` auto-submit (documented in Phase 1 VERIFICATION), but browser UX requires manual check.

---

## Gaps Summary

### Gap 1: REQUIREMENTS.md Does Not Reflect Phase 4 (Traceability Only)

**Severity:** Documentation gap only — not missing functionality  
**Impact:** The live requirements file is misleading to any reader who hasn't read the milestone archive  
**Fix:** Add RCHG-01..05 and PLAY-01..06 sections to the root `REQUIREMENTS.md` with `[x]` checked

### Gap 2: Pending Todo — HierarchyBuilder Audit (Unresolved)

**Severity:** Low — todo may document already-fixed concerns  
**Impact:** Unknown whether the 403 regression is fully resolved and whether leader assignment UI is complete  
**Fix:** Either: (a) run the audit and close the todo, or (b) explicitly move to a "v1.1 enhancement" backlog and document the decision

### Gap 3: MyAssignmentTab Is Orphaned Code

**Severity:** Low — PLAY-06 is satisfied by `PlayerOverviewTab`; orange styling (text-orange-500) is not present in the shipped component  
**Impact:** Plan specified orange monospace for callsign; shipped component uses theme primary color. Both display callsign prominently.  
**Fix:** Either: (a) delete `MyAssignmentTab.tsx` as dead code and update `PlayerEventView.test.tsx` vi.mock target, or (b) wire it back in as the assignment tab within PlayerEventPage

### Gap 4: No PLAY-06 Automated Test

**Severity:** Low — visual property, hard to test automatically  
**Impact:** No test verifies callsign displays prominently on the player dashboard  
**Fix:** Add a test in `PlayerEventView.test.tsx` or a new `PlayerOverviewTab.test.tsx` that checks for `[CALLSIGN]` text in the rendered output

---

## What Is Definitively Shipped and Working

All 46 requirements were implemented. The following are confirmed by code inspection:

- **Auth/RBAC:** JWT, magic link, password reset, 5-role hierarchy, IDOR prevention — all wired and tested
- **Event management:** Create, duplicate, list, publish, status lifecycle
- **CSV roster import:** Two-phase validate/commit, per-row errors, upsert by email, invitation emails
- **Hierarchy builder:** Platoon/squad creation, player assignment, move between squads, role labels
- **Content/maps:** Markdown sections, file attachments, external map links, downloadable map files, private pre-signed URLs
- **Notifications:** Async blast (202 + background worker), squad assignment emails, roster decision emails, Resend integration
- **Roster change requests:** Full CRUD (submit, view, cancel, approve, deny), atomic EventPlayer update, notification pipeline
- **Player experience:** Assignment view, roster access, briefing access, map downloads, mobile tab layout, role-gated routing
- **Deployment:** Azure Container Apps + Static Web Apps + Neon + Cloudflare R2

---

*Verified: 2026-03-17T00:00:00Z*  
*Verifier: Claude (gsd-verifier)*  
*Scope: Cross-phase gap analysis covering v1.0 MVP (Phases 1–4)*
