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

namespace MilsimPlanning.Api.Tests.ChannelAssignments;

/// <summary>
/// Integration tests for ChannelAssignment CRUD API.
/// Covers AC-04 (NATO range), AC-05 (25 kHz spacing), AC-07 (persistence),
/// AC-08 (list), AC-09 (edit/delete), auth, and IDOR.
/// </summary>
public class ChannelAssignmentTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _squadId;
    protected Guid _channelVhfId;
    protected Guid _channelUhfId;
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;

    public ChannelAssignmentTestsBase(PostgreSqlFixture fixture)
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

        // Commander
        var cmdEmail = $"ca-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Player
        var playerEmail = $"ca-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Seed event hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _squadId = Guid.NewGuid();

        var squad = new Squad { Id = _squadId, PlatoonId = _platoonId, Name = "Alpha-1", Order = 1 };
        var platoon = new Platoon { Id = _platoonId, FactionId = _factionId, Name = "Alpha Platoon", Order = 1, Squads = [squad] };
        var faction = new Faction { Id = _factionId, Name = "Blue Force", CommanderId = commander.Id, EventId = _eventId, Platoons = [platoon] };
        var testEvent = new Event { Id = _eventId, Name = "CA Test Event", Status = EventStatus.Draft, FactionId = _factionId, Faction = faction };

        db.Events.Add(testEvent);
        await db.SaveChangesAsync();

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        // Seed RadioChannels (bypassing soft-delete filter)
        _channelVhfId = Guid.NewGuid();
        _channelUhfId = Guid.NewGuid();

        await db.RadioChannels.AddRangeAsync(
            new RadioChannel { Id = _channelVhfId, EventId = _eventId, Name = "Command Net", Scope = ChannelScope.VHF, Order = 0 },
            new RadioChannel { Id = _channelUhfId, EventId = _eventId, Name = "Air Net", Scope = ChannelScope.UHF, Order = 1 }
        );
        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
}

// ── Frequency validation tests (AC-04, AC-05) ─────────────────────────────────

[Trait("Category", "ChannelAssignment_FrequencyValidation")]
public class ChannelAssignmentFrequencyValidationTests : ChannelAssignmentTestsBase
{
    public ChannelAssignmentFrequencyValidationTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateAssignment_VhfValidFrequency_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 30.025m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primaryFrequency").GetDecimal().Should().Be(30.025m);
    }

    [Fact]
    public async Task CreateAssignment_UhfValidFrequency_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelUhfId, squadId = _squadId, primaryFrequency = 225.025m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateAssignment_VhfOutOfRange_Returns422()
    {
        // 90.0 MHz is above VHF max of 87.975
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 90.0m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("out of range");
    }

    [Fact]
    public async Task CreateAssignment_UhfOutOfRange_Returns422()
    {
        // 100.0 MHz is below UHF min of 225
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelUhfId, squadId = _squadId, primaryFrequency = 100.0m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateAssignment_InvalidSpacing_Returns422()
    {
        // 30.012 is not a multiple of 0.025
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 30.012m });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("25 kHz");
    }

    [Fact]
    public async Task CreateAssignment_Boundary_VhfMax_Returns201()
    {
        // 87.975 is the VHF maximum — should succeed
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 87.975m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateAssignment_Boundary_VhfMin_Returns201()
    {
        // 30.0 is the VHF minimum — should succeed
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 30.0m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

// ── CRUD tests (AC-07, AC-08, AC-09) ─────────────────────────────────────────

[Trait("Category", "ChannelAssignment_Crud")]
public class ChannelAssignmentCrudTests : ChannelAssignmentTestsBase
{
    public ChannelAssignmentCrudTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateAssignment_AsCommander_Returns201WithCorrectBody()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 36.500m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("radioChannelId").GetGuid().Should().Be(_channelVhfId);
        body.GetProperty("squadId").GetGuid().Should().Be(_squadId);
        body.GetProperty("primaryFrequency").GetDecimal().Should().Be(36.500m);
        body.GetProperty("channelScope").GetString().Should().Be("VHF");
    }

    [Fact]
    public async Task GetAssignments_AsPlayer_ReturnsAssignmentList()
    {
        // Create one assignment first
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 40.025m });

        var response = await _playerClient.GetAsync(
            $"/api/events/{_eventId}/channel-assignments?limit=20&offset=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateAssignment_AsCommander_Returns200WithUpdatedFrequency()
    {
        // Create first
        var createResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 45.025m });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = created.GetProperty("id").GetGuid();

        // Update
        var updateResponse = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments/{assignmentId}",
            new { primaryFrequency = 45.050m });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("primaryFrequency").GetDecimal().Should().Be(45.050m);
    }

    [Fact]
    public async Task DeleteAssignment_AsCommander_Returns204()
    {
        // Create first
        var createResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 50.025m });

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = created.GetProperty("id").GetGuid();

        // Delete
        var deleteResponse = await _commanderClient.DeleteAsync(
            $"/api/events/{_eventId}/channel-assignments/{assignmentId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAssignment_ThenGet_IsExcludedFromList()
    {
        // Create
        var createResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 55.025m });

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = created.GetProperty("id").GetGuid();

        // Delete
        await _commanderClient.DeleteAsync($"/api/events/{_eventId}/channel-assignments/{assignmentId}");

        // Get and assert not in list
        var listResponse = await _commanderClient.GetAsync($"/api/events/{_eventId}/channel-assignments");
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotContain(i => i.GetProperty("id").GetGuid() == assignmentId);
    }

    [Fact]
    public async Task CreateAssignment_AsPlayer_Returns403()
    {
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 36.500m });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAssignments_IDOR_Returns403ForOtherEvent()
    {
        var otherEventId = Guid.NewGuid();
        var response = await _commanderClient.GetAsync($"/api/events/{otherEventId}/channel-assignments");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAssignments_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/events/{_eventId}/channel-assignments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonClient.Dispose();
    }

    [Fact]
    public async Task UpdateAssignment_WithInvalidFrequency_Returns422()
    {
        // Create valid assignment
        var createResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = _channelVhfId, squadId = _squadId, primaryFrequency = 60.025m });

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var assignmentId = created.GetProperty("id").GetGuid();

        // Update with invalid frequency
        var updateResponse = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments/{assignmentId}",
            new { primaryFrequency = 200.0m }); // out of VHF range

        updateResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateAssignment_NonExistentChannel_Returns404()
    {
        var fakeChannelId = Guid.NewGuid();
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/channel-assignments",
            new { radioChannelId = fakeChannelId, squadId = _squadId, primaryFrequency = 36.500m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
