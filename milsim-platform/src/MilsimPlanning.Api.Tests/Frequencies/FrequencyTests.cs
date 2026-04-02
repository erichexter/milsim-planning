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

    protected HttpClient _commanderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _outsiderClient = null!;

    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _squadId;
    protected Guid _platoon2Id;
    protected Guid _squad2Id;
    protected string _commanderUserId = string.Empty;
    protected string _platoonLeaderUserId = string.Empty;
    protected string _squadLeaderUserId = string.Empty;
    protected string _playerUserId = string.Empty;

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
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
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

        // Create commander
        var commander = await CreateUser(userManager, "freq-cmdr", "faction_commander");
        _commanderUserId = commander.Id;

        // Create platoon leader
        var platoonLeader = await CreateUser(userManager, "freq-pl", "platoon_leader");
        _platoonLeaderUserId = platoonLeader.Id;

        // Create squad leader
        var squadLeader = await CreateUser(userManager, "freq-sl", "squad_leader");
        _squadLeaderUserId = squadLeader.Id;

        // Create player
        var player = await CreateUser(userManager, "freq-player", "player");
        _playerUserId = player.Id;

        // Create outsider (not a member of the event)
        var outsider = await CreateUser(userManager, "freq-outsider", "player");

        // Seed event + faction + platoons + squads
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _squadId = Guid.NewGuid();
        _platoon2Id = Guid.NewGuid();
        _squad2Id = Guid.NewGuid();

        var faction = new Faction
        {
            Id = _factionId,
            Name = "Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
            CommandPrimaryFrequency = "148.500",
            CommandBackupFrequency = "149.000"
        };

        var platoon = new Platoon
        {
            Id = _platoonId,
            FactionId = _factionId,
            Name = "Alpha",
            Order = 1,
            PrimaryFrequency = "150.000",
            BackupFrequency = "150.500"
        };

        var squad = new Squad
        {
            Id = _squadId,
            PlatoonId = _platoonId,
            Name = "Alpha-1",
            Order = 1,
            PrimaryFrequency = "151.000",
            BackupFrequency = "151.500"
        };

        var platoon2 = new Platoon
        {
            Id = _platoon2Id,
            FactionId = _factionId,
            Name = "Bravo",
            Order = 2,
            PrimaryFrequency = "152.000",
            BackupFrequency = null
        };

        var squad2 = new Squad
        {
            Id = _squad2Id,
            PlatoonId = _platoon2Id,
            Name = "Bravo-1",
            Order = 1,
            PrimaryFrequency = "153.000",
            BackupFrequency = "153.500"
        };

        var testEvent = new Event
        {
            Id = _eventId,
            Name = "Freq Test Event",
            Status = EventStatus.Draft,
            FactionId = _factionId,
            Faction = faction
        };

        db.Events.Add(testEvent);
        db.Platoons.AddRange(platoon, platoon2);
        db.Squads.AddRange(squad, squad2);

        // Event memberships
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        // EventPlayer assignments: platoon leader → Alpha platoon, squad leader → Alpha-1, player → Alpha-1
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = "freq-pl@test.com", Name = "PL",
            UserId = platoonLeader.Id, PlatoonId = _platoonId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = "freq-sl@test.com", Name = "SL",
            UserId = squadLeader.Id, SquadId = _squadId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = "freq-player@test.com", Name = "Player",
            UserId = player.Id, SquadId = _squadId
        });

        await db.SaveChangesAsync();

        // Create authenticated clients
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");

        _platoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_platoonLeaderClient, platoonLeader.Id, "platoon_leader");

        _squadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_squadLeaderClient, squadLeader.Id, "squad_leader");

        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");

        _outsiderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_outsiderClient, outsider.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _squadLeaderClient.Dispose();
        _playerClient.Dispose();
        _outsiderClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static async Task<AppUser> CreateUser(UserManager<AppUser> userManager, string prefix, string role)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.com";
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "TestPass123!");
        await userManager.AddToRoleAsync(user, role);
        user.Profile = new UserProfile { UserId = user.Id, Callsign = prefix, DisplayName = prefix, User = user };
        return user;
    }

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

// ── GET /api/events/{eventId}/frequencies ───────────────────────────────────

[Trait("Category", "FREQ_Get")]
public class GetFrequencyTests : FrequencyTestsBase
{
    public GetFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsFactionCommander_ReturnsAllFrequencies()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("command").GetProperty("primary").GetString().Should().Be("148.500");
        body.GetProperty("command").GetProperty("backup").GetString().Should().Be("149.000");
        body.GetProperty("platoons").GetArrayLength().Should().Be(2);
        body.GetProperty("squads").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsCommandAndOwnPlatoonAndSquads()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Platoon leader gets command frequencies
        body.GetProperty("command").GetProperty("primary").GetString().Should().Be("148.500");

        // Only own platoon (Alpha)
        var platoons = body.GetProperty("platoons");
        platoons.GetArrayLength().Should().Be(1);
        platoons[0].GetProperty("platoonName").GetString().Should().Be("Alpha");

        // Only squads in own platoon (Alpha-1)
        var squads = body.GetProperty("squads");
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("squadName").GetString().Should().Be("Alpha-1");
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsOwnPlatoonAndOwnSquad()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Squad leader does NOT get command frequencies
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);

        // Gets own platoon (Alpha)
        var platoons = body.GetProperty("platoons");
        platoons.GetArrayLength().Should().Be(1);
        platoons[0].GetProperty("platoonName").GetString().Should().Be("Alpha");

        // Gets own squad (Alpha-1)
        var squads = body.GetProperty("squads");
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("squadName").GetString().Should().Be("Alpha-1");
    }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsOnlyOwnSquad()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoons").GetArrayLength().Should().Be(0);

        var squads = body.GetProperty("squads");
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("squadName").GetString().Should().Be("Alpha-1");
    }

    [Fact]
    public async Task GetFrequencies_PlayerWithNoSquad_ReturnsEmpty()
    {
        // Create a player with no squad assignment
        using var db = GetDb();
        var userManager = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"freq-unassigned-{Guid.NewGuid():N}@test.com";
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "TestPass123!");
        await userManager.AddToRoleAsync(user, "player");
        user.Profile = new UserProfile { UserId = user.Id, Callsign = "Unassigned", DisplayName = "Unassigned", User = user };

        db.EventMemberships.Add(new EventMembership { UserId = user.Id, EventId = _eventId, Role = "player" });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = email, Name = "Unassigned",
            UserId = user.Id
        });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, "player");

        var response = await client.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoons").GetArrayLength().Should().Be(0);
        body.GetProperty("squads").GetArrayLength().Should().Be(0);

        client.Dispose();
    }

    [Fact]
    public async Task GetFrequencies_NonMemberEvent_Returns403()
    {
        var response = await _outsiderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencies_NonExistentEvent_Returns404()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{Guid.NewGuid()}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── PUT /api/events/{eventId}/frequencies/command ───────────────────────────

[Trait("Category", "FREQ_PutCommand")]
public class PutCommandFrequencyTests : FrequencyTestsBase
{
    public PutCommandFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateCommandFrequencies_AsFactionCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/command",
            new { primary = "160.000", backup = "161.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify persisted
        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency.Should().Be("160.000");
        faction!.CommandBackupFrequency.Should().Be("161.000");
    }

    [Fact]
    public async Task UpdateCommandFrequencies_NullClears_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/command",
            new { primary = (string?)null, backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency.Should().BeNull();
        faction!.CommandBackupFrequency.Should().BeNull();
    }

    [Fact]
    public async Task UpdateCommandFrequencies_AsPlatoonLeader_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/command",
            new { primary = "160.000", backup = "161.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCommandFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/command",
            new { primary = "160.000", backup = "161.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── PUT /api/events/{eventId}/frequencies/platoons/{platoonId} ──────────────

[Trait("Category", "FREQ_PutPlatoon")]
public class PutPlatoonFrequencyTests : FrequencyTestsBase
{
    public PutPlatoonFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsFactionCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/platoons/{_platoonId}",
            new { primary = "170.000", backup = "171.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PrimaryFrequency.Should().Be("170.000");
        platoon!.BackupFrequency.Should().Be("171.000");
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsPlatoonLeaderOwnPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/platoons/{_platoonId}",
            new { primary = "170.500", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsPlatoonLeaderDifferentPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/platoons/{_platoon2Id}",
            new { primary = "170.000", backup = "171.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/platoons/{_platoonId}",
            new { primary = "170.000", backup = "171.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_PlatoonFromDifferentEvent_Returns404()
    {
        // Create a separate event with its own platoon
        using var db = GetDb();
        var otherEventId = Guid.NewGuid();
        var otherFactionId = Guid.NewGuid();
        var otherPlatoonId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = otherEventId, Name = "Other Event", Status = EventStatus.Draft,
            FactionId = otherFactionId,
            Faction = new Faction { Id = otherFactionId, Name = "Other", CommanderId = _commanderUserId, EventId = otherEventId }
        });
        db.Platoons.Add(new Platoon { Id = otherPlatoonId, FactionId = otherFactionId, Name = "Other-P", Order = 1 });
        db.EventMemberships.Add(new EventMembership { UserId = _commanderUserId, EventId = otherEventId, Role = "faction_commander" });
        await db.SaveChangesAsync();

        // Try to update that platoon under our event — IDOR
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/platoons/{otherPlatoonId}",
            new { primary = "170.000", backup = "171.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── PUT /api/events/{eventId}/frequencies/squads/{squadId} ──────────────────

[Trait("Category", "FREQ_PutSquad")]
public class PutSquadFrequencyTests : FrequencyTestsBase
{
    public PutSquadFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequencies_AsFactionCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{_squadId}",
            new { primary = "180.000", backup = "181.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.PrimaryFrequency.Should().Be("180.000");
        squad!.BackupFrequency.Should().Be("181.000");
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsSquadLeaderOwnSquad_Returns204()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{_squadId}",
            new { primary = "180.500", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsSquadLeaderDifferentSquad_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{_squad2Id}",
            new { primary = "180.000", backup = "181.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeaderSquadInOwnPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{_squadId}",
            new { primary = "180.750", backup = "181.750" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeaderSquadInDifferentPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{_squad2Id}",
            new { primary = "180.000", backup = "181.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{_squadId}",
            new { primary = "180.000", backup = "181.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_SquadFromDifferentEvent_Returns404()
    {
        // Create a separate event with its own squad
        using var db = GetDb();
        var otherEventId = Guid.NewGuid();
        var otherFactionId = Guid.NewGuid();
        var otherPlatoonId = Guid.NewGuid();
        var otherSquadId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = otherEventId, Name = "Other Event 2", Status = EventStatus.Draft,
            FactionId = otherFactionId,
            Faction = new Faction { Id = otherFactionId, Name = "Other2", CommanderId = _commanderUserId, EventId = otherEventId }
        });
        db.Platoons.Add(new Platoon { Id = otherPlatoonId, FactionId = otherFactionId, Name = "Other-P2", Order = 1 });
        db.Squads.Add(new Squad { Id = otherSquadId, PlatoonId = otherPlatoonId, Name = "Other-S2", Order = 1 });
        db.EventMemberships.Add(new EventMembership { UserId = _commanderUserId, EventId = otherEventId, Role = "faction_commander" });
        await db.SaveChangesAsync();

        // Try to update that squad under our event — IDOR
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/frequencies/squads/{otherSquadId}",
            new { primary = "180.000", backup = "181.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
