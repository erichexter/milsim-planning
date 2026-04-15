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

namespace MilsimPlanning.Api.Tests.Rsvp;

public class RsvpTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _playerClient = null!;
    protected HttpClient _commanderClient = null!;
    protected Guid _eventId;
    protected string _playerUserId = string.Empty;
    protected string _commanderUserId = string.Empty;

    // A user with EventMembership but NO EventPlayer record
    protected HttpClient _memberOnlyClient = null!;
    protected string _memberOnlyUserId = string.Empty;

    // A system_admin for testing 404 (bypasses ScopeGuard)
    protected HttpClient _adminClient = null!;
    protected string _adminUserId = string.Empty;

    public RsvpTestsBase(PostgreSqlFixture fixture)
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
        var cmdEmail = $"rsvp-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Create player (with EventPlayer record — can RSVP)
        var playerEmail = $"rsvp-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Create member-only user (has EventMembership but NO EventPlayer)
        var memberEmail = $"rsvp-member-{Guid.NewGuid():N}@test.com";
        var memberOnly = new AppUser { UserName = memberEmail, Email = memberEmail, EmailConfirmed = true };
        await userManager.CreateAsync(memberOnly, "TestPass123!");
        await userManager.AddToRoleAsync(memberOnly, "player");
        memberOnly.Profile = new UserProfile { UserId = memberOnly.Id, Callsign = "MemberOnly", DisplayName = "MemberOnly", User = memberOnly };
        _memberOnlyUserId = memberOnly.Id;

        // Create system_admin (bypasses ScopeGuard — used for 404 tests)
        var adminEmail = $"rsvp-admin-{Guid.NewGuid():N}@test.com";
        var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        await userManager.CreateAsync(admin, "TestPass123!");
        await userManager.AddToRoleAsync(admin, "system_admin");
        admin.Profile = new UserProfile { UserId = admin.Id, Callsign = "Admin", DisplayName = "Admin", User = admin };
        _adminUserId = admin.Id;

        // Seed event
        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction
        {
            Id = factionId,
            Name = "RSVP Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
        };
        var testEvent = new Event
        {
            Id = _eventId,
            Name = "RSVP Test Event",
            Status = EventStatus.Draft,
            FactionId = factionId,
            Faction = faction
        };

        db.Events.Add(testEvent);
        await db.SaveChangesAsync();

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        db.EventMemberships.Add(new EventMembership { UserId = memberOnly.Id, EventId = _eventId, Role = "player" });

        // Only the player gets an EventPlayer record (faction assignment)
        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = playerEmail.ToLower(), Name = "Player 1", UserId = player.Id });

        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
        _memberOnlyClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_memberOnlyClient, memberOnly.Id, "player");
        _adminClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_adminClient, admin.Id, "system_admin");
    }

    public Task DisposeAsync()
    {
        _playerClient.Dispose();
        _commanderClient.Dispose();
        _memberOnlyClient.Dispose();
        _adminClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
}

[Trait("Category", "RSVP_Put")]
public class RsvpPutTests : RsvpTestsBase
{
    public RsvpPutTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SetRsvp_Attending_Returns200WithDto()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Attending" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("eventId").GetString().Should().Be(_eventId.ToString());
        body.GetProperty("userId").GetString().Should().Be(_playerUserId);
        body.GetProperty("status").GetString().Should().Be("Attending");
        body.GetProperty("respondedAt").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SetRsvp_Maybe_Returns200()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Maybe" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Maybe");
    }

    [Fact]
    public async Task SetRsvp_NotAttending_Returns200()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "NotAttending" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("NotAttending");
    }

    [Fact]
    public async Task SetRsvp_Upsert_UpdatesExistingRsvp()
    {
        // First RSVP
        await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Attending" });

        // Update RSVP
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Maybe" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Maybe");
    }

    [Fact]
    public async Task SetRsvp_InvalidStatus_Returns400()
    {
        var response = await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Invalid" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Bad Request");
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("detail").GetString().Should().Contain("Invalid RSVP status");
    }

    [Fact]
    public async Task SetRsvp_NoEventPlayer_Returns403()
    {
        var response = await _memberOnlyClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Attending" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Forbidden");
        body.GetProperty("status").GetInt32().Should().Be(403);
        body.GetProperty("detail").GetString().Should().Contain("not assigned to a faction");
    }

    [Fact]
    public async Task SetRsvp_NoEventMembership_Returns403()
    {
        // Create a user with no membership at all
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var outsiderEmail = $"rsvp-outsider-{Guid.NewGuid():N}@test.com";
        var outsider = new AppUser { UserName = outsiderEmail, Email = outsiderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(outsider, "TestPass123!");
        await userManager.AddToRoleAsync(outsider, "player");

        var outsiderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(outsiderClient, outsider.Id, "player");

        var response = await outsiderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Attending" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        outsiderClient.Dispose();
    }

    [Fact]
    public async Task SetRsvp_NonExistentEvent_Returns404()
    {
        // system_admin bypasses ScopeGuard, allowing us to reach the 404 path
        var nonExistentId = Guid.NewGuid();
        var response = await _adminClient.PutAsJsonAsync(
            $"/api/events/{nonExistentId}/rsvp",
            new { status = "Attending" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("detail").GetString().Should().Contain(nonExistentId.ToString());
    }

    [Fact]
    public async Task SetRsvp_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Attending" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

[Trait("Category", "RSVP_Get")]
public class RsvpGetTests : RsvpTestsBase
{
    public RsvpGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetRsvp_AfterSet_ReturnsDto()
    {
        // Set RSVP first
        await _playerClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/rsvp",
            new { status = "Attending" });

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/rsvp");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("eventId").GetString().Should().Be(_eventId.ToString());
        body.GetProperty("userId").GetString().Should().Be(_playerUserId);
        body.GetProperty("status").GetString().Should().Be("Attending");
    }

    [Fact]
    public async Task GetRsvp_BeforeSet_ReturnsNull()
    {
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/rsvp");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("null");
    }

    [Fact]
    public async Task GetRsvp_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/events/{_eventId}/rsvp");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
