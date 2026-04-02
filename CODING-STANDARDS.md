# CODING-STANDARDS.md

Standards for the milsim-planning codebase. Every developer agent reads this before writing any code. The Architect references this when writing API contracts. The Code Reviewer checks against this.

---

## Identity & Primary Keys

- **All primary keys are `Guid`** — never `int`, never `string`
- **Route constraints must be typed:** `[HttpGet("{eventId:guid}")]` not `[HttpGet("{eventId}")]`
- **TypeScript represents Guids as `string`** (UUID format: `"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"`)
- **Never pass string literals as IDs** — if it's an entity ID, it's a Guid on the backend and a UUID-format string on the frontend

```csharp
// ✅ Correct
[HttpGet("{factionId:guid}/frequencies")]
public async Task<IActionResult> GetFrequencies(Guid factionId)

// ❌ Wrong
[HttpGet("{factionId}/frequencies")]
public async Task<IActionResult> GetFrequencies(string factionId)
```

```typescript
// ✅ Correct — Guid arrives as UUID string from API
const factionId: string = faction.id; // "a1b2c3d4-..."

// ❌ Wrong — never use arbitrary string as ID
const factionId = "faction"; // hard-coded string is not an ID
```

---

## API Routes

- **Pattern:** `/api/{resource}/{id:guid}/{sub-resource}`
- **HTTP verbs:** GET (read), POST (create), PUT (full replace), PATCH (partial update), DELETE
- **Routes are plural nouns:** `/api/events` not `/api/event`
- **Sub-resources follow the parent:** `/api/events/{eventId:guid}/frequencies`
- **Routes must match the contract exactly** — copy verbatim from the sequence diagram/API contract

```csharp
// ✅ Correct route pattern
[Route("api/events/{eventId:guid}/frequencies")]
[HttpGet]
public async Task<ActionResult<FrequencyDto>> GetFrequencies(Guid eventId)

// ❌ Wrong — missing plural, missing type constraint
[HttpGet("api/event/{eventId}/frequency")]
```

---

## DTOs and Request/Response Models

- **Response DTO naming:** `{Feature}Dto` — e.g., `FrequencyDto`, `EventDto`, `PlayerDto`
- **Request model naming:** `{Action}{Feature}Request` — e.g., `UpdateFrequencyRequest`, `CreateEventRequest`
- **Never return entity objects directly** — always map to a DTO
- **Nullable fields use `?`:** `string? PrimaryFrequency` not `string PrimaryFrequency`
- **All IDs in DTOs are `Guid`** — consistent with entities

```csharp
// ✅ Correct DTO
public class FrequencyDto
{
    public Guid FactionId { get; set; }
    public string? CommandPrimary { get; set; }
    public string? CommandBackup { get; set; }
}

// ❌ Wrong — using string ID, returning raw entity fields
public class FrequencyResponse
{
    public string factionId { get; set; } // wrong type + wrong casing
}
```

```typescript
// ✅ Correct TypeScript interface matching DTO
interface FrequencyDto {
  factionId: string;          // Guid as UUID string
  commandPrimary: string | null;
  commandBackup: string | null;
}
```

---

## Error Response Shapes

**All API errors follow a standard shape.** Developers must implement this shape. Code Reviewer must verify it. Architect must reference it in contracts — no "returns 403", always "returns 403 with `ProblemDetails`".

### Standard error shape (ASP.NET Core ProblemDetails)

```csharp
// ✅ Use ProblemDetails for all error responses
return Problem(
    title: "Forbidden",
    detail: "Insufficient role to access this resource.",
    statusCode: 403
);
```

```json
// ✅ What the client receives
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "Insufficient role to access this resource."
}
```

### Standard error codes and their shapes

| Status | When | `title` | `detail` |
|--------|------|---------|---------|
| 400 | Invalid input | "Bad Request" | Specific validation message |
| 401 | No/expired token | "Unauthorized" | "Authentication required." |
| 403 | Wrong role / IDOR | "Forbidden" | "Insufficient role to access this resource." |
| 404 | Entity not found | "Not Found" | "[Entity] {id} not found." |
| 409 | Conflict | "Conflict" | Specific conflict message |

### In API contracts
Every endpoint in the Architect's plan must specify errors as:
```
- 403: { status: 403, title: "Forbidden", detail: "Insufficient role..." }
- 404: { status: 404, title: "Not Found", detail: "[Entity] {id} not found." }
```
Not just `→ 403`. Always the full shape.

---

## Test Naming and Structure

- **Test class naming:** `{Feature}Tests` — e.g., `FrequencyTests`, `HierarchyTests`
- **Test method naming:** `{Feature}_{Scenario}_{ExpectedResult}`
  - e.g., `GetFrequencies_AsSquadMember_ReturnsOnlySquadFrequency`
  - e.g., `UpdateFrequency_AsNonCommander_Returns403`
- **Every endpoint must have integration tests** — no exceptions
- **Tests use real PostgreSQL via Testcontainers** — no mocked databases
- **Test coverage required:**
  - Happy path for every endpoint
  - Authorization: assert the right roles can/cannot access
  - IDOR: assert users cannot access other events' data
  - Edge cases: null/empty inputs, non-existent IDs

```csharp
// ✅ Correct test method name
[Fact]
public async Task GetFrequencies_AsSquadMember_ReturnsOnlySquadFrequency()

// ❌ Wrong — vague name, unclear what's being tested
[Fact]
public async Task TestFrequencies()
```

---

## Entity and Migration Rules

- **Entity properties added to migration must also be added to entity class** — always both, never just one
- **Migrations are delta-based** — never drop-and-recreate, always additive
- **Nullable columns use `string?` in entities** — not empty string defaults
- **Every entity has a `Guid Id` primary key**

```csharp
// ✅ Correct — entity class updated to match migration
public class Squad
{
    public Guid Id { get; set; }
    public string? SquadPrimaryFrequency { get; set; }  // added with migration
    public string? SquadBackupFrequency { get; set; }   // added with migration
}

// ❌ Wrong — migration added columns but entity class not updated
public class Squad
{
    public Guid Id { get; set; }
    // frequency columns missing — compile error in FrequencyService
}
```

---

## Service Layer Rules

- **No N+1 queries** — always use `.Include()` for related entities, never query inside a loop
- **Services throw exceptions; controllers catch them:**
  - `KeyNotFoundException` → 404
  - `ArgumentException` → 400
  - `ForbiddenException` → 403
  - `InvalidOperationException` → 409
- **ScopeGuard.AssertEventAccess first** — before any business logic in service methods
- **No raw SQL** — always EF Core

```csharp
// ✅ Correct — Include to avoid N+1
var squads = await _db.Squads
    .Include(s => s.Players)
    .Where(s => s.PlatoonId == platoonId)
    .ToListAsync();

// ❌ Wrong — N+1 query
var squads = await _db.Squads.Where(s => s.PlatoonId == platoonId).ToListAsync();
foreach (var squad in squads)
{
    squad.Players = await _db.Players.Where(p => p.SquadId == squad.Id).ToListAsync();
}
```

---

## Frontend Rules

- **All API calls go through `lib/api.ts`** — never `fetch()` directly in components
- **Server state uses TanStack Query** — no manual useState + useEffect for API data
- **Hook naming:** `use{Feature}` returning `{ data, isLoading, error, refetch }`
- **Component files are PascalCase:** `FrequencyDisplay.tsx` not `frequency-display.tsx`
- **Zero console errors** before declaring done — DevTools Console must be clean
- **No `any` types** — TypeScript strict mode, explicit types everywhere

```typescript
// ✅ Correct hook pattern
export function useFrequencies(eventId: string) {
  return useQuery({
    queryKey: ['frequencies', eventId],
    queryFn: () => getFrequencies(eventId),
  });
}

// ❌ Wrong — manual fetch in component
const [data, setData] = useState(null);
useEffect(() => {
  fetch('/api/frequencies').then(r => r.json()).then(setData);
}, []);
```

---

## Git Commit Standards

- **Every task gets a commit** before marking done
- **Commit message format:** `[HEX-X] {type}: {description}`
  - Types: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`
  - e.g., `[HEX-14] feat: add radio frequency fields to Squad, Platoon, Faction entities`
- **Commit after each logical unit** — backend commit, then frontend commit
- **Include the issue identifier** in every commit message

---

## Pre-Implementation Verification

Before writing any code, answer these questions:

1. **IDs:** Are all entity IDs `Guid`? Do my route constraints use `:guid`?
2. **Routes:** Did I copy the routes verbatim from the API contract?
3. **DTOs:** Do my DTO field names and types match the API contract exactly?
4. **Tests:** Does my file manifest include a test file? What test cases will I write?
5. **Entity + Migration:** If I'm adding a column, am I updating both the migration AND the entity class?
6. **N+1:** Am I querying inside any loops? If yes, convert to `.Include()`.

If any answer is "no" or "unsure" — resolve before writing code.
