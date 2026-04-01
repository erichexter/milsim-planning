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
/// Integration tests for radio frequency management endpoints.
/// </summary>
public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _outsiderClient = null!;
    protected HttpClient _commanderBClient = null!;
    protected Guid _eventId;
    protected Guid _factionId;
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;
    protected string _commanderBUserId = string.Empty;

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

        // Commander A
        var commanderEmail = $"freq-cmdr-a-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "CmdrA", DisplayName = "CmdrA", User = commander };
        _commanderUserId = commander.Id;

        // Player (will get EventPlayer with squad)
        var playerEmail = $"freq-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Outsider (no EventMembership)
        var outsiderEmail = $"freq-outsider-{Guid.NewGuid():N}@test.com";
        var outsider = new AppUser { UserName = outsiderEmail, Email = outsiderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(outsider, "TestPass123!");
        await userManager.AddToRoleAsync(outsider, "player");
        outsider.Profile = new UserProfile { UserId = outsider.Id, Callsign = "Outsider", DisplayName = "Outsider", User = outsider };

        // Commander B (different faction/event — for IDOR testing)
        var commanderBEmail = $"freq-cmdr-b-{Guid.NewGuid():N}@test.com";
        var commanderB = new AppUser { UserName = commanderBEmail, Email = commanderBEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commanderB, "TestPass123!");
        await userManager.AddToRoleAsync(commanderB, "faction_commander");
        commanderB.Profile = new UserProfile { UserId = commanderB.Id, Callsign = "CmdrB", DisplayName = "CmdrB", User = commanderB };
        _commanderBUserId = commanderB.Id;

        // Seed event A
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        var faction = new Faction { Id = _factionId, Name = "Test Faction A", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Freq Test Event", Status = EventStatus.Draft, FactionId = _factionId, Faction = faction };
        db.Events.Add(testEvent);

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        await db.SaveChangesAsync();

        // Seed event B (for Commander B / IDOR)
        var eventBId = Guid.NewGuid();
        var factionBId = Guid.NewGuid();
        var factionB = new Faction { Id = factionBId, Name = "Test Faction B", CommanderId = commanderB.Id, EventId = eventBId };
        var eventB = new Event { Id = eventBId, Name = "Freq Test Event B", Status = EventStatus.Draft, FactionId = factionBId, Faction = factionB };
        db.Events.Add(eventB);
        db.EventMemberships.Add(new EventMembership { UserId = commanderB.Id, EventId = eventBId, Role = "faction_commander" });
        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
        _outsiderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_outsiderClient, outsider.Id, "player");
        _commanderBClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderBClient, commanderB.Id, "faction_commander");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _outsiderClient.Dispose();
        _commanderBClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected async Task<(Guid platoonId, Guid squadId)> SeedPlatoonAndSquad(string platoonName = "Test Platoon", string squadName = "Test Squad")
    {
        using var db = GetDb();
        var platoon = new Platoon { FactionId = _factionId, Name = platoonName, Order = 1 };
        db.Platoons.Add(platoon);
        await db.SaveChangesAsync();

        var squad = new Squad { PlatoonId = platoon.Id, Name = squadName, Order = 1 };
        db.Squads.Add(squad);
        await db.SaveChangesAsync();

        return (platoon.Id, squad.Id);
    }

    protected async Task SeedEventPlayerWithSquad(Guid squadId, Guid platoonId)
    {
        using var db = GetDb();
        var ep = new EventPlayer
        {
            EventId = _eventId,
            Email = $"ep-{Guid.NewGuid():N}@test.com",
            Name = "Test Player",
            UserId = _playerUserId,
            SquadId = squadId,
            PlatoonId = platoonId
        };
        db.EventPlayers.Add(ep);
        await db.SaveChangesAsync();
    }
}

// ── GetFrequencies ────────────────────────────────────────────────────────────

[Trait("Category", "FREQ_Get")]
public class GetFrequencyTests : FrequencyTestsBase
{
    public GetFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsCommander_ReturnsCommandSection()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("command").ValueKind.Should().NotBe(JsonValueKind.Null,
            because: "commander should see command section via Faction.CommanderId");
        body.GetProperty("squad").ValueKind.Should().Be(JsonValueKind.Null,
            because: "commander without EventPlayer squad should have null squad section");
    }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsSquadSectionOnly()
    {
        var (platoonId, squadId) = await SeedPlatoonAndSquad("P1", "S1");
        await SeedEventPlayerWithSquad(squadId, platoonId);

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").ValueKind.Should().NotBe(JsonValueKind.Null,
            because: "player with squad assignment should see squad section");
        body.GetProperty("platoon").ValueKind.Should().Be(JsonValueKind.Null,
            because: "player should not see platoon section");
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null,
            because: "player should not see command section");
    }

    [Fact]
    public async Task GetFrequencies_Outsider_Returns403()
    {
        var response = await _outsiderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencies_ReturnsSquadNameAndId()
    {
        var (platoonId, squadId) = await SeedPlatoonAndSquad("Platoon-Alpha", "Squad-Alpha");
        await SeedEventPlayerWithSquad(squadId, platoonId);

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var squad = body.GetProperty("squad");
        squad.GetProperty("squadId").GetString().Should().Be(squadId.ToString());
        squad.GetProperty("squadName").GetString().Should().Be("Squad-Alpha");
    }

    [Fact]
    public async Task GetFrequencies_AfterPutSquad_ReflectsNewValues()
    {
        var (platoonId, squadId) = await SeedPlatoonAndSquad("Platoon-B", "Squad-B");
        await SeedEventPlayerWithSquad(squadId, platoonId);

        await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{squadId}/frequencies",
            new { primary = "43.325", backup = "44.000" });

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var squad = body.GetProperty("squad");
        squad.GetProperty("primary").GetString().Should().Be("43.325");
        squad.GetProperty("backup").GetString().Should().Be("44.000");
    }
}

// ── UpdateSquadFrequencies ────────────────────────────────────────────────────

[Trait("Category", "FREQ_UpdateSquad")]
public class UpdateSquadFrequencyTests : FrequencyTestsBase
{
    public UpdateSquadFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequency_AsCommander_Returns204()
    {
        var (_, squadId) = await SeedPlatoonAndSquad();

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{squadId}/frequencies",
            new { primary = "43.325", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(squadId);
        squad!.PrimaryFrequency.Should().Be("43.325");
        squad.BackupFrequency.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlayer_Returns403()
    {
        var (_, squadId) = await SeedPlatoonAndSquad();

        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{squadId}/frequencies",
            new { primary = "43.325", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_NonExistentSquad_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{Guid.NewGuid()}/frequencies",
            new { primary = "43.325", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSquadFrequency_WrongFactionCommander_Returns403()
    {
        // Squad belongs to faction A; Commander B does not own faction A
        var (_, squadId) = await SeedPlatoonAndSquad();

        var response = await _commanderBClient.PutAsJsonAsync(
            $"/api/squads/{squadId}/frequencies",
            new { primary = "43.325", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "Commander B does not own this squad's faction (IDOR protection)");
    }

    [Fact]
    public async Task UpdateSquadFrequency_NullValues_Clears()
    {
        var (_, squadId) = await SeedPlatoonAndSquad();

        // Set values first
        await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{squadId}/frequencies",
            new { primary = "43.325", backup = "44.000" });

        // Now clear them
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{squadId}/frequencies",
            new { primary = (string?)null, backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(squadId);
        squad!.PrimaryFrequency.Should().BeNull();
        squad.BackupFrequency.Should().BeNull();
    }
}

// ── UpdatePlatoonFrequencies ──────────────────────────────────────────────────

[Trait("Category", "FREQ_UpdatePlatoon")]
public class UpdatePlatoonFrequencyTests : FrequencyTestsBase
{
    public UpdatePlatoonFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsCommander_Returns204()
    {
        var (platoonId, _) = await SeedPlatoonAndSquad();

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{platoonId}/frequencies",
            new { primary = "41.000", backup = "42.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var platoon = await db.Platoons.FindAsync(platoonId);
        platoon!.PrimaryFrequency.Should().Be("41.000");
        platoon.BackupFrequency.Should().Be("42.000");
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_NonExistentPlatoon_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{Guid.NewGuid()}/frequencies",
            new { primary = "41.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_WrongFactionCommander_Returns403()
    {
        var (platoonId, _) = await SeedPlatoonAndSquad();

        var response = await _commanderBClient.PutAsJsonAsync(
            $"/api/platoons/{platoonId}/frequencies",
            new { primary = "41.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── UpdateFactionFrequencies ──────────────────────────────────────────────────

[Trait("Category", "FREQ_UpdateFaction")]
public class UpdateFactionFrequencyTests : FrequencyTestsBase
{
    public UpdateFactionFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateFactionFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "40.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency.Should().Be("40.000");
        faction.CommandBackupFrequency.Should().BeNull();
    }

    [Fact]
    public async Task UpdateFactionFrequency_NonExistentFaction_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{Guid.NewGuid()}/frequencies",
            new { primary = "40.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateFactionFrequency_WrongFactionCommander_Returns403()
    {
        var response = await _commanderBClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "40.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
