# Feature Research

**Domain:** Airsoft / MilSim Event Coordination & Roster Management Platform
**Researched:** 2026-03-12
**Confidence:** HIGH (features derived directly from PROJECT.md requirements + comparative analysis of TeamSnap, MilSim West, RunSignup, and direct milsim community patterns)

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features commanders and players assume exist. Missing these = product feels broken.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Event creation & management** | Commanders need a container to organize everything: dates, location, faction details | LOW | Title, dates, location, status (draft/published/archived). Events are the root object everything else hangs from. |
| **CSV roster import** | External registration systems (Eventbrite, custom forms) are the source of truth. Manual re-entry of 600+ players is a non-starter. | MEDIUM | Parse: name, email, callsign, team affiliation. Handle encoding issues, duplicate detection, error reporting per-row. |
| **Platoon/squad hierarchy builder** | Commanders MUST organize 300–800 players into structured units. Flat lists don't scale. | MEDIUM | Multi-level tree: Faction → Platoon → Squad → Player. Named units with leaders. Drag-assign or bulk-assign players. |
| **Player assignment to unit** | Players need to know exactly which unit they're in. Commanders need to fill positions. | LOW | Many-to-one: player assigned to exactly one squad per event. Display assignment on player's dashboard. |
| **Player-facing event dashboard** | Players log in and immediately see: their unit, callsign, event details, and where to find everything else | LOW | Single-screen summary: assignment, links to docs/maps, event status. Mobile-first layout. |
| **Role-based access control (RBAC)** | Hierarchical org = hierarchical permissions. Platoon leaders should not edit squads they don't own. | MEDIUM | Roles: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player. Scoped to event. |
| **Email/password + magic link auth** | Players range from tech-savvy to "I barely use email." Magic link removes password friction for one-time logins. | MEDIUM | Standard auth stack + passwordless magic link flow. Email delivery critical path. |
| **Custom information sections** | Commanders need to publish free-form briefing content: mission overview, rules of engagement, parking instructions, etc. | MEDIUM | Ordered sections per event. Markdown rendering + file attachments. Visible to appropriate roles. |
| **Document/file hosting and download** | PDFs, waivers, TACSOPs, and reference docs must be accessible from mobile. | MEDIUM | Upload: PDF, JPEG, PNG, KMZ. CDN-served. Direct download links. Files tied to an event. |
| **Map resource management** | Maps are the most critical pre-event resource. Players MUST be able to access offline. | MEDIUM | Upload raster maps (JPEG/PNG) and PDF maps for offline use. Link to external interactive maps (CalTopo, Google MyMaps). KMZ file support. |
| **Event publish + email notification blast** | Publishing an event is a coordinated moment: commanders flip a switch and everyone is notified simultaneously. | MEDIUM | Status transition: draft → published triggers email to all enrolled players. Email includes direct link to event. |
| **Roster change request workflow** | Players get moved around, have conflicts, or need to switch squads. Commanders need a structured inbox, not email chaos. | MEDIUM | Player submits request with reason. Commander sees pending requests, approves/denies with optional note. Player gets notified of outcome. |
| **Responsive mobile UI** | Players access the platform from phones in the field. Desktop-primary apps are a dealbreaker. | MEDIUM | Mobile-first layout. Touch-friendly tap targets. Document downloads work on iOS/Android. |
| **Past event retention** | Events from prior years serve as reference for recurring participants and after-action review. | LOW | Events stored indefinitely. Archived state hides from active list but remains accessible. |

---

### Differentiators (Competitive Advantage)

Features that set this platform apart from spreadsheets, Discord, and generic event tools.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Hierarchical permission scoping** | Platoon leaders can manage their platoon's roster without seeing or touching other platoons. Squad leaders can view their squad but not reassign members. This mirrors real military command structure and is absent from generic tools. | HIGH | Not just global roles — roles are scoped to a node in the hierarchy. A user can be Platoon Leader of 1st Platoon and Player in another event. |
| **Offline-ready downloadable maps** | CalTopo and Google MyMaps require connectivity. Hosting downloadable PNG/PDF/KMZ files means players can cache maps before stepping out of cell coverage. | MEDIUM | Handled by file hosting infrastructure. Key differentiator is the UX: make download obvious on mobile, group map files by type. |
| **Single authoritative event briefing** | Right now commanders send info over Discord, GroupMe, Facebook, email, and Google Drive links — players inevitably miss something. One URL with all info in one structured place is a meaningful upgrade over the status quo. | MEDIUM | The product's entire value prop is this. Information sections + maps + roster + assignments all under one event URL. |
| **Faction-scoped multi-event management** | Commanders run ~8 events/year. The platform should let them clone structures, reuse rosters, and see a history without starting from scratch each time. | MEDIUM | Event templates or cloning. Roster re-import picks up changes vs. prior import. |
| **Callsign-first player identity** | Airsoft players are universally known by callsign, not real name. The platform must surface callsigns prominently everywhere — assignments, rosters, notifications. Real names stay secondary. | LOW | Data model and UI choice. Most generic platforms lead with legal name. This platform leads with callsign. |
| **Targeted sub-unit notifications** | Commander needs to notify only 2nd Platoon about a change without spamming the whole faction. | MEDIUM | Notification target: whole event, specific platoon, specific squad, or individual player. Avoids notification fatigue. |
| **Commander approval workflow for roster changes** | Structured request/approve/deny cycle with audit trail. Better than "DM me on Discord." | MEDIUM | Builds trust: players know their request was seen. Commanders have a record. |
| **Information section ordering** | Commanders can order sections logically (Mission Overview first, Parking Info last). Content hierarchy communicates priority. | LOW | Drag-to-reorder or explicit order field. Low complexity, high perceived quality. |

---

### Anti-Features (Deliberately NOT Build in v1)

Features that seem like good ideas but create disproportionate complexity or undermine the core value.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **In-app messaging / chat** | "Why do I have to go to Discord?" | Real-time chat is a full product vertical. Notification, moderation, threading, mobile push, read receipts — each alone is weeks of work. None of it is planning-critical. | Link to the Discord/GroupMe/Signal group in the event info section. Commanders already have a comms channel; don't compete with it. |
| **Interactive/embedded maps** | "Can I see the AO on a map in the app?" | Interactive mapping (CalTopo, Leaflet with custom layers) requires significant effort and duplicates purpose-built tools players already use. | Host downloadable map files (PNG/PDF/KMZ) + link to CalTopo/Google MyMaps. Best of both worlds at a fraction of the cost. |
| **Registration / ticket sales** | "Can players sign up for the event through here?" | Registration is a separate product with payment processing, waivers, capacity management, waitlists. Entangles compliance, PCI scope, and support burden. | Import from existing registration system via CSV. Clear ownership boundary. |
| **Automatic squad auto-assignment** | "Can the system balance squads automatically?" | Auto-assignment removes commander control over the thing they care most about. Commanders have existing team relationships, leadership considerations, experience levels to account for. | Manual assignment with bulk operations (assign by team affiliation, drag-and-drop). |
| **Real-time game tracking / live status** | "Can I see where my squad is?" | GPS tracking, live status, respawn counting — this is an entirely separate product category with hardware requirements, battery concerns, and safety implications. | Out of scope. Dedicated tools exist (Voxer, dedicated milsim apps). |
| **Attendance tracking / check-in** | "Can I mark who showed up?" | Requires day-of coordination workflow, QR codes or manual check-in, and real-time roster changes under field conditions. High effort, niche value. | Roster export to CSV lets commanders use a clipboard or existing check-in tooling. |
| **OAuth / social login (Google, Facebook)** | "Let me just log in with Google" | Adds 3rd-party dependency, raises privacy questions for players who don't want their military sim activity linked to their Google identity, complicates account merging. | Email/password + magic link covers all cases cleanly for v1. |
| **Player statistics / history tracking** | "Show me my record across all events" | Gamification and leaderboards are fun but deeply out of scope for a logistics and coordination tool. | Focus on assignment history within the platform — players can see which events they participated in. Stats can be a v2+ feature. |
| **Bulk email marketing / newsletters** | "Can I send announcements to all past players?" | Mass email without careful opt-in management = SPAM. Requires unsubscribe flows, bounce handling, deliverability maintenance — a full email platform. | Transactional notifications only (event published, request approved). Keep email surface minimal and trusted. |
| **Mobile native app (iOS/Android)** | "Can you make an app?" | Native app = 2x (or 3x with React Native issues) build/maintenance burden, app store approvals, push notification infrastructure. PWA with responsive design serves mobile equally well for this use case. | Responsive web app works offline for document access. Add "Add to Home Screen" PWA manifest for app-like experience at no extra cost. |

---

## Feature Dependencies

```
Event Creation
    └──required by──> Roster Import
    └──required by──> Hierarchy Builder
    └──required by──> Information Sections
    └──required by──> Map/Document Upload
    └──required by──> Event Publish + Notification Blast

Roster Import
    └──required by──> Player Accounts (email as identity key)
    └──required by──> Hierarchy Builder (assigns imported players to units)

Player Accounts (auth)
    └──required by──> Player Dashboard
    └──required by──> Roster Change Request

Hierarchy Builder
    └──required by──> RBAC (scoped permissions need nodes to scope to)
    └──required by──> Player Assignment Display
    └──required by──> Targeted Sub-Unit Notifications [differentiator]

Event Publish
    └──required by──> Email Notification Blast (publish is the trigger)

RBAC
    └──enhances──> Hierarchy Builder (Platoon Leaders can manage their subtree)
    └──required by──> Roster Change Request Workflow (players can only request for themselves)

Information Sections
    └──enhanced by──> Section Ordering [differentiator]

Map/Document Upload
    └──enables──> Offline Map Downloads [differentiator]
    └──enhanced by──> External Map Links

Roster Change Request
    └──requires──> Player Accounts
    └──requires──> RBAC (only commanders approve)
    └──enhanced by──> Targeted Notifications (player gets notified of outcome)
```

### Dependency Notes

- **Event Creation is the root:** Nothing else can exist without an event. It is phase 1 of the build.
- **Roster Import unlocks Player Accounts:** The import email is the player's account key. Auth must support invite/activation flow, not just self-registration.
- **RBAC must come before Hierarchy permissions:** A generic auth system must be in place before scoped hierarchy permissions can be layered on. Don't build them as one monolith.
- **Notification requires deliverability infrastructure:** Email blast at 800 players is not `sendmail`. Transactional email provider (SendGrid/Postmark) integration is a prerequisite for any notification feature.
- **Offline maps are table stakes but simple to implement:** They're just correctly-configured file hosting (Content-Disposition headers, direct S3/CDN URLs). Not a separate feature build — just a correctness requirement on file upload.

---

## MVP Definition

### Launch With (v1)

Minimum viable product — the set of features that validates the core thesis: *"Commanders can publish a complete event briefing and every player receives it without anything falling through the cracks."*

- [ ] **Event creation and management** — root object; nothing works without it
- [ ] **CSV roster import** — the primary player onboarding path; no manual entry at 600+ players
- [ ] **Platoon/squad hierarchy builder** — the central commander workflow; table stakes for milsim structure
- [ ] **Player assignment to unit** — players need to know where they are
- [ ] **Role-based access control (5 roles)** — required to safely scope commander, leader, and player actions
- [ ] **Email/password + magic link auth** — players need to log in; magic link reduces abandonment
- [ ] **Custom information sections (markdown + attachments)** — the briefing content delivery mechanism
- [ ] **Document/map file upload and download** — offline map access is a field-critical requirement
- [ ] **Event publish + email notification blast** — the moment of delivery; this is what makes it a publishing tool
- [ ] **Roster change request workflow** — structured feedback loop; prevents Discord chaos
- [ ] **Player-facing event dashboard** — the player's entry point: assignment + docs + info in one place
- [ ] **Responsive mobile UI** — players are on phones; non-negotiable

### Add After Validation (v1.x)

Features to add once core usage patterns are established (1–2 events run through the platform).

- [ ] **Targeted sub-unit notifications** — validate that commanders actually need per-platoon email vs. whole-event email first
- [ ] **Information section ordering** — add when commanders have enough sections to need curation
- [ ] **Event cloning / template reuse** — add after the 2nd or 3rd event; amortizes roster import setup
- [ ] **Faction-scoped multi-event history view** — add when 3+ events exist to view historically
- [ ] **Callsign-prominent UI polish** — easy refinement once base layout is proven

### Future Consideration (v2+)

Features to defer until strong product-market fit is established.

- [ ] **Multi-faction event support** — supporting both OPFOR and BLUFOR factions with separate commander hierarchies requires a more complex data model; validate single-faction first
- [ ] **KMZ map parsing / preview** — rendering KMZ in-browser is non-trivial; hosting for download is sufficient v1
- [ ] **API for registration platform integration** — CSV works; real API integration is high-effort with uncertain ROI until platform is proven
- [ ] **Player event history / profile** — useful after multiple events have been run through the platform
- [ ] **PWA / "Add to Home Screen"** — manifest + service worker; low effort but only worthwhile when platform is regularly used

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Event creation | HIGH | LOW | P1 |
| CSV roster import | HIGH | MEDIUM | P1 |
| Hierarchy builder | HIGH | MEDIUM | P1 |
| Player assignment + dashboard | HIGH | LOW | P1 |
| RBAC (5 roles) | HIGH | MEDIUM | P1 |
| Auth (email/password + magic link) | HIGH | MEDIUM | P1 |
| Information sections (markdown + attachments) | HIGH | MEDIUM | P1 |
| Document/map file upload + download | HIGH | MEDIUM | P1 |
| Event publish + notification blast | HIGH | MEDIUM | P1 |
| Roster change request workflow | MEDIUM | MEDIUM | P1 |
| Responsive mobile UI | HIGH | MEDIUM | P1 |
| Targeted sub-unit notifications | MEDIUM | MEDIUM | P2 |
| Section ordering | LOW | LOW | P2 |
| Event cloning | MEDIUM | MEDIUM | P2 |
| Callsign-prominent UI | MEDIUM | LOW | P2 |
| Multi-faction support | MEDIUM | HIGH | P3 |
| KMZ in-browser preview | LOW | HIGH | P3 |
| Player history / stats | LOW | MEDIUM | P3 |
| PWA manifest | LOW | LOW | P3 |

**Priority key:**
- P1: Must have for launch — missing any of these and the product doesn't deliver its core promise
- P2: Should have — meaningful improvement, add after first event cycle
- P3: Nice to have — future consideration, defer until PMF established

---

## Competitor Feature Analysis

*Note: No direct airsoft-specific planning platform was found with documented feature sets. The closest analogues are sports team management (TeamSnap), large-event participant coordination (RunSignup), and how established milsim operators (MilSim West) currently solve this problem manually.*

| Feature | MilSim West (current state) | TeamSnap | RunSignup | This Platform |
|---------|------------------------------|----------|-----------|---------------|
| Event info distribution | Static Squarespace pages + PDF downloads (TACSOP, Waiver, Rules docs) | Not applicable | Registration confirmation emails | Structured sections with Markdown + attachments, event-scoped |
| Roster management | External registration system only; no in-platform roster builder | Roster list with player profiles | Participant list with CSV export | CSV import + platoon/squad hierarchy builder |
| Unit hierarchy | No platoon/squad structure in any tooling — managed off-platform (Discord/email) | Team-level only, no sub-unit hierarchy | No hierarchy | Full Faction → Platoon → Squad hierarchy with scoped roles |
| File/map hosting | PDF links on Squarespace page | No map/document hosting | None | CDN-hosted files with direct download, typed by format |
| Notifications | Email blasts from registration platform only | Email + push notifications | Email confirmation + blasts | Transactional email: event publish, request status changes |
| Role-based access | None — one admin per platform | Team Manager, Team Member | Event Director, Registrant | 5 roles scoped to hierarchy nodes |
| Mobile experience | Squarespace mobile-responsive (minimal) | Full mobile app | Mobile responsive | Mobile-first responsive web app |
| Roster change requests | Ad-hoc (Discord DMs, email) | None | None | Structured request/approve/deny workflow |

**Key insight:** The milsim community currently cobbles together 4–6 separate tools (registration system, Squarespace/Google Site, Discord/GroupMe, Google Drive, email) to accomplish what this platform does in one place. The competitive pressure is not against existing purpose-built tools — it's against the friction of the current multi-tool workflow.

---

## Sources

- **MilSim West (milsimwest.com):** Observed how a leading milsim operator currently distributes event information — TACSOP/Docs page with static PDFs, no player-specific portal. Confirmed gap this platform fills. (MEDIUM confidence — direct observation, current site)
- **TeamSnap (teamsnap.com/teams):** Sports team management — features: roster management, scheduling, messaging, assignments, availability tracking. Confirms table stakes for participant-facing coordination tools. (HIGH confidence — official feature pages)
- **RunSignup (info.runsignup.com):** Endurance event registration and participant management — confirms CSV export, email communications, role differentiation between directors and participants. (HIGH confidence — official feature pages)
- **PROJECT.md requirements:** Primary source of feature scope. All "Active" requirements treated as validated table stakes for this platform. (HIGH confidence — direct spec)
- **CalTopo (caltopo.com):** Backcountry mapping tool used by milsim operators — offline maps, collaborative editing, KMZ/GPX support. Confirms why this platform should link-to rather than replicate interactive mapping. (HIGH confidence — official product page)
- **MilSim West TACSOP/Docs page:** Confirmed that real events distribute content as: versioned PDF TACSOP, waiver PDFs, rules PDFs — all static downloads. No player login required, no personalized assignment. This is the problem to solve. (HIGH confidence — direct observation)

---
*Feature research for: Airsoft / MilSim event coordination and roster management platform*
*Researched: 2026-03-12*
