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

namespace MilsimPlanning.Api.Tests.RadioChannels;

/// <summary>
/// Integration tests for radio channel CRUD API (Story 1).
/// Covers AC-01 through AC-08: create, list, edit, duplicate name validation, VHF/UHF scope.
/// </summary>
public class RadioChannelTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;
    protected Guid _factionId;
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;

    public RadioChannelTestsBase(PostgreSqlFixture fixture)
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
        var cmdEmail = $"rc-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Player
        var playerEmail = $"rc-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Seed event
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();

        var faction = new Faction { Id = _factionId, Name = "Blue Force", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "RC Test Event", Status = EventStatus.Draft, FactionId = _factionId, Faction = faction };

        db.Events.Add(testEvent);
        await db.SaveChangesAsync();

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
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

public class RadioChannelTests : RadioChannelTestsBase
{
    public RadioChannelTests(PostgreSqlFixture fixture) : base(fixture) { }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateChannel_AsCommander_ReturnsCreatedChannel()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Command Net", scope = "VHF" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Command Net");
        body.GetProperty("scope").GetString().Should().Be("VHF");
    }

    [Fact]
    public async Task CreateChannel_WithUHFScope_ReturnsUHFChannel()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Air Net", scope = "UHF" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("scope").GetString().Should().Be("UHF");
    }

    [Fact]
    public async Task CreateChannel_WithInvalidScope_Returns400()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Bad Scope Channel", scope = "INVALID" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateChannel_AsPlayer_Returns403()
    {
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Unauthorized Channel", scope = "VHF" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateChannel_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Anon Channel", scope = "VHF" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonClient.Dispose();
    }

    // ── AC-05: Duplicate name validation ──────────────────────────────────────

    [Fact]
    public async Task CreateChannel_DuplicateName_Returns409WithMessage()
    {
        // Create first
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Dup Net", scope = "VHF" });

        // Try duplicate
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Dup Net", scope = "UHF" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("Channel name already exists");
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListChannels_AsPlayer_ReturnsChannels()
    {
        // Create a channel first
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "List Test Net", scope = "VHF" });

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/radio-channels");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task ListChannels_IDOR_Returns403ForOtherEvent()
    {
        var otherEventId = Guid.NewGuid();
        var response = await _commanderClient.GetAsync($"/api/events/{otherEventId}/radio-channels");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListChannels_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/events/{_eventId}/radio-channels");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonClient.Dispose();
    }

    // ── AC-07: Edit channel name ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateChannel_AsCommander_ReturnsUpdatedChannel()
    {
        // Create
        var createResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Edit Me Net", scope = "VHF" });

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var channelId = created.GetProperty("id").GetGuid();

        // Update
        var updateResponse = await _commanderClient.PatchAsJsonAsync(
            $"/api/radio-channels/{channelId}",
            new { name = "Renamed Net", scope = "UHF" });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Renamed Net");
        body.GetProperty("scope").GetString().Should().Be("UHF");
    }

    [Fact]
    public async Task UpdateChannel_DuplicateName_Returns409()
    {
        // Create two channels
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "First Net", scope = "VHF" });

        var secondResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Second Net", scope = "VHF" });

        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = second.GetProperty("id").GetGuid();

        // Try to rename second to first's name
        var updateResponse = await _commanderClient.PatchAsJsonAsync(
            $"/api/radio-channels/{secondId}",
            new { name = "First Net", scope = "VHF" });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateChannel_AsPlayer_Returns403()
    {
        // Create channel as commander
        var createResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = "Player Edit Test", scope = "VHF" });

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var channelId = created.GetProperty("id").GetGuid();

        // Player tries to update
        var updateResponse = await _playerClient.PatchAsJsonAsync(
            $"/api/radio-channels/{channelId}",
            new { name = "Hacked Name", scope = "VHF" });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateChannel_NonExistent_Returns404()
    {
        var fakeId = Guid.NewGuid();
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/radio-channels/{fakeId}",
            new { name = "Ghost Channel", scope = "VHF" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── AC-06 + AC-08: Channel list shows name and scope ──────────────────────

    [Fact]
    public async Task CreateChannel_ThenList_AppearsWithNameAndScope()
    {
        var uniqueName = $"List Verify {Guid.NewGuid():N}";

        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/radio-channels",
            new { name = uniqueName, scope = "UHF" });

        var listResponse = await _commanderClient.GetAsync($"/api/events/{_eventId}/radio-channels");
        var channels = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

        var found = channels.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("name").GetString() == uniqueName);

        found.ValueKind.Should().Be(JsonValueKind.Object);
        found.GetProperty("scope").GetString().Should().Be("UHF");
    }
}
