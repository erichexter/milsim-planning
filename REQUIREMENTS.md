# Airsoft Event Planning Application
## Requirements Specification

**Version:** Draft  
**Purpose:** Define functional and technical requirements for a faction event planning system used in large airsoft / milsim events.

---

## 1. Problem Definition

Large airsoft and milsim events often involve hundreds of players organized into factions, platoons, and squads.

Faction commanders must distribute large amounts of operational and logistical information before the event.

Currently this coordination is handled through:

- Email chains
- PDF attachments
- Spreadsheets
- External tools

This results in:

- Players missing critical information
- Repeated logistical questions
- Difficult roster management
- Fragmented communication

The goal of this application is to create a centralized planning platform where faction commanders can organize players, distribute event information, and notify participants of updates.

---

## 2. Business Goals

Primary goals of the system:

- Reduce administrative overhead for faction commanders
- Improve communication between command and players
- Make event preparation more organized and predictable
- Reduce confusion before events
- Replace scattered tools (email, spreadsheets, PDFs)
- Improve the overall player experience

---

## 3. Target Users

### Faction Commander

Primary administrator of the faction.

**Responsibilities:**
- Create and manage events
- Import player rosters
- Assign platoons and squads
- Approve roster change requests
- Publish event information
- Upload documents and maps
- Send update notifications

### Platoon Leaders

**Responsibilities:**
- View faction roster structure
- View platoon assignments
- Access event information

**Limitations:**
- Cannot modify roster assignments

### Squad Leaders

**Responsibilities:**
- View squad roster
- View platoon structure
- Access event information

**Limitations:**
- Cannot modify roster assignments

### Players

**Capabilities:**
- View squad and platoon assignments
- View entire faction roster structure
- Access event information
- Download maps and documents
- Request roster changes

---

## 4. Core Capabilities

### Event Management

- Create events
- Duplicate events from previous templates
- Organize faction structure

**Hierarchy:**

```
Event
 └── Faction
      └── Platoons
           └── Squads
                └── Players
```

### Roster Management

Players are imported from an external registration system.

**Import method:** CSV upload

**Imported fields:**
- Name
- Email
- Callsign
- Team Affiliation

Team affiliation helps commanders assign players to squads.

**Roster management features:**
- Assign players to platoons
- Assign players to squads
- Move players between squads
- Process roster change requests

### Event Information System

Faction commanders create custom information sections.

**Examples:**
- Communications plan
- Event schedule
- Logistics / what to bring
- Rules
- Objectives
- Maps

Each section may contain:
- Markdown text
- Attachments (PDF, images)

### Map Resources

Maps are typically hosted on external platforms.

The system supports:
- External map platform links
- Setup instructions for players
- Optional downloadable files

**Supported files:**
- PDF maps
- JPEG / PNG maps
- KMZ overlays

Players may download maps for offline use.

### Notifications

The system sends email notifications when:
- Event information is updated
- Squad assignments change
- Roster requests are approved or denied

Email delivery uses an external service such as SendGrid.

---

## 5. User Workflows

### Event Setup Workflow

1. Create Event
2. Import Player Roster (CSV)
3. Review Team Affiliations
4. Create Platoons
5. Create Squads
6. Assign Players
7. Create Information Sections
8. Upload Documents and Maps
9. Publish Event
10. Send Notifications

After publication:
- Additional documents may be uploaded
- Assignments may be updated
- Players may request roster changes

### Player Workflow

1. Receive Notification Email
2. Login to System
3. View Squad Assignment
4. Review Event Information
5. Download Maps / Documents
6. Request Roster Change (optional)

### Roster Change Workflow

1. Player submits request
2. Faction Commander reviews request
3. Commander approves or denies
4. Roster updates
5. Notification email sent

---

## 6. Data Model

### Player

Persistent identity information.

**Fields:**
- Name
- Email
- Callsign
- Team Affiliation

### Event

**Fields:**
- Event Name
- Event Location
- Event Description
- Start Date
- End Date
- Status

### Organizational Entities

- Faction
- Platoon
- Squad

### Event Participation

```
EventRegistration
 ├── Player
 ├── Event
 ├── Faction
 ├── Platoon
 └── Squad
```

### Additional Entities

- InformationSection
- Document
- MapResource
- RosterChangeRequest
- Notification

---

## 7. External Systems & Integrations

### Registration System

Players are imported from an external registration system.

**Integration method:** Manual CSV upload

### Email System

Email notifications are sent using a transactional email provider.

**Example:** SendGrid

### External Map Platforms

The system links to external mapping tools.

The application provides:
- Setup instructions
- Download links
- Optional map files

---

## 8. Non-Functional Requirements

### Scale

- Typical event size: 300–400 players
- Maximum supported size: 750–800 players

### Usage Pattern

Peak traffic occurs when:
- Event is published
- Notification emails are sent

Secondary activity occurs:
- Several days before the event

### Device Support

Players will access the system from:
- Mobile phones
- Desktop / laptop computers

The UI must support both.

### Offline Use

Players may download:
- Maps
- PDFs
- Event documents

These can be viewed offline during the event.

---

## 9. Security Model

### Roles

- System Admin
- Faction Commander
- Platoon Leader
- Squad Leader
- Player

### Permissions

**Faction Commander:**
- Full event administration

**Leadership:**
- Read-only roster access

**Players:**
- View roster
- Access information
- Request roster changes

### Data Visibility

**Visible to all players:**
- Name
- Callsign
- Team affiliation
- Squad assignments
- Platoon assignments

**Visible only to leadership:**
- Email addresses

### Authentication

**Supported login methods:**
- Email + password
- Magic login link

### Account Creation

```
Player imported via CSV
↓
System sends invitation email
↓
Player completes account setup
↓
Account becomes active
```

---

## 10. Operational Constraints

### Hosting Model

- **Frontend:** Static website
- **Backend:** API service
- **Hosting:** Cloud platform

### Event Frequency

Typical usage:
- ~8 events per year
- Approximately monthly

### Data Retention

Past events should be retained indefinitely.

### Administration

- Faction Commander is the highest operational role
- A System Admin may exist for platform management

---

## 11. Assumptions

- Registration systems remain external
- Email remains the primary communication channel
- Squad assignments are managed manually
- External map systems remain separate tools

---

## 12. Out of Scope (Version 1)

The following features are intentionally excluded from the first version:

- Internal messaging or chat
- Direct API integration with registration platforms
- Interactive mapping
- Automatic squad assignment
- Real-time game tracking
- In-game command tools
- Player attendance tracking
