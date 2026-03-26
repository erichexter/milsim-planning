# Airsoft Event Planning Application

## What This Is

A centralized web platform for faction commanders to organize and run large airsoft / milsim events. Commanders import player rosters, build faction hierarchy (platoons → squads), publish event information and maps, and notify players of updates. Players get a single place to find their assignment, access event documents, and request roster changes. Deployed to Azure (Container Apps + Static Web Apps) with Cloudflare R2 for file storage and Neon for the database.

## Core Value

Faction commanders can publish a complete event briefing — roster, assignments, information sections, and maps — and every player receives it without anything falling through the cracks.

## Requirements

### Validated

- ✓ Faction commander can create and manage events — v1.0
- ✓ Faction commander can import players via CSV (name, email, callsign, team affiliation) — v1.0
- ✓ Faction commander can build platoon/squad hierarchy and assign players — v1.0
- ✓ Faction commander can create custom information sections (markdown + attachments) — v1.0
- ✓ Faction commander can upload and link map resources (PDF, JPEG/PNG, KMZ, external links) — v1.0
- ✓ Faction commander can publish events and send email notifications — v1.0
- ✓ Players can log in, view their assignment, access event info, and download files — v1.0
- ✓ Players can submit roster change requests; commander can approve/deny — v1.0
- ✓ Email/password and magic link authentication — v1.0
- ✓ Role-based access: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player — v1.0
- ✓ Responsive UI supporting mobile and desktop — v1.0

### Active

- [ ] New user can create an account (displayName, email, password) without admin invite — v1.1
- [ ] Login page shows "Create an account" link to /auth/register — v1.1
- [ ] Self-registered users receive faction_commander role immediately — v1.1
- [ ] /auth/register page validates input and shows clear error messages — v1.1
- [ ] Authenticated users visiting /auth/register are redirected to /dashboard — v1.1

### Out of Scope

- Internal messaging / chat — high complexity, not core to planning value
- Direct API integration with registration platforms — manual CSV sufficient for v1
- Interactive mapping — external tools handle this
- Automatic squad assignment — manual control required by commanders
- Real-time game tracking — in-game tooling out of scope
- In-game command tools — out of scope
- Player attendance tracking — out of scope

## Context

- v1.0 shipped 2026-03-17 — deployed to Azure Container Apps + Static Web Apps
- ~20 phases worth of work, 20 plans, 108 backend tests, 60 frontend tests
- Tech stack: C# .NET 10 / ASP.NET Core API, React + Vite frontend, PostgreSQL (Neon), Cloudflare R2, Resend
- Events run ~8 times per year for 300–800 players
- Peak traffic: event publish + notification blast, then steady activity several days before event
- Players access from mobile phones during events (offline map downloads critical)
- External registration system is source of truth; app imports from it via CSV

## Constraints

- **Scale**: Must support 750–800 players per event
- **Device**: Mobile-first responsive UI (players in field on phones)
- **Offline**: Maps and documents must be downloadable for offline use
- **Auth**: Email/password + magic link (no OAuth required for v1)
- **Data**: Past events retained indefinitely
- **Integration**: External registration system — CSV import only (no API)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| CSV import for roster | Registration systems vary; manual upload keeps integration simple | ✓ Validated — works well for event scale |
| External map platforms | Interactive mapping is high complexity; linking is sufficient for v1 | ✓ Validated — CalTopo/Google MyMaps integration via links works |
| Email/password + magic link | Balances security and ease of access for non-technical players | ✓ Validated |
| Static frontend + API backend | Separation enables independent scaling; frontend CDN-hosted | ✓ Validated — Azure Static Web Apps + Container Apps |
| **API: C# .NET 10 (ASP.NET Core)** | User decision — locked | ✓ Shipped |
| **Frontend: React (Vite)** | User decision — locked | ✓ Shipped |
| MinimumRoleHandler sole role evaluator | No raw role string comparisons in business logic | ✓ Good — consistent authorization |
| ScopeGuard.AssertEventAccess first in services | IDOR prevention contract | ✓ Good — zero cross-event leaks in tests |
| Delta EF migrations | Keep InitialSchema intact; never drop-and-recreate | ✓ Good — safe replay in CI |
| Profile callsign overrides roster callsign | Players who set callsign in profile take precedence over CSV | ✓ Validated |
| Bulk assign destination encoding: squad/platoon GUID | Clean API contract for hierarchy builder | ✓ Good |
| Pre-signed URLs generated on demand | Never persist signed URLs; always fresh from R2Key | ✓ Good — security correct |
| Notification blast async via Channel + BackgroundService | Non-blocking; 202 after enqueue | ✓ Good — scales for 800 recipients |
| Azure Container Apps (scale-to-zero) + Neon free tier | Minimal cost for prototype | ✓ ~$1–3/mo operational cost |

## Current Milestone: v1.1 Registration

**Goal:** Add self-service user registration so new faction commanders and players can create accounts without waiting for an admin invite.

**Target features:**
- POST /api/auth/register backend endpoint with validation and tests
- /auth/register frontend page (Display Name, Email, Password, Confirm Password)
- Auth guard on /auth/register — redirect authenticated users to /dashboard
- "Create an account" link on LoginPage

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-03-25 — v1.1 milestone started*
