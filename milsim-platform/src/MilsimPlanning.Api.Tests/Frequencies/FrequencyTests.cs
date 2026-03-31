using System.Net;
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
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Frequencies;

public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // Clients
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _outsiderClient = null!;
    protected HttpClient _unauthenticatedClient = null!;

    // IDs
    protected Guid _eventId;
    protected Guid _otherEventId;
    protected Guid _squadId;
    protected Guid _platoonId;
    protected Guid _factionId;

    // User IDs
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;
    protected string _squadLeaderUserId = string.Empty;
    protected string _platoonLeaderUserId = string.Empty;
    protected string _outsiderUserId = string.Empty;

    public FrequencyTestsBase(PostgreSqlFixture fixture) => _fixture = fixture;

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
                        ["Jwt:Secret"]   = "dev-placeholder-secret-32-chars!!",
                        ["Jwt:Issuer"]   = "milsim-tests",
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
                        options.DefaultChallengeScheme    = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Commander
        var commanderEmail = $"freq-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Player
        var playerEmail = $"freq-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Squad leader
        var slEmail = $"freq-sl-{Guid.NewGuid():N}@test.com";
        var squadLeader = new AppUser { UserName = slEmail, Email = slEmail, EmailConfirmed = true };
        await userManager.CreateAsync(squadLeader, "TestPass123!");
        await userManager.AddToRoleAsync(squadLeader, "squad_leader");
        squadLeader.Profile = new UserProfile { UserId = squadLeader.Id, Callsign = "SL1", DisplayName = "SL1", User = squadLeader };
        _squadLeaderUserId = squadLeader.Id;

        // Platoon leader
        var plEmail = $"freq-pl-{Guid.NewGuid():N}@test.com";
        var platoonLeader = new AppUser { UserName = plEmail, Email = plEmail, EmailConfirmed = true };
        await userManager.CreateAsync(platoonLeader, "TestPass123!");
        await userManager.AddToRoleAsync(platoonLeader, "platoon_leader");
        platoonLeader.Profile = new UserProfile { UserId = platoonLeader.Id, Callsign = "PL1", DisplayName = "PL1", User = platoonLeader };
        _platoonLeaderUserId = platoonLeader.Id;

        // Outsider (not a member of the event)
        var outsiderEmail = $"freq-outsider-{Guid.NewGuid():N}@test.com";
        var outsider = new AppUser { UserName = outsiderEmail, Email = outsiderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(outsider, "TestPass123!");
        await userManager.AddToRoleAsync(outsider, "player");
        outsider.Profile = new UserProfile { UserId = outsider.Id, Callsign = "Outsider", DisplayName = "Outsider", User = outsider };
        _outsiderUserId = outsider.Id;

        // Seed primary event + faction hierarchy
        _eventId   = Guid.NewGuid();
        _factionId = Guid.NewGuid();

        var faction = new Faction
        {
            Id          = _factionId,
            Name        = "Test Faction",
            CommanderId = commander.Id,
            EventId     = _eventId
        };
        var testEvent = new Event
        {
            Id       = _eventId,
            Name     = "Freq Test Event",
            Status   = EventStatus.Draft,
            FactionId = _factionId,
            Faction  = faction
        };
        db.Events.Add(testEvent);

        // Platoon + Squad
        var platoon = new Platoon { FactionId = _factionId, Name = "Alpha Platoon", Order = 1 };
        db.Platoons.Add(platoon);
        await db.SaveChangesAsync();
        _platoonId = platoon.Id;

        var squad = new Squad { PlatoonId = platoon.Id, Name = "Alpha-1", Order = 1 };
        db.Squads.Add(squad);
        await db.SaveChangesAsync();
        _squadId = squad.Id;

        // EventMemberships
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id,     EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id,        EventId = _eventId, Role = "player" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id,   EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });

        // EventPlayers (linked to users, assigned to positions)
        var playerEp = new EventPlayer
        {
            EventId  = _eventId,
            Email    = playerEmail.ToLowerInvariant(),
            Name     = "Test Player",
            UserId   = player.Id,
            SquadId  = squad.Id,
            PlatoonId = platoon.Id
        };
        var slEp = new EventPlayer
        {
            EventId   = _eventId,
            Email     = slEmail.ToLowerInvariant(),
            Name      = "Squad Leader",
            UserId    = squadLeader.Id,
            SquadId   = squad.Id,
            PlatoonId = platoon.Id
        };
        var plEp = new EventPlayer
        {
            EventId   = _eventId,
            Email     = plEmail.ToLowerInvariant(),
            Name      = "Platoon Leader",
            UserId    = platoonLeader.Id,
            PlatoonId = platoon.Id
        };
        db.EventPlayers.AddRange(playerEp, slEp, plEp);

        // Second event (for IDOR tests)
        _otherEventId = Guid.NewGuid();
        var otherFactionId = Guid.NewGuid();
        var otherFaction = new Faction { Id = otherFactionId, Name = "Other Faction", CommanderId = commander.Id, EventId = _otherEventId };
        var otherEvent = new Event { Id = _otherEventId, Name = "Other Event", Status = EventStatus.Draft, FactionId = otherFactionId, Faction = otherFaction };
        db.Events.Add(otherEvent);
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _otherEventId, Role = "faction_commander" });

        await db.SaveChangesAsync();

        // Create clients
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");

        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");

        _squadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_squadLeaderClient, squadLeader.Id, "squad_leader");

        _platoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_platoonLeaderClient, platoonLeader.Id, "platoon_leader");

        _outsiderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_outsiderClient, outsider.Id, "player");

        _unauthenticatedClient = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _squadLeaderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _outsiderClient.Dispose();
        _unauthenticatedClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

// ── GET /api/events/{eventId}/frequencies ─────────────────────────────────────

[Trait("Category", "Frequency_Get")]
public class FrequencyGetTests : FrequencyTestsBase
{
    public FrequencyGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsOnlySquadFrequency()
    {
        // Seed a squad frequency so we can assert it comes back
        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.SquadPrimaryFrequency = "34.500";
        squad.SquadBackupFrequency   = "35.000";
        await db.SaveChangesAsync();

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").GetProperty("primary").GetString().Should().Be("34.500");
        body.GetProperty("squad").GetProperty("backup").GetString().Should().Be("35.000");
        body.GetProperty("platoon").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsSquadAndPlatoonFrequencies()
    {
        using var db = GetDb();
        var squad   = await db.Squads.FindAsync(_squadId);
        squad!.SquadPrimaryFrequency = "36.000";
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PlatoonPrimaryFrequency = "40.000";
        await db.SaveChangesAsync();

        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").GetProperty("primary").GetString().Should().Be("36.000");
        body.GetProperty("platoon").GetProperty("primary").GetString().Should().Be("40.000");
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsPlatoonAndCommandFrequencies()
    {
        using var db = GetDb();
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PlatoonPrimaryFrequency = "41.000";
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency = "50.000";
        await db.SaveChangesAsync();

        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoon").GetProperty("primary").GetString().Should().Be("41.000");
        body.GetProperty("command").GetProperty("primary").GetString().Should().Be("50.000");
    }

    [Fact]
    public async Task GetFrequencies_AsFactionCommander_ReturnsCommandFrequencyOnly()
    {
        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency = "51.000";
        faction.CommandBackupFrequency   = "52.000";
        await db.SaveChangesAsync();

        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoon").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("command").GetProperty("primary").GetString().Should().Be("51.000");
        body.GetProperty("command").GetProperty("backup").GetString().Should().Be("52.000");
    }

    [Fact]
    public async Task GetFrequencies_WhenNotEventMember_Returns403()
    {
        var response = await _outsiderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencies_WhenPlayerNotYetAssigned_ReturnsNullSquadFrequency()
    {
        // Create a player that has no squad assigned
        using var db = GetDb();
        var unassignedEmail = $"unassigned-{Guid.NewGuid():N}@test.com";
        var unassignedEp = new EventPlayer
        {
            EventId = _eventId,
            Email   = unassignedEmail,
            Name    = "Unassigned Player",
            UserId  = _playerUserId   // reuse player userId but with no squad/platoon
        };
        // Remove existing player ep first so we don't clash on UserId
        var existingEp = await db.EventPlayers.FirstOrDefaultAsync(ep => ep.UserId == _playerUserId && ep.EventId == _eventId);
        if (existingEp != null)
        {
            existingEp.SquadId   = null;
            existingEp.PlatoonId = null;
            await db.SaveChangesAsync();
        }

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Squad exists in DTO but primary/backup are null since no squad assigned
        body.GetProperty("squad").GetProperty("primary").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoon").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFrequencies_Unauthenticated_Returns401()
    {
        var response = await _unauthenticatedClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// ── PUT squad frequencies ─────────────────────────────────────────────────────

[Trait("Category", "Frequency_UpdateSquad")]
public class UpdateSquadFrequencyTests : FrequencyTestsBase
{
    public UpdateSquadFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequency_AsFactionCommander_Succeeds()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/squads/{_squadId}/frequencies",
            new { primary = "37.500", backup = "38.000" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("37.500");
        body.GetProperty("backup").GetString().Should().Be("38.000");

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.SquadPrimaryFrequency.Should().Be("37.500");
        squad.SquadBackupFrequency.Should().Be("38.000");
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsNonCommander_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/squads/{_squadId}/frequencies",
            new { primary = "37.500" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_WithSquadFromDifferentEvent_Returns403()
    {
        // _squadId belongs to _eventId; try to set it via _otherEventId
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_otherEventId}/squads/{_squadId}/frequencies",
            new { primary = "99.999" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── PUT platoon frequencies ───────────────────────────────────────────────────

[Trait("Category", "Frequency_UpdatePlatoon")]
public class UpdatePlatoonFrequencyTests : FrequencyTestsBase
{
    public UpdatePlatoonFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsFactionCommander_Succeeds()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/platoons/{_platoonId}/frequencies",
            new { primary = "42.000", backup = "43.000" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("42.000");
        body.GetProperty("backup").GetString().Should().Be("43.000");

        using var db = GetDb();
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PlatoonPrimaryFrequency.Should().Be("42.000");
        platoon.PlatoonBackupFrequency.Should().Be("43.000");
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsNonCommander_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/platoons/{_platoonId}/frequencies",
            new { primary = "42.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── PUT command frequencies ───────────────────────────────────────────────────

[Trait("Category", "Frequency_UpdateCommand")]
public class UpdateCommandFrequencyTests : FrequencyTestsBase
{
    public UpdateCommandFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateCommandFrequency_AsFactionCommander_Succeeds()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "53.000", backup = "54.000" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("53.000");
        body.GetProperty("backup").GetString().Should().Be("54.000");

        using var db = GetDb();
        var faction = await db.Factions.FirstAsync(f => f.EventId == _eventId);
        faction.CommandPrimaryFrequency.Should().Be("53.000");
        faction.CommandBackupFrequency.Should().Be("54.000");
    }
}
