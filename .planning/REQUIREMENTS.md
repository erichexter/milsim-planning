# Requirements: Airsoft Event Planning Application

**Defined:** 2026-03-12
**Core Value:** Faction commanders can publish a complete event briefing and every player receives it.

## v1 Requirements

### Authentication

- [x] **AUTH-01**: User can create an account via invitation email sent when imported via CSV
- [x] **AUTH-02**: User can log in with email and password
- [x] **AUTH-03**: User can log in via magic link sent to their email
- [x] **AUTH-04**: User session persists across browser refresh
- [x] **AUTH-05**: User can reset their password via email link
- [x] **AUTH-06**: User can log out from any page

### Authorization

- [x] **AUTHZ-01**: System enforces five roles: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player
- [x] **AUTHZ-02**: Faction Commander has full administrative access to their event
- [x] **AUTHZ-03**: Platoon Leader and Squad Leader have read-only access to roster and event information
- [x] **AUTHZ-04**: Players can view roster, access event information, and submit roster change requests
- [x] **AUTHZ-05**: Email addresses are visible only to leadership roles (Platoon Leader and above)
- [x] **AUTHZ-06**: All data is scoped to the authenticated user event membership (no cross-event leakage)

### Event Management

- [x] **EVNT-01**: Faction Commander can create a new event with name, location, description, start date, end date
- [x] **EVNT-02**: Faction Commander can duplicate an existing event as a template for a new event
- [x] **EVNT-03**: Faction Commander can view a list of all events they manage
- [x] **EVNT-04**: Event has a status lifecycle: Draft to Published
- [x] **EVNT-05**: Faction Commander can publish an event (making it visible to players)
- [x] **EVNT-06**: Publishing an event is decoupled from sending notifications

### Roster Import

- [x] **ROST-01**: Faction Commander can upload a CSV file to import players into an event
- [x] **ROST-02**: CSV import validates all rows and shows a preview before committing (two-phase: validate then commit)
- [x] **ROST-03**: CSV import errors are reported per-row before any data is saved
- [x] **ROST-04**: Imported fields include: Name, Email, Callsign, Team Affiliation
- [x] **ROST-05**: Re-importing a CSV updates existing players by email (upsert, not duplicate)
- [x] **ROST-06**: Players not yet registered receive an invitation email after import

### Hierarchy Management

- [x] **HIER-01**: Faction Commander can create Platoons within an event Faction
- [x] **HIER-02**: Faction Commander can create Squads within a Platoon
- [x] **HIER-03**: Faction Commander can assign players to Platoons
- [x] **HIER-04**: Faction Commander can assign players to Squads
- [x] **HIER-05**: Faction Commander can move players between Squads
- [x] **HIER-06**: Full faction roster is visible to all faction members

### Content Management

- [x] **CONT-01**: Faction Commander can create custom information sections within an event
- [x] **CONT-02**: Each information section supports Markdown text content
- [x] **CONT-03**: Each information section supports file attachments (PDF, images)
- [x] **CONT-04**: Faction Commander can reorder information sections
- [x] **CONT-05**: Faction Commander can edit or delete information sections

### Map Resources

- [x] **MAPS-01**: Faction Commander can add external map platform links to an event
- [x] **MAPS-02**: Faction Commander can add setup instructions for each external map link
- [x] **MAPS-03**: Faction Commander can upload downloadable map files (PDF, JPEG, PNG, KMZ)
- [x] **MAPS-04**: Players can download map files for offline use
- [x] **MAPS-05**: Uploaded files are stored privately with authenticated time-limited download links

### Notifications

- [x] **NOTF-01**: Faction Commander can send an email notification blast to all event participants
- [x] **NOTF-02**: Email notifications are sent when squad assignments change
- [x] **NOTF-03**: Email notifications are sent when a roster change request is approved or denied
- [x] **NOTF-04**: Notification emails are delivered via a transactional email provider
- [x] **NOTF-05**: Bulk notification send is processed asynchronously

### Roster Change Requests

- [ ] **RCHG-01**: Player can submit a roster change request
- [ ] **RCHG-02**: Faction Commander can view all pending roster change requests for an event
- [ ] **RCHG-03**: Faction Commander can approve or deny a roster change request
- [ ] **RCHG-04**: Player receives an email notification when their request is approved or denied
- [ ] **RCHG-05**: Roster is updated automatically when a change request is approved

### Player Experience

- [ ] **PLAY-01**: Player can view their squad and platoon assignment for an event
- [ ] **PLAY-02**: Player can view the full faction roster (names, callsigns, team affiliation, assignments)
- [ ] **PLAY-03**: Player can access all published event information sections
- [ ] **PLAY-04**: Player can download maps and documents from an event
- [ ] **PLAY-05**: Player-facing UI is fully functional on mobile phones (responsive, 44px touch targets)
- [ ] **PLAY-06**: Callsign is displayed prominently in all roster views

## v2 Requirements

### Notifications Enhanced

- **NOTF-V2-01**: Player can configure notification preferences (opt out of certain email types)
- **NOTF-V2-02**: In-app notification center for updates

### Multi-Faction Events

- **MFAC-01**: Event supports multiple factions (BLUFOR/OPFOR structure)
- **MFAC-02**: Each faction has its own commander and roster

### API Integration

- **INTG-01**: Direct API integration with external registration platforms (beyond CSV)

### Analytics

- **ANLT-01**: Faction Commander can view roster completeness metrics

## Out of Scope

| Feature | Reason |
|---------|--------|
| Internal messaging / chat | High complexity; Discord fills this for most communities |
| Interactive mapping (in-app) | External tools are purpose-built; replicating them adds scope |
| Automatic squad assignment | Commanders require manual control |
| Real-time game tracking | In-game tooling; out of scope for pre-event planning |
| In-game command tools | Out of scope |
| Player attendance tracking | Out of scope for v1 |
| Native mobile app | Responsive web covers mobile needs for v1 |
| OAuth / SSO login | Email/password + magic link sufficient for v1 |
| Payments / ticketing | External registration system handles this |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTH-01 | Phase 1 | Complete |
| AUTH-02 | Phase 1 | Complete |
| AUTH-03 | Phase 1 | Complete |
| AUTH-04 | Phase 1 | Complete |
| AUTH-05 | Phase 1 | Complete |
| AUTH-06 | Phase 1 | Complete |
| AUTHZ-01 | Phase 1 | Complete |
| AUTHZ-02 | Phase 1 | Complete |
| AUTHZ-03 | Phase 1 | Complete |
| AUTHZ-04 | Phase 1 | Complete |
| AUTHZ-05 | Phase 1 | Complete |
| AUTHZ-06 | Phase 1 | Complete |
| EVNT-01 | Phase 2 | Complete |
| EVNT-02 | Phase 2 | Complete |
| EVNT-03 | Phase 2 | Complete |
| EVNT-04 | Phase 2 | Complete |
| EVNT-05 | Phase 2 | Complete |
| EVNT-06 | Phase 2 | Complete |
| ROST-01 | Phase 2 | Complete |
| ROST-02 | Phase 2 | Complete |
| ROST-03 | Phase 2 | Complete |
| ROST-04 | Phase 2 | Complete |
| ROST-05 | Phase 2 | Complete |
| ROST-06 | Phase 2 | Complete |
| HIER-01 | Phase 2 | Complete |
| HIER-02 | Phase 2 | Complete |
| HIER-03 | Phase 2 | Complete |
| HIER-04 | Phase 2 | Complete |
| HIER-05 | Phase 2 | Complete |
| HIER-06 | Phase 2 | Complete |
| CONT-01 | Phase 3 | Complete |
| CONT-02 | Phase 3 | Complete |
| CONT-03 | Phase 3 | Complete |
| CONT-04 | Phase 3 | Complete |
| CONT-05 | Phase 3 | Complete |
| MAPS-01 | Phase 3 | Complete |
| MAPS-02 | Phase 3 | Complete |
| MAPS-03 | Phase 3 | Complete |
| MAPS-04 | Phase 3 | Complete |
| MAPS-05 | Phase 3 | Complete |
| NOTF-01 | Phase 3 | Complete |
| NOTF-02 | Phase 3 | Complete |
| NOTF-03 | Phase 3 | Complete |
| NOTF-04 | Phase 3 | Complete |
| NOTF-05 | Phase 3 | Complete |
| RCHG-01 | Phase 4 | Pending |
| RCHG-02 | Phase 4 | Pending |
| RCHG-03 | Phase 4 | Pending |
| RCHG-04 | Phase 4 | Pending |
| RCHG-05 | Phase 4 | Pending |
| PLAY-01 | Phase 4 | Pending |
| PLAY-02 | Phase 4 | Pending |
| PLAY-03 | Phase 4 | Pending |
| PLAY-04 | Phase 4 | Pending |
| PLAY-05 | Phase 4 | Pending |
| PLAY-06 | Phase 4 | Pending |

**Coverage:**
- v1 requirements: 56 total (AUTH×6, AUTHZ×6, EVNT×6, ROST×6, HIER×6, CONT×5, MAPS×5, NOTF×5, RCHG×5, PLAY×6)
- Mapped to phases: 56
- Unmapped: 0

---
*Requirements defined: 2026-03-12*
*Last updated: 2026-03-12 after initial definition*
