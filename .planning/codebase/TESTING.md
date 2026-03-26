---
focus: quality
generated: 2026-03-25
---

# Testing Patterns

## Summary

The project has two separate test suites: a Vitest-based frontend suite in `web/` and an xUnit-based integration test project in `milsim-platform/src/MilsimPlanning.Api.Tests/`. Both suites run in CI on every push to `master` and must pass before any deployment occurs. The frontend uses MSW (Mock Service Worker) for API mocking; the backend uses Testcontainers to spin up a real PostgreSQL instance for every test class.

---

## Frontend Tests (web/)

### Framework

- **Runner:** Vitest v4 — config embedded in `web/vite.config.ts` under the `test` key
- **DOM environment:** `happy-dom` (faster than jsdom)
- **Assertion library:** Vitest built-in (`expect`) + `@testing-library/jest-dom` matchers
- **Component rendering:** `@testing-library/react` v16
- **API mocking:** MSW v2 (`msw/node` server)

### Run Commands

```bash
# From the web/ directory
pnpm test              # Watch mode (interactive)
pnpm test --run        # Single run (used in CI)
pnpm lint              # ESLint check
```

In CI (`.github/workflows/deploy.yml`):
```bash
pnpm --prefix web install --frozen-lockfile
pnpm --prefix web test --run
```

### Vitest Config (`web/vite.config.ts`)

```typescript
test: {
  environment: 'happy-dom',
  globals: true,                          // describe/it/expect available globally
  setupFiles: ['./src/test-setup.ts'],
}
```

### Global Setup (`web/src/test-setup.ts`)

```typescript
import '@testing-library/jest-dom';
import { beforeAll, afterEach, afterAll } from 'vitest';
import { server } from './mocks/server';

beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

Every test file benefits from jest-dom matchers (`toBeInTheDocument`, `toBeDisabled`, etc.) and the MSW server lifecycle without any per-file boilerplate.

### MSW Server Setup

- Server: `web/src/mocks/server.ts` — `setupServer(...handlers)` from `msw/node`
- Default handlers: `web/src/mocks/handlers.ts` — minimal stubs for `GET /api/events` and `POST /api/events`
- Tests override handlers with `server.use(http.get(...))` inside the test body
- Handler overrides are reset after each test (`afterEach(() => server.resetHandlers())`)

```typescript
// Default handlers (web/src/mocks/handlers.ts)
export const handlers = [
  http.get('/api/events', () => HttpResponse.json([])),
  http.post('/api/events', () => HttpResponse.json({ id: 'default-id', ... }, { status: 201 })),
];

// Per-test override pattern
server.use(
  http.get('/api/events/evt-1/notification-blasts', () => HttpResponse.json([]))
);
```

### Test File Organization

Two co-existing locations (inconsistency present):
- `web/src/__tests__/` — older tests (hooks, utility modules, core components)
- `web/src/tests/` — newer page-level tests (no leading underscores)

Both locations are picked up by Vitest automatically. New tests should go in `web/src/tests/` to match the newer pattern.

**Files in `web/src/__tests__/`:**
- `api.test.ts`, `auth.test.ts` — utility/library unit tests
- `useAuth.test.tsx` — hook tests
- `EventList.test.tsx`, `RosterView.test.tsx`, `HierarchyBuilder.test.tsx`, `CsvImportPage.test.tsx`, `MagicLinkConfirmPage.test.tsx`, `ProtectedRoute.test.tsx`

**Files in `web/src/tests/`:**
- `NotificationBlastPage.test.tsx`, `PlayerEventView.test.tsx`, `SectionEditor.test.tsx`, `BriefingPage.test.tsx`, `MapResourcesPage.test.tsx`, `Play06CallsignDisplay.test.tsx`, `ChangeRequestForm.test.tsx`

### Frontend Test Patterns

**Provider wrapper helper (used in most page tests):**
```typescript
function renderWithProviders(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/events']}>
        <Routes>
          <Route path="/events" element={ui} />
          <Route path="/events/:id" element={<div>Event Detail</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}
```

Always disable query retries (`retry: false`) in test `QueryClient` to avoid flakiness.

**Auth token helper (for role-gated pages):**
```typescript
function setRoleToken(role: string) {
  const payload = { sub: 'user-1', email: 'tester@example.com', role, exp: Math.floor(Date.now() / 1000) + 3600 };
  localStorage.setItem('milsim_token', `x.${btoa(JSON.stringify(payload))}.x`);
}
```

**Async assertions:**
- Use `await screen.findByText(...)` or `await screen.findByRole(...)` for elements that appear after data loads
- Use `waitFor(() => expect(...).toBeInTheDocument())` for conditional assertions

**Hook testing:**
```typescript
// web/src/__tests__/useAuth.test.tsx
const { result } = renderHook(() => useAuth());
act(() => { result.current.login(token); });
expect(result.current.user?.email).toBe('test@example.com');
```

**Module mocking (for heavy dependencies not under test):**
```typescript
vi.mock('../components/player/PlayerOverviewTab', () => ({
  PlayerOverviewTab: ({ eventId }: { eventId: string }) => (
    <div data-testid="player-overview-tab">Overview for {eventId}</div>
  ),
}));
```

**Capturing request bodies:**
```typescript
let capturedBody: { copyInfoSectionIds: string[] } | undefined;
server.use(
  http.post('/api/events/evt-1/duplicate', async ({ request }) => {
    capturedBody = (await request.json()) as { copyInfoSectionIds: string[] };
    return HttpResponse.json({ ... }, { status: 201 });
  })
);
// ... render and interact ...
await waitFor(() => {
  expect(capturedBody).toBeDefined();
  expect(Array.isArray(capturedBody!.copyInfoSectionIds)).toBe(true);
});
```

**API/utility unit tests (no DOM):**
```typescript
// web/src/__tests__/api.test.ts
const mockFetch = vi.fn();
beforeEach(() => { localStorage.clear(); mockFetch.mockReset(); vi.stubGlobal('fetch', mockFetch); });
afterEach(() => { vi.restoreAllMocks(); });

mockFetch.mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ data: 'ok' }) });
await api.get('/test');
const [, options] = mockFetch.mock.calls[0];
expect((options as RequestInit).headers).toMatchObject({ Authorization: 'Bearer test-token-123' });
```

### Coverage

No coverage thresholds configured. Coverage reporting not enforced in CI.

---

## Backend Tests (milsim-platform/)

### Framework

- **Runner:** xUnit v2.9.3 — `milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj`
- **Assertions:** FluentAssertions v7
- **HTTP testing:** `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`)
- **Database:** Testcontainers.PostgreSql v4 — real PostgreSQL 16 in Docker
- **Mocking:** Moq v4.20
- **Coverage collector:** coverlet.collector (collection only, no threshold)

### Run Commands

```bash
# From the repo root
dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj

# In CI (.github/workflows/deploy.yml)
dotnet test milsim-platform/src/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj
```

**Requirement:** Docker must be running — Testcontainers starts a `postgres:16-alpine` container per `PostgreSqlFixture` instance.

### Test File Organization

All backend tests live in `milsim-platform/src/MilsimPlanning.Api.Tests/`, organized by feature area:

```
MilsimPlanning.Api.Tests/
├── Fixtures/
│   ├── PostgreSqlFixture.cs          # Shared Testcontainers DB lifecycle
│   └── IntegrationTestAuthHandler.cs # Custom auth handler for test identity
├── Auth/
│   └── AuthTests.cs
├── Authorization/
│   └── AuthorizationTests.cs
├── Events/
│   └── EventTests.cs                 # Multiple test classes in one file
├── Content/
│   └── InfoSectionTests.cs
├── Hierarchy/
│   └── HierarchyTests.cs
├── Maps/
│   └── MapResourceTests.cs
├── Migrations/
│   └── Phase2StatusMigrationTests.cs
├── Notifications/
│   └── NotificationTests.cs
├── Player/
│   └── PlayerTests.cs
├── Roster/
│   └── RosterImportTests.cs
└── RosterChangeRequests/
    └── RosterChangeRequestTests.cs
```

### Infrastructure Fixtures

**`PostgreSqlFixture`** (`Fixtures/PostgreSqlFixture.cs`) — shared via `IClassFixture<PostgreSqlFixture>`:
```csharp
public class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    public string ConnectionString => _container.GetConnectionString();
    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

One container is shared across all test classes that use `IClassFixture<PostgreSqlFixture>`.

**`IntegrationTestAuthHandler`** (`Fixtures/IntegrationTestAuthHandler.cs`) — bypasses real JWT auth:
```csharp
// Apply to HttpClient before test calls
IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, "faction_commander");
// Sets X-Test-UserId and X-Test-Role headers; handler converts to ClaimsPrincipal
```

### Backend Test Pattern

All feature test files use the same base pattern:

1. **Test class** implements `IClassFixture<PostgreSqlFixture>` and `IAsyncLifetime`
2. **`InitializeAsync`**: creates `WebApplicationFactory<Program>`, replaces DB connection string and `IEmailService` with mock, applies migrations, seeds roles, creates test users with appropriate roles
3. **`DisposeAsync`**: disposes all `HttpClient` instances and the factory
4. **Test methods**: `[Fact]` methods, named with `MethodUnderTest_Scenario_ExpectedResult` pattern

```csharp
// EventTests.cs — base class pattern
public class EventTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected HttpClient _commanderClient = null!;   // faction_commander
    protected HttpClient _playerClient = null!;      // player role (tests 403)
    protected HttpClient _commanderBClient = null!;  // second commander (tests scope isolation)

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "dev-placeholder-secret-32-chars!!",
                        ...
                    }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(_fixture.ConnectionString));
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
                    services.AddAuthentication(...).AddScheme<..., IntegrationTestAuthHandler>(...);
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        // seed roles and test users...
    }
}
```

Feature test classes extend the base:
```csharp
[Trait("Category", "EVNT_Create")]
public class EventCreateTests : EventTestsBase
{
    public EventCreateTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateEvent_ValidRequest_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Op Thunder", location = "Forest Base", startDate = "2026-06-01" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<EventDto>();
        dto!.Name.Should().Be("Op Thunder");
    }
}
```

### Naming Conventions (Backend Tests)

- Test method name: `{MethodOrScenario}_{Context}_{ExpectedOutcome}` — `CreateEvent_ValidRequest_Returns201`, `CreateEvent_PlayerRole_Returns403`
- `[Trait("Category", "...")]` on classes to tag by requirement area — `EVNT_Create`, `EVNT_Publish`, `EVNT_Duplicate`
- Helper methods: `CreateUserAsync(email, role)`, `CreateAuthenticatedClient(user, role)`, `SeedEventWithStructureAsync()`

### Mocking Pattern (Backend)

```csharp
// Mock IEmailService
_emailMock = new Mock<IEmailService>();
_emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
          .Returns(Task.CompletedTask);

// Register mock (also register typed mock for Verify() access in tests)
services.AddSingleton(_emailMock.Object);
services.AddSingleton(_emailMock);

// Verify it was never called
_emailMock.Verify(e => e.SendAsync(
    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
    Times.Never);
```

### FluentAssertions Style

```csharp
response.StatusCode.Should().Be(HttpStatusCode.Created);
dto.Should().NotBeNull();
dto!.Id.Should().NotBeEmpty();
events.Should().Contain(e => e.Name == "Commander A Event");
events.Should().NotContain(e => e.Name == "Commander B Event",
    because: "list must be scoped to the current commander only (EVNT-03)");
newFaction.Platoons.Should().HaveCount(1, because: "platoon structure must be copied");
```

Always use the `because:` parameter on assertions when the reason is domain-specific or non-obvious.

---

## CI Pipeline

From `.github/workflows/deploy.yml` — `test-backend` and `test-frontend` jobs run in parallel, and both must pass before `deploy-api` or `deploy-frontend` jobs are triggered:

```
push to master
  ├── test-backend  (dotnet test)
  ├── test-frontend (pnpm test --run)
  └── [both pass]
        ├── deploy-api
        └── deploy-frontend
```

**Frontend CI:** Node 22, pnpm (latest), frozen lockfile install, single-run vitest.
**Backend CI:** .NET 10, requires Docker available on runner for Testcontainers (ubuntu-latest has Docker).

---

## What Is Not Tested

- No E2E tests (no Playwright, Cypress, or Selenium setup detected)
- No frontend coverage thresholds enforced
- No backend coverage thresholds enforced
- `web/src/components/ui/` shadcn primitives are not tested directly (treated as trusted library wrappers)
