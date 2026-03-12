# Requirements: Airsoft Event Planning Application

**Defined:** 2026-03-12
**Core Value:** Faction commanders can publish a complete event briefing and every player receives it.

## v1 Requirements

### Authentication

- [ ] **AUTH-01**: User can create an account via invitation email sent when imported via CSV
- [ ] **AUTH-02**: User can log in with email and password
- [ ] **AUTH-03**: User can log in via magic link sent to their email
- [ ] **AUTH-04**: User session persists across browser refresh
- [ ] **AUTH-05**: User can reset their password via email link
- [ ] **AUTH-06**: User can log out from any page

### Authorization

- [ ] **AUTHZ-01**: System enforces five roles: System Admin, Faction Commander, Platoon Leader, Squad Leader, Player
- [ ] **AUTHZ-02**: Faction Commander has full administrative access to their event
- [ ] **AUTHZ-03**: Platoon Leader and Squad Leader have read-only access to roster and event information
- [ ] **AUTHZ-04**: Players can view roster, access event information, and submit roster change requests
- [ ] **AUTHZ-05**: Email addresses are visible only to leadership roles (Platoon Leader and above)
- [ ] **AUTHZ-06**: All data is scoped to the authenticated user event membership (no cross-event leakage)

### Event Management

- [ ] **EVNT-01**: Faction Commander can create a new event with name, location, description, start date, end date
- [ ] **EVNT-02**: Faction Commander can duplicate an existing event as a template for a new event
- [ ] **EVNT-03**: Faction Commander can view a list of all events they manage
- [ ] **EVNT-04**: Event has a status lifecycle: Draft to Published
- [ ] **EVNT-05**: Faction Commander can publish an event (making it visible to players)
- [ ] **EVNT-06**: Publishing an event is decoupled from sending notifications

### Roster Import

- [ ] **ROST-01**: Faction Commander can upload a CSV file to import players into an event
- [ ] **ROST-02**: CSV import validates all rows and shows a preview before committing (two-phase: validate then commit)
- [ ] **ROST-03**: CSV import errors are reported per-row before any data is saved
- [ ] **ROST-04**: Imported fields include: Name, Email, Callsign, Team Affiliation
- [ ] **ROST-05**: Re-importing a CSV updates existing players by email (upsert, not duplicate)
- [ ] **ROST-06**: Players not yet registered receive an invitation email after import

### Hierarchy Management

- [ ] **HIER-01**: Faction Commander can create Platoons within an event Faction
- [ ] **HIER-02**: Faction Commander can create Squads within a Platoon
- [ ] **HIER-03**: Faction Commander can assign players to Platoons
- [ ] **HIER-04**: Faction Commander can assign players to Squads
- [ ] **HIER-05**: Faction Commander can move players between Squads
- [ ] **HIER-06**: Full faction roster is visible to all faction members

### Content Management

- [ ] **CONT-01**: Faction Commander can create custom information sections within an event
- [ ] **CONT-02**: Each information section supports Markdown text content
- [ ] **CONT-03**: Each information section supports file attachments (PDF, images)
- [ ] **CONT-04**: Faction Commander can reorder information sections
- [ ] **CONT-05**: Faction Commander can edit or delete information sections

### Map Resources

- [ ] **MAPS-01**: Faction Commander can add external map platform links to an event
- [ ] **MAPS-02**: Faction Commander can add setup instructions for each external map link
- [ ] **MAPS-03**: Faction Commander can upload downloadable map files (PDF, JPEG, PNG, KMZ)
- [ ] **MAPS-04**: Players can download map files for offline use
- [ ] **MAPS-05**: Uploaded files are stored privately with authenticated time-limited download links

### Notifications

- [ ] **NOTF-01**: Faction Commander can send an email notification blast to all event participants
- [ ] **NOTF-02**: Email notifications are sent when squad assignments change
- [ ] **NOTF-03**: Email notifications are sent when a roster change request is approved or denied
- [ ] **NOTF-04**: Notification emails are delivered via a transactional email provider
- [ ] **NOTF-05**: Bulk notification send is processed asynchronously

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
| AUTH-01 | Phase 1 | Pending |
| AUTH-02 | Phase 1 | Pending |
| AUTH-03 | Phase 1 | Pending |
| AUTH-04 | Phase 1 | Pending |
| AUTH-05 | Phase 1 | Pending |
| AUTH-06 | Phase 1 | Pending |
| AUTHZ-01 | Phase 1 | Pending |
| AUTHZ-02 | Phase 1 | Pending |
| AUTHZ-03 | Phase 1 | Pending |
| AUTHZ-04 | Phase 1 | Pending |
| AUTHZ-05 | Phase 1 | Pending |
| AUTHZ-06 | Phase 1 | Pending |
| EVNT-01 | Phase 2 | Pending |
| EVNT-02 | Phase 2 | Pending |
| EVNT-03 | Phase 2 | Pending |
| EVNT-04 | Phase 2 | Pending |
| EVNT-05 | Phase 2 | Pending |
| EVNT-06 | Phase 2 | Pending |
| ROST-01 | Phase 3 | Pending |
| ROST-02 | Phase 3 | Pending |
| ROST-03 | Phase 3 | Pending |
| ROST-04 | Phase 3 | Pending |
| ROST-05 | Phase 3 | Pending |
| ROST-06 | Phase 3 | Pending |
| HIER-01 | Phase 3 | Pending |
| HIER-02 | Phase 3 | Pending |
| HIER-03 | Phase 3 | Pending |
| HIER-04 | Phase 3 | Pending |
| HIER-05 | Phase 3 | Pending |
| HIER-06 | Phase 3 | Pending |
| CONT-01 | Phase 4 | Pending |
| CONT-02 | Phase 4 | Pending |
| CONT-03 | Phase 4 | Pending |
| CONT-04 | Phase 4 | Pending |
| CONT-05 | Phase 4 | Pending |
| MAPS-01 | Phase 4 | Pending |
| MAPS-02 | Phase 4 | Pending |
| MAPS-03 | Phase 4 | Pending |
| MAPS-04 | Phase 4 | Pending |
| MAPS-05 | Phase 4 | Pending |
| NOTF-01 | Phase 5 | Pending |
| NOTF-02 | Phase 5 | Pending |
| NOTF-03 | Phase 5 | Pending |
| NOTF-04 | Phase 5 | Pending |
| NOTF-05 | Phase 5 | Pending |
| RCHG-01 | Phase 5 | Pending |
| RCHG-02 | Phase 5 | Pending |
| RCHG-03 | Phase 5 | Pending |
| RCHG-04 | Phase 5 | Pending |
| RCHG-05 | Phase 5 | Pending |
| PLAY-01 | Phase 6 | Pending |
| PLAY-02 | Phase 6 | Pending |
| PLAY-03 | Phase 6 | Pending |
| PLAY-04 | Phase 6 | Pending |
| PLAY-05 | Phase 6 | Pending |
| PLAY-06 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 54 total
- Mapped to phases: 54
- Unmapped: 0

---
*Requirements defined: 2026-03-12*
*Last updated: 2026-03-12 after initial definition*
