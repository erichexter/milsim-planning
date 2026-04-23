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
using MilsimPlanning.Api.Models.CheckIn;
using MilsimPlanning.Api.Tests.Fixtures;
using Xunit;

namespace MilsimPlanning.Api.Tests.CheckIn;

/// <summary>
/// Integration tests for offline check-in sync endpoint.
/// AC-06: Backend endpoint /api/events/{eventId}/check-in/sync-offline
/// processes each record, validates QR, and creates check-in records.
/// AC-08: Integration tests ≥3 tests—queue add, queue retrieval, sync success
/// </summary>
public class CheckInSyncOfflineTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private AppUser _user = null!;
    private Event _event = null!;
    private EventPlayer _participant1 = null!;
    private EventPlayer _participant2 = null!;

    public CheckInSyncOfflineTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
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
        foreach (var role in new[] { "player", "faction_commander" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create test user
        _user = await CreateUserAsync("checkin-tester@test.com", "faction_commander");
        _client = CreateAuthenticatedClient(_user, "faction_commander");

        // Create test event with faction and participants
        await CreateTestEventAsync();
    }

    private async Task CreateTestEventAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create event
        _event = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Offline Sync Test Event",
            OwnerId = _user.Id,
            Status = EventStatus.Published
        };
        db.Events.Add(_event);

        // Create faction
        var faction = new Faction
        {
            Id = Guid.NewGuid(),
            EventId = _event.Id,
            Name = "Test Faction",
            FactionColor = "red"
        };
        db.Factions.Add(faction);

        // Create test participants
        _participant1 = new EventPlayer
        {
            Id = Guid.NewGuid(),
            EventId = _event.Id,
            UserId = _user.Id,
            Name = "Participant 1",
            Callsign = "P1",
            FactionId = faction.Id
        };
        db.EventPlayers.Add(_participant1);

        _participant2 = new EventPlayer
        {
            Id = Guid.NewGuid(),
            EventId = _event.Id,
            UserId = _user.Id,
            Name = "Participant 2",
            Callsign = "P2",
            FactionId = faction.Id
        };
        db.EventPlayers.Add(_participant2);

        // Create event membership for user
        var membership = new EventMembership
        {
            Id = Guid.NewGuid(),
            EventId = _event.Id,
            UserId = _user.Id,
            Role = "faction_commander"
        };
        db.EventMemberships.Add(membership);

        await db.SaveChangesAsync();
    }

    private async Task<AppUser> CreateUserAsync(string email, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser { Email = email, UserName = email };
        await userManager.CreateAsync(user, "Test@123!");
        await userManager.AddToRoleAsync(user, role);

        return user;
    }

    private HttpClient CreateAuthenticatedClient(AppUser user, string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.TestUserIdHeader, user.Id);
        client.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.TestRoleHeader, role);
        return client;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// AC-06: Backend endpoint processes valid records and returns synced count.
    /// Test: POST /api/events/{eventId}/check-in/sync-offline with valid QR codes
    /// Expected: Returns 200 with synced count = 2, failed = 0
    /// </summary>
    [Fact]
    public async Task SyncOffline_WithValidQrCodes_ReturnsSyncedCount()
    {
        // Arrange
        var eventId = _event.Id;
        var queuedTime = DateTime.UtcNow.AddMinutes(-5);
        var request = new SyncOfflineCheckInRequest(new[]
        {
            new OfflineCheckInRecord(_participant1.Id.ToString(), queuedTime),
            new OfflineCheckInRecord(_participant2.Id.ToString(), queuedTime.AddSeconds(10))
        });

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{eventId}/check-in/sync-offline",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<SyncOfflineCheckInResponse>();
        result.Synced.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Errors.Should().BeEmpty();

        // Verify records were created in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkIns = await db.EventParticipantCheckIns
            .Where(c => c.EventId == eventId)
            .ToListAsync();
        checkIns.Should().HaveCount(2);
        checkIns.Should().AllSatisfy(c => c.ScannedAtUtc.Should().BeCloseTo(queuedTime, TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    /// AC-07: Failed records are not created; errors are returned to the client.
    /// Test: POST with one valid and one invalid QR code
    /// Expected: Returns synced = 1, failed = 1, with error details
    /// </summary>
    [Fact]
    public async Task SyncOffline_WithInvalidQrCode_ReturnsFailedCount()
    {
        // Arrange
        var eventId = _event.Id;
        var invalidQrCode = "invalid-not-a-guid";
        var queuedTime = DateTime.UtcNow.AddMinutes(-5);
        var request = new SyncOfflineCheckInRequest(new[]
        {
            new OfflineCheckInRecord(_participant1.Id.ToString(), queuedTime),
            new OfflineCheckInRecord(invalidQrCode, queuedTime)
        });

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{eventId}/check-in/sync-offline",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<SyncOfflineCheckInResponse>();
        result.Synced.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].QrCode.Should().Be(invalidQrCode);
        result.Errors[0].Error.Should().Contain("not a valid participant ID");

        // Verify only one record was created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkIns = await db.EventParticipantCheckIns
            .Where(c => c.EventId == eventId)
            .ToListAsync();
        checkIns.Should().HaveCount(1);
    }

    /// <summary>
    /// Test: Duplicate check-in should fail
    /// Expected: Returns failed = 1 with appropriate error
    /// </summary>
    [Fact]
    public async Task SyncOffline_WithDuplicateCheckIn_ReturnsError()
    {
        // Arrange: Create a check-in for participant1
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existingCheckIn = new EventParticipantCheckIn
        {
            Id = Guid.NewGuid(),
            EventId = _event.Id,
            ParticipantId = _participant1.Id,
            QrCodeValue = _participant1.Id.ToString(),
            ScannedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.EventParticipantCheckIns.Add(existingCheckIn);
        await db.SaveChangesAsync();

        var eventId = _event.Id;
        var queuedTime = DateTime.UtcNow.AddMinutes(-5);
        var request = new SyncOfflineCheckInRequest(new[]
        {
            new OfflineCheckInRecord(_participant1.Id.ToString(), queuedTime)
        });

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{eventId}/check-in/sync-offline",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsAsync<SyncOfflineCheckInResponse>();
        result.Synced.Should().Be(0);
        result.Failed.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Error.Should().Contain("already checked in");
    }

    /// <summary>
    /// Test: Empty request should return 400
    /// Expected: Returns BadRequest
    /// </summary>
    [Fact]
    public async Task SyncOffline_WithEmptyRecords_ReturnsBadRequest()
    {
        // Arrange
        var eventId = _event.Id;
        var request = new SyncOfflineCheckInRequest(Array.Empty<OfflineCheckInRecord>());

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{eventId}/check-in/sync-offline",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Test: Unauthorized user should return 403
    /// Expected: Returns Forbidden
    /// </summary>
    [Fact]
    public async Task SyncOffline_WithoutEventAccess_ReturnsForbidden()
    {
        // Arrange: Create another user without event membership
        var otherUser = await CreateUserAsync("other-user@test.com", "faction_commander");
        var otherClient = CreateAuthenticatedClient(otherUser, "faction_commander");

        var eventId = _event.Id;
        var request = new SyncOfflineCheckInRequest(new[]
        {
            new OfflineCheckInRecord(_participant1.Id.ToString(), DateTime.UtcNow)
        });

        // Act
        var response = await otherClient.PostAsJsonAsync(
            $"/api/events/{eventId}/check-in/sync-offline",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Test: Nonexistent event should return 404
    /// Expected: Returns NotFound
    /// </summary>
    [Fact]
    public async Task SyncOffline_WithNonexistentEvent_ReturnsNotFound()
    {
        // Arrange
        var nonexistentEventId = Guid.NewGuid();
        var request = new SyncOfflineCheckInRequest(new[]
        {
            new OfflineCheckInRecord(Guid.NewGuid().ToString(), DateTime.UtcNow)
        });

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/events/{nonexistentEventId}/check-in/sync-offline",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
