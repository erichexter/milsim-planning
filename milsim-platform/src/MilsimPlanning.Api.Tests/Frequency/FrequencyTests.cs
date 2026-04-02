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
/// Integration tests for the Frequency Management API (HEX-171).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // Clients with different roles
    protected HttpClient _commanderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _outsiderClient = null!;

    // IDs for the seeded hierarchy
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

        async Task<AppUser> CreateUser(string emailPrefix, string role)
        {
            var email = $"freq-{emailPrefix}-{Guid.NewGuid():N}@test.com";
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, "TestPass123!");
            await userManager.AddToRoleAsync(user, role);
            user.Profile = new UserProfile { UserId = user.Id, Callsign = emailPrefix, DisplayName = emailPrefix, User = user };
            return user;
        }

        var commander = await CreateUser("commander", "faction_commander");
        var platoonLeader = await CreateUser("platoon-leader", "platoon_leader");
        var squadLeader = await CreateUser("squad-leader", "squad_leader");
        var player = await CreateUser("player", "player");
        var outsider = await CreateUser("outsider", "player");

        _commanderUserId = commander.Id;
        _platoonLeaderUserId = platoonLeader.Id;
        _squadLeaderUserId = squadLeader.Id;
        _playerUserId = player.Id;

        // Seed hierarchy: Event → Faction → Platoon → Squad
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _platoon2Id = Guid.NewGuid();
        _squadId = Guid.NewGuid();
        _squad2Id = Guid.NewGuid();

        var faction = new Faction { Id = _factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Freq Test Event", Status = EventStatus.Draft, FactionId = _factionId, Faction = faction };
        db.Events.Add(testEvent);

        var platoon = new Platoon { Id = _platoonId, FactionId = _factionId, Name = "1 Platoon", Order = 1 };
        var platoon2 = new Platoon { Id = _platoon2Id, FactionId = _factionId, Name = "2 Platoon", Order = 2 };
        db.Platoons.AddRange(platoon, platoon2);

        var squad = new Squad { Id = _squadId, PlatoonId = _platoonId, Name = "Alpha Squad", Order = 1 };
        var squad2 = new Squad { Id = _squad2Id, PlatoonId = _platoon2Id, Name = "Bravo Squad", Order = 1 };
        db.Squads.AddRange(squad, squad2);

        // EventMemberships
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        // outsider has no membership

        // EventPlayers: link users to hierarchy positions
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = platoonLeader.Email!, Name = "PlatoonLeader",
            UserId = platoonLeader.Id, PlatoonId = _platoonId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = squadLeader.Email!, Name = "SquadLeader",
            UserId = squadLeader.Id, SquadId = _squadId, PlatoonId = _platoonId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = player.Email!, Name = "Player",
            UserId = player.Id, SquadId = _squadId, PlatoonId = _platoonId
        });

        await db.SaveChangesAsync();

        // Create HTTP clients
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

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

// ── GET /api/events/{eventId}/frequencies ─────────────────────────────────────

[Trait("Category", "FREQ_Get")]
public class GetFrequenciesTests : FrequencyTestsBase
{
    public GetFrequenciesTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsOnlySquadSection()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequenciesDto>();
        dto.Should().NotBeNull();
        dto!.Squad.Should().NotBeNull();
        dto.Platoon.Should().BeNull();
        dto.Command.Should().BeNull();
        dto.AllPlatoons.Should().BeNull();
        dto.AllSquads.Should().BeNull();
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsSquadAndPlatoon()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequenciesDto>();
        dto!.Squad.Should().NotBeNull();
        dto.Platoon.Should().NotBeNull();
        dto.Command.Should().BeNull();
        dto.AllPlatoons.Should().BeNull();
        dto.AllSquads.Should().BeNull();
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsPlatoonAndCommand()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequenciesDto>();
        dto!.Squad.Should().BeNull();
        dto.Platoon.Should().NotBeNull();
        dto.Command.Should().NotBeNull();
        dto.AllPlatoons.Should().BeNull();
        dto.AllSquads.Should().BeNull();
    }

    [Fact]
    public async Task GetFrequencies_AsCommander_ReturnsCommandAllPlatoonsAllSquads()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FrequenciesDto>();
        dto!.Squad.Should().BeNull();
        dto.Platoon.Should().BeNull();
        dto.Command.Should().NotBeNull();
        dto.AllPlatoons.Should().NotBeNull();
        dto.AllSquads.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFrequencies_NonEventMember_Returns403()
    {
        var response = await _outsiderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencies_ReturnsCorrectSquadData()
    {
        // Pre-set frequencies on the squad
        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.SquadPrimaryFrequency = "148.500";
        squad.SquadBackupFrequency = "148.600";
        await db.SaveChangesAsync();

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");
        var dto = await response.Content.ReadFromJsonAsync<FrequenciesDto>();

        dto!.Squad!.Id.Should().Be(_squadId);
        dto.Squad.Name.Should().Be("Alpha Squad");
        dto.Squad.Primary.Should().Be("148.500");
        dto.Squad.Backup.Should().Be("148.600");
    }
}

// ── PATCH /api/squads/{squadId}/frequencies ────────────────────────────────────

[Trait("Category", "FREQ_PatchSquad")]
public class PatchSquadFrequenciesTests : FrequencyTestsBase
{
    public PatchSquadFrequenciesTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PatchSquadFrequencies_AsSquadLeader_OwnSquad_Returns204()
    {
        var response = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "148.500", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.SquadPrimaryFrequency.Should().Be("148.500");
        squad.SquadBackupFrequency.Should().BeNull();
    }

    [Fact]
    public async Task PatchSquadFrequencies_AsSquadLeader_DifferentSquad_Returns403()
    {
        var response = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "148.500", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchSquadFrequencies_AsPlatoonLeader_SquadInOwnPlatoon_Returns204()
    {
        // squad belongs to platoon1 and platoon leader is assigned to platoon1
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "148.700", backup = "148.750" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var squad = await db.Squads.FindAsync(_squadId);
        squad!.SquadPrimaryFrequency.Should().Be("148.700");
        squad.SquadBackupFrequency.Should().Be("148.750");
    }

    [Fact]
    public async Task PatchSquadFrequencies_AsPlatoonLeader_SquadInDifferentPlatoon_Returns403()
    {
        // squad2 belongs to platoon2, platoon leader is assigned to platoon1
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "148.800", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchSquadFrequencies_AsCommander_Returns204()
    {
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "147.000", backup = "147.050" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PatchSquadFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "148.500", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchSquadFrequencies_NonExistentSquad_Returns404()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/squads/{nonExistentId}/frequencies",
            new { primary = "148.500", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── PATCH /api/platoons/{platoonId}/frequencies ────────────────────────────────

[Trait("Category", "FREQ_PatchPlatoon")]
public class PatchPlatoonFrequenciesTests : FrequencyTestsBase
{
    public PatchPlatoonFrequenciesTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PatchPlatoonFrequencies_AsPlatoonLeader_OwnPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "148.100", backup = "148.150" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var platoon = await db.Platoons.FindAsync(_platoonId);
        platoon!.PlatoonPrimaryFrequency.Should().Be("148.100");
        platoon.PlatoonBackupFrequency.Should().Be("148.150");
    }

    [Fact]
    public async Task PatchPlatoonFrequencies_AsPlatoonLeader_DifferentPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoon2Id}/frequencies",
            new { primary = "148.200", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchPlatoonFrequencies_AsSquadLeader_Returns403()
    {
        // RequirePlatoonLeader policy blocks squad_leader
        var response = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "148.100", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchPlatoonFrequencies_AsCommander_Returns204()
    {
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoon2Id}/frequencies",
            new { primary = "148.300", backup = "148.350" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PatchPlatoonFrequencies_NonExistentPlatoon_Returns404()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/platoons/{nonExistentId}/frequencies",
            new { primary = "148.100", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── PATCH /api/factions/{factionId}/frequencies ────────────────────────────────

[Trait("Category", "FREQ_PatchFaction")]
public class PatchFactionFrequenciesTests : FrequencyTestsBase
{
    public PatchFactionFrequenciesTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PatchFactionFrequencies_AsCommander_OwnFaction_Returns204()
    {
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "147.000", backup = "147.500" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency.Should().Be("147.000");
        faction.CommandBackupFrequency.Should().Be("147.500");
    }

    [Fact]
    public async Task PatchFactionFrequencies_AsPlatoonLeader_Returns403()
    {
        // RequireFactionCommander policy blocks platoon_leader
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "147.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchFactionFrequencies_NonExistentFaction_Returns404()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/factions/{nonExistentId}/frequencies",
            new { primary = "147.000", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchFactionFrequencies_ClearsFrequencyWithNull()
    {
        // First set a frequency
        await _commanderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "147.000", backup = "147.500" });

        // Then clear it
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = (string?)null, backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var faction = await db.Factions.FindAsync(_factionId);
        faction!.CommandPrimaryFrequency.Should().BeNull();
        faction.CommandBackupFrequency.Should().BeNull();
    }
}
