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
        db.EventMemberships.Add(new EventMembership
        {
            EventId = eventId,
            UserId = commanderUser.Id,
            Role = "faction_commander"
        });
        db.EventMemberships.Add(new EventMembership
        {
            EventId = eventId,
            UserId = playerUser.Id,
            Role = "player"
        });

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

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(because: string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, role);

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            Callsign = email,
            DisplayName = email,
            User = user
        };
        await db.SaveChangesAsync();
        return user;
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
        throw new NotImplementedException("TODO: implement POST /roster-change-requests then make this pass");
    }

    [Fact]
    public async Task SubmitRequest_AlreadyPending_Returns409()
    {
        throw new NotImplementedException("TODO: implement duplicate pending guard then make this pass");
    }

    [Fact]
    public async Task SubmitRequest_PlayerRole_CancelRequest_Returns204()
    {
        throw new NotImplementedException("TODO: implement DELETE /roster-change-requests/{id} then make this pass");
    }

    [Fact]
    public async Task GetMine_WithPendingRequest_ReturnsRequest()
    {
        throw new NotImplementedException("TODO: implement GET /roster-change-requests/mine then make this pass");
    }

    [Fact]
    public async Task GetMine_NoPendingRequest_Returns204()
    {
        throw new NotImplementedException("TODO: implement GET /roster-change-requests/mine empty case then make this pass");
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
        throw new NotImplementedException("TODO: implement GET /roster-change-requests (commander) then make this pass");
    }

    [Fact]
    public async Task ListPending_PlayerRole_Returns403()
    {
        throw new NotImplementedException("TODO: implement commander-only policy on list endpoint then make this pass");
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
        throw new NotImplementedException("TODO: implement POST /{id}/approve updating PlatoonId/SquadId then make this pass");
    }

    [Fact]
    public async Task Approve_EnqueuesRosterChangeDecisionJob_WithApprovedDecision()
    {
        throw new NotImplementedException("TODO: implement notification enqueue after approve then make this pass");
    }

    [Fact]
    public async Task Deny_EnqueuesRosterChangeDecisionJob_WithDeniedDecision()
    {
        throw new NotImplementedException("TODO: implement notification enqueue after deny then make this pass");
    }

    [Fact]
    public async Task Approve_PlayerRole_Returns403()
    {
        throw new NotImplementedException("TODO: implement RequireFactionCommander policy on approve endpoint then make this pass");
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Returns422()
    {
        throw new NotImplementedException("TODO: implement Status != Pending guard then make this pass");
    }
}
