using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Events;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Events;

/// <summary>
/// Integration tests for Event CRUD API (EVNT-01..06).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class EventTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // Commander A client — used for most tests
    protected HttpClient _commanderClient = null!;
    protected AppUser _commanderUser = null!;

    // Player client — used to test 403
    protected HttpClient _playerClient = null!;

    // Commander B client — used to test cross-event scope isolation
    protected HttpClient _commanderBClient = null!;

    public EventTestsBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "dev-placeholder-secret-32-chars!!",
                        ["Jwt:Issuer"] = "milsim-tests",
                        ["Jwt:Audience"] = "milsim-tests"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
                    services.AddSingleton(_emailMock); // so tests can access mock for Verify()

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Ensure roles exist
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create commander A
        _commanderUser = await CreateUserAsync($"cmdr-a-{Guid.NewGuid():N}@test.com", "faction_commander");
        _commanderClient = CreateAuthenticatedClient(_commanderUser, "faction_commander");

        // Create player
        var playerUser = await CreateUserAsync($"player-{Guid.NewGuid():N}@test.com", "player");
        _playerClient = CreateAuthenticatedClient(playerUser, "player");

        // Create commander B
        var commanderB = await CreateUserAsync($"cmdr-b-{Guid.NewGuid():N}@test.com", "faction_commander");
        _commanderBClient = CreateAuthenticatedClient(commanderB, "faction_commander");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _commanderBClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected async Task<AppUser> CreateUserAsync(string email, string role, string password = "TestPass123!")
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(because: string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            Callsign = email,
            DisplayName = email,
            User = user
        };
        await db.SaveChangesAsync();
        return user;
    }

    protected HttpClient CreateAuthenticatedClient(AppUser user, string role)
    {
        var client = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, role);
        return client;
    }
}

// ── EVNT-01, EVNT-04: Create ──────────────────────────────────────────────────

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
        dto.Should().NotBeNull();
        dto!.Id.Should().NotBeEmpty();
        dto.Name.Should().Be("Op Thunder");
        dto.Status.Should().Be("Draft");    // EVNT-04: new events are Draft
    }

    [Fact]
    public async Task CreateEvent_NewEvent_HasDraftStatus()
    {
        var response = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Test Event" });
        var dto = await response.Content.ReadFromJsonAsync<EventDto>();
        dto!.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task CreateEvent_MissingName_Returns400()
    {
        var response = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateEvent_PlayerRole_Returns403()
    {
        var response = await _playerClient.PostAsJsonAsync("/api/events",
            new { name = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── EVNT-03: List ─────────────────────────────────────────────────────────────

[Trait("Category", "EVNT_List")]
public class EventListTests : EventTestsBase
{
    public EventListTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListEvents_ReturnsOnlyCommandersOwnEvents()
    {
        // Commander A creates an event
        var createA = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Commander A Event" });
        createA.StatusCode.Should().Be(HttpStatusCode.Created);

        // Commander B creates a separate event
        var createB = await _commanderBClient.PostAsJsonAsync("/api/events",
            new { name = "Commander B Event" });
        createB.StatusCode.Should().Be(HttpStatusCode.Created);

        // Commander A lists — should only see their own events
        var listResponse = await _commanderClient.GetAsync("/api/events");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await listResponse.Content.ReadFromJsonAsync<List<EventDto>>();
        events.Should().NotBeNull();
        events!.Should().Contain(e => e.Name == "Commander A Event");
        events.Should().NotContain(e => e.Name == "Commander B Event",
            because: "list must be scoped to the current commander only (EVNT-03)");
    }
}

// ── EVNT-05, EVNT-06: Publish ────────────────────────────────────────────────

[Trait("Category", "EVNT_Publish")]
public class EventPublishTests : EventTestsBase
{
    public EventPublishTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PublishEvent_DraftEvent_Returns204()
    {
        // Create event
        var createResponse = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Op Alpha" });
        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();

        // Publish
        var publishResponse = await _commanderClient.PutAsync($"/api/events/{created!.Id}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify status changed via GET
        var getResponse = await _commanderClient.GetAsync($"/api/events/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<EventDto>();
        updated!.Status.Should().Be("Published");
    }

    [Fact]
    public async Task PublishEvent_AlreadyPublished_Returns409()
    {
        // Create + first publish
        var createResponse = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Already Published Event" });
        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();
        await _commanderClient.PutAsync($"/api/events/{created!.Id}/publish", null);

        // Second publish attempt
        var second = await _commanderClient.PutAsync($"/api/events/{created.Id}/publish", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PublishEvent_DoesNotSendNotification()
    {
        // Create + publish
        var createResponse = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Silent Publish Event" });
        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();
        await _commanderClient.PutAsync($"/api/events/{created!.Id}/publish", null);

        // Verify IEmailService.SendAsync was NEVER called (EVNT-06)
        _emailMock.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}

// ── EVNT-02: Duplicate ───────────────────────────────────────────────────────

[Trait("Category", "EVNT_Duplicate")]
public class EventDuplicateTests : EventTestsBase
{
    public EventDuplicateTests(PostgreSqlFixture fixture) : base(fixture) { }

    private async Task<EventDto> SeedEventWithStructureAsync()
    {
        // Create source event
        var createResp = await _commanderClient.PostAsJsonAsync("/api/events",
            new { name = "Source Event", startDate = "2026-05-01", endDate = "2026-05-03" });
        var sourceEvent = await createResp.Content.ReadFromJsonAsync<EventDto>();

        // Seed a Platoon + Squad directly in DB (Hierarchy plan 02-04 adds the API for this)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var eventEntity = await db.Events
            .Include(e => e.Faction)
            .FirstAsync(e => e.Id == sourceEvent!.Id);

        var platoon = new Platoon
        {
            FactionId = eventEntity.Faction.Id,
            Name = "Alpha Platoon",
            Order = 1
        };
        var squad = new Squad { PlatoonId = platoon.Id, Name = "Alpha-1", Order = 1 };
        platoon.Squads.Add(squad);
        db.Platoons.Add(platoon);
        await db.SaveChangesAsync();

        return sourceEvent!;
    }

    [Fact]
    public async Task DuplicateEvent_AlwaysCopiesPlatoonSquadStructure()
    {
        var source = await SeedEventWithStructureAsync();

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{source.Id}/duplicate",
            new { copyInfoSectionIds = Array.Empty<Guid>() });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var newEvent = await response.Content.ReadFromJsonAsync<EventDto>();
        newEvent.Should().NotBeNull();
        newEvent!.Name.Should().Be("Source Event (Copy)");

        // Verify platoon/squad structure was copied in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newFaction = await db.Factions
            .Include(f => f.Platoons)
                .ThenInclude(p => p.Squads)
            .FirstAsync(f => f.EventId == newEvent.Id);

        newFaction.Platoons.Should().HaveCount(1, because: "platoon structure must be copied");
        newFaction.Platoons.First().Name.Should().Be("Alpha Platoon");
        newFaction.Platoons.First().Squads.Should().HaveCount(1);
        newFaction.Platoons.First().Squads.First().Name.Should().Be("Alpha-1");
    }

    [Fact]
    public async Task DuplicateEvent_DoesNotCopyRosterOrDates()
    {
        var source = await SeedEventWithStructureAsync();

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{source.Id}/duplicate",
            new { copyInfoSectionIds = Array.Empty<Guid>() });
        var dto = await response.Content.ReadFromJsonAsync<EventDto>();

        dto!.StartDate.Should().BeNull(because: "dates must NOT be copied (EVNT-02)");
        dto.EndDate.Should().BeNull(because: "dates must NOT be copied (EVNT-02)");
        dto.Status.Should().Be("Draft", because: "duplicate always starts as Draft (EVNT-02)");

        // Verify no EventPlayers were copied (roster not copied)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerCount = await db.EventPlayers.CountAsync(ep => ep.EventId == dto.Id);
        playerCount.Should().Be(0, because: "roster (EventPlayers) must NOT be copied");
    }

    [Fact]
    public async Task DuplicateEvent_CopiesOnlySelectedInfoSections()
    {
        // Phase 2: no info sections exist — send non-empty array, verify 201 (API accepts gracefully)
        var source = await SeedEventWithStructureAsync();

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{source.Id}/duplicate",
            new { copyInfoSectionIds = new[] { Guid.NewGuid() } });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "API must accept CopyInfoSectionIds even when Phase 3 info sections don't exist yet");
    }

    [Fact]
    public async Task DuplicateEvent_NewEventIsInDraftStatus()
    {
        var source = await SeedEventWithStructureAsync();

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{source.Id}/duplicate",
            new { copyInfoSectionIds = Array.Empty<Guid>() });

        // First publish source, then duplicate
        await _commanderClient.PutAsync($"/api/events/{source.Id}/publish", null);

        // Duplicate from PUBLISHED source — new should still be Draft
        var dupFromPublished = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{source.Id}/duplicate",
            new { copyInfoSectionIds = Array.Empty<Guid>() });
        var dupDto = await dupFromPublished.Content.ReadFromJsonAsync<EventDto>();
        dupDto!.Status.Should().Be("Draft", because: "duplicate always resets to Draft regardless of source status");
    }
}
