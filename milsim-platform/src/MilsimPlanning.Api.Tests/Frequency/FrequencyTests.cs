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
using MilsimPlanning.Api.Models.Frequency;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Frequency;

/// <summary>
/// Base fixture for Frequency integration tests.
/// Sets up an event with one faction, one platoon, one squad,
/// and four users: faction_commander, platoon_leader, squad_leader, and player.
/// Each user has an EventPlayer row linked via UserId where applicable.
/// </summary>
public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    protected HttpClient _commanderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _altSquadLeaderClient = null!;
    protected HttpClient _altPlatoonLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _nonMemberClient = null!;

    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _altPlatoonId;
    protected Guid _squadId;
    protected Guid _altSquadId;
    protected Guid _altPlatoonSquadId;
    protected string _commanderUserId = string.Empty;

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

        // Commander
        var commanderEmail = $"freq-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Platoon leader
        var plEmail = $"freq-pl-{Guid.NewGuid():N}@test.com";
        var platoonLeader = new AppUser { UserName = plEmail, Email = plEmail, EmailConfirmed = true };
        await userManager.CreateAsync(platoonLeader, "TestPass123!");
        await userManager.AddToRoleAsync(platoonLeader, "platoon_leader");
        platoonLeader.Profile = new UserProfile { UserId = platoonLeader.Id, Callsign = "PL1", DisplayName = "PL1", User = platoonLeader };

        // Squad leader
        var slEmail = $"freq-sl-{Guid.NewGuid():N}@test.com";
        var squadLeader = new AppUser { UserName = slEmail, Email = slEmail, EmailConfirmed = true };
        await userManager.CreateAsync(squadLeader, "TestPass123!");
        await userManager.AddToRoleAsync(squadLeader, "squad_leader");
        squadLeader.Profile = new UserProfile { UserId = squadLeader.Id, Callsign = "SL1", DisplayName = "SL1", User = squadLeader };

        // Alt squad leader (different squad — for 403 tests)
        var sl2Email = $"freq-sl2-{Guid.NewGuid():N}@test.com";
        var altSquadLeader = new AppUser { UserName = sl2Email, Email = sl2Email, EmailConfirmed = true };
        await userManager.CreateAsync(altSquadLeader, "TestPass123!");
        await userManager.AddToRoleAsync(altSquadLeader, "squad_leader");
        altSquadLeader.Profile = new UserProfile { UserId = altSquadLeader.Id, Callsign = "SL2", DisplayName = "SL2", User = altSquadLeader };

        // Alt platoon leader (different platoon — for IDOR 403 tests)
        var pl2Email = $"freq-pl2-{Guid.NewGuid():N}@test.com";
        var altPlatoonLeader = new AppUser { UserName = pl2Email, Email = pl2Email, EmailConfirmed = true };
        await userManager.CreateAsync(altPlatoonLeader, "TestPass123!");
        await userManager.AddToRoleAsync(altPlatoonLeader, "platoon_leader");
        altPlatoonLeader.Profile = new UserProfile { UserId = altPlatoonLeader.Id, Callsign = "PL2", DisplayName = "PL2", User = altPlatoonLeader };

        // Player
        var playerEmail = $"freq-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "P1", DisplayName = "P1", User = player };

        // Non-member
        var outsiderEmail = $"freq-outsider-{Guid.NewGuid():N}@test.com";
        var outsider = new AppUser { UserName = outsiderEmail, Email = outsiderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(outsider, "TestPass123!");
        await userManager.AddToRoleAsync(outsider, "player");
        outsider.Profile = new UserProfile { UserId = outsider.Id, Callsign = "OUT", DisplayName = "Outsider", User = outsider };

        // Seed event, faction, platoon, squads
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _altPlatoonId = Guid.NewGuid();
        _squadId = Guid.NewGuid();
        _altSquadId = Guid.NewGuid();
        _altPlatoonSquadId = Guid.NewGuid();

        var faction = new Faction
        {
            Id = _factionId,
            Name = "Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
            CommandPrimaryFrequency = "121.5",
            CommandBackupFrequency = "243.0"
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

        var platoon = new Platoon
        {
            Id = _platoonId,
            FactionId = _factionId,
            Name = "Alpha Platoon",
            Order = 1,
            PrimaryFrequency = "130.0",
            BackupFrequency = "140.0"
        };
        db.Platoons.Add(platoon);

        var squad = new Squad
        {
            Id = _squadId,
            PlatoonId = _platoonId,
            Name = "Alpha-1",
            Order = 1,
            PrimaryFrequency = "155.0",
            BackupFrequency = "160.0"
        };
        db.Squads.Add(squad);

        var altSquad = new Squad
        {
            Id = _altSquadId,
            PlatoonId = _platoonId,
            Name = "Alpha-2",
            Order = 2
        };
        db.Squads.Add(altSquad);

        var altPlatoon = new Platoon
        {
            Id = _altPlatoonId,
            FactionId = _factionId,
            Name = "Bravo Platoon",
            Order = 2
        };
        db.Platoons.Add(altPlatoon);

        var altPlatoonSquad = new Squad
        {
            Id = _altPlatoonSquadId,
            PlatoonId = _altPlatoonId,
            Name = "Bravo-1",
            Order = 1
        };
        db.Squads.Add(altPlatoonSquad);

        // EventMemberships
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = altSquadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = altPlatoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        // EventPlayer rows for non-commander users (linked via UserId)
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = plEmail,
            Name = "PL1",
            UserId = platoonLeader.Id,
            PlatoonId = _platoonId
        });

        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = slEmail,
            Name = "SL1",
            UserId = squadLeader.Id,
            SquadId = _squadId,
            PlatoonId = _platoonId
        });

        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = sl2Email,
            Name = "SL2",
            UserId = altSquadLeader.Id,
            SquadId = _altSquadId,
            PlatoonId = _platoonId
        });

        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = playerEmail,
            Name = "P1",
            UserId = player.Id,
            SquadId = _squadId,
            PlatoonId = _platoonId
        });

        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId,
            Email = pl2Email,
            Name = "PL2",
            UserId = altPlatoonLeader.Id,
            PlatoonId = _altPlatoonId
        });

        await db.SaveChangesAsync();

        // Create HTTP clients
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");

        _platoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_platoonLeaderClient, platoonLeader.Id, "platoon_leader");

        _squadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_squadLeaderClient, squadLeader.Id, "squad_leader");

        _altSquadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_altSquadLeaderClient, altSquadLeader.Id, "squad_leader");

        _altPlatoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_altPlatoonLeaderClient, altPlatoonLeader.Id, "platoon_leader");

        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");

        _nonMemberClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_nonMemberClient, outsider.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _squadLeaderClient.Dispose();
        _altSquadLeaderClient.Dispose();
        _altPlatoonLeaderClient.Dispose();
        _playerClient.Dispose();
        _nonMemberClient.Dispose();
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

[Trait("Category", "FREQ_GetEvent")]
public class FrequencyTests : FrequencyTestsBase
{
    public FrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetEventFrequencies_AsPlayer_ReturnsSquadLevelOnly()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto.Should().NotBeNull();
        dto!.Squad.Should().NotBeNull();
        dto.Squad!.Id.Should().Be(_squadId);
        dto.Platoon.Should().BeNull();
        dto.Command.Should().BeNull();
    }

    [Fact]
    public async Task GetEventFrequencies_AsSquadLeader_ReturnsSquadAndPlatoonLevel()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto.Should().NotBeNull();
        dto!.Squad.Should().NotBeNull();
        dto.Squad!.Id.Should().Be(_squadId);
        dto.Platoon.Should().NotBeNull();
        dto.Platoon!.Id.Should().Be(_platoonId);
        dto.Command.Should().BeNull();
    }

    [Fact]
    public async Task GetEventFrequencies_AsPlatoonLeader_ReturnsPlatoonAndCommandLevel()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto.Should().NotBeNull();
        dto!.Squad.Should().BeNull();
        dto.Platoon.Should().NotBeNull();
        dto.Platoon!.Id.Should().Be(_platoonId);
        dto.Command.Should().NotBeNull();
        dto.Command!.Id.Should().Be(_factionId);
    }

    [Fact]
    public async Task GetEventFrequencies_AsFactionCommander_ReturnsCommandLevelOnly()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto.Should().NotBeNull();
        dto!.Squad.Should().BeNull();
        dto.Platoon.Should().BeNull();
        dto.Command.Should().NotBeNull();
        dto.Command!.Id.Should().Be(_factionId);
    }

    [Fact]
    public async Task GetEventFrequencies_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEventFrequencies_AsNonMember_Returns403()
    {
        var response = await _nonMemberClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsOwningSquadLeader_Returns200()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "160.0", backup = "170.0" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequencyLevelDto>();
        dto.Should().NotBeNull();
        dto!.Primary.Should().Be("160.0");
        dto.Backup.Should().Be("170.0");
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsDifferentSquadLeader_Returns403()
    {
        // altSquadLeader is linked to altSquad, not _squadId
        var response = await _altSquadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "999.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeaderOfSquadPlatoon_Returns200()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "155.5", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequencyLevelDto>();
        dto.Should().NotBeNull();
        dto!.Primary.Should().Be("155.5");
        dto.Backup.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "000.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsOwningPlatoonLeader_Returns200()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "135.0", backup = "145.0" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequencyLevelDto>();
        dto.Should().NotBeNull();
        dto!.Primary.Should().Be("135.0");
        dto.Backup.Should().Be("145.0");
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_AsDifferentPlatoonLeader_Returns403()
    {
        // squadLeader has role "squad_leader" — not permitted to update platoon frequencies
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "999.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFactionFrequencies_AsFactionCommander_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "125.0", backup = "250.0" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequencyLevelDto>();
        dto.Should().NotBeNull();
        dto!.Primary.Should().Be("125.0");
        dto.Backup.Should().Be("250.0");
    }

    [Fact]
    public async Task UpdateFactionFrequencies_AsPlatoonLeader_Returns403()
    {
        // platoonLeader role is not faction_commander — forbidden
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "999.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_AsPlatoonLeaderOfDifferentPlatoon_Returns403()
    {
        // altPlatoonLeader is assigned to _altPlatoonId, not _platoonId
        // attempting to update a squad under _platoonId should be forbidden
        var response = await _altPlatoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "999.0" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequencies_WithNonExistentSquadId_Returns404()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{Guid.NewGuid()}/frequencies",
            new { primary = "160.0" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePlatoonFrequencies_WithNonExistentPlatoonId_Returns404()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{Guid.NewGuid()}/frequencies",
            new { primary = "135.0" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateFactionFrequencies_WithNonExistentFactionId_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{Guid.NewGuid()}/frequencies",
            new { primary = "125.0" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
