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
    protected string _commanderUserId = string.Empty;

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
        _commanderUserId = commander.Id;

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

// ── AssignToPlatoon ──────────────────────────────────────────────────────────

[Trait("Category", "HIER_AssignPlatoon")]
public class AssignPlatoonTests : HierarchyTestsBase
{
    public AssignPlatoonTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AssignPlayerToPlatoon_SetsPlatoonId_ClearsSquadId()
    {
        // Seed a player and put them in a squad first
        var playerId = await SeedEventPlayer(_eventId, $"pltn-{Guid.NewGuid():N}@test.com");
        var squadId = await SeedSquad(_eventId, "Alpha-1");

        await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{playerId}/squad",
            new { squadId });

        // Get platoon that owns the squad
        using var db = GetDb();
        var player = await db.EventPlayers.FindAsync(playerId);
        var platoonId = player!.PlatoonId!.Value;

        // Now assign directly to the platoon (HQ slot) — squad should clear
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{playerId}/platoon",
            new { platoonId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "assigning to a valid platoon slot should succeed");

        using var db2 = GetDb();
        var updated = await db2.EventPlayers.FindAsync(playerId);
        updated!.PlatoonId.Should().Be(platoonId,
            because: "player should be in the platoon HQ slot");
        updated.SquadId.Should().BeNull(
            because: "platoon-level assignment clears squad assignment");
    }

    [Fact]
    public async Task AssignPlayerToPlatoon_WrongEvent_Returns403()
    {
        // Seed a player in our event
        var playerId = await SeedEventPlayer(_eventId, $"pltn-idor-{Guid.NewGuid():N}@test.com");

        // Create a platoon in a different event (no faction link for simplicity — use raw DB seed)
        Guid foreignPlatoonId;
        {
            using var db = GetDb();
            var foreignEventId = Guid.NewGuid();
            var foreignFactionId = Guid.NewGuid();
            // Reuse commander as the foreign faction's commander to satisfy FK on AspNetUsers
            var foreignFaction = new Faction { Id = foreignFactionId, Name = "Foreign Faction", CommanderId = _commanderUserId, EventId = foreignEventId };
            var foreignEvent = new Event { Id = foreignEventId, Name = "Foreign Event", Status = EventStatus.Draft, FactionId = foreignFactionId, Faction = foreignFaction };
            db.Events.Add(foreignEvent);
            var foreignPlatoon = new Platoon { FactionId = foreignFactionId, Name = "Foreign Platoon", Order = 1 };
            db.Platoons.Add(foreignPlatoon);
            await db.SaveChangesAsync();
            foreignPlatoonId = foreignPlatoon.Id;
        }

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{playerId}/platoon",
            new { platoonId = foreignPlatoonId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "assigning to a platoon from a different event violates IDOR protection");
    }
}

// ── SetRole ──────────────────────────────────────────────────────────────────

[Trait("Category", "HIER_SetRole")]
public class SetRoleTests : HierarchyTestsBase
{
    public SetRoleTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SetRole_ValidLabel_PersistsRole()
    {
        var playerId = await SeedEventPlayer(_eventId, $"role-{Guid.NewGuid():N}@test.com");

        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/event-players/{playerId}/role",
            new { role = "Platoon Sergeant" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var player = await db.EventPlayers.FindAsync(playerId);
        player!.Role.Should().Be("Platoon Sergeant");
    }

    [Fact]
    public async Task SetRole_NullValue_ClearsRole()
    {
        // Seed player with a role already set
        var playerId = await SeedEventPlayer(_eventId, $"role-clear-{Guid.NewGuid():N}@test.com");
        await _commanderClient.PatchAsJsonAsync(
            $"/api/event-players/{playerId}/role",
            new { role = "Squad Leader" });

        // Now clear it
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/event-players/{playerId}/role",
            new { role = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var player = await db.EventPlayers.FindAsync(playerId);
        player!.Role.Should().BeNull(because: "null role should clear the field");
    }
}

// ── BulkAssign ───────────────────────────────────────────────────────────────

[Trait("Category", "HIER_BulkAssign")]
public class BulkAssignTests : HierarchyTestsBase
{
    public BulkAssignTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task BulkAssign_ToSquad_AssignsAllPlayers()
    {
        var p1 = await SeedEventPlayer(_eventId, $"bulk1-{Guid.NewGuid():N}@test.com");
        var p2 = await SeedEventPlayer(_eventId, $"bulk2-{Guid.NewGuid():N}@test.com");
        var p3 = await SeedEventPlayer(_eventId, $"bulk3-{Guid.NewGuid():N}@test.com");
        var squadId = await SeedSquad(_eventId, "Alpha-1");

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/players/bulk-assign",
            new { playerIds = new[] { p1, p2, p3 }, destination = $"squad:{squadId}" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        foreach (var pid in new[] { p1, p2, p3 })
        {
            var player = await db.EventPlayers.FindAsync(pid);
            player!.SquadId.Should().Be(squadId,
                because: $"player {pid} should be assigned to squad {squadId}");
        }
    }

    [Fact]
    public async Task BulkAssign_ToPlatoon_SetsPlatoonIdClearsSquad()
    {
        var p1 = await SeedEventPlayer(_eventId, $"bulkpltn1-{Guid.NewGuid():N}@test.com");
        var p2 = await SeedEventPlayer(_eventId, $"bulkpltn2-{Guid.NewGuid():N}@test.com");

        // Get the event's faction platoon (seeded in HierarchyTestsBase)
        using var db = GetDb();
        var faction = await db.Factions.FirstAsync(f => f.EventId == _eventId);
        var platoon = new Platoon { FactionId = faction.Id, Name = "HQ Platoon", Order = 99 };
        db.Platoons.Add(platoon);
        await db.SaveChangesAsync();
        var platoonId = platoon.Id;

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/players/bulk-assign",
            new { playerIds = new[] { p1, p2 }, destination = $"platoon:{platoonId}" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db2 = GetDb();
        foreach (var pid in new[] { p1, p2 })
        {
            var player = await db2.EventPlayers.FindAsync(pid);
            player!.PlatoonId.Should().Be(platoonId);
            player.SquadId.Should().BeNull(
                because: "platoon assignment clears squad");
        }
    }

    [Fact]
    public async Task BulkAssign_SquadFromAnotherEvent_Returns403()
    {
        var playerId = await SeedEventPlayer(_eventId, $"bulk-idor-{Guid.NewGuid():N}@test.com");

        // Create a squad in a foreign event
        Guid foreignSquadId;
        {
            using var db = GetDb();
            var foreignEventId = Guid.NewGuid();
            var foreignFactionId = Guid.NewGuid();
            // Reuse commander as the foreign faction's commander to satisfy FK on AspNetUsers
            var foreignFaction = new Faction { Id = foreignFactionId, Name = "Foreign Faction", CommanderId = _commanderUserId, EventId = foreignEventId };
            var foreignEvent = new Event { Id = foreignEventId, Name = "Foreign Event", Status = EventStatus.Draft, FactionId = foreignFactionId, Faction = foreignFaction };
            db.Events.Add(foreignEvent);
            var foreignPlatoon = new Platoon { FactionId = foreignFactionId, Name = "Foreign Platoon", Order = 1 };
            db.Platoons.Add(foreignPlatoon);
            await db.SaveChangesAsync();
            var foreignSquad = new Squad { PlatoonId = foreignPlatoon.Id, Name = "Foreign Squad", Order = 1 };
            db.Squads.Add(foreignSquad);
            await db.SaveChangesAsync();
            foreignSquadId = foreignSquad.Id;
        }

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/players/bulk-assign",
            new { playerIds = new[] { playerId }, destination = $"squad:{foreignSquadId}" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "squad belongs to a different event (IDOR protection)");
    }

    [Fact]
    public async Task BulkAssign_InvalidDestinationFormat_Returns400()
    {
        var playerId = await SeedEventPlayer(_eventId, $"bulk-bad-{Guid.NewGuid():N}@test.com");

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/players/bulk-assign",
            new { playerIds = new[] { playerId }, destination = "invalid:abc123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "destination format must be 'squad:{id}' or 'platoon:{id}'");
    }
}

// ── Callsign Precedence (HierarchyService) ───────────────────────────────────

[Trait("Category", "HIER_CallsignPrecedence")]
public class CallsignPrecedenceTests : HierarchyTestsBase
{
    public CallsignPrecedenceTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetRoster_ProfileCallsignOverridesRosterCallsign()
    {
        // Seed an EventPlayer with a roster callsign and link them to an AppUser
        // whose UserProfile has a different callsign — profile callsign should win.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = $"profile-cs-{Guid.NewGuid():N}@test.com";
        var appUser = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(appUser, "TestPass123!");
        await userManager.AddToRoleAsync(appUser, "player");

        // UserProfile callsign = "GHOSTRIDER" (the one that should win)
        appUser.Profile = new UserProfile
        {
            UserId = appUser.Id,
            Callsign = "GHOSTRIDER",
            DisplayName = "Ghost",
            User = appUser
        };
        await db.SaveChangesAsync();

        // EventPlayer with roster callsign "ALPHA-1" (should be overridden)
        var eventPlayer = new EventPlayer
        {
            EventId = _eventId,
            Email = email,
            Name = "Profile User",
            Callsign = "ALPHA-1",     // roster callsign — should NOT appear in roster output
            UserId = appUser.Id       // linked to AppUser
        };
        db.EventPlayers.Add(eventPlayer);
        db.EventMemberships.Add(new EventMembership { EventId = _eventId, UserId = appUser.Id, Role = "player" });
        await db.SaveChangesAsync();

        // GET roster — player callsign should be profile callsign
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/roster");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var roster = await response.Content.ReadFromJsonAsync<RosterHierarchyDto>();
        var player = roster!.UnassignedPlayers.FirstOrDefault(p => p.Name == "Profile User");
        player.Should().NotBeNull(because: "player should appear in the unassigned list");
        player!.Callsign.Should().Be("GHOSTRIDER",
            because: "profile callsign takes precedence over roster CSV callsign");
    }

    [Fact]
    public async Task GetRoster_BlankProfileCallsign_FallsBackToRosterCallsign()
    {
        // Seed an EventPlayer linked to a user whose profile has no callsign set.
        // Roster callsign should be used as the fallback.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = $"blank-cs-{Guid.NewGuid():N}@test.com";
        var appUser = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(appUser, "TestPass123!");
        await userManager.AddToRoleAsync(appUser, "player");

        // UserProfile with blank callsign
        appUser.Profile = new UserProfile
        {
            UserId = appUser.Id,
            Callsign = "",    // blank — should not override roster callsign
            DisplayName = "Blank CS User",
            User = appUser
        };
        await db.SaveChangesAsync();

        var eventPlayer = new EventPlayer
        {
            EventId = _eventId,
            Email = email,
            Name = "Blank CS User",
            Callsign = "BRAVO-2",   // roster callsign — should be used as fallback
            UserId = appUser.Id
        };
        db.EventPlayers.Add(eventPlayer);
        db.EventMemberships.Add(new EventMembership { EventId = _eventId, UserId = appUser.Id, Role = "player" });
        await db.SaveChangesAsync();

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/roster");
        var roster = await response.Content.ReadFromJsonAsync<RosterHierarchyDto>();

        var player = roster!.UnassignedPlayers.FirstOrDefault(p => p.Name == "Blank CS User");
        player.Should().NotBeNull();
        player!.Callsign.Should().Be("BRAVO-2",
            because: "blank profile callsign falls back to the roster CSV callsign");
    }
}
