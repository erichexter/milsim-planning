using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using System.Net.Http.Headers;
using Xunit;

namespace MilsimPlanning.Api.Tests.Notifications;

/// <summary>
/// Integration test stubs for Notification Blast API (NOTF-01..05).
/// All test bodies are Assert.True(true) — replaced in Plan 03-04.
/// </summary>
public class NotificationTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;

    public NotificationTestsBase(PostgreSqlFixture fixture)
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
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
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

        var commanderEmail = $"notf-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };

        var playerEmail = $"notf-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };

        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Notification Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };

        db.Events.Add(testEvent);
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        _commanderClient = _factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authService.GenerateJwt(commander, "faction_commander"));
        _playerClient = _factory.CreateClient();
        _playerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authService.GenerateJwt(player, "player"));
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
}

// ── NOTF_Blast ────────────────────────────────────────────────────────────────

[Trait("Category", "NOTF_Blast")]
public class NotificationBlastTests : NotificationTestsBase
{
    public NotificationBlastTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SendBlast_ValidRequest_Returns202()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task SendBlast_CreatesNotificationBlastRecord()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task GetBlastLog_ReturnsChronologicalList()
    {
        Assert.True(true);
    }
}

// ── NOTF_Squad ────────────────────────────────────────────────────────────────

[Trait("Category", "NOTF_Squad")]
public class SquadChangeNotificationTests : NotificationTestsBase
{
    public SquadChangeNotificationTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AssignSquad_PlayerWithAccount_EnqueuesSquadChangeJob()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task AssignSquad_PlayerWithoutAccount_DoesNotEnqueueJob()
    {
        Assert.True(true);
    }
}
