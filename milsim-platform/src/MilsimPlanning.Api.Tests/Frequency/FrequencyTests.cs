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

public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // Clients for each role
    protected HttpClient _commanderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _adminClient = null!;
    protected HttpClient _outsiderClient = null!;

    // IDs
    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _platoon2Id;
    protected Guid _squadId;
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

        // Create faction commander
        var cmdEmail = $"freq-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "FreqCmdr", DisplayName = "FreqCmdr", User = commander };
        _commanderUserId = commander.Id;

        // Create platoon leader
        var plEmail = $"freq-pl-{Guid.NewGuid():N}@test.com";
        var platoonLeader = new AppUser { UserName = plEmail, Email = plEmail, EmailConfirmed = true };
        await userManager.CreateAsync(platoonLeader, "TestPass123!");
        await userManager.AddToRoleAsync(platoonLeader, "platoon_leader");
        platoonLeader.Profile = new UserProfile { UserId = platoonLeader.Id, Callsign = "FreqPL", DisplayName = "FreqPL", User = platoonLeader };
        _platoonLeaderUserId = platoonLeader.Id;

        // Create squad leader
        var slEmail = $"freq-sl-{Guid.NewGuid():N}@test.com";
        var squadLeader = new AppUser { UserName = slEmail, Email = slEmail, EmailConfirmed = true };
        await userManager.CreateAsync(squadLeader, "TestPass123!");
        await userManager.AddToRoleAsync(squadLeader, "squad_leader");
        squadLeader.Profile = new UserProfile { UserId = squadLeader.Id, Callsign = "FreqSL", DisplayName = "FreqSL", User = squadLeader };
        _squadLeaderUserId = squadLeader.Id;

        // Create player
        var playerEmail = $"freq-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "FreqPlayer", DisplayName = "FreqPlayer", User = player };
        _playerUserId = player.Id;

        // Create outsider (NOT a member of the event)
        var outsiderEmail = $"freq-outsider-{Guid.NewGuid():N}@test.com";
        var outsider = new AppUser { UserName = outsiderEmail, Email = outsiderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(outsider, "TestPass123!");
        await userManager.AddToRoleAsync(outsider, "player");
        outsider.Profile = new UserProfile { UserId = outsider.Id, Callsign = "FreqOutsider", DisplayName = "FreqOutsider", User = outsider };

        // Create system_admin (bypasses ScopeGuard, used for 404 tests)
        var adminEmail = $"freq-admin-{Guid.NewGuid():N}@test.com";
        var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        await userManager.CreateAsync(admin, "TestPass123!");
        await userManager.AddToRoleAsync(admin, "system_admin");
        admin.Profile = new UserProfile { UserId = admin.Id, Callsign = "FreqAdmin", DisplayName = "FreqAdmin", User = admin };

        // Seed event + faction + hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        var faction = new Faction
        {
            Id = _factionId,
            Name = "Freq Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
            CommandPrimaryFrequency = "150.000",
            CommandBackupFrequency = "151.000"
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

        // Platoon 1 (platoon leader's platoon)
        _platoonId = Guid.NewGuid();
        var platoon1 = new Platoon
        {
            Id = _platoonId,
            FactionId = _factionId,
            Name = "Alpha Platoon",
            Order = 1,
            PrimaryFrequency = "152.000",
            BackupFrequency = "153.000"
        };
        db.Platoons.Add(platoon1);

        // Platoon 2 (different platoon)
        _platoon2Id = Guid.NewGuid();
        var platoon2 = new Platoon
        {
            Id = _platoon2Id,
            FactionId = _factionId,
            Name = "Bravo Platoon",
            Order = 2,
            PrimaryFrequency = "154.000",
            BackupFrequency = "155.000"
        };
        db.Platoons.Add(platoon2);

        // Squad 1 in Platoon 1 (squad leader's squad)
        _squadId = Guid.NewGuid();
        var squad1 = new Squad
        {
            Id = _squadId,
            PlatoonId = _platoonId,
            Name = "Alpha Squad",
            Order = 1,
            PrimaryFrequency = "156.000",
            BackupFrequency = "157.000"
        };
        db.Squads.Add(squad1);

        // Squad 2 in Platoon 2
        _squad2Id = Guid.NewGuid();
        var squad2 = new Squad
        {
            Id = _squad2Id,
            PlatoonId = _platoon2Id,
            Name = "Bravo Squad",
            Order = 1,
            PrimaryFrequency = "158.000",
            BackupFrequency = "159.000"
        };
        db.Squads.Add(squad2);

        // EventMemberships
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        // EventPlayers — assign to squads/platoons
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = playerEmail.ToLowerInvariant(),
            Name = "FreqPlayer",
            Callsign = "FP",
            UserId = player.Id,
            SquadId = _squadId,
            PlatoonId = _platoonId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = slEmail.ToLowerInvariant(),
            Name = "FreqSL",
            Callsign = "FSL",
            UserId = squadLeader.Id,
            SquadId = _squadId,
            PlatoonId = _platoonId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = plEmail.ToLowerInvariant(),
            Name = "FreqPL",
            Callsign = "FPL",
            UserId = platoonLeader.Id,
            PlatoonId = _platoonId
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

        _adminClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_adminClient, admin.Id, "system_admin");

        _outsiderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_outsiderClient, outsider.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _squadLeaderClient.Dispose();
        _playerClient.Dispose();
        _adminClient.Dispose();
        _outsiderClient.Dispose();
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

[Trait("Category", "FREQ_Get")]
public class GetFrequencyTests : FrequencyTestsBase
{
    public GetFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsOnlyOwnSquad()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoons").ValueKind.Should().Be(JsonValueKind.Null);

        var squads = body.GetProperty("squads");
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("squadName").GetString().Should().Be("Alpha Squad");
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsOwnPlatoonAndSquad()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);

        var platoons = body.GetProperty("platoons");
        platoons.GetArrayLength().Should().Be(1);
        platoons[0].GetProperty("platoonName").GetString().Should().Be("Alpha Platoon");

        var squads = body.GetProperty("squads");
        squads.GetArrayLength().Should().Be(1);
        squads[0].GetProperty("squadName").GetString().Should().Be("Alpha Squad");
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsCommandAndOwnPlatoon()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var command = body.GetProperty("command");
        command.GetProperty("primary").GetString().Should().Be("150.000");
        command.GetProperty("backup").GetString().Should().Be("151.000");

        var platoons = body.GetProperty("platoons");
        platoons.GetArrayLength().Should().Be(1);
        platoons[0].GetProperty("platoonName").GetString().Should().Be("Alpha Platoon");

        body.GetProperty("squads").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFrequencies_AsCommander_ReturnsAllLevels()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var command = body.GetProperty("command");
        command.GetProperty("primary").GetString().Should().Be("150.000");

        body.GetProperty("platoons").GetArrayLength().Should().Be(2);
        body.GetProperty("squads").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetFrequencies_AsSystemAdmin_ReturnsAllLevels()
    {
        var response = await _adminClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var command = body.GetProperty("command");
        command.GetProperty("primary").GetString().Should().Be("150.000");
        command.GetProperty("backup").GetString().Should().Be("151.000");

        body.GetProperty("platoons").GetArrayLength().Should().Be(2);
        body.GetProperty("squads").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetFrequencies_AsOutsider_Returns403()
    {
        var response = await _outsiderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencies_NonExistentEvent_Returns404()
    {
        var response = await _adminClient.GetAsync($"/api/events/{Guid.NewGuid()}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────────

[Trait("Category", "FREQ_UpdateSquad")]
public class UpdateSquadFrequencyTests : FrequencyTestsBase
{
    public UpdateSquadFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequency_AsSquadLeader_Returns204()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "160.000", backup = "161.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify persisted
        var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.PrimaryFrequency.Should().Be("160.000");
        squad.BackupFrequency.Should().Be("161.000");
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlatoonLeader_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "162.000", backup = "163.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "164.000", backup = "165.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "166.000", backup = "167.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsDifferentSquadLeader_Returns403()
    {
        // Squad leader of Alpha Squad tries to update Bravo Squad
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "190.000", backup = "191.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_NonExistentSquad_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync(
            $"/api/squads/{Guid.NewGuid()}/frequencies",
            new { primary = "168.000", backup = "169.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────────

[Trait("Category", "FREQ_UpdatePlatoon")]
public class UpdatePlatoonFrequencyTests : FrequencyTestsBase
{
    public UpdatePlatoonFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsPlatoonLeader_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "170.000", backup = "171.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var db = GetDb();
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PrimaryFrequency.Should().Be("170.000");
        platoon.BackupFrequency.Should().Be("171.000");
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "172.000", backup = "173.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "174.000", backup = "175.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_DifferentPlatoonLeader_Returns403()
    {
        // Platoon leader of Platoon 1 tries to update Platoon 2
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon2Id}/frequencies",
            new { primary = "176.000", backup = "177.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── PUT /api/events/{eventId}/command-frequencies ─────────────────────────────

[Trait("Category", "FREQ_UpdateCommand")]
public class UpdateCommandFrequencyTests : FrequencyTestsBase
{
    public UpdateCommandFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateCommandFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "180.000", backup = "181.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var db = GetDb();
        var faction = await db.Factions.FirstAsync(f => f.EventId == _eventId);
        faction.CommandPrimaryFrequency.Should().Be("180.000");
        faction.CommandBackupFrequency.Should().Be("181.000");
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsPlatoonLeader_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "182.000", backup = "183.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsSystemAdmin_Returns204()
    {
        var response = await _adminClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "186.000", backup = "187.000" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var db = GetDb();
        var faction = await db.Factions.FirstAsync(f => f.EventId == _eventId);
        faction.CommandPrimaryFrequency.Should().Be("186.000");
        faction.CommandBackupFrequency.Should().Be("187.000");
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "184.000", backup = "185.000" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
