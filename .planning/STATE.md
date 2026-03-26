---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: Registration
status: executing
stopped_at: Completed 05-self-service-registration plan 01 (backend registration endpoint)
last_updated: "2026-03-26T17:44:38.500Z"
last_activity: 2026-03-26
progress:
  total_phases: 1
  completed_phases: 0
  total_plans: 2
  completed_plans: 1
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-25)

**Core value:** Faction commanders can publish a complete event briefing — roster, assignments, information sections, and maps — and every player receives it without anything falling through the cracks.
**Current focus:** Phase 05 — self-service-registration

## Current Position

Phase: 05 (self-service-registration) — EXECUTING
Plan: 2 of 2
Status: Ready to execute
Last activity: 2026-03-26

## Accumulated Context

### Decisions

Key decisions logged in PROJECT.md Key Decisions table.

- API stack: C# .NET 10 / ASP.NET Core — shipped
- Frontend: React (Vite) — shipped
- Database: PostgreSQL (Neon free tier) — deployed
- Auth: Email/password + magic link — shipped
- Files: Cloudflare R2 private storage with pre-signed URLs — deployed
- Email: Resend transactional provider — deployed
- Hosting: Azure Container Apps (scale-to-zero) + Static Web Apps — deployed ~$1-3/mo
- [Phase 05-self-service-registration]: Self-registered users get faction_commander role automatically; EmailConfirmed=true with no activation token; Callsign=empty string for NOT NULL column

### Pending Todos

None — milestone just started

### Blockers/Concerns

None open

## Session Continuity

Last session: 2026-03-26T17:44:38.494Z
Stopped at: Completed 05-self-service-registration plan 01 (backend registration endpoint)
Resume: `/gsd:plan-phase 5` after roadmap is created
