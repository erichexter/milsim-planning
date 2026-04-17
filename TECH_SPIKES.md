# Tech Spikes for EventOwner Implementation

## Spike 1: Event Creator Identification & Data Migration

### Current State Analysis

**Event Creation Flow:**
- `EventService.CreateEventAsync()` creates:
  1. Event record with Name, Location, Description, Dates, Status=Draft
  2. Faction record with CommanderId = current user
  3. EventMembership record with (userId, eventId, role='faction_commander')

**Schema Current:**
- Events table: Id, Name, Location, Description, StartDate, EndDate, Status, FactionId
- EventMemberships table: Id, UserId, EventId, Role (string), JoinedAt
- **Missing:** Event.CreatedById (HLTD assumes this exists)

### Required Changes

#### 1. Add CreatedById to Event Entity
- Add `public string CreatedById { get; set; }` to Event.cs
- Create migration to add column
- Update CreateEventAsync to populate CreatedById

#### 2. Event Creator Identification Strategy
For existing events, identify the creator:
- **Primary method:** Use the Faction.CommanderId (person who leads the faction)
- **Rationale:** Current events are created by FactionCommanders, and the creator is stored as the faction commander

SQL to identify and migrate:
```sql
-- Identify events and their creators
SELECT e.Id, f.CommanderId
FROM Events e
INNER JOIN Factions f ON e.Id = f.EventId;

-- Migrate: Set CreatedById for existing events
UPDATE Events e
SET CreatedById = f.CommanderId
FROM Factions f
WHERE e.Id = f.EventId;

-- Create EventOwner role records for existing creators
INSERT INTO EventMemberships (Id, UserId, EventId, Role, JoinedAt)
SELECT
    gen_random_uuid(),
    f.CommanderId,
    e.Id,
    'event_owner',
    CURRENT_TIMESTAMP
FROM Events e
INNER JOIN Factions f ON e.Id = f.EventId
WHERE NOT EXISTS (
    SELECT 1 FROM EventMemberships
    WHERE UserId = f.CommanderId
    AND EventId = e.Id
    AND Role = 'event_owner'
);
```

### Acceptance Criteria
- [x] Confirmed Event creation flow stores creator in Faction.CommanderId
- [ ] Add CreatedById field to Event (entity + migration)
- [ ] Populate CreatedById for existing events (migration script)
- [ ] Create EventOwner EventMembership for existing creators (migration script)
- [ ] Test migration on staging database
- [ ] Verify row counts before/after migration
- [ ] Design rollback procedure

---

## Spike 2: Multi-Faction Model Validation

### Current State Analysis

**Event ↔ Faction Relationship:**
- Event.FactionId → Guid (FK to Faction.Id)
- Faction.EventId → Guid (FK to Event.Id)
- AppDbContext config: "1:1 in v1; Faction owns the FK"
- **Result:** Currently 1:1 relationship (one event, one faction)

**HLTD Assumption:**
- Says "EventOwner can view/edit all factions, platoons, squads, players within event"
- API says: "GET /api/events/{id}/members returns members from ALL factions"
- Suggests: Multiple factions per event possible

### Gap Analysis

Current schema only supports **1 faction per event**. HLTD design assumes **multiple factions per event**.

### Options

**Option A: Keep current 1:1 model**
- EventOwner sees single faction (by definition, only one exists)
- Simpler implementation
- No schema change required
- Limitation: Can't expand to multi-faction events later

**Option B: Change to 1:many model**
- Remove Event.FactionId (or keep as "primary faction")
- Let Faction.EventId be the only FK (Faction owns relationship)
- Allow multiple factions per event
- Requires schema migration
- More flexible for future

### Recommendation

For v1 EventOwner, **implement Option A (keep 1:1)**:
- No breaking schema changes
- EventOwner inherits all FactionCommander permissions
- When EventOwner views event, they see their single faction
- Future: Upgrade to multi-faction if needed

### Acceptance Criteria
- [ ] Confirmed Event-Faction is 1:1
- [ ] Confirmed API behavior with 1 faction per event works
- [ ] Document assumption: "EventOwner can manage the single faction assigned to their event"
- [ ] No schema changes required for v1

---

## Summary

**Spike 1 Effort:** 1-2 days
- Add CreatedById field
- Create migration scripts
- Test on staging

**Spike 2 Effort:** 1 day
- Schema validation complete
- Decision: Keep 1:1 model for v1
- Document assumption for architecture

**Blocker Status:**
- Spike 1: BLOCKER (need migration strategy)
- Spike 2: RESOLVED (1:1 model works for v1)
