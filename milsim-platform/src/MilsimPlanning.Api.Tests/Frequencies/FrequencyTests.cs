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

    protected HttpClient _commanderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _adminClient = null!;
    protected HttpClient _outsiderClient = null!;

    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoon1Id;
    protected Guid _platoon2Id;
    protected Guid _squad1Id;
    protected Guid _squad2Id;

    protected string _commanderUserId = null!;
    protected string _platoonLeaderUserId = null!;
    protected string _squadLeaderUserId = null!;
    protected string _playerUserId = null!;
    protected string _adminUserId = null!;

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
        var uid = Guid.NewGuid().ToString("N");

        // Create users
        var commander = await CreateUser(userManager, $"freq-cmdr-{uid}@test.com", "faction_commander");
        _commanderUserId = commander.Id;

        var platoonLeader = await CreateUser(userManager, $"freq-pl-{uid}@test.com", "platoon_leader");
        _platoonLeaderUserId = platoonLeader.Id;

        var squadLeader = await CreateUser(userManager, $"freq-sl-{uid}@test.com", "squad_leader");
        _squadLeaderUserId = squadLeader.Id;

        var player = await CreateUser(userManager, $"freq-player-{uid}@test.com", "player");
        _playerUserId = player.Id;

        var admin = await CreateUser(userManager, $"freq-admin-{uid}@test.com", "system_admin");
        _adminUserId = admin.Id;

        var outsider = await CreateUser(userManager, $"freq-outsider-{uid}@test.com", "player");

        // Seed event + faction + hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoon1Id = Guid.NewGuid();
        _platoon2Id = Guid.NewGuid();
        _squad1Id = Guid.NewGuid();
        _squad2Id = Guid.NewGuid();

        var faction = new Faction
        {
            Id = _factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId,
            CommandPrimaryFrequency = "100.0", CommandBackupFrequency = "100.5"
        };
        var testEvent = new Event
        {
            Id = _eventId, Name = "Freq Test Event", Status = EventStatus.Draft,
            FactionId = _factionId, Faction = faction
        };

        var platoon1 = new Platoon
        {
            Id = _platoon1Id, FactionId = _factionId, Name = "Platoon Alpha", Order = 1,
            PrimaryFrequency = "110.0", BackupFrequency = "110.5"
        };
        var platoon2 = new Platoon
        {
            Id = _platoon2Id, FactionId = _factionId, Name = "Platoon Bravo", Order = 2,
            PrimaryFrequency = "120.0", BackupFrequency = "120.5"
        };

        var squad1 = new Squad
        {
            Id = _squad1Id, PlatoonId = _platoon1Id, Name = "Squad 1", Order = 1,
            PrimaryFrequency = "111.0", BackupFrequency = "111.5"
        };
        var squad2 = new Squad
        {
            Id = _squad2Id, PlatoonId = _platoon2Id, Name = "Squad 2", Order = 1,
            PrimaryFrequency = "121.0", BackupFrequency = "121.5"
        };

        db.Events.Add(testEvent);
        db.Platoons.AddRange(platoon1, platoon2);
        db.Squads.AddRange(squad1, squad2);

        // Memberships for all users (except outsider)
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        db.EventMemberships.Add(new EventMembership { UserId = admin.Id, EventId = _eventId, Role = "system_admin" });

        // EventPlayers with squad/platoon assignments
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = player.Email!, Name = "Player", UserId = player.Id,
            SquadId = _squad1Id, PlatoonId = _platoon1Id
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = squadLeader.Email!, Name = "Squad Leader", UserId = squadLeader.Id,
            SquadId = _squad1Id, PlatoonId = _platoon1Id
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = platoonLeader.Email!, Name = "Platoon Leader", UserId = platoonLeader.Id,
            PlatoonId = _platoon1Id
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

    private static async Task<AppUser> CreateUser(UserManager<AppUser> userManager, string email, string role)
    {
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await userManager.CreateAsync(user, "TestPass123!");
        await userManager.AddToRoleAsync(user, role);
        user.Profile = new UserProfile { UserId = user.Id, Callsign = role, DisplayName = role, User = user };
        return user;
    }
}

// ── FREQ-01: GET /api/events/{eventId}/frequencies ──────────────────────────

[Trait("Category", "FREQ_Get")]
public class GetFrequencyTests : FrequencyTestsBase
{
    public GetFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsCommander_ReturnsAllTiers()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto.Should().NotBeNull();
        dto!.Command.Should().NotBeNull();
        dto.Command!.Primary.Should().Be("100.0");
        dto.Command.Backup.Should().Be("100.5");
        dto.Platoons.Should().HaveCount(2);
        dto.Squads.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFrequencies_AsSystemAdmin_ReturnsSameAsCommander()
    {
        var response = await _adminClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto!.Command.Should().NotBeNull();
        dto.Platoons.Should().HaveCount(2);
        dto.Squads.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsCommandAndOwnPlatoon()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto!.Command.Should().NotBeNull();
        dto.Platoons.Should().HaveCount(1);
        dto.Platoons[0].PlatoonId.Should().Be(_platoon1Id);
        dto.Squads.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsOwnSquadAndPlatoon()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto!.Command.Should().BeNull();
        dto.Platoons.Should().HaveCount(1);
        dto.Platoons[0].PlatoonId.Should().Be(_platoon1Id);
        dto.Squads.Should().HaveCount(1);
        dto.Squads[0].SquadId.Should().Be(_squad1Id);
    }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsOnlyOwnSquad()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<EventFrequenciesDto>();
        dto!.Command.Should().BeNull();
        dto.Platoons.Should().BeEmpty();
        dto.Squads.Should().HaveCount(1);
        dto.Squads[0].SquadId.Should().Be(_squad1Id);
        dto.Squads[0].Primary.Should().Be("111.0");
    }

    [Fact]
    public async Task GetFrequencies_AsNonMember_Returns403()
    {
        var response = await _outsiderClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencies_NonexistentEvent_Returns404()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{Guid.NewGuid()}/frequencies");
        // ScopeGuard throws ForbiddenException for non-member events
        // but if user is commander with no membership for random event, it returns 403
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }
}

// ── FREQ-02: PUT /api/squads/{squadId}/frequencies ──────────────────────────

[Trait("Category", "FREQ_SquadUpdate")]
public class UpdateSquadFrequencyTests : FrequencyTestsBase
{
    public UpdateSquadFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateSquadFrequency_AsSquadLeaderOfSquad_Returns204()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsSquadLeaderOfDifferentSquad_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlatoonLeaderContainingSquad_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlatoonLeaderNotContainingSquad_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/squads/{_squad2Id}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        // Player doesn't have RequireSquadLeader policy — returns 403 at auth layer
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_ClearFrequencies_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squad1Id}/frequencies",
            new { primary = (string?)null, backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_NonexistentSquad_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{Guid.NewGuid()}/frequencies",
            new { primary = "150.0", backup = "150.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── FREQ-03: PUT /api/platoons/{platoonId}/frequencies ──────────────────────

[Trait("Category", "FREQ_PlatoonUpdate")]
public class UpdatePlatoonFrequencyTests : FrequencyTestsBase
{
    public UpdatePlatoonFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsPlatoonLeaderOfPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon1Id}/frequencies",
            new { primary = "200.0", backup = "200.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsPlatoonLeaderOfDifferentPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon2Id}/frequencies",
            new { primary = "200.0", backup = "200.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon1Id}/frequencies",
            new { primary = "200.0", backup = "200.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon1Id}/frequencies",
            new { primary = "200.0", backup = "200.5" });

        // squad_leader doesn't have RequirePlatoonLeader policy
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/platoons/{_platoon1Id}/frequencies",
            new { primary = "200.0", backup = "200.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_NonexistentPlatoon_Returns404()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{Guid.NewGuid()}/frequencies",
            new { primary = "200.0", backup = "200.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── FREQ-04: PUT /api/events/{eventId}/command-frequencies ──────────────────

[Trait("Category", "FREQ_CommandUpdate")]
public class UpdateCommandFrequencyTests : FrequencyTestsBase
{
    public UpdateCommandFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateCommandFrequency_AsCommander_Returns204()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "300.0", backup = "300.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsSystemAdmin_Returns204()
    {
        var response = await _adminClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "300.0", backup = "300.5" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsPlatoonLeader_Returns403()
    {
        var response = await _platoonLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "300.0", backup = "300.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "300.0", backup = "300.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCommandFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/command-frequencies",
            new { primary = "300.0", backup = "300.5" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCommandFrequency_NonexistentEvent_Returns404OrForbidden()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{Guid.NewGuid()}/command-frequencies",
            new { primary = "300.0", backup = "300.5" });

        // ScopeGuard may return 403 for non-member event
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }
}
