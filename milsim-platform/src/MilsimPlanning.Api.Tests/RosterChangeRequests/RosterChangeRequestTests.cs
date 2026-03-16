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
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.RosterChangeRequests;

/// <summary>
/// Integration tests for Roster Change Request API (RCHG-01..05).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class RosterChangeRequestTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected Mock<INotificationQueue> _queueMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;
    protected Guid _eventId;
    protected Guid _platoonId;
    protected Guid _squadId;
    protected Guid _eventPlayerId;

    public RosterChangeRequestTestsBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _queueMock = new Mock<INotificationQueue>();
        _queueMock.Setup(q => q.EnqueueAsync(It.IsAny<NotificationJob>(), It.IsAny<CancellationToken>()))
                  .Returns(ValueTask.CompletedTask);
        _queueMock.Setup(q => q.ReadAllAsync(It.IsAny<CancellationToken>()))
                  .Returns((CancellationToken ct) => EmptyQueueAsync(ct));

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

                    services.RemoveAll<INotificationQueue>();
                    services.AddSingleton(_queueMock.Object);
                    services.AddSingleton(_queueMock); // so tests can verify via mock

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Ensure roles exist
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create commander user
        var commanderUser = await CreateUserAsync($"cmdr-{Guid.NewGuid():N}@test.com", "faction_commander");
        _commanderUserId = commanderUser.Id;
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, _commanderUserId, "faction_commander");

        // Create player user
        var playerUser = await CreateUserAsync($"player-{Guid.NewGuid():N}@test.com", "player");
        _playerUserId = playerUser.Id;
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, _playerUserId, "player");

        // Seed: create event, faction, platoon, squad, EventMemberships, EventPlayer
        _eventId = await SeedEventDataAsync(db, commanderUser, playerUser);
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedEventDataAsync(AppDbContext db, AppUser commanderUser, AppUser playerUser)
    {
        // Create faction + event (Event.FactionId is a data column; Faction.EventId owns the 1:1 FK)
        var eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commanderUser.Id, EventId = eventId };
        var @event = new Event { Id = eventId, Name = "Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };
        db.Events.Add(@event);

        // Create platoon + squad
        var platoon = new Platoon { FactionId = factionId, Name = "Alpha Platoon", Order = 1 };
        var squad = new Squad { PlatoonId = platoon.Id, Name = "Alpha-1", Order = 1 };
        platoon.Squads.Add(squad);
        db.Platoons.Add(platoon);

        // Create EventMemberships (scope guard check)
        db.EventMemberships.Add(new EventMembership { EventId = eventId, UserId = commanderUser.Id, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { EventId = eventId, UserId = playerUser.Id, Role = "player" });

        // Create EventPlayer for the player
        var eventPlayer = new EventPlayer
        {
            EventId = eventId,
            Email = playerUser.Email!,
            Name = playerUser.Email!,
            UserId = playerUser.Id,
            PlatoonId = null,
            SquadId = null
        };
        db.EventPlayers.Add(eventPlayer);

        await db.SaveChangesAsync();

        _platoonId = platoon.Id;
        _squadId = squad.Id;
        _eventPlayerId = eventPlayer.Id;

        return eventId;
    }

    protected async Task<AppUser> CreateUserAsync(string email, string role, string password = "TestPass123!")
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(because: string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);

        user.Profile = new UserProfile { UserId = user.Id, Callsign = email, DisplayName = email, User = user };
        await db.SaveChangesAsync();
        return user;
    }

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>Helper: submit a change request as the player and return the request ID.</summary>
    protected async Task<Guid> SubmitChangeRequestAsync(string note = "Please move me to Alpha Squad")
    {
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests",
            new { note });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async IAsyncEnumerable<NotificationJob> EmptyQueueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { });
        yield break;
    }
}

// ── RCHG_Submit ───────────────────────────────────────────────────────────────

[Trait("Category", "RCHG_Submit")]
public class RosterChangeRequest_SubmitTests : RosterChangeRequestTestsBase
{
    public RosterChangeRequest_SubmitTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SubmitRequest_ValidNote_Returns201()
    {
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests",
            new { note = "I'd like to move to Alpha Squad please" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task SubmitRequest_AlreadyPending_Returns409()
    {
        // First submit succeeds
        await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests",
            new { note = "First request" });

        // Second submit returns 409 Conflict
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests",
            new { note = "Second request" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SubmitRequest_PlayerRole_CancelRequest_Returns204()
    {
        // Submit first
        var requestId = await SubmitChangeRequestAsync();

        // Cancel it
        var deleteResponse = await _playerClient.DeleteAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMine_WithPendingRequest_ReturnsRequest()
    {
        // Submit a request
        await SubmitChangeRequestAsync("Get me to Alpha Squad");

        // Get mine
        var response = await _playerClient.GetAsync(
            $"/api/events/{_eventId}/roster-change-requests/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Pending");
        body.GetProperty("note").GetString().Should().Be("Get me to Alpha Squad");
    }

    [Fact]
    public async Task GetMine_NoPendingRequest_Returns204()
    {
        // No request submitted — fresh player
        var anotherPlayer = await CreateUserAsync($"no-req-{Guid.NewGuid():N}@test.com", "player");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EventMemberships.Add(new EventMembership { EventId = _eventId, UserId = anotherPlayer.Id, Role = "player" });
        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = anotherPlayer.Email!, Name = "Another", UserId = anotherPlayer.Id });
        await db.SaveChangesAsync();

        var freshClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(freshClient, anotherPlayer.Id, "player");

        var response = await freshClient.GetAsync(
            $"/api/events/{_eventId}/roster-change-requests/mine");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

// ── RCHG_Review ──────────────────────────────────────────────────────────────

[Trait("Category", "RCHG_Review")]
public class RosterChangeRequest_ReviewTests : RosterChangeRequestTestsBase
{
    public RosterChangeRequest_ReviewTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListPending_CommanderRole_ReturnsPendingRequests()
    {
        // Submit a request as player
        await SubmitChangeRequestAsync("Move me to Alpha");

        // List as commander
        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/roster-change-requests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var requests = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        requests.Should().NotBeNull();
        requests!.Should().HaveCountGreaterThanOrEqualTo(1);
        requests!.Any(r => r.GetProperty("note").GetString() == "Move me to Alpha").Should().BeTrue();
    }

    [Fact]
    public async Task ListPending_PlayerRole_Returns403()
    {
        var response = await _playerClient.GetAsync(
            $"/api/events/{_eventId}/roster-change-requests");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── RCHG_Decision ────────────────────────────────────────────────────────────

[Trait("Category", "RCHG_Decision")]
public class RosterChangeRequest_DecisionTests : RosterChangeRequestTestsBase
{
    public RosterChangeRequest_DecisionTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Approve_UpdatesEventPlayerAssignment()
    {
        var requestId = await SubmitChangeRequestAsync("Move me to Alpha-1");

        var approveBody = new { platoonId = _platoonId, squadId = _squadId, commanderNote = (string?)null };
        var approveResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve", approveBody);

        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify EventPlayer updated in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.EventPlayers.FindAsync(_eventPlayerId);
        player!.SquadId.Should().Be(_squadId);
        player.PlatoonId.Should().Be(_platoonId);
    }

    [Fact]
    public async Task Approve_EnqueuesRosterChangeDecisionJob_WithApprovedDecision()
    {
        var requestId = await SubmitChangeRequestAsync("Move me to Alpha-1");

        var approveBody = new { platoonId = _platoonId, squadId = _squadId, commanderNote = (string?)null };
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve", approveBody);

        // Verify notification enqueued with "approved" decision
        var mockQueue = _factory.Services.GetRequiredService<Mock<INotificationQueue>>();
        mockQueue.Verify(q => q.EnqueueAsync(
            It.Is<RosterChangeDecisionJob>(j => j.Decision == "approved"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deny_EnqueuesRosterChangeDecisionJob_WithDeniedDecision()
    {
        var requestId = await SubmitChangeRequestAsync("Move me to Alpha-1");

        var denyBody = new { commanderNote = "Not approved at this time." };
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}/deny", denyBody);

        // Verify notification enqueued with "denied" decision
        var mockQueue = _factory.Services.GetRequiredService<Mock<INotificationQueue>>();
        mockQueue.Verify(q => q.EnqueueAsync(
            It.Is<RosterChangeDecisionJob>(j => j.Decision == "denied"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_PlayerRole_Returns403()
    {
        var requestId = await SubmitChangeRequestAsync();

        var approveBody = new { platoonId = _platoonId, squadId = _squadId };
        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve", approveBody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Returns422()
    {
        var requestId = await SubmitChangeRequestAsync();

        // First approval succeeds
        var approveBody = new { platoonId = _platoonId, squadId = _squadId };
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve", approveBody);

        // Second approval should return 422
        var secondApprove = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-requests/{requestId}/approve", approveBody);

        secondApprove.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
