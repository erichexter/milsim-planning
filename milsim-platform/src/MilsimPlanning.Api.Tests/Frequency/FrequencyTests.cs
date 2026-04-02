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

namespace MilsimPlanning.Api.Tests.Frequency;

/// <summary>
/// Integration tests for Frequency API (HEX-177).
/// Tests RBAC-scoped GET and ownership-checked PUT endpoints.
/// </summary>
public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // Clients
    protected HttpClient _commanderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _otherCommanderClient = null!;

    // IDs
    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoon1Id;
    protected Guid _platoon2Id;
    protected Guid _squad1Id;
    protected Guid _squad2Id;

    public FrequencyTestsBase(PostgreSqlFixture fixture)
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

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        // Create commander (faction owner)
        var commanderEmail = $"freq-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");

        // Create squad leader
        var slEmail = $"freq-sl-{Guid.NewGuid():N}@test.com";
        var squadLeader = new AppUser { UserName = slEmail, Email = slEmail, EmailConfirmed = true };
        await userManager.CreateAsync(squadLeader, "TestPass123!");
        await userManager.AddToRoleAsync(squadLeader, "squad_leader");

        // Create platoon leader
        var plEmail = $"freq-pl-{Guid.NewGuid():N}@test.com";
        var platoonLeader = new AppUser { UserName = plEmail, Email = plEmail, EmailConfirmed = true };
        await userManager.CreateAsync(platoonLeader, "TestPass123!");
        await userManager.AddToRoleAsync(platoonLeader, "platoon_leader");

        // Create player
        var playerEmail = $"freq-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");

        // Create other commander (for IDOR test — NOT a member of event1)
        var otherCmdrEmail = $"freq-other-cmdr-{Guid.NewGuid():N}@test.com";
        var otherCommander = new AppUser { UserName = otherCmdrEmail, Email = otherCmdrEmail, EmailConfirmed = true };
        await userManager.CreateAsync(otherCommander, "TestPass123!");
        await userManager.AddToRoleAsync(otherCommander, "faction_commander");

        // Seed event + hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoon1Id = Guid.NewGuid();
        _platoon2Id = Guid.NewGuid();
        _squad1Id = Guid.NewGuid();
        _squad2Id = Guid.NewGuid();

        var faction = new Faction { Id = _factionId, Name = "Alpha Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Freq Test Event", Status = EventStatus.Draft, FactionId = _factionId, Faction = faction };
        var platoon1 = new Platoon { Id = _platoon1Id, FactionId = _factionId, Name = "Platoon 1", Order = 1 };
        var platoon2 = new Platoon { Id = _platoon2Id, FactionId = _factionId, Name = "Platoon 2", Order = 2 };
        var squad1 = new Squad { Id = _squad1Id, PlatoonId = _platoon1Id, Name = "Squad 1", Order = 1 };
        var squad2 = new Squad { Id = _squad2Id, PlatoonId = _platoon2Id, Name = "Squad 2", Order = 1 };

        db.Events.Add(testEvent);
        db.Platoons.Add(platoon1);
        db.Platoons.Add(platoon2);
        db.Squads.Add(squad1);
        db.Squads.Add(squad2);

        // EventMemberships (all except otherCommander)
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        // EventPlayers linked to users (so service can look up by UserId)
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, UserId = squadLeader.Id,
            Email = slEmail, Name = "Squad Leader",
            SquadId = _squad1Id, PlatoonId = _platoon1Id
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, UserId = platoonLeader.Id,
            Email = plEmail, Name = "Platoon Leader",
            PlatoonId = _platoon1Id
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, UserId = player.Id,
            Email = playerEmail, Name = "Player",
            SquadId = _squad1Id, PlatoonId = _platoon1Id
        });

        await db.SaveChangesAsync();

        // Set frequencies on squad1, platoon1, faction for GET assertions
        var sq1 = await db.Squads.FindAsync(_squad1Id);
        sq1!.PrimaryFrequency = "46.000";
        sq1.BackupFrequency = "47.000";
        var pl1 = await db.Platoons.FindAsync(_platoon1Id);
        pl1!.PrimaryFrequency = "50.000";
        pl1.BackupFrequency = "51.000";
        var f = await db.Factions.FindAsync(_factionId);
        f!.CommandPrimaryFrequency = "55.000";
        f.CommandBackupFrequency = "56.000";
        await db.SaveChangesAsync();

        // Create clients
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _squadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_squadLeaderClient, squadLeader.Id, "squad_leader");
        _platoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_platoonLeaderClient, platoonLeader.Id, "platoon_leader");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
        _otherCommanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_otherCommanderClient, otherCommander.Id, "faction_commander");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _squadLeaderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _playerClient.Dispose();
        _otherCommanderClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
}

// ── GET /api/events/{eventId}/frequencies — role-scoped view ─────────────────

[Trait("Category", "Frequency_EventView")]
public class FrequencyEventViewTests : FrequencyTestsBase
{
    public FrequencyEventViewTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEventFrequencies_AsPlayer_ReturnsOnlySquad()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoons").ValueKind.Should().Be(JsonValueKind.Null);
        var squads = body.GetProperty("squads");
        squads.ValueKind.Should().NotBe(JsonValueKind.Null);
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("id").GetString().Should().Be(_squad1Id.ToString());
    }

    [Fact]
    public async Task GetEventFrequencies_AsSquadLeader_ReturnsSquadAndPlatoon()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
        var platoons = body.GetProperty("platoons");
        platoons.GetArrayLength().Should().Be(1);
        platoons[0].GetProperty("id").GetString().Should().Be(_platoon1Id.ToString());
        var squads = body.GetProperty("squads");
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("id").GetString().Should().Be(_squad1Id.ToString());
    }

    [Fact]
    public async Task GetEventFrequencies_AsPlatoonLeader_ReturnsPlatoonAndCommand()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squads").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("command").ValueKind.Should().NotBe(JsonValueKind.Null);
        body.GetProperty("command").GetProperty("id").GetString().Should().Be(_factionId.ToString());
        var platoons = body.GetProperty("platoons");
        platoons.GetArrayLength().Should().Be(1);
        platoons[0].GetProperty("id").GetString().Should().Be(_platoon1Id.ToString());
    }

    [Fact]
    public async Task GetEventFrequencies_AsFactionCommander_ReturnsAllThreeLevels()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("command").ValueKind.Should().NotBe(JsonValueKind.Null);
        body.GetProperty("command").GetProperty("id").GetString().Should().Be(_factionId.ToString());
        body.GetProperty("platoons").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("squads").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }
}

// ── PUT /api/squads/{squadId}/frequencies — ownership checks ─────────────────

[Trait("Category", "Frequency_SquadPut")]
public class FrequencySquadPutTests : FrequencyTestsBase
{
    public FrequencySquadPutTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "40.000", backup = "41.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsSquadLeaderOwnSquad_Returns204()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "42.000", backup = "43.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsSquadLeaderOtherSquad_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "42.000", backup = "43.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeaderSquadInMyPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "48.000", backup = "49.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeaderSquadNotInMyPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "48.000", backup = "49.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── PUT /api/platoons/{platoonId}/frequencies — ownership checks ──────────────

[Trait("Category", "Frequency_PlatoonPut")]
public class FrequencyPlatoonPutTests : FrequencyTestsBase
{
    public FrequencyPlatoonPutTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon1Id}/frequencies",
            new { primary = "40.000", backup = "41.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsPlatoonLeaderOwnPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon1Id}/frequencies",
            new { primary = "52.000", backup = "53.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

// ── PUT /api/factions/{factionId}/command-frequencies — IDOR guard ───────────

[Trait("Category", "Frequency_FactionPut")]
public class FrequencyFactionPutTests : FrequencyTestsBase
{
    public FrequencyFactionPutTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateFactionCommandFrequencies_AsPlatoonLeader_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/command-frequencies",
            new { primary = "55.000", backup = "56.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFactionCommandFrequencies_AsOwnFactionCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/command-frequencies",
            new { primary = "57.000", backup = "58.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateFactionCommandFrequencies_AsOtherFactionCommander_Returns403_IDOR()
    {
        // otherCommander is NOT a member of this event → ScopeGuard blocks → 403
        var response = await _otherCommanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/command-frequencies",
            new { primary = "99.000", backup = "99.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFactionCommandFrequencies_AsOwnCommander_PersistsValues()
    {
        var putResponse = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/command-frequencies",
            new { primary = "60.000", backup = "61.000" });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _commanderClient.GetAsync($"/api/factions/{_factionId}/command-frequencies");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("60.000");
        body.GetProperty("backup").GetString().Should().Be("61.000");
    }
}

// ── GET /api/squads/{squadId}/frequencies — ownership checks ──────────────────

[Trait("Category", "Frequency_SquadGet")]
public class FrequencySquadGetTests : FrequencyTestsBase
{
    public FrequencySquadGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetSquadFrequencies_AsSquadLeaderOwnSquad_Returns200()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/squads/{_squad1Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(_squad1Id.ToString());
    }

    [Fact]
    public async Task GetSquadFrequencies_AsSquadLeaderOtherSquad_Returns403()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/squads/{_squad2Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSquadFrequencies_AsPlatoonLeaderSquadInMyPlatoon_Returns200()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/squads/{_squad1Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(_squad1Id.ToString());
    }

    [Fact]
    public async Task GetSquadFrequencies_AsPlatoonLeaderSquadNotInMyPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/squads/{_squad2Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSquadFrequencies_AsFactionCommander_Returns200()
    {
        var response = await _commanderClient.GetAsync($"/api/squads/{_squad1Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// ── GET /api/platoons/{platoonId}/frequencies — ownership checks ───────────────

[Trait("Category", "Frequency_PlatoonGet")]
public class FrequencyPlatoonGetTests : FrequencyTestsBase
{
    public FrequencyPlatoonGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetPlatoonFrequencies_AsPlatoonLeaderOwnPlatoon_Returns200()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/platoons/{_platoon1Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(_platoon1Id.ToString());
    }

    [Fact]
    public async Task GetPlatoonFrequencies_AsPlatoonLeaderOtherPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/platoons/{_platoon2Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatoonFrequencies_AsFactionCommander_Returns200()
    {
        var response = await _commanderClient.GetAsync($"/api/platoons/{_platoon1Id}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// ── GET /api/factions/{factionId}/command-frequencies — ownership checks ───────

[Trait("Category", "Frequency_FactionGet")]
public class FrequencyFactionGetTests : FrequencyTestsBase
{
    public FrequencyFactionGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFactionCommandFrequencies_AsOwnFactionCommander_Returns200()
    {
        var response = await _commanderClient.GetAsync($"/api/factions/{_factionId}/command-frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(_factionId.ToString());
    }

    [Fact]
    public async Task GetFactionCommandFrequencies_AsOtherFactionCommander_Returns403_IDOR()
    {
        // otherCommander is NOT a member of this event → ScopeGuard blocks → 403
        var response = await _otherCommanderClient.GetAsync($"/api/factions/{_factionId}/command-frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
