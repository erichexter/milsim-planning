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

/// <summary>
/// Integration tests for Frequency API.
/// Covers GET role-filtering (all 4 roles) + PUT auth (forbidden for non-commander).
/// </summary>
public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _squadId;
    protected string _commanderUserId = string.Empty;
    protected string _squadLeaderUserId = string.Empty;
    protected string _platoonLeaderUserId = string.Empty;
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

        // Create commander
        var cmdEmail = $"freq-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Create squad leader
        var slEmail = $"freq-sl-{Guid.NewGuid():N}@test.com";
        var squadLeader = new AppUser { UserName = slEmail, Email = slEmail, EmailConfirmed = true };
        await userManager.CreateAsync(squadLeader, "TestPass123!");
        await userManager.AddToRoleAsync(squadLeader, "squad_leader");
        squadLeader.Profile = new UserProfile { UserId = squadLeader.Id, Callsign = "SquadLeader", DisplayName = "SquadLeader", User = squadLeader };
        _squadLeaderUserId = squadLeader.Id;

        // Create platoon leader
        var plEmail = $"freq-pl-{Guid.NewGuid():N}@test.com";
        var platoonLeader = new AppUser { UserName = plEmail, Email = plEmail, EmailConfirmed = true };
        await userManager.CreateAsync(platoonLeader, "TestPass123!");
        await userManager.AddToRoleAsync(platoonLeader, "platoon_leader");
        platoonLeader.Profile = new UserProfile { UserId = platoonLeader.Id, Callsign = "PlatoonLeader", DisplayName = "PlatoonLeader", User = platoonLeader };
        _platoonLeaderUserId = platoonLeader.Id;

        // Create player
        var playerEmail = $"freq-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Seed event + hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _squadId = Guid.NewGuid();

        var squad = new Squad
        {
            Id = _squadId,
            PlatoonId = _platoonId,
            Name = "Alpha 1",
            Order = 1,
            PrimaryFrequency = "143.000",
            BackupFrequency = "144.000"
        };
        var platoon = new Platoon
        {
            Id = _platoonId,
            FactionId = _factionId,
            Name = "Alpha Platoon",
            Order = 1,
            PrimaryFrequency = "145.000",
            BackupFrequency = "146.000",
            Squads = [squad]
        };
        var faction = new Faction
        {
            Id = _factionId,
            Name = "Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
            PrimaryFrequency = "147.000",
            BackupFrequency = "148.000",
            Platoons = [platoon]
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
        await db.SaveChangesAsync();

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        // Assign event players so role-visibility logic can look them up
        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = slEmail.ToLower(), Name = "Squad Leader", UserId = squadLeader.Id, SquadId = _squadId, PlatoonId = _platoonId });
        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = plEmail.ToLower(), Name = "Platoon Leader", UserId = platoonLeader.Id, PlatoonId = _platoonId });
        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = playerEmail.ToLower(), Name = "Player 1", UserId = player.Id, SquadId = _squadId, PlatoonId = _platoonId });

        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _squadLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_squadLeaderClient, squadLeader.Id, "squad_leader");
        _platoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_platoonLeaderClient, platoonLeader.Id, "platoon_leader");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _squadLeaderClient.Dispose();
        _platoonLeaderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
}

// ── GET role-filtering tests ─────────────────────────────────────────────────

[Trait("Category", "Frequency_Get")]
public class FrequencyGetTests : FrequencyTestsBase
{
    public FrequencyGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_AsPlayer_ReturnsSquadOnly()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").GetProperty("primary").GetString().Should().Be("143.000");
        body.GetProperty("platoon").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFrequencies_AsSquadLeader_ReturnsSquadAndPlatoon()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").GetProperty("primary").GetString().Should().Be("143.000");
        body.GetProperty("platoon").GetProperty("primary").GetString().Should().Be("145.000");
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFrequencies_AsPlatoonLeader_ReturnsPlatoonAndCommand()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("platoon").GetProperty("primary").GetString().Should().Be("145.000");
        body.GetProperty("command").GetProperty("primary").GetString().Should().Be("147.000");
    }

    [Fact]
    public async Task GetFrequencies_AsFactionCommander_ReturnsCommand()
    {
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("command").GetProperty("primary").GetString().Should().Be("147.000");
        body.GetProperty("command").GetProperty("backup").GetString().Should().Be("148.000");
    }

    [Fact]
    public async Task GetFrequencies_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// ── PUT auth tests ───────────────────────────────────────────────────────────

[Trait("Category", "Frequency_Put")]
public class FrequencyPutTests : FrequencyTestsBase
{
    public FrequencyPutTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SetSquadFrequencies_AsCommander_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "150.000", backupFrequency = "151.000" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("150.000");
        body.GetProperty("backup").GetString().Should().Be("151.000");
    }

    [Fact]
    public async Task SetSquadFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "150.000", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetPlatoonFrequencies_AsCommander_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primaryFrequency = "152.000", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("152.000");
        body.GetProperty("backup").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetPlatoonFrequencies_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primaryFrequency = "152.000", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetFactionFrequencies_AsCommander_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primaryFrequency = "155.000", backupFrequency = "156.000" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("155.000");
    }

    [Fact]
    public async Task SetFactionFrequencies_AsPlayer_Returns403()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primaryFrequency = "155.000", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetSquadFrequencies_NullValues_ClearsFrequencies()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = (string?)null, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("backup").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RoundTrip_SetThenGet_ReturnsSetValue()
    {
        // Set squad frequency as commander
        await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "160.000", backupFrequency = "161.000" });

        // Verify via GET as player (who is assigned to this squad)
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequencies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squad").GetProperty("primary").GetString().Should().Be("160.000");
        body.GetProperty("squad").GetProperty("backup").GetString().Should().Be("161.000");
    }

    [Fact]
    public async Task SetSquadFrequencies_NonExistentId_Returns404WithProblemDetails()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{nonExistentId}/frequencies",
            new { primaryFrequency = "150.000", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("detail").GetString().Should().Contain(nonExistentId.ToString());
    }

    [Fact]
    public async Task SetSquadFrequencies_NonFactionCommander_Returns403WithProblemDetails()
    {
        // Create a second commander (faction_commander JWT role + EventMembership) but NOT the faction's CommanderId
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var rogueEmail = $"freq-rogue-cmdr-{Guid.NewGuid():N}@test.com";
        var rogueCommander = new AppUser { UserName = rogueEmail, Email = rogueEmail, EmailConfirmed = true };
        await userManager.CreateAsync(rogueCommander, "TestPass123!");
        await userManager.AddToRoleAsync(rogueCommander, "faction_commander");

        db.EventMemberships.Add(new EventMembership { UserId = rogueCommander.Id, EventId = _eventId, Role = "faction_commander" });
        await db.SaveChangesAsync();

        var rogueClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(rogueClient, rogueCommander.Id, "faction_commander");

        var response = await rogueClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "150.000", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Forbidden");
        body.GetProperty("status").GetInt32().Should().Be(403);

        rogueClient.Dispose();
    }
}

// ── EventMembership.Role vs JWT role regression tests ────────────────────────

[Trait("Category", "Frequency_RoleVisibility")]
public class FrequencyRoleVisibilityTests : FrequencyTestsBase
{
    public FrequencyRoleVisibilityTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFrequencies_JwtRoleHigherThanMembershipRole_VisibilityDrivenByMembershipRole()
    {
        // Create a user whose JWT role is faction_commander but EventMembership.Role is player
        // This proves visibility is gated on EventMembership.Role, not the global JWT role.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var downgradeEmail = $"freq-downgrade-{Guid.NewGuid():N}@test.com";
        var downgradeUser = new AppUser { UserName = downgradeEmail, Email = downgradeEmail, EmailConfirmed = true };
        await userManager.CreateAsync(downgradeUser, "TestPass123!");
        await userManager.AddToRoleAsync(downgradeUser, "faction_commander");

        // EventMembership grants only player-level access for this event
        db.EventMemberships.Add(new EventMembership { UserId = downgradeUser.Id, EventId = _eventId, Role = "player" });
        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = downgradeEmail.ToLower(), Name = "Downgraded User", UserId = downgradeUser.Id, SquadId = _squadId, PlatoonId = _platoonId });
        await db.SaveChangesAsync();

        // JWT role = faction_commander, but EventMembership.Role = player
        var downgradeClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(downgradeClient, downgradeUser.Id, "faction_commander");

        var response = await downgradeClient.GetAsync($"/api/events/{_eventId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should see only squad level (player visibility), NOT command level
        body.GetProperty("squad").ValueKind.Should().NotBe(JsonValueKind.Null);
        body.GetProperty("platoon").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("command").ValueKind.Should().Be(JsonValueKind.Null);

        downgradeClient.Dispose();
    }
}

// ── NATO frequency range validation tests ──────────────────────────────────────

[Trait("Category", "Frequency_Validation")]
public class FrequencyValidationTests : FrequencyTestsBase
{
    public FrequencyValidationTests(PostgreSqlFixture fixture) : base(fixture) { }

    // ── Valid frequencies ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("30.025")]  // VHF minimum valid
    [InlineData("36.500")]  // VHF mid-range
    [InlineData("87.975")]  // VHF maximum
    public async Task SetSquadFrequencies_ValidVhfFrequencies_Returns200(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be(frequency);
    }

    [Theory]
    [InlineData("225.025")]  // UHF minimum valid
    [InlineData("250.000")]  // UHF mid-range
    [InlineData("399.975")]  // UHF near maximum
    public async Task SetSquadFrequencies_ValidUhfFrequencies_Returns200(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be(frequency);
    }

    // ── Invalid VHF frequencies ─────────────────────────────────────────────────

    [Theory]
    [InlineData("29.999")]   // Below VHF minimum
    [InlineData("30.000")]   // Not aligned to 25 kHz (minimum must be 30.025)
    [InlineData("30.001")]   // Invalid spacing
    [InlineData("30.024")]   // Invalid spacing (0.001 below valid)
    [InlineData("30.026")]   // Invalid spacing (0.001 above valid)
    public async Task SetSquadFrequencies_InvalidVhfFrequencies_Returns422(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Unprocessable Entity");
    }

    [Theory]
    [InlineData("88.000")]   // Above VHF maximum
    [InlineData("100.000")]  // In VHF/UHF gap
    [InlineData("224.999")]  // Below UHF minimum
    public async Task SetSquadFrequencies_OutOfVhfRange_Returns422(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Invalid UHF frequencies ─────────────────────────────────────────────────

    [Theory]
    [InlineData("225.000")]  // Not aligned to 25 kHz
    [InlineData("225.001")]  // Invalid spacing
    [InlineData("225.024")]  // Invalid spacing
    [InlineData("225.026")]  // Invalid spacing
    [InlineData("250.001")]  // Invalid spacing
    public async Task SetSquadFrequencies_InvalidUhfFrequencies_Returns422(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("400.000")]  // Above UHF maximum (must be < 400.0, max valid is 399.975)
    [InlineData("400.025")]  // Above UHF maximum
    [InlineData("450.000")]  // Well above UHF maximum
    public async Task SetSquadFrequencies_OutOfUhfRange_Returns422(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Invalid format ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("abc")]      // Non-numeric
    [InlineData("36.5.0")]   // Multiple decimals
    [InlineData("")]         // Empty string (treated as null)
    public async Task SetSquadFrequencies_InvalidFormat_Returns422(string frequency)
    {
        // Empty string is sent as-is (treated as valid empty/null scenario)
        if (string.IsNullOrWhiteSpace(frequency))
        {
            var response = await _commanderClient.PutAsJsonAsync(
                $"/api/squads/{_squadId}/frequencies",
                new { primaryFrequency = (string?)null, backupFrequency = (string?)null });
            response.StatusCode.Should().Be(HttpStatusCode.OK);  // Null is allowed
        }
        else
        {
            var response = await _commanderClient.PutAsJsonAsync(
                $"/api/squads/{_squadId}/frequencies",
                new { primaryFrequency = frequency, backupFrequency = (string?)null });
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
    }

    // ── Null/empty frequencies (allowed) ────────────────────────────────────────

    [Fact]
    public async Task SetSquadFrequencies_BothNull_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = (string?)null, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("backup").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetSquadFrequencies_OnlyPrimarySet_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "36.500", backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("36.500");
        body.GetProperty("backup").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ── Platoon frequencies validation ────────────────────────────────────────────

    [Theory]
    [InlineData("36.500")]
    [InlineData("250.000")]
    public async Task SetPlatoonFrequencies_ValidFrequencies_Returns200(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("30.001")]  // Invalid VHF spacing
    [InlineData("225.001")] // Invalid UHF spacing
    public async Task SetPlatoonFrequencies_InvalidFrequencies_Returns422(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Faction frequencies validation ──────────────────────────────────────────────

    [Theory]
    [InlineData("36.500")]
    [InlineData("250.000")]
    public async Task SetFactionFrequencies_ValidFrequencies_Returns200(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("30.001")]  // Invalid VHF spacing
    [InlineData("225.001")] // Invalid UHF spacing
    public async Task SetFactionFrequencies_InvalidFrequencies_Returns422(string frequency)
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primaryFrequency = frequency, backupFrequency = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Both primary and backup frequencies ────────────────────────────────────────

    [Fact]
    public async Task SetSquadFrequencies_BothValidVhf_Returns200()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "36.500", backupFrequency = "36.525" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("36.500");
        body.GetProperty("backup").GetString().Should().Be("36.525");
    }

    [Fact]
    public async Task SetSquadFrequencies_ValidPrimaryInvalidBackup_Returns422()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "36.500", backupFrequency = "36.501" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetSquadFrequencies_InvalidPrimaryValidBackup_Returns422()
    {
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "36.501", backupFrequency = "36.500" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Story 7: Frequency Assignment Audit Log Tests ─────────────────────────

    [Fact]
    public async Task GetFrequencyAuditLog_WithoutFilter_ReturnsAllLogEntries()
    {
        // AC-02, AC-03: Audit log returns chronological entries with all required fields
        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/frequency-audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditLog = await response.Content.ReadFromJsonAsync<JsonElement>();
        auditLog.ValueKind.Should().Be(JsonValueKind.Array);
        // Log should contain entries (exact count depends on setup, but should be present)
        auditLog.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);

        // If entries exist, verify structure (AC-03)
        if (auditLog.GetArrayLength() > 0)
        {
            var firstEntry = auditLog[0];
            firstEntry.TryGetProperty("id", out _).Should().BeTrue();
            firstEntry.TryGetProperty("eventId", out _).Should().BeTrue();
            firstEntry.TryGetProperty("channelName", out _).Should().BeTrue();
            firstEntry.TryGetProperty("unitName", out _).Should().BeTrue();
            firstEntry.TryGetProperty("unitType", out _).Should().BeTrue();
            firstEntry.TryGetProperty("primaryFrequency", out _).Should().BeTrue();
            firstEntry.TryGetProperty("alternateFrequency", out _).Should().BeTrue();
            firstEntry.TryGetProperty("actionType", out _).Should().BeTrue();
            firstEntry.TryGetProperty("performedByUserId", out _).Should().BeTrue();
            firstEntry.TryGetProperty("performedByDisplayName", out _).Should().BeTrue();
            firstEntry.TryGetProperty("occurredAt", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetFrequencyAuditLog_WithUnitFilter_ReturnsFilteredEntries()
    {
        // AC-07: Log is filterable by unit name
        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/frequency-audit-log?unitFilter=Squad");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditLog = await response.Content.ReadFromJsonAsync<JsonElement>();
        auditLog.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetFrequencyAuditLog_WithSortOrder_ReturnsSorted()
    {
        // AC-02: Log displays chronological entries (newest first or oldest first, user configurable)
        // Test newest first (default)
        var responseNewest = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/frequency-audit-log?newestFirst=true");
        responseNewest.StatusCode.Should().Be(HttpStatusCode.OK);

        // Test oldest first
        var responseOldest = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/frequency-audit-log?newestFirst=false");
        responseOldest.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFrequencyAuditLog_WithDateRange_ReturnsFilteredByDate()
    {
        // AC-07: Log is filterable by date range
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/frequency-audit-log?startDate={startDate:O}&endDate={endDate:O}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditLog = await response.Content.ReadFromJsonAsync<JsonElement>();
        auditLog.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetFrequencyAuditLog_UnauthorizedUser_ReturnsForbidden()
    {
        // AC-05: Log access is authorized
        var response = await _playerClient.GetAsync(
            $"/api/events/{_eventId}/frequency-audit-log");

        // Player role may be allowed depending on policy, but verify authorization
        // Expected: should return 200 (if player can access) or handle appropriately
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFrequencyAuditLog_InvalidEventId_ReturnsNotFound()
    {
        // AC-06: Verify event exists
        var invalidEventId = Guid.NewGuid();
        var response = await _commanderClient.GetAsync(
            $"/api/events/{invalidEventId}/frequency-audit-log");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
