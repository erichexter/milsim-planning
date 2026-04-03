using System.Net;
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
using MilsimPlanning.Api.Models.Frequencies;
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

    // Clients for each role
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _systemAdminClient = null!;

    // IDs
    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _platoon2Id;
    protected Guid _squadId;
    protected Guid _squad2Id;

    protected string _commanderUserId = null!;
    protected string _playerUserId = null!;
    protected string _squadLeaderUserId = null!;
    protected string _platoonLeaderUserId = null!;
    protected string _systemAdminUserId = null!;

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

        // Create users for each role
        _commanderUserId = await CreateUser(userManager, "freq-cmdr", "faction_commander");
        _playerUserId = await CreateUser(userManager, "freq-player", "player");
        _squadLeaderUserId = await CreateUser(userManager, "freq-sl", "squad_leader");
        _platoonLeaderUserId = await CreateUser(userManager, "freq-pl", "platoon_leader");
        _systemAdminUserId = await CreateUser(userManager, "freq-admin", "system_admin");

        // Seed event + faction + hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _platoon2Id = Guid.NewGuid();
        _squadId = Guid.NewGuid();
        _squad2Id = Guid.NewGuid();

        var faction = new Faction
        {
            Id = _factionId,
            Name = "Test Faction",
            CommanderId = _commanderUserId,
            EventId = _eventId,
            CommandPrimaryFrequency = "155.500",
            CommandBackupFrequency = "155.750"
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

        var platoon1 = new Platoon
        {
            Id = _platoonId,
            FactionId = _factionId,
            Name = "1st Platoon",
            Order = 1,
            PrimaryFrequency = "148.000",
            BackupFrequency = "148.250"
        };
        var platoon2 = new Platoon
        {
            Id = _platoon2Id,
            FactionId = _factionId,
            Name = "2nd Platoon",
            Order = 2,
            PrimaryFrequency = "149.000",
            BackupFrequency = null
        };
        db.Platoons.AddRange(platoon1, platoon2);

        var squad1 = new Squad
        {
            Id = _squadId,
            PlatoonId = _platoonId,
            Name = "Alpha",
            Order = 1,
            PrimaryFrequency = "151.000",
            BackupFrequency = "151.250"
        };
        var squad2 = new Squad
        {
            Id = _squad2Id,
            PlatoonId = _platoon2Id,
            Name = "Bravo",
            Order = 1,
            PrimaryFrequency = "152.000",
            BackupFrequency = null
        };
        db.Squads.AddRange(squad1, squad2);

        // EventMemberships
        db.EventMemberships.AddRange(
            new EventMembership { UserId = _commanderUserId, EventId = _eventId, Role = "faction_commander" },
            new EventMembership { UserId = _playerUserId, EventId = _eventId, Role = "player" },
            new EventMembership { UserId = _squadLeaderUserId, EventId = _eventId, Role = "squad_leader" },
            new EventMembership { UserId = _platoonLeaderUserId, EventId = _eventId, Role = "platoon_leader" },
            new EventMembership { UserId = _systemAdminUserId, EventId = _eventId, Role = "system_admin" }
        );

        // EventPlayers — assign each to appropriate position
        db.EventPlayers.AddRange(
            new EventPlayer { EventId = _eventId, Email = "player@test.com", Name = "Player", UserId = _playerUserId, SquadId = _squadId, PlatoonId = _platoonId },
            new EventPlayer { EventId = _eventId, Email = "sl@test.com", Name = "Squad Leader", UserId = _squadLeaderUserId, SquadId = _squadId, PlatoonId = _platoonId },
            new EventPlayer { EventId = _eventId, Email = "pl@test.com", Name = "Platoon Leader", UserId = _platoonLeaderUserId, PlatoonId = _platoonId },
            new EventPlayer { EventId = _eventId, Email = "cmdr@test.com", Name = "Commander", UserId = _commanderUserId, PlatoonId = _platoonId },
            new EventPlayer { EventId = _eventId, Email = "admin@test.com", Name = "Admin", UserId = _systemAdminUserId, PlatoonId = _platoonId }
        );

        await db.SaveChangesAsync();

        // Create authenticated clients
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, _commanderUserId, "faction_commander");

        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, _playerUserId, "player");

        _squadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_squadLeaderClient, _squadLeaderUserId, "squad_leader");

        _platoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_platoonLeaderClient, _platoonLeaderUserId, "platoon_leader");

        _systemAdminClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_systemAdminClient, _systemAdminUserId, "system_admin");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _squadLeaderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _systemAdminClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static async Task<string> CreateUser(UserManager<AppUser> userManager, string prefix, string role)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.com";
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "TestPass123!");
        await userManager.AddToRoleAsync(user, role);
        user.Profile = new UserProfile { UserId = user.Id, Callsign = prefix, DisplayName = prefix, User = user };
        return user.Id;
    }

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

// ── FREQ-01: Read Frequencies ───────────────────────────────────────────────

[Trait("Category", "FREQ_Read")]
public class FrequencyReadTests : FrequencyTestsBase
{
    public FrequencyReadTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsOnlySquadFrequency()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<FrequencyReadDto>();
        dto!.Squad.Should().NotBeNull();
        dto.Squad!.SquadName.Should().Be("Alpha");
        dto.Squad.Primary.Should().Be("151.000");
        dto.Squad.Backup.Should().Be("151.250");
        dto.Platoon.Should().BeNull();
        dto.Command.Should().BeNull();
        dto.AllFrequencies.Should().BeNull();
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsSquadAndPlatoon()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<FrequencyReadDto>();
        dto!.Squad.Should().NotBeNull();
        dto.Squad!.SquadName.Should().Be("Alpha");
        dto.Platoon.Should().NotBeNull();
        dto.Platoon!.PlatoonName.Should().Be("1st Platoon");
        dto.Platoon.Primary.Should().Be("148.000");
        dto.Command.Should().BeNull();
        dto.AllFrequencies.Should().BeNull();
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsPlatoonAndCommand()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<FrequencyReadDto>();
        dto!.Squad.Should().BeNull();
        dto.Platoon.Should().NotBeNull();
        dto.Platoon!.PlatoonName.Should().Be("1st Platoon");
        dto.Command.Should().NotBeNull();
        dto.Command!.FactionName.Should().Be("Test Faction");
        dto.Command.Primary.Should().Be("155.500");
        dto.AllFrequencies.Should().BeNull();
    }

    [Fact]
    public async Task GetFrequencies_AsCommander_ReturnsAllFrequencies()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<FrequencyReadDto>();
        dto!.Squad.Should().BeNull();
        dto.Platoon.Should().BeNull();
        dto.Command.Should().NotBeNull();
        dto.Command!.FactionName.Should().Be("Test Faction");
        dto.AllFrequencies.Should().NotBeNull();
        dto.AllFrequencies!.Platoons.Should().HaveCount(2);
        dto.AllFrequencies.Squads.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFrequencies_AsSystemAdmin_ReturnsSameAsCommander()
    {
        var response = await _systemAdminClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<FrequencyReadDto>();
        dto!.Command.Should().NotBeNull();
        dto.AllFrequencies.Should().NotBeNull();
        dto.AllFrequencies!.Platoons.Should().HaveCount(2);
        dto.AllFrequencies.Squads.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFrequencies_NoEventPlayer_Returns404()
    {
        // Create a user with membership but no EventPlayer
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"freq-no-ep-{Guid.NewGuid():N}@test.com";
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "TestPass123!");
        await userManager.AddToRoleAsync(user, "player");
        user.Profile = new UserProfile { UserId = user.Id, Callsign = "NoEP", DisplayName = "NoEP", User = user };
        db.EventMemberships.Add(new EventMembership { UserId = user.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, "player");

        var response = await client.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── FREQ-02: Update Squad Frequencies ───────────────────────────────────────

[Trait("Category", "FREQ_UpdateSquad")]
public class FrequencyUpdateSquadTests : FrequencyTestsBase
{
    public FrequencyUpdateSquadTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequencies_AsSquadLeader_Returns204()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new UpdateFrequencyRequest { Primary = "148.250", Backup = null });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.PrimaryFrequency.Should().Be("148.250");
        squad.BackupFrequency.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeader_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new UpdateFrequencyRequest { Primary = "160.000", Backup = "160.500" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new UpdateFrequencyRequest { Primary = "170.000", Backup = null });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsSquadLeaderOfDifferentSquad_Returns403()
    {
        // squad2 is in platoon2, but our squad leader is in platoon1/squad1
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_NonExistentSquad_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{Guid.NewGuid()}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── FREQ-03: Update Platoon Frequencies ─────────────────────────────────────

[Trait("Category", "FREQ_UpdatePlatoon")]
public class FrequencyUpdatePlatoonTests : FrequencyTestsBase
{
    public FrequencyUpdatePlatoonTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsPlatoonLeader_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new UpdateFrequencyRequest { Primary = "148.500", Backup = "148.750" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PrimaryFrequency.Should().Be("148.500");
        platoon.BackupFrequency.Should().Be("148.750");
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new UpdateFrequencyRequest { Primary = "170.000" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsPlatoonLeaderOfDifferentPlatoon_Returns403()
    {
        // Our platoon leader is in platoon1, trying to update platoon2
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon2Id}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_NonExistentPlatoon_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{Guid.NewGuid()}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── FREQ-04: Update Command (Faction) Frequencies ───────────────────────────

[Trait("Category", "FREQ_UpdateCommand")]
public class FrequencyUpdateCommandTests : FrequencyTestsBase
{
    public FrequencyUpdateCommandTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateCommandFrequencies_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new UpdateFrequencyRequest { Primary = "155.250", Backup = "155.500" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency.Should().Be("155.250");
        faction.CommandBackupFrequency.Should().Be("155.500");
    }

    [Fact]
    public async Task UpdateCommandFrequencies_AsSystemAdmin_Returns204()
    {
        var response = await _systemAdminClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new UpdateFrequencyRequest { Primary = "156.000" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateCommandFrequencies_AsPlatoonLeader_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCommandFrequencies_NonExistentFaction_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{Guid.NewGuid()}/frequencies",
            new UpdateFrequencyRequest { Primary = "999.000" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
