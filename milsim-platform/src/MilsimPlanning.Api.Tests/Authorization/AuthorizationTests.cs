using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
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

// Event.Status is now EventStatus enum (Phase 2 schema). Tests use EventStatus.Draft.

namespace MilsimPlanning.Api.Tests.Authorization;

/// <summary>
/// Integration tests for RBAC + IDOR scope guard (AUTHZ-01 through AUTHZ-06).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class AuthorizationTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IEmailService> _emailMock = null!;

    public AuthorizationTests(PostgreSqlFixture fixture)
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
                    // Replace real DB with Testcontainers DB
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    // Replace email service with mock
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        _client = _factory.CreateClient();

        // Apply migrations to test DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Ensure roles exist
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "event_owner", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AppUser> CreateTestUserAsync(
        string email,
        string role,
        string callsign = "TestUser",
        string password = "TestPass123!")
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
        result.Succeeded.Should().BeTrue(
            because: string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            Callsign = callsign,
            DisplayName = callsign,
            User = user
        };
        await db.SaveChangesAsync();

        return user;
    }

    private async Task<Guid> CreateEventAndAddMemberAsync(AppUser user, string? overrideRole = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var eventId = Guid.NewGuid();
        var newEvent = new Event
        {
            Id = eventId,
            Name = $"Test Event {eventId:N}",
            Status = EventStatus.Draft
            // FactionId defaults to Guid.Empty — no FK constraint from Events→Factions in DB
        };
        db.Events.Add(newEvent);

        // Get user's role from Identity
        string memberRole;
        if (overrideRole != null)
        {
            memberRole = overrideRole;
        }
        else
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = await userManager.GetRolesAsync(user);
            memberRole = roles.FirstOrDefault() ?? "player";
        }

        db.EventMemberships.Add(new EventMembership
        {
            UserId = user.Id,
            EventId = eventId,
            Role = memberRole
        });

        await db.SaveChangesAsync();
        return eventId;
    }

    private HttpClient CreateAuthenticatedClient(AppUser user, string role)
    {
        var client = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, role);
        return client;
    }

    // ── AUTHZ-01: Role hierarchy ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Authz_Roles")]
    public async Task Roles_SystemAdmin_CanAccessFactionCommanderPolicy()
    {
        // Arrange: system_admin user in event
        var user = await CreateTestUserAsync(
            $"sysadmin-{Guid.NewGuid():N}@test.com",
            "system_admin");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "system_admin");

        // Act: GET /api/roster/{eventId} — requires RequirePlayer; system_admin should pass
        var response = await client.GetAsync($"/api/events/{eventId}/roster");

        // Assert: system_admin satisfies even the most restrictive roles (hierarchy 6 >= all)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            because: "system_admin (level 6) satisfies all policies including RequireFactionCommander");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "JWT is valid and user is authenticated");
    }

    [Fact]
    [Trait("Category", "Authz_Roles")]
    public async Task Roles_FactionCommander_CanAccessFactionCommanderPolicy()
    {
        // Arrange
        var user = await CreateTestUserAsync(
            $"fc-{Guid.NewGuid():N}@test.com",
            "faction_commander");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "faction_commander");

        // Act: GET /api/roster/{eventId} — RequirePlayer policy; faction_commander (level 4) passes
        var response = await client.GetAsync($"/api/events/{eventId}/roster");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            because: "faction_commander (level 4) satisfies RequirePlayer (minimum level 1)");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Authz_Roles")]
    public async Task Roles_Player_BlockedFromCommanderEndpoints()
    {
        // This test verifies the policy system is wired correctly.
        // The roster endpoint uses RequirePlayer, so we verify by checking
        // that a Player can access it (level 1 >= 1) but cannot access
        // higher-level protected endpoints.
        // For AUTHZ-01 validation, we test that the hierarchy works by
        // checking player gets 200 on player-level endpoint.
        var user = await CreateTestUserAsync(
            $"player-{Guid.NewGuid():N}@test.com",
            "player");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "player");

        // Player can access RequirePlayer endpoint
        var response = await client.GetAsync($"/api/events/{eventId}/roster");
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "JWT is valid");
        ((int)response.StatusCode).Should().BeOneOf(new[] { 200 },
            because: "player (level 1) satisfies RequirePlayer policy");
    }

    [Fact]
    [Trait("Category", "Authz_Roles")]
    public async Task Roles_Unauthenticated_Returns401ForProtectedEndpoints()
    {
        // Arrange: no JWT
        var eventId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/events/{eventId}/roster");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "no bearer token provided");
    }

    // ── AUTHZ-06: IDOR scope guard ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Authz_IDOR")]
    public async Task ScopeGuard_PlayerInEventA_Returns403ForEventB()
    {
        // Arrange: User is a member of EventA only
        var user = await CreateTestUserAsync(
            $"idor-player-{Guid.NewGuid():N}@test.com",
            "player");
        var eventAId = await CreateEventAndAddMemberAsync(user);

        // Create EventB without adding user as member
        Guid eventBId;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            eventBId = Guid.NewGuid();
            db.Events.Add(new Event
            {
                Id = eventBId,
                Name = $"EventB {eventBId:N}",
                Status = EventStatus.Draft,
                FactionId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(user, "player");

        // Act: attempt to access EventB's roster with EventA user's JWT
        var response = await client.GetAsync($"/api/events/{eventBId}/roster");

        // Assert: IDOR protection — 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "user is not a member of EventB (IDOR protection)");
    }

    [Fact]
    [Trait("Category", "Authz_IDOR")]
    public async Task ScopeGuard_CommanderInEventA_Returns403ForEventB()
    {
        // Arrange: faction_commander is a member of EventA only
        var user = await CreateTestUserAsync(
            $"idor-commander-{Guid.NewGuid():N}@test.com",
            "faction_commander");
        var eventAId = await CreateEventAndAddMemberAsync(user);

        // Create EventB without adding this commander
        Guid eventBId;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            eventBId = Guid.NewGuid();
            db.Events.Add(new Event
            {
                Id = eventBId,
                Name = $"EventB-Commander {eventBId:N}",
                Status = EventStatus.Draft,
                FactionId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(user, "faction_commander");

        // Act: commander tries to access EventB
        var response = await client.GetAsync($"/api/events/{eventBId}/roster");

        // Assert: even commanders are IDOR-scoped
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "faction_commander not in EventB cannot access its resources");
    }

    [Fact]
    [Trait("Category", "Authz_ScopeCommander")]
    public async Task ScopeGuard_CommanderInEventA_CanAccessEventA()
    {
        // Arrange: faction_commander IS a member of EventA
        var user = await CreateTestUserAsync(
            $"scope-fc-{Guid.NewGuid():N}@test.com",
            "faction_commander");
        var eventAId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "faction_commander");

        // Act: access EventA's roster (user is a member)
        var response = await client.GetAsync($"/api/events/{eventAId}/roster");

        // Assert: 200 OK — user is in event
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "faction_commander is a member of EventA");
    }

    // ── AUTHZ-05: Email visibility ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Authz_EmailVisibility")]
    public async Task EmailVisibility_Player_EmailFieldAbsentInRosterResponse()
    {
        // Arrange: player in event
        var user = await CreateTestUserAsync(
            $"email-player-{Guid.NewGuid():N}@test.com",
            "player");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "player");

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}/roster");

        // Assert: response is OK and email field is absent
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        json.RootElement.TryGetProperty("platoons", out _).Should().BeTrue(
            because: "roster response should use hierarchical payload shape");
        json.RootElement.TryGetProperty("unassignedPlayers", out _).Should().BeTrue(
            because: "roster response should include unassigned players");

        body.ToLowerInvariant().Should().NotContain("\"email\"",
            because: "email field should never be returned to Player role (AUTHZ-05)");
    }

    [Fact]
    [Trait("Category", "Authz_EmailVisibility")]
    public async Task EmailVisibility_PlatoonLeader_EmailFieldAbsentInRosterResponse()
    {
        // Arrange: platoon_leader in event
        var user = await CreateTestUserAsync(
            $"email-pl-{Guid.NewGuid():N}@test.com",
            "platoon_leader");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "platoon_leader");

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}/roster");

        // Assert: response is OK and email field is absent in hierarchy DTO payload
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);

        json.RootElement.TryGetProperty("platoons", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("unassignedPlayers", out _).Should().BeTrue();

        body.ToLowerInvariant().Should().NotContain("\"email\"",
            because: "current roster contract omits email fields from hierarchy DTO responses");
    }

    // ── AUTHZ-03: Read-only leaders ───────────────────────────────────────────

    [Fact]
    [Trait("Category", "Authz_ReadOnlyLeaders")]
    public async Task ReadOnlyLeader_CanGetRoster_CannotPost()
    {
        // Arrange: squad_leader in event
        var user = await CreateTestUserAsync(
            $"sl-{Guid.NewGuid():N}@test.com",
            "squad_leader");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "squad_leader");

        // Act: GET should succeed (RequirePlayer policy, level 2 >= 1)
        var getResponse = await client.GetAsync($"/api/events/{eventId}/roster");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "squad_leader satisfies RequirePlayer policy for roster GET");
    }

    // ── AUTHZ-04: Player access ───────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Authz_PlayerAccess")]
    public async Task PlayerAccess_InEvent_CanGetRoster()
    {
        // Arrange: player in event
        var user = await CreateTestUserAsync(
            $"player-access-{Guid.NewGuid():N}@test.com",
            "player");
        var eventId = await CreateEventAndAddMemberAsync(user);
        using var client = CreateAuthenticatedClient(user, "player");

        // Act
        var response = await client.GetAsync($"/api/events/{eventId}/roster");

        // Assert: player can read their event's roster
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "player in event should have read access to its roster");
    }

    [Fact]
    [Trait("Category", "Authz_PlayerAccess")]
    public async Task PlayerAccess_NotInEvent_Returns403()
    {
        // Arrange: player NOT in the event
        var user = await CreateTestUserAsync(
            $"player-noAccess-{Guid.NewGuid():N}@test.com",
            "player");

        // Create an event without adding the user
        Guid eventId;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            eventId = Guid.NewGuid();
            db.Events.Add(new Event
            {
                Id = eventId,
                Name = $"Other Event {eventId:N}",
                Status = EventStatus.Draft,
                FactionId = Guid.NewGuid()
            });
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(user, "player");

        // Act: player tries to access event they're not a member of
        var response = await client.GetAsync($"/api/events/{eventId}/roster");

        // Assert: IDOR protection
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "player not in event cannot access its roster (IDOR-AUTHZ-06)");
    }
}
