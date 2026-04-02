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

namespace MilsimPlanning.Api.Tests.Frequency;

/// <summary>
/// Base fixture for frequency integration tests.
/// Seeds: faction_commander, platoon_leader (with EventPlayer.PlatoonId),
/// squad_leader (with EventPlayer.SquadId), player (with EventPlayer.SquadId), outsider.
/// </summary>
public class FrequencyTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    protected HttpClient _commanderClient = null!;
    protected HttpClient _platoonLeaderClient = null!;
    protected HttpClient _squadLeaderClient = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _outsiderClient = null!;

    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _squadId;

    // A second squad in the same platoon (for cross-platoon/squad IDOR tests)
    protected Guid _otherSquadId;
    // A second platoon (for cross-platoon IDOR tests)
    protected Guid _otherPlatoonId;

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

        var commander = await CreateUser(userManager, "freq-cmdr", "faction_commander");
        var platoonLeader = await CreateUser(userManager, "freq-pl", "platoon_leader");
        var squadLeader = await CreateUser(userManager, "freq-sl", "squad_leader");
        var player = await CreateUser(userManager, "freq-player", "player");
        var outsider = await CreateUser(userManager, "freq-outsider", "player");

        // Seed hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _squadId = Guid.NewGuid();
        _otherSquadId = Guid.NewGuid();
        _otherPlatoonId = Guid.NewGuid();

        var faction = new Faction { Id = _factionId, Name = "Freq Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Freq Test Event", Status = EventStatus.Draft, FactionId = _factionId, Faction = faction };

        db.Events.Add(testEvent);

        var platoon = new Platoon { Id = _platoonId, FactionId = _factionId, Name = "Alpha Platoon", Order = 1 };
        var otherPlatoon = new Platoon { Id = _otherPlatoonId, FactionId = _factionId, Name = "Bravo Platoon", Order = 2 };
        db.Platoons.Add(platoon);
        db.Platoons.Add(otherPlatoon);

        var squad = new Squad { Id = _squadId, PlatoonId = _platoonId, Name = "Alpha Squad", Order = 1 };
        var otherSquad = new Squad { Id = _otherSquadId, PlatoonId = _otherPlatoonId, Name = "Bravo Squad", Order = 1 };
        db.Squads.Add(squad);
        db.Squads.Add(otherSquad);

        // EventMemberships (event-level access)
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = platoonLeader.Id, EventId = _eventId, Role = "platoon_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = squadLeader.Id, EventId = _eventId, Role = "squad_leader" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        // EventPlayers (hierarchy-level placement)
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = platoonLeader.Email!, Name = "PL One",
            UserId = platoonLeader.Id, PlatoonId = _platoonId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = squadLeader.Email!, Name = "SL One",
            UserId = squadLeader.Id, PlatoonId = _platoonId, SquadId = _squadId
        });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = _eventId, Email = player.Email!, Name = "Player One",
            UserId = player.Id, PlatoonId = _platoonId, SquadId = _squadId
        });

        await db.SaveChangesAsync();

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

    private static async Task<AppUser> CreateUser(UserManager<AppUser> um, string prefix, string role)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.com";
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await um.CreateAsync(user, "TestPass123!");
        await um.AddToRoleAsync(user, role);
        user.Profile = new UserProfile { UserId = user.Id, Callsign = prefix, DisplayName = prefix, User = user };
        return user;
    }
}
