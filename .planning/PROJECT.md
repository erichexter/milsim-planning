# Airsoft Event Planning Application

## What This Is

A centralized web platform for faction commanders to organize and run large airsoft / milsim events. Commanders import player rosters, build faction hierarchy (platoons → squads), publish event information and maps, and notify players of updates. Players get a single place to find their assignment, access event documents, and request roster changes.

## Core Value

Faction commanders can publish a complete event briefing — roster, assignments, information sections, and maps — and every player receives it without anything falling through the cracks.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Faction commander can create and manage events
- [ ] Faction commander can import players via CSV (name, email, callsign, team affiliation)
- [ ] Faction commander can build platoon/squad hierarchy and assign players
- [ ] Faction commander can create custom information sections (markdown + attachments)
- [ ] Faction commander can upload and link map resources (PDF, JPEG/PNG, KMZ, external links)
- [ ] Faction commander can publish events and send email notifications
- [ ] Players can log in, view their assignment, access event info, and download files
- [ ] Players can submit roster change requests; commander can approve/deny
- [ ] Email/password and magic link authentication
- [ ] Role-based access: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player
- [ ] Responsive UI supporting mobile and desktop

### Out of Scope

- Internal messaging / chat — high complexity, not core to planning value
- Direct API integration with registration platforms — manual CSV sufficient for v1
- Interactive mapping — external tools handle this
- Automatic squad assignment — manual control required by commanders
- Real-time game tracking — in-game tooling out of scope
- In-game command tools — out of scope
- Player attendance tracking — out of scope

## Context

- Events run ~8 times per year for 300–800 players
- Peak traffic: event publish + notification blast, then steady activity several days before event
- Players access from mobile phones during events (offline map downloads critical)
- External registration system is source of truth; app imports from it via CSV
- External map platforms (e.g. CalTopo, Google MyMaps) remain separate; app links to them and hosts downloadable files
- Email via transactional provider (SendGrid or equivalent)
- Hosting: static frontend + API backend on cloud platform

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
| CSV import for roster | Registration systems vary; manual upload keeps integration simple | — Pending |
| External map platforms | Interactive mapping is high complexity; linking is sufficient for v1 | — Pending |
| Email/password + magic link | Balances security and ease of access for non-technical players | — Pending |
| Static frontend + API backend | Separation enables independent scaling; frontend CDN-hosted | — Pending |

---
*Last updated: 2026-03-12 after initialization*
