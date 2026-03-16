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

namespace MilsimPlanning.Api.Tests.Player;

public class PlayerTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
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

    public PlayerTestsBase(PostgreSqlFixture fixture)
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

        var commanderUser = await CreateUserAsync($"cmdr-{Guid.NewGuid():N}@test.com", "faction_commander");
        _commanderUserId = commanderUser.Id;
        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, _commanderUserId, "faction_commander");

        var playerUser = await CreateUserAsync($"player-{Guid.NewGuid():N}@test.com", "player");
        _playerUserId = playerUser.Id;
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, _playerUserId, "player");

        _eventId = await SeedEventDataAsync(db, commanderUser, playerUser);
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> SeedEventDataAsync(AppDbContext db, AppUser commanderUser, AppUser playerUser)
    {
        var eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commanderUser.Id, EventId = eventId };
        var @event = new Event { Id = eventId, Name = "Player Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };
        db.Events.Add(@event);

        var platoon = new Platoon { FactionId = factionId, Name = "Alpha Platoon", Order = 1 };
        var squad = new Squad { PlatoonId = platoon.Id, Name = "Alpha-1", Order = 1 };
        platoon.Squads.Add(squad);
        db.Platoons.Add(platoon);

        db.EventMemberships.Add(new EventMembership { EventId = eventId, UserId = commanderUser.Id, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { EventId = eventId, UserId = playerUser.Id, Role = "player" });

        var eventPlayer = new EventPlayer
        {
            EventId = eventId,
            Email = playerUser.Email!,
            Name = "Test Player",
            Callsign = "WOLF-01",
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

    private static async IAsyncEnumerable<NotificationJob> EmptyQueueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { });
        yield break;
    }
}

[Trait("Category", "PLAY_Assignment")]
public class Player_AssignmentTests : PlayerTestsBase
{
    public Player_AssignmentTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetMyAssignment_ReturnsPlayerCallsignPlatoonSquad()
    {
        // Assign the player to a platoon + squad first
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.EventPlayers.FindAsync(_eventPlayerId);
            player!.PlatoonId = _platoonId;
            player.SquadId = _squadId;
            await db.SaveChangesAsync();
        }

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/my-assignment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("callsign").GetString().Should().Be("WOLF-01");
        body.GetProperty("isAssigned").GetBoolean().Should().BeTrue();
        body.GetProperty("platoon").GetProperty("id").GetGuid().Should().Be(_platoonId);
        body.GetProperty("squad").GetProperty("id").GetGuid().Should().Be(_squadId);
    }

    [Fact]
    public async Task GetMyAssignment_UnassignedPlayer_ReturnsIsAssignedFalse()
    {
        // Player in seed data has no platoon/squad assignment
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/my-assignment");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("isAssigned").GetBoolean().Should().BeFalse();
        body.GetProperty("platoon").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        body.GetProperty("squad").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

}
