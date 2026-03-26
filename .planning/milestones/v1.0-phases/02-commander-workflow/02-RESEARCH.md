# Phase 2: Commander Workflow - Research

**Researched:** 2026-03-12
**Domain:** CSV Import Pipeline · EF Core Upsert · Hierarchy Management · shadcn/ui DataTable + Accordion + Combobox · React Router v7 Data Mode · File Upload Testing
**Confidence:** HIGH — all core claims verified against official CsvHelper docs, Microsoft EF Core docs (aspnetcore-10.0), shadcn/ui official docs, and Phase 1 established patterns.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**CSV Import Preview (ROST-02, ROST-03):** Errors-only preview. Show only rows with errors or warnings. Valid rows summarized as count at the top ("N valid, N errors, N warnings"). Errors block commit; warnings allow it. Commander re-uploads corrected CSV to clear errors — no in-app row editing.

**Team Affiliation Grouping (HIER-01..05):** Show affiliation groups as-is from CSV. No fuzzy matching, no merge UI. Players grouped by raw affiliation string. Within each group, inline squad assignment via click-cell → dropdown.

**Event Duplication (EVNT-02):** Always copies platoon/squad structure. Commander selects which information sections to copy via checkboxes. Does NOT copy: roster, maps, event dates, published status (always resets to Draft).

**Roster View (HIER-06):** Grouped by platoon → squad (accordion/tree) with cross-squad search. Callsign displayed prominently.

### Claude's Discretion

- Exact shadcn/ui components for hierarchy builder table (CommandTable, DataTable, etc.)
- Pagination strategy for roster view (server-side vs client-side — 400 rows is fine client-side)
- Exact column set for player roster table beyond: Name, Callsign, Team Affiliation, Platoon, Squad
- API endpoint structure for hierarchy mutations (REST vs batch operations)
- Whether event duplication is a modal dialog or a separate page/step

### Deferred Ideas (OUT OF SCOPE)

- Fuzzy affiliation matching / merge UI
- In-app CSV row editing — re-upload to fix errors
- Map resource duplication
- Any player-facing features (player dashboard, mobile optimization) — Phase 4
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| EVNT-01 | Faction Commander can create a new event with name, location, description, start date, end date | EF Core entity + POST endpoint + React Hook Form with Zod |
| EVNT-02 | Faction Commander can duplicate an existing event as template | Deep-clone service: always copy Platoon/Squad structure; selective info sections via checkboxes; reset dates + Draft status |
| EVNT-03 | Faction Commander can view list of all events they manage | GET /events scoped to faction; client-side list with TanStack Query |
| EVNT-04 | Event has status lifecycle: Draft to Published | Simple `EventStatus` enum (Draft=0, Published=1) in EF entity; no state machine needed |
| EVNT-05 | Faction Commander can publish an event | PUT /events/{id}/publish; `RequireFactionCommander` policy + scope guard |
| EVNT-06 | Publishing is decoupled from sending notifications | Publish = status flip only; notification blast is a separate Phase 3 concern |
| ROST-01 | Commander can upload CSV file to import players | `IFormFile` endpoint + CsvHelper 33.x two-phase validate-then-commit |
| ROST-02 | CSV import validates all rows and shows preview before committing | Phase 1: POST /roster/validate → returns CsvValidationResult; Phase 2: POST /roster/commit |
| ROST-03 | CSV import errors reported per-row before any data saved | CsvHelper row-by-row validation with try/catch; collect `CsvImportError` list |
| ROST-04 | Imported fields: Name, Email, Callsign, Team Affiliation | `RosterImportRow` DTO with `[Name(...)]` attribute mapping |
| ROST-05 | Re-importing updates existing players by email (upsert, not duplicate) | EF Core: `FirstOrDefaultAsync` by email + `SetValues` pattern; see Upsert Pattern below |
| ROST-06 | Players not yet registered receive invitation email after import | Reuse Phase 1 invite flow (Resend SDK); trigger synchronously after commit (400 rows = fast enough) |
| HIER-01 | Commander can create Platoons within an event Faction | POST /events/{id}/platoons; Platoon entity with FK to Event/Faction |
| HIER-02 | Commander can create Squads within a Platoon | POST /platoons/{id}/squads; Squad entity with FK to Platoon |
| HIER-03 | Commander can assign players to Platoons | PUT /event-players/{id}/platoon; update EventPlayer.PlatoonId |
| HIER-04 | Commander can assign players to Squads | PUT /event-players/{id}/squad; update EventPlayer.SquadId |
| HIER-05 | Commander can move players between Squads | Same as HIER-04 — the PUT operation replaces the assignment |
| HIER-06 | Full faction roster visible to all faction members | GET /events/{id}/roster with platoon→squad grouping; `RequirePlayer` policy |
</phase_requirements>

---

## Summary

Phase 2 is built on three distinct technical concerns: (1) a two-phase CSV import pipeline using CsvHelper 33.x with per-row validation, (2) EF Core entity management for the event→faction→platoon→squad→player hierarchy, and (3) React UI components for the hierarchy builder and roster viewer.

The CSV pipeline is architecturally the most important decision: validate-then-commit means two separate API endpoints. The validate endpoint reads all rows with CsvHelper, runs business rules, and returns structured errors without touching the database. The commit endpoint re-reads the validated CSV (or accepts the same data) and executes upserts. For 400 rows, in-memory processing is entirely appropriate — no streaming or background jobs.

EF Core upsert by email (ROST-05) should use the query-then-update pattern (`FirstOrDefaultAsync` by email → `CurrentValues.SetValues` for existing, `Add` for new), NOT `ExecuteUpdate` or `context.Update()` which bypass change tracking in confusing ways for this use case. The natural key is email; all EventPlayer records are keyed by (EventId, Email).

The React hierarchy builder is a TanStack Table (`@tanstack/react-table`) wrapped by shadcn/ui `Table` primitives, with a Combobox in the Squad cell for inline assignment. The roster view uses shadcn/ui `Accordion` with a cross-column filter input for the search requirement.

**Primary recommendation:** CsvHelper 33.x with manual row-by-row try/catch for validation; EF Core query-then-update upsert; TanStack Table + shadcn/ui Combobox for hierarchy builder; shadcn/ui Accordion for roster view.

---

## Standard Stack

### Core (Phase 2 additions to Phase 1 foundation)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `CsvHelper` | 33.0.1 | CSV parsing + header mapping | 367M NuGet downloads; official AWS-sponsored .NET CSV standard; strongly-typed `GetRecords<T>()` with `[Name]` attributes; handles quoted fields, encoding edge cases |
| `@tanstack/react-table` | 8.x | Headless table engine for hierarchy builder | Required by shadcn/ui DataTable pattern; provides sorting, filtering, cell rendering; no opinions on style |
| shadcn/ui `Accordion` | (copied to codebase) | Roster view: collapsible platoon→squad groups | Radix UI primitive—accessible by default; supports `type="multiple"` for expand-all |
| shadcn/ui `Combobox` | (copied to codebase) | Inline squad assignment cell in hierarchy table | Built on Base UI; supports `itemToStringValue` for object items; keyboard accessible |
| shadcn/ui `Table` | (copied to codebase) | Hierarchy builder table shell | Wraps TanStack Table; consistent with rest of shadcn/ui component set |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `react-dropzone` | 14.x | CSV file drag-and-drop UI | Already in stack from Phase 1; use for the CSV upload input |
| `FluentValidation.AspNetCore` | 11.x | Business rule validation per CSV row | Use for: email format, name max length, required fields — beyond what CsvHelper attribute validation covers |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Query-then-update upsert | `context.Update()` auto-upsert | `context.Update()` works for auto-generated keys but email is the natural key here — natural-key upsert requires query first |
| TanStack Table + shadcn/ui Table | shadcn/ui DataTable full component | DataTable is just the TanStack Table guide applied — building it from scratch gives full control over the Squad Combobox cell renderer |
| Accordion for roster | Custom tree component | Accordion handles platoon→squad grouping cleanly with built-in expand/collapse + accessibility |

### Installation (Phase 2 additions)

```bash
# Already in project from Phase 1:
# CsvHelper 33.x, FluentValidation.AspNetCore, react-dropzone

# Add TanStack Table for hierarchy builder:
pnpm add @tanstack/react-table

# shadcn/ui components (run from web/ directory):
pnpm dlx shadcn@latest add accordion
pnpm dlx shadcn@latest add combobox
pnpm dlx shadcn@latest add table
pnpm dlx shadcn@latest add checkbox
pnpm dlx shadcn@latest add dialog
```

---

## Architecture Patterns

### Data Model: Event Hierarchy

```
Event (name, location, description, startDate, endDate, status: Draft/Published)
  └── Faction (commanderId FK to AppUser)          ← 1 Faction per Event in v1
        ├── Platoon (name, order)
        │     └── Squad (name, order)
        └── EventPlayer (eventId, userId?, email, name, callsign, teamAffiliation, platoonId?, squadId?)
```

**EventPlayer is the join table** between an Event and a player identity. It has:
- `Email` — the natural key for upsert (player may not have a user account yet)
- `UserId` — nullable FK to `AppUser` (null until the player accepts their invitation and registers)
- `PlatoonId`, `SquadId` — nullable FKs, assigned by commander via hierarchy builder

```csharp
// Data/Entities/Event.cs
public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Location { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public Guid FactionId { get; set; }
    public Faction Faction { get; set; } = null!;
}

public enum EventStatus { Draft = 0, Published = 1 }

// Data/Entities/EventPlayer.cs
public class EventPlayer
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Email { get; set; } = null!;       // natural key for upsert
    public string Name { get; set; } = null!;
    public string? Callsign { get; set; }
    public string? TeamAffiliation { get; set; }
    public string? UserId { get; set; }               // null until invite accepted
    public Guid? PlatoonId { get; set; }
    public Guid? SquadId { get; set; }
    public Event Event { get; set; } = null!;
    public Platoon? Platoon { get; set; }
    public Squad? Squad { get; set; }
}

// Data/Entities/Platoon.cs
public class Platoon
{
    public Guid Id { get; set; }
    public Guid FactionId { get; set; }
    public string Name { get; set; } = null!;
    public int Order { get; set; }
    public Faction Faction { get; set; } = null!;
    public ICollection<Squad> Squads { get; set; } = [];
    public ICollection<EventPlayer> Players { get; set; } = [];
}

// Data/Entities/Squad.cs
public class Squad
{
    public Guid Id { get; set; }
    public Guid PlatoonId { get; set; }
    public string Name { get; set; } = null!;
    public int Order { get; set; }
    public Platoon Platoon { get; set; } = null!;
    public ICollection<EventPlayer> Players { get; set; } = [];
}
```

---

### Pattern 1: CSV Import — Two-Phase Validate-Then-Commit

**What:** Two endpoints. POST /events/{id}/roster/validate reads the CSV, runs validation, returns errors without touching DB. POST /events/{id}/roster/commit re-runs parsing and writes to DB.

**Why two endpoints:** The validate response is shown in the errors-only preview UI. The commander sees the error count, fixes the CSV, and re-uploads. The commit is a separate explicit action.

**CsvHelper mapping:**
```csharp
// Source: joshclose.github.io/CsvHelper/getting-started
// Models/CsvImport/RosterImportRow.cs
public class RosterImportRow
{
    [Name("name")]   public string Name { get; set; } = null!;
    [Name("email")]  public string Email { get; set; } = null!;
    [Name("callsign")] public string? Callsign { get; set; }
    [Name("team")]   public string? TeamAffiliation { get; set; }
}
```

**Validation service — row-by-row error collection:**
```csharp
// Source: CsvHelper official docs + FluentValidation pattern
// Services/RosterService.cs
public async Task<CsvValidationResult> ValidateRosterCsvAsync(IFormFile file, Guid eventId)
{
    var errors = new List<CsvRowError>();
    var validCount = 0;
    var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    using var reader = new StreamReader(file.OpenReadStream());
    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

    // Prepare header for case-insensitive matching
    csv.Context.RegisterClassMap<RosterImportRowMap>();

    // Read all at once — 400 rows is in-memory fine
    List<(int Row, RosterImportRow? Data, string? ParseError)> rows = new();

    try
    {
        csv.Read();
        csv.ReadHeader();
    }
    catch (CsvHelperException ex)
    {
        return new CsvValidationResult
        {
            FatalError = $"CSV header could not be read: {ex.Message}"
        };
    }

    var rowNum = 1;
    while (csv.Read())
    {
        rowNum++;
        try
        {
            var record = csv.GetRecord<RosterImportRow>();
            rows.Add((rowNum, record, null));
        }
        catch (CsvHelperException ex)
        {
            rows.Add((rowNum, null, ex.Message));
        }
    }

    foreach (var (row, data, parseError) in rows)
    {
        if (parseError is not null)
        {
            errors.Add(new CsvRowError(row, "parse", parseError, Severity.Error));
            continue;
        }

        // Business validation
        if (string.IsNullOrWhiteSpace(data!.Email) || !IsValidEmail(data.Email))
            errors.Add(new CsvRowError(row, "email", "Invalid or missing email", Severity.Error));

        if (string.IsNullOrWhiteSpace(data.Name))
            errors.Add(new CsvRowError(row, "name", "Name is required", Severity.Error));

        if (!seenEmails.Add(data.Email.ToLowerInvariant()))
            errors.Add(new CsvRowError(row, "email", "Duplicate email in this CSV", Severity.Error));

        if (string.IsNullOrWhiteSpace(data.Callsign))
            errors.Add(new CsvRowError(row, "callsign", "Callsign is missing", Severity.Warning));

        validCount++;
    }

    return new CsvValidationResult
    {
        ValidCount = validCount - errors.Count(e => e.Severity == Severity.Error),
        ErrorCount = errors.Count(e => e.Severity == Severity.Error),
        WarningCount = errors.Count(e => e.Severity == Severity.Warning),
        Errors = errors
    };
}
```

**Commit phase — upsert by email:**
```csharp
public async Task CommitRosterCsvAsync(IFormFile file, Guid eventId)
{
    // Re-parse (same logic as validate, skip errors)
    var importRows = ParseCsvRows(file); // extract shared helper

    foreach (var row in importRows.Where(r => r.IsValid))
    {
        // UPSERT by email — natural key
        var existing = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId
                && ep.Email.ToLower() == row.Email.ToLower());

        if (existing is null)
        {
            _db.EventPlayers.Add(new EventPlayer
            {
                EventId = eventId,
                Email = row.Email,
                Name = row.Name,
                Callsign = row.Callsign,
                TeamAffiliation = row.TeamAffiliation
            });
        }
        else
        {
            // Update name/callsign/team from CSV — do NOT overwrite squad assignments
            existing.Name = row.Name;
            existing.Callsign = row.Callsign;
            existing.TeamAffiliation = row.TeamAffiliation;
        }
    }

    await _db.SaveChangesAsync();

    // ROST-06: Send invites to new unregistered players
    await _inviteService.SendPendingInvitesAsync(eventId);
}
```

**Why NOT Npgsql ON CONFLICT DO UPDATE (raw SQL upsert)?**
- The query-then-update pattern in EF Core handles the 400-row case cleanly with no SQL complexity
- Raw `INSERT ... ON CONFLICT` requires dropping to Dapper or `ExecuteRawSqlAsync` — breaking EF Core change tracking
- At 400 rows, the N+1 queries (one SELECT per row) are acceptable: ~400 round trips in a single transaction, < 1 second on local postgres

---

### Pattern 2: EF Core — Event Duplication (EVNT-02)

**What:** Deep-clone the event structure. Always copy platoon/squad tree. Selectively copy info sections (Phase 3 scope, but structure must be planned now). Never copy roster, maps, dates, or published status.

```csharp
// Services/EventService.cs
public async Task<Event> DuplicateEventAsync(
    Guid sourceEventId,
    DuplicateEventRequest request)  // request.CopyInfoSectionIds: Guid[]
{
    var source = await _db.Events
        .Include(e => e.Faction)
            .ThenInclude(f => f.Platoons)
                .ThenInclude(p => p.Squads)
        .FirstOrDefaultAsync(e => e.Id == sourceEventId)
        ?? throw new NotFoundException();

    ScopeGuard.AssertEventAccess(_currentUser, sourceEventId);

    var newEvent = new Event
    {
        Name = $"{source.Name} (Copy)",
        Location = source.Location,
        Description = source.Description,
        StartDate = null,      // LOCKED: dates not copied
        EndDate = null,        // LOCKED: dates not copied
        Status = EventStatus.Draft,  // LOCKED: always Draft
        Faction = new Faction
        {
            CommanderId = source.Faction.CommanderId,
            Platoons = source.Faction.Platoons.Select(p => new Platoon
            {
                Name = p.Name,
                Order = p.Order,
                Squads = p.Squads.Select(s => new Squad
                {
                    Name = s.Name,
                    Order = s.Order
                }).ToList()
            }).ToList()
        }
    };

    // Info sections copied selectively (Phase 3 data model — placeholder FK list)
    // newEvent.InfoSections = CopySelectedSections(source, request.CopyInfoSectionIds);

    _db.Events.Add(newEvent);
    await _db.SaveChangesAsync();
    return newEvent;
}
```

---

### Pattern 3: Hierarchy Builder — TanStack Table + Combobox Cell

**What:** A table of all EventPlayers, grouped by TeamAffiliation string (client-side grouping). Each row has a Squad column that renders a shadcn/ui Combobox for inline assignment. Changes are sent to the API immediately (optimistic updates via TanStack Query `useMutation`).

**Client-side grouping approach:**
```typescript
// Source: shadcn/ui DataTable pattern + TanStack Table grouping
// Grouping is done client-side by pre-sorting/grouping the data before passing to useReactTable

// Group players by TeamAffiliation before rendering
const playersByAffiliation = useMemo(() => {
  const groups = new Map<string, EventPlayer[]>();
  for (const player of players) {
    const key = player.teamAffiliation ?? "(No Team)";
    const group = groups.get(key) ?? [];
    group.push(player);
    groups.set(key, group);
  }
  return groups;
}, [players]);
```

**Squad assignment Combobox in table cell:**
```typescript
// Source: shadcn/ui Combobox docs (ui.shadcn.com/docs/components/radix/combobox)
// components/hierarchy/SquadCell.tsx
function SquadCell({ player, squads }: { player: EventPlayer; squads: Squad[] }) {
  const mutation = useMutation({
    mutationFn: (squadId: string | null) =>
      api.put(`/event-players/${player.id}/squad`, { squadId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["roster", player.eventId] }),
  });

  return (
    <Combobox
      items={squads}
      itemToStringValue={(s) => s.name}
      value={squads.find(s => s.id === player.squadId) ?? null}
      onValueChange={(squad) => mutation.mutate(squad?.id ?? null)}
    >
      <ComboboxInput placeholder="Assign squad..." />
      <ComboboxContent>
        <ComboboxEmpty>No squads found.</ComboboxEmpty>
        <ComboboxList>
          {(squad) => (
            <ComboboxItem key={squad.id} value={squad}>
              {squad.name}
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
```

---

### Pattern 4: Roster View — Accordion + Cross-Squad Search

**What:** Platoons as accordion triggers. Each platoon section contains squad subsections. Players listed in each squad. A text input filters by name OR callsign across all groups.

```typescript
// Source: shadcn/ui Accordion docs (ui.shadcn.com/docs/components/radix/accordion)
// pages/roster/RosterView.tsx

export function RosterView({ eventId }: { eventId: string }) {
  const [search, setSearch] = useState("");
  const { data: hierarchy } = useQuery({
    queryKey: ["events", eventId, "roster"],
    queryFn: () => api.get<RosterHierarchy>(`/events/${eventId}/roster`),
  });

  // Client-side filter — 400 players, no server round-trip needed
  const filtered = useMemo(() => {
    if (!search.trim()) return hierarchy;
    const q = search.toLowerCase();
    return hierarchy?.platoons.map(platoon => ({
      ...platoon,
      squads: platoon.squads.map(squad => ({
        ...squad,
        players: squad.players.filter(p =>
          p.name.toLowerCase().includes(q) ||
          (p.callsign ?? "").toLowerCase().includes(q)
        )
      })).filter(s => s.players.length > 0)
    })).filter(p => p.squads.length > 0);
  }, [hierarchy, search]);

  return (
    <div>
      <Input
        placeholder="Search by name or callsign..."
        value={search}
        onChange={e => setSearch(e.target.value)}
        className="mb-4 max-w-sm"
      />
      <Accordion type="multiple">
        {filtered?.platoons.map(platoon => (
          <AccordionItem key={platoon.id} value={platoon.id}>
            <AccordionTrigger>
              {platoon.name} ({platoon.squads.flatMap(s => s.players).length} players)
            </AccordionTrigger>
            <AccordionContent>
              {platoon.squads.map(squad => (
                <div key={squad.id} className="ml-4 mb-3">
                  <h4 className="font-semibold text-sm mb-1">{squad.name}</h4>
                  <div className="space-y-1">
                    {squad.players.map(p => (
                      <div key={p.id} className="flex gap-2 text-sm">
                        <span className="font-mono font-bold text-orange-400">
                          [{p.callsign ?? "—"}]
                        </span>
                        <span>{p.name}</span>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </AccordionContent>
          </AccordionItem>
        ))}
      </Accordion>
    </div>
  );
}
```

---

### Pattern 5: IFormFile Upload in Integration Tests

**What:** Testing CSV upload endpoints with `MultipartFormDataContent` in xUnit + WebApplicationFactory.

```csharp
// Source: Microsoft official integration test docs (aspnetcore-10.0)
// tests/MilsimPlanning.Api.Tests/Roster/RosterImportTests.cs

[Fact]
[Trait("Category", "ROST_Validate")]
public async Task ValidateRoster_WithValidCsv_ReturnsZeroErrors()
{
    // Arrange — build a MultipartFormDataContent with a CSV file
    var csvContent = "name,email,callsign,team\nJohn Smith,john@test.com,GHOST,Alpha Squad";
    var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");

    using var form = new MultipartFormDataContent();
    form.Add(fileContent, "file", "roster.csv");

    // Auth — use the TestAuthHandler pattern from Phase 1
    _client.DefaultRequestHeaders.Authorization = new("Bearer", _commanderToken);

    // Act
    var response = await _client.PostAsync($"/api/events/{_eventId}/roster/validate", form);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
    result!.ErrorCount.Should().Be(0);
    result.ValidCount.Should().Be(1);
}

[Fact]
[Trait("Category", "ROST_Validate")]
public async Task ValidateRoster_WithMissingEmail_ReturnsRowError()
{
    var csvContent = "name,email,callsign,team\nJohn Smith,,GHOST,Alpha Squad";
    // ... (same form setup)
    var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
    result!.Errors.Should().ContainSingle(e => e.Field == "email");
}

[Fact]
[Trait("Category", "ROST_Commit")]
public async Task CommitRoster_WithPreviouslyValidatedCsv_UpsertsPlayers()
{
    // Arrange — seed one existing player with old callsign
    await SeedPlayer(eventId: _eventId, email: "john@test.com", callsign: "OLD");

    var csvContent = "name,email,callsign,team\nJohn Smith,john@test.com,GHOST,Alpha Squad";
    // ... (form setup)

    // Act — commit
    var response = await _client.PostAsync($"/api/events/{_eventId}/roster/commit", form);

    // Assert — player was updated (upsert), not duplicated
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var player = await _db.EventPlayers
        .SingleAsync(p => p.Email == "john@test.com" && p.EventId == _eventId);
    player.Callsign.Should().Be("GHOST");
}
```

**Controller pattern for IFormFile:**
```csharp
// Controllers/RosterController.cs
[ApiController]
[Route("api/events/{eventId:guid}/roster")]
[Authorize(Policy = "RequireFactionCommander")]
public class RosterController : ControllerBase
{
    [HttpPost("validate")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit — 400 rows is well under 1MB
    public async Task<ActionResult<CsvValidationResult>> Validate(
        Guid eventId,
        IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("File must be a CSV");

        var result = await _rosterService.ValidateRosterCsvAsync(file, eventId);
        return Ok(result);
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit(Guid eventId, IFormFile file)
    {
        // Re-validate before commit (defense in depth)
        var validation = await _rosterService.ValidateRosterCsvAsync(file, eventId);
        if (validation.ErrorCount > 0)
            return UnprocessableEntity(validation);

        await _rosterService.CommitRosterCsvAsync(file, eventId);
        return NoContent();
    }
}
```

---

### Pattern 6: Event Status Lifecycle (EVNT-04, EVNT-05, EVNT-06)

**What:** Simple enum in EF entity. No state machine library needed.

```csharp
// Simple publish endpoint — no notification side effect (EVNT-06)
[HttpPut("{id:guid}/publish")]
[Authorize(Policy = "RequireFactionCommander")]
public async Task<IActionResult> PublishEvent(Guid id)
{
    var evt = await _eventService.GetEventForCommanderAsync(id, _currentUser.UserId);
    if (evt.Status == EventStatus.Published)
        return Conflict("Event is already published");

    evt.Status = EventStatus.Published;
    await _db.SaveChangesAsync();
    return NoContent();
}
```

---

### Recommended Project Structure (Phase 2 additions)

```
src/MilsimPlanning.Api/
├── Controllers/
│   ├── EventsController.cs          ← EVNT-01..06 (create, list, publish, duplicate)
│   ├── RosterController.cs          ← ROST-01..06 (validate, commit)
│   └── HierarchyController.cs       ← HIER-01..06 (platoons, squads, assignments)
├── Services/
│   ├── EventService.cs              ← create, list, duplicate, publish
│   ├── RosterService.cs             ← CSV validation pipeline + upsert commit
│   └── HierarchyService.cs          ← platoon/squad CRUD + player assignment
├── Data/Entities/
│   ├── Event.cs                     ← new
│   ├── Faction.cs                   ← new
│   ├── Platoon.cs                   ← new
│   ├── Squad.cs                     ← new
│   └── EventPlayer.cs               ← new (upsert target)
└── Models/
    ├── CsvImport/
    │   ├── RosterImportRow.cs       ← CsvHelper DTO
    │   ├── CsvValidationResult.cs   ← validate response
    │   └── CsvRowError.cs           ← per-row error detail
    ├── Events/
    │   ├── CreateEventRequest.cs
    │   ├── DuplicateEventRequest.cs
    │   └── EventDto.cs
    └── Hierarchy/
        ├── AssignSquadRequest.cs
        └── RosterHierarchyDto.cs    ← platoon→squad→player tree

web/src/
├── pages/
│   ├── events/
│   │   ├── EventList.tsx            ← EVNT-03
│   │   ├── EventDetail.tsx          ← event status, publish button
│   │   └── CreateEventDialog.tsx    ← EVNT-01 (modal or page — at discretion)
│   └── roster/
│       ├── CsvImportPage.tsx        ← ROST-01, ROST-02, ROST-03 (upload + error preview)
│       ├── HierarchyBuilder.tsx     ← HIER-01..05 (platoon/squad mgmt + inline assign)
│       └── RosterView.tsx           ← HIER-06 (accordion + search)
└── components/
    └── hierarchy/
        ├── SquadCell.tsx            ← Combobox cell renderer
        └── PlayerRow.tsx            ← row in hierarchy table
```

### Anti-Patterns to Avoid

- **Using `context.Update(entity)` for email-keyed upsert:** `Update()` treats the entity as modified by primary key; email is not the PK. Use `FirstOrDefaultAsync` by email then `CurrentValues.SetValues`.
- **Overwriting squad assignments on CSV re-import:** Re-import (ROST-05) updates name/callsign/team from CSV but MUST NOT reset PlatoonId/SquadId — commander's assignments should survive a re-import.
- **All validation in the commit endpoint only:** Validate-then-commit is the UX contract (ROST-02). A commit-only endpoint with inline errors would force the commander to page through errors during commit.
- **CsvHelper `GetRecords<T>().ToList()` inside a try/catch:** CsvHelper's `GetRecords` is lazy — exceptions happen during enumeration, not at the call site. You must iterate row-by-row or use a try/catch around `ToList()` (which catches only the first failure). Prefer the manual `while (csv.Read())` pattern to collect all errors.
- **Sending invitation emails synchronously in the validate phase:** Invites (ROST-06) go only after commit, never during validation. Sending during validate would invite players when the commander still intends to fix errors.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CSV parsing with header mapping | Custom `string.Split(',')` parser | CsvHelper `GetRecords<T>()` with `[Name]` attributes | Handles quoted fields, escaped commas, BOM, encoding edge cases; 367M downloads for a reason |
| Per-row error collection | Custom parsing loop | CsvHelper manual `while (csv.Read())` + try/catch | CsvHelper's exception types (`HeaderValidationException`, `MissingFieldException`) provide field name + row number out of the box |
| Table with sorting + filtering | Custom React table | TanStack Table (`@tanstack/react-table`) | Battle-tested headless table; handles sorting, filtering, grouping state with minimal re-renders |
| Combobox with accessibility | Custom dropdown with search input | shadcn/ui `Combobox` (Base UI primitive) | Keyboard navigation, aria roles, focus management built in |
| Collapsible sections with accessibility | Custom accordion | shadcn/ui `Accordion` | Radix UI Accordion handles keyboard navigation, aria-expanded, focus correctly |

**Key insight:** The hierarchy builder's inline-editing UX (click cell → open combobox → select squad → close) is a standard pattern that requires careful focus management and keyboard support. Building it custom is a significant accessibility risk. shadcn/ui Combobox handles this correctly.

---

## Common Pitfalls

### Pitfall 1: CsvHelper Error Recovery — All vs First Error

**What goes wrong:** Using `csv.GetRecords<T>().ToList()` inside a single try/catch only catches the *first* parsing error. The remaining rows are never validated. Commander sees "1 error" but after fixing it, finds 5 more errors.

**Why it happens:** `GetRecords<T>()` is `IEnumerable<T>` — it yields lazily. The exception fires during `ToList()` enumeration, which stops at the first throw.

**How to avoid:** Manual row-by-row loop:
```csharp
while (csv.Read())
{
    try { var record = csv.GetRecord<RosterImportRow>(); /* validate */ }
    catch (CsvHelperException ex) { errors.Add(new CsvRowError(rowNum, "parse", ex.Message)); }
    rowNum++;
}
```

**Warning signs:** Validate endpoint always returns exactly 1 error regardless of how many bad rows exist in the CSV.

---

### Pitfall 2: CSV Re-import Wipes Squad Assignments (ROST-05)

**What goes wrong:** The commit upsert overwrites ALL fields on existing players, including PlatoonId and SquadId. A commander who has carefully assigned 200 players loses all assignments when re-importing to add 10 new players.

**How to avoid:** In the upsert path for existing players, only update the fields that come from CSV (Name, Callsign, TeamAffiliation). Never touch PlatoonId/SquadId during CSV import.

```csharp
// WRONG:
context.Entry(existing).CurrentValues.SetValues(newRow); // overwrites everything

// RIGHT:
existing.Name = newRow.Name;
existing.Callsign = newRow.Callsign;
existing.TeamAffiliation = newRow.TeamAffiliation;
// PlatoonId and SquadId: untouched
```

---

### Pitfall 3: MultipartFormData Content-Type Header Required for IFormFile

**What goes wrong:** Integration test sends CSV bytes as `StringContent` or `ByteArrayContent` without the correct multipart form boundary. ASP.NET Core's `IFormFile` binder sees no file; parameter is null; endpoint returns 400 or NullReferenceException.

**How to avoid:** Always use `MultipartFormDataContent` with named part matching the parameter name:
```csharp
var form = new MultipartFormDataContent();
form.Add(fileContent, "file", "roster.csv"); // "file" must match IFormFile parameter name
```

**Warning signs:** Integration test gets 400 Bad Request even with valid CSV content.

---

### Pitfall 4: EF Core Email Comparison Case Sensitivity in PostgreSQL

**What goes wrong:** `ep.Email == row.Email` in EF Core LINQ generates `= 'john@test.com'` in PostgreSQL, which IS case-sensitive. `John@test.com` and `john@test.com` would create two records for the same player.

**How to avoid:** Normalize to lowercase before storing:
```csharp
Email = row.Email.ToLowerInvariant() // on store
```
And query with:
```csharp
.FirstOrDefaultAsync(ep => ep.Email == row.Email.ToLowerInvariant())
```
Or use `EF.Functions.ILike(ep.Email, row.Email)` for case-insensitive matching at DB level.

---

### Pitfall 5: Accordion `type="single"` Collapses Active Section on Re-render

**What goes wrong:** Roster view uses `type="single"` accordion. When the search filter changes state, React re-renders, and the accordion collapses back to its default (first item or nothing). Commander loses their place.

**How to avoid:** Use `type="multiple"` with controlled state:
```tsx
const [openPlatoons, setOpenPlatoons] = useState<string[]>([]);
<Accordion type="multiple" value={openPlatoons} onValueChange={setOpenPlatoons}>
```
Or initialize with all platoon IDs open by default for the roster view.

---

### Pitfall 6: CSV Header Matching is Case-Sensitive by Default

**What goes wrong:** Commander exports CSV with column headers `Name`, `Email` (capital N, E). CsvHelper's `[Name("name")]` attribute doesn't match `Name`. All records parse as empty/null.

**How to avoid:** Configure case-insensitive header matching:
```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    PrepareHeaderForMatch = args => args.Header.ToLowerInvariant()
};
```
And ensure `[Name]` attributes use lowercase.

---

## Code Examples

### CSV Validate Endpoint (Controller)

```csharp
// Source: CsvHelper official getting-started + ASP.NET Core IFormFile pattern
[HttpPost("validate")]
[RequestSizeLimit(10 * 1024 * 1024)]
public async Task<ActionResult<CsvValidationResult>> Validate(
    Guid eventId,
    IFormFile file)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);
    if (file is null || file.Length == 0)
        return BadRequest(new { error = "No file uploaded" });

    var result = await _rosterService.ValidateRosterCsvAsync(file, eventId);
    return Ok(result);
}
```

### EF Core Hierarchy Query (for Roster View Response)

```csharp
// Source: EF Core official docs — related data loading
// Services/HierarchyService.cs
public async Task<RosterHierarchyDto> GetRosterHierarchyAsync(Guid eventId)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var platoons = await _db.Platoons
        .Where(p => p.Faction.Events.Any(e => e.Id == eventId))
        .Include(p => p.Squads)
        .OrderBy(p => p.Order)
        .ToListAsync();

    var players = await _db.EventPlayers
        .Where(ep => ep.EventId == eventId)
        .ToListAsync();

    // Map to DTO tree
    return new RosterHierarchyDto
    {
        Platoons = platoons.Select(p => new PlatoonDto
        {
            Id = p.Id,
            Name = p.Name,
            Squads = p.Squads.OrderBy(s => s.Order).Select(s => new SquadDto
            {
                Id = s.Id,
                Name = s.Name,
                Players = players
                    .Where(ep => ep.SquadId == s.Id)
                    .Select(ep => new PlayerDto
                    {
                        Id = ep.Id,
                        Name = ep.Name,
                        Callsign = ep.Callsign,
                        TeamAffiliation = ep.TeamAffiliation
                    }).ToList()
            }).ToList()
        }).ToList(),
        UnassignedPlayers = players
            .Where(ep => ep.SquadId is null)
            .Select(ep => new PlayerDto { /* ... */ }).ToList()
    };
}
```

### Event Duplication Modal (React)

```typescript
// components/events/DuplicateEventDialog.tsx
// At Claude's discretion — modal dialog approach
export function DuplicateEventDialog({ eventId, infoSections }: Props) {
  const [selectedSections, setSelectedSections] = useState<string[]>([]);
  const mutation = useMutation({
    mutationFn: () => api.post(`/events/${eventId}/duplicate`, {
      copyInfoSectionIds: selectedSections
    }),
    onSuccess: (newEvent) => navigate(`/events/${newEvent.id}`),
  });

  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="outline">Duplicate Event</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Duplicate Event</DialogTitle>
          <DialogDescription>
            Platoon/squad structure will always be copied.
            Select which information sections to include:
          </DialogDescription>
        </DialogHeader>
        {infoSections.map(section => (
          <div key={section.id} className="flex items-center gap-2">
            <Checkbox
              checked={selectedSections.includes(section.id)}
              onCheckedChange={(checked) =>
                setSelectedSections(prev =>
                  checked ? [...prev, section.id] : prev.filter(id => id !== section.id)
                )
              }
            />
            <label>{section.title}</label>
          </div>
        ))}
        <Button onClick={() => mutation.mutate()} disabled={mutation.isPending}>
          {mutation.isPending ? "Duplicating..." : "Duplicate"}
        </Button>
      </DialogContent>
    </Dialog>
  );
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `CsvHelper.GetRecords<T>().ToList()` for validation | Manual `while (csv.Read())` per-row loop | Stable | Enables collecting all row errors, not just first |
| EF Core `context.Update()` for upsert by natural key | Query-then-update (`FindAsync` + `SetValues`) | Stable | `Update()` requires PK to be set; natural key upsert requires query first |
| Custom drag-and-drop table | TanStack Table + shadcn/ui Table | 2023-present | TanStack Table v8 introduced stable headless API; shadcn/ui DataTable guide codifies the pattern |
| dnd-kit for drag-and-drop assignment | Inline Combobox in table cell (click → dropdown) | Phase 2 decision | User context decided: "select-and-confirm" fallback; avoids dnd-kit complexity entirely |
| `System.Formats.Csv` (.NET 9) | CsvHelper 33.x | .NET 9 (2024) | `System.Formats.Csv` is low-level reader (no class mapping, no error messages); CsvHelper still correct for application-level import |

**Deprecated/outdated:**
- `IAsyncEnumerable` streaming from CsvHelper: not needed for 400 rows; in-memory is simpler
- EF Core InMemory provider for tests: official docs recommend against it; use Testcontainers PostgreSQL (established in Phase 1)

---

## Open Questions

1. **EventPlayer primary key structure**
   - What we know: EventPlayer needs a synthetic Guid PK; Email is the natural key for upsert
   - What's unclear: Whether to add a unique index on (EventId, Email) to enforce no-duplicate constraint at DB level
   - Recommendation: YES — add `HasIndex(ep => new { ep.EventId, ep.Email }).IsUnique()` in `OnModelCreating`. This prevents duplicates even if the service layer has a bug.

2. **Invitation email after commit — synchronous vs async (ROST-06)**
   - What we know: For 400 players, all of whom are new, sending 400 invitation emails synchronously would take 400 * ~100ms = 40 seconds (unacceptable)
   - What's unclear: In practice, re-imports typically add 0–20 new players; the common case is fast
   - Recommendation: Count new (unregistered) players. If ≤ 20, send synchronously. If > 20, queue to the `NotificationQueue` (Channel-based BackgroundService from Phase 1 stack). Phase 3 adds proper async blast anyway.

3. **Hierarchy builder — batch assignments vs individual PUT per player**
   - What we know: Commander assigns squads to 200 players inline; one PUT per cell change = 200 individual API calls if done sequentially
   - What's unclear: Whether the UX should be "auto-save on change" (immediate mutation) or "save all" button (batch)
   - Recommendation: Auto-save on cell change (immediate TanStack Query mutation) with debounce + toast feedback. Individual PUTs are fine at this scale — commanders typically assign a few dozen players per session, not all 200 at once.

---

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json` — this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9+ (established Phase 1) |
| Config file | `tests/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` |
| Quick run command | `dotnet test tests/MilsimPlanning.Api.Tests/ --filter "Category=Unit"` |
| Full suite command | `dotnet test tests/` |
| React tests | `pnpm test --run` (Vitest) |

### API Endpoints Needing Integration Tests

| Endpoint | Method | Auth Required | Test Category |
|----------|--------|---------------|---------------|
| `/api/events` | POST | FactionCommander | `EVNT_Create` |
| `/api/events` | GET | FactionCommander | `EVNT_List` |
| `/api/events/{id}/duplicate` | POST | FactionCommander + scope | `EVNT_Duplicate` |
| `/api/events/{id}/publish` | PUT | FactionCommander + scope | `EVNT_Publish` |
| `/api/events/{id}/roster/validate` | POST (multipart) | FactionCommander + scope | `ROST_Validate` |
| `/api/events/{id}/roster/commit` | POST (multipart) | FactionCommander + scope | `ROST_Commit` |
| `/api/events/{id}/platoons` | POST | FactionCommander + scope | `HIER_Platoon` |
| `/api/platoons/{id}/squads` | POST | FactionCommander + scope | `HIER_Squad` |
| `/api/event-players/{id}/squad` | PUT | FactionCommander + scope | `HIER_Assign` |
| `/api/events/{id}/roster` | GET | RequirePlayer + scope | `HIER_Roster` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EVNT-01 | Create event returns 201 with new event | Integration | `dotnet test --filter "Category=EVNT_Create"` | ❌ Wave 0 |
| EVNT-01 | Create event with missing required field returns 400 | Integration | `dotnet test --filter "Category=EVNT_Create"` | ❌ Wave 0 |
| EVNT-02 | Duplicate always includes platoon/squad structure | Integration | `dotnet test --filter "Category=EVNT_Duplicate"` | ❌ Wave 0 |
| EVNT-02 | Duplicate does not copy roster or dates | Integration | `dotnet test --filter "Category=EVNT_Duplicate"` | ❌ Wave 0 |
| EVNT-02 | Duplicate copies only selected info sections | Integration | `dotnet test --filter "Category=EVNT_Duplicate"` | ❌ Wave 0 |
| EVNT-03 | List returns only commander's own events | Integration | `dotnet test --filter "Category=EVNT_List"` | ❌ Wave 0 |
| EVNT-04 | New event starts in Draft status | Integration | `dotnet test --filter "Category=EVNT_Create"` | ❌ Wave 0 |
| EVNT-05 | Publish transitions status to Published | Integration | `dotnet test --filter "Category=EVNT_Publish"` | ❌ Wave 0 |
| EVNT-05 | Publishing already-published event returns 409 | Integration | `dotnet test --filter "Category=EVNT_Publish"` | ❌ Wave 0 |
| EVNT-06 | Publish does NOT trigger notification (no email sent) | Integration | `dotnet test --filter "Category=EVNT_Publish"` | ❌ Wave 0 |
| ROST-01 | Upload endpoint accepts CSV IFormFile | Integration | `dotnet test --filter "Category=ROST_Validate"` | ❌ Wave 0 |
| ROST-02 | Validate returns structured result before commit | Integration | `dotnet test --filter "Category=ROST_Validate"` | ❌ Wave 0 |
| ROST-03 | All row errors returned in single validate call | Integration | `dotnet test --filter "Category=ROST_Validate"` | ❌ Wave 0 |
| ROST-03 | Validate with errors does not persist any data | Integration | `dotnet test --filter "Category=ROST_Validate"` | ❌ Wave 0 |
| ROST-04 | Parsed fields: Name, Email, Callsign, TeamAffiliation | Unit | `dotnet test --filter "Category=Unit"` | ❌ Wave 0 |
| ROST-05 | Re-import updates existing player name/callsign | Integration | `dotnet test --filter "Category=ROST_Commit"` | ❌ Wave 0 |
| ROST-05 | Re-import does NOT overwrite existing squad assignments | Integration | `dotnet test --filter "Category=ROST_Commit"` | ❌ Wave 0 |
| ROST-06 | New players receive invitation email after commit | Integration (mock IResend) | `dotnet test --filter "Category=ROST_Commit"` | ❌ Wave 0 |
| ROST-06 | Existing registered players do NOT receive invitation | Integration (mock IResend) | `dotnet test --filter "Category=ROST_Commit"` | ❌ Wave 0 |
| HIER-01 | Create platoon returns 201 | Integration | `dotnet test --filter "Category=HIER_Platoon"` | ❌ Wave 0 |
| HIER-02 | Create squad within platoon returns 201 | Integration | `dotnet test --filter "Category=HIER_Squad"` | ❌ Wave 0 |
| HIER-03 | Assign player to platoon updates PlatoonId | Integration | `dotnet test --filter "Category=HIER_Assign"` | ❌ Wave 0 |
| HIER-04 | Assign player to squad updates SquadId | Integration | `dotnet test --filter "Category=HIER_Assign"` | ❌ Wave 0 |
| HIER-05 | Move player to different squad replaces assignment | Integration | `dotnet test --filter "Category=HIER_Assign"` | ❌ Wave 0 |
| HIER-06 | GET roster returns platoon→squad→player tree | Integration | `dotnet test --filter "Category=HIER_Roster"` | ❌ Wave 0 |
| HIER-06 | Player in event can GET roster (RequirePlayer) | Integration | `dotnet test --filter "Category=HIER_Roster"` | ❌ Wave 0 |
| HIER-06 | Player NOT in event gets 403 (IDOR) | Integration | `dotnet test --filter "Category=HIER_Roster"` | ❌ Wave 0 |

### React Component Testing Strategy

**Framework:** Vitest + @testing-library/react + MSW (established Phase 1)

| Component | Test Approach | What to Test |
|-----------|---------------|--------------|
| `CsvImportPage` | RTL + MSW mock | File selection triggers POST; error-only table shows correct rows; valid count summary displays |
| `HierarchyBuilder` | RTL + MSW mock | Combobox opens on cell click; selecting squad triggers PUT mutation; optimistic update shown |
| `RosterView` | RTL + MSW mock | Accordion renders platoon sections; search input filters players by callsign; search clears correctly |
| `DuplicateEventDialog` | RTL | Checkboxes toggle info sections; submit calls API with correct section IDs |

```typescript
// Source: @testing-library/react + MSW 2.x pattern
// web/src/tests/RosterView.test.tsx
import { render, screen, fireEvent } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "../mocks/server";

const mockHierarchy = {
  platoons: [{
    id: "plat-1", name: "Alpha Platoon",
    squads: [{
      id: "squad-1", name: "Alpha 1",
      players: [
        { id: "p1", name: "John Smith", callsign: "GHOST" },
        { id: "p2", name: "Jane Doe", callsign: "NOVA" }
      ]
    }]
  }],
  unassignedPlayers: []
};

test("search filters players by callsign", async () => {
  server.use(
    http.get("/api/events/evt-1/roster", () => HttpResponse.json(mockHierarchy))
  );
  render(<RosterView eventId="evt-1" />);

  // Type in search — callsign filter
  fireEvent.change(screen.getByPlaceholderText("Search by name or callsign..."), {
    target: { value: "GHOST" }
  });

  expect(screen.getByText("John Smith")).toBeInTheDocument();
  expect(screen.queryByText("Jane Doe")).not.toBeInTheDocument();
});
```

### Integration Test: CSV Upload Pattern

```csharp
// Source: Microsoft official integration test docs (aspnetcore-10.0)
// The key pattern for testing IFormFile endpoints
[Fact]
[Trait("Category", "ROST_Validate")]
public async Task ValidateRoster_WithMultipleErrors_ReturnsAllErrors()
{
    // Two bad rows — verify BOTH are returned (not just first)
    var csv = "name,email,callsign,team\n" +
              ",invalid-email,,Alpha\n" +  // row 2: bad email + missing name
              "Bob,,GHOST,Bravo";          // row 3: missing email

    using var form = new MultipartFormDataContent();
    var fileBytes = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
    fileBytes.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
    form.Add(fileBytes, "file", "roster.csv");

    _client.DefaultRequestHeaders.Authorization = new("Bearer", _commanderToken);
    var response = await _client.PostAsync($"/api/events/{_eventId}/roster/validate", form);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
    result!.ErrorCount.Should().BeGreaterThan(1); // both rows have errors
    result.Errors.Should().Contain(e => e.Row == 2);
    result.Errors.Should().Contain(e => e.Row == 3);
}
```

### Sampling Rate

- **Per task commit:** `dotnet test tests/MilsimPlanning.Api.Tests/ --filter "Category=Unit"` (unit tests only — fast, no Docker)
- **Per wave merge:** `dotnet test tests/ && pnpm --filter web test --run` (full suite: xUnit + Testcontainers + Vitest)
- **Phase gate:** Full suite green before `/gsd-verify-work`; specifically ROST tests + HIER_Roster IDOR test must pass

### Wave 0 Gaps

- [ ] `tests/MilsimPlanning.Api.Tests/Events/EventTests.cs` — covers EVNT-01 through EVNT-06
- [ ] `tests/MilsimPlanning.Api.Tests/Roster/RosterImportTests.cs` — covers ROST-01 through ROST-06
- [ ] `tests/MilsimPlanning.Api.Tests/Hierarchy/HierarchyTests.cs` — covers HIER-01 through HIER-06
- [ ] `tests/MilsimPlanning.Api.Tests/Unit/CsvValidationTests.cs` — unit tests for CsvHelper row-by-row parsing
- [ ] `web/src/tests/RosterView.test.tsx` — accordion + search behavior
- [ ] `web/src/tests/HierarchyBuilder.test.tsx` — Combobox cell + mutation
- [ ] `web/src/mocks/handlers/roster.ts` — MSW handlers for roster endpoints (if not created in Phase 1)
- [ ] EF Core migration: `dotnet ef migrations add CommanderWorkflow` — adds Event, Faction, Platoon, Squad, EventPlayer tables

---

## Sources

### Primary (HIGH confidence)
- `joshclose.github.io/CsvHelper/getting-started` — CsvHelper reading patterns, `GetRecords<T>()`, `PrepareHeaderForMatch`, manual row-by-row loop; confirmed 2026-03-12
- `joshclose.github.io/CsvHelper/examples/configuration/attributes` — `[Name]`, `[Ignore]`, `[Optional]`, `CsvConfiguration.FromAttributes<T>()` — confirmed 2026-03-12
- `learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete` — `ExecuteUpdate`/`ExecuteDelete` capabilities and limitations; confirmed 2026-03-12
- `learn.microsoft.com/en-us/ef/core/saving/disconnected-entities` — `InsertOrUpdate` query-then-update pattern with `SetValues`; confirmed 2026-03-12
- `learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0` — `WebApplicationFactory`, `MultipartFormDataContent` test pattern, `ConfigureTestServices`, mock auth; updated 2026-03-10
- `ui.shadcn.com/docs/components/radix/data-table` — TanStack Table + shadcn/ui Table integration; `@tanstack/react-table` as dependency; confirmed 2026-03-12
- `ui.shadcn.com/docs/components/radix/accordion` — `type="multiple"`, `AccordionItem`, `AccordionTrigger`, `AccordionContent`; confirmed 2026-03-12
- `ui.shadcn.com/docs/components/radix/combobox` — `itemToStringValue` for object items, `onValueChange`, `ComboboxItem`; confirmed 2026-03-12
- `www.npgsql.org/efcore/index.html` — `UseNpgsql()`, `AddDbContextPool`, EF 10 configuration pattern; confirmed 2026-03-12

### Secondary (MEDIUM confidence)
- Phase 1 RESEARCH.md — established patterns for `WebApplicationFactory`, `TestAuthHandler`, `ICurrentUser`, `ScopeGuard`, EF Core entity structure; directly applicable

### Tertiary (LOW confidence — none)
No unverified WebSearch-only claims in this document.

---

## Metadata

**Confidence breakdown:**
- CSV import pipeline (CsvHelper): HIGH — verified against official CsvHelper docs
- EF Core upsert by natural key: HIGH — verified against official EF Core disconnected entities docs
- shadcn/ui component choices (Accordion, Combobox, DataTable): HIGH — verified against official shadcn/ui docs
- Integration test file upload pattern: HIGH — verified against official ASP.NET Core integration test docs
- Hierarchy data model design: MEDIUM — derived from requirements; specific FK constraints are design decisions not verified against external source

**Research date:** 2026-03-12
**Valid until:** 2026-06-12 (stable libraries; CsvHelper 33.x API is stable; EF Core 10 patterns are versioned and backward-compatible)
