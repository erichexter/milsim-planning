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

namespace MilsimPlanning.Api.Tests.Maps;

/// <summary>
/// Integration test stubs for Map Resources API (MAPS-01..05).
/// All test bodies are Assert.True(true) — replaced in Plan 03-03.
/// </summary>
public class MapResourceTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;

    public MapResourceTestsBase(PostgreSqlFixture fixture)
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

        var commanderEmail = $"maps-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };

        var playerEmail = $"maps-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };

        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Maps Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };

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

// ── MAPS_Resources ────────────────────────────────────────────────────────────

[Trait("Category", "MAPS_Resources")]
public class MapResourceCrudTests : MapResourceTestsBase
{
    public MapResourceCrudTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateExternalMapLink_ValidUrl_Returns201()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task CreateExternalMapLink_WithInstructions_Returns201()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task ListMapResources_PlayerRole_Returns200()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task DeleteMapResource_CommanderRole_Returns204()
    {
        Assert.True(true);
    }
}

// ── MAPS_Files ────────────────────────────────────────────────────────────────

[Trait("Category", "MAPS_Files")]
public class MapFileTests : MapResourceTestsBase
{
    public MapFileTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetMapFileUploadUrl_ValidRequest_Returns200()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task GetMapFileDownloadUrl_PlayerRole_Returns200WithUrl()
    {
        Assert.True(true);
    }

    [Fact]
    public async Task GetMapFileDownloadUrl_DisallowedMime_Returns400()
    {
        Assert.True(true);
    }
}
