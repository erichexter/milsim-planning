using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using MilsimPlanning.Api.Models.Hierarchy;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Hierarchy;

/// <summary>
/// Integration tests for Hierarchy API (HIER-01..06).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class HierarchyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _outsiderPlayerClient = null!;
    protected Guid _eventId;

    public HierarchyTestsBase(PostgreSqlFixture fixture)
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

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create commander
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var commanderEmail = $"hier-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };

        // Create player (member of the event)
        var playerEmail = $"hier-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };

        // Create outsider player (NOT a member of the event)
        var outsiderEmail = $"hier-outsider-{Guid.NewGuid():N}@test.com";
        var outsider = new AppUser { UserName = outsiderEmail, Email = outsiderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(outsider, "TestPass123!");
        await userManager.AddToRoleAsync(outsider, "player");
        outsider.Profile = new UserProfile { UserId = outsider.Id, Callsign = "Outsider", DisplayName = "Outsider", User = outsider };

        // Seed event + faction
        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Hier Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };

        db.Events.Add(testEvent);

        // EventMembership for commander and player (not outsider)
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        await db.SaveChangesAsync();

        // Create authenticated clients
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
        _outsiderPlayerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_outsiderPlayerClient, outsider.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _outsiderPlayerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected async Task<Guid> SeedEventPlayer(Guid eventId, string email = "player@test.com")
    {
        using var db = GetDb();
        var ep = new EventPlayer
        {
            EventId = eventId,
            Email = email.ToLowerInvariant(),
            Name = "Test Player",
            Callsign = "ALPHA"
        };
        db.EventPlayers.Add(ep);
        await db.SaveChangesAsync();
        return ep.Id;
    }

    protected async Task<Guid> SeedSquad(Guid eventId, string squadName)
    {
        // Create a platoon for the event's faction, then a squad within it
        using var db = GetDb();
        var faction = await db.Factions.FirstAsync(f => f.EventId == eventId);
        var platoon = new Platoon
        {
            FactionId = faction.Id,
            Name = $"Platoon-{Guid.NewGuid():N}",
            Order = 1
        };
        db.Platoons.Add(platoon);
        await db.SaveChangesAsync();

        var squad = new Squad { PlatoonId = platoon.Id, Name = squadName, Order = 1 };
        db.Squads.Add(squad);
        await db.SaveChangesAsync();
        return squad.Id;
    }
}

// ── HIER-01: Create Platoon ──────────────────────────────────────────────────

[Trait("Category", "HIER_Platoon")]
public class PlatoonTests : HierarchyTestsBase
{
    public PlatoonTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreatePlatoon_ValidRequest_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/platoons",
            new { name = "Alpha Platoon" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Alpha Platoon");
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreatePlatoon_PlayerRole_Returns403()
    {
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/platoons",
            new { name = "Bravo Platoon" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── HIER-02: Create Squad ────────────────────────────────────────────────────

[Trait("Category", "HIER_Squad")]
public class SquadTests : HierarchyTestsBase
{
    public SquadTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateSquad_WithinPlatoon_Returns201()
    {
        // First create a platoon
        var platoonRes = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/platoons",
            new { name = "Alpha Platoon" });
        platoonRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var platoonId = (await platoonRes.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString();

        // Then create a squad within it
        var squadRes = await _commanderClient.PostAsJsonAsync(
            $"/api/platoons/{platoonId}/squads",
            new { name = "Alpha-1" });

        squadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await squadRes.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Alpha-1");
    }
}

// ── HIER-03 / HIER-04 / HIER-05: Player Assignment ──────────────────────────

[Trait("Category", "HIER_Assign")]
public class PlayerAssignmentTests : HierarchyTestsBase
{
    public PlayerAssignmentTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AssignPlayerToSquad_UpdatesSquadId()
    {
        var playerId = await SeedEventPlayer(_eventId, $"assign-{Guid.NewGuid():N}@test.com");
        var squadId = await SeedSquad(_eventId, "Alpha-1");

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{playerId}/squad",
            new { squadId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var player = await db.EventPlayers.FindAsync(playerId);
        player!.SquadId.Should().Be(squadId);
    }

    [Fact]
    public async Task MovePlayerToSquad_ReplacesExistingAssignment()
    {
        var playerId = await SeedEventPlayer(_eventId, $"move-{Guid.NewGuid():N}@test.com");
        var squad1Id = await SeedSquad(_eventId, "Squad-1");
        var squad2Id = await SeedSquad(_eventId, "Squad-2");

        // Assign to squad 1
        await _commanderClient.PutAsJsonAsync($"/api/event-players/{playerId}/squad", new { squadId = squad1Id });
        // Move to squad 2
        await _commanderClient.PutAsJsonAsync($"/api/event-players/{playerId}/squad", new { squadId = squad2Id });

        using var db = GetDb();
        var player = await db.EventPlayers.FindAsync(playerId);
        player!.SquadId.Should().Be(squad2Id); // replaced, not appended
    }
}

// ── HIER-06: Roster View ─────────────────────────────────────────────────────

[Trait("Category", "HIER_Roster")]
public class RosterViewTests : HierarchyTestsBase
{
    public RosterViewTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetRoster_PlayerInEvent_Returns200()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/roster");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var roster = await response.Content.ReadFromJsonAsync<RosterHierarchyDto>();
        roster.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRoster_PlayerNotInEvent_Returns403()
    {
        var response = await _outsiderPlayerClient.GetAsync($"/api/events/{_eventId}/roster");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoster_ReturnsHierarchyTree()
    {
        // Create platoon + squad via API
        var platoonRes = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/platoons", new { name = "Alpha" });
        var platoonId = (await platoonRes.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!;

        var squadRes = await _commanderClient.PostAsJsonAsync(
            $"/api/platoons/{platoonId}/squads", new { name = "A-1" });
        var squadId = Guid.Parse((await squadRes.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        // Seed one assigned player and one unassigned player
        var assignedPlayerId = await SeedEventPlayer(_eventId, $"assigned-{Guid.NewGuid():N}@test.com");
        await SeedEventPlayer(_eventId, $"unassigned-{Guid.NewGuid():N}@test.com");

        // Assign the first player to the squad
        await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{assignedPlayerId}/squad",
            new { squadId });

        // Get roster
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/roster");
        var roster = await response.Content.ReadFromJsonAsync<RosterHierarchyDto>();

        roster!.Platoons.Should().ContainSingle(p => p.Name == "Alpha");
        var platoon = roster.Platoons.First(p => p.Name == "Alpha");
        platoon.Squads.Should().ContainSingle(s => s.Name == "A-1");
        platoon.Squads[0].Players.Should().HaveCount(1);
        roster.UnassignedPlayers.Should().HaveCount(1);
    }
}
