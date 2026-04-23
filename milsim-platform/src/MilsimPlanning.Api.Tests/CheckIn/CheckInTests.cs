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
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.CheckIn;

/// <summary>
/// Integration tests for kiosk check-in endpoint (POST /api/events/{eventId}/check-in/record-scan).
/// Tests QR validation, duplicate detection, and error handling.
/// </summary>
public class CheckInTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // Authenticated clients
    protected HttpClient _staffClient = null!;
    protected AppUser _staffUser = null!;

    protected HttpClient _playerClient = null!;
    protected AppUser _playerUser = null!;

    // Test event and participants
    protected Event _event = null!;
    protected EventPlayer _participant1 = null!;
    protected EventPlayer _participant2 = null!;

    public CheckInTestsBase(PostgreSqlFixture fixture)
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
                    services.AddSingleton(_emailMock);

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(IntegrationTestAuthHandler.SchemeName, _ => { });
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

        // Create staff user (faction commander)
        _staffUser = await CreateUserAsync($"staff-{Guid.NewGuid():N}@test.com", "faction_commander");
        _staffClient = CreateAuthenticatedClient(_staffUser, "faction_commander");

        // Create player user
        _playerUser = await CreateUserAsync($"player-{Guid.NewGuid():N}@test.com", "player");
        _playerClient = CreateAuthenticatedClient(_playerUser, "player");

        // Set up test event with hierarchy
        await SeedEventWithParticipantsAsync();
    }

    public Task DisposeAsync()
    {
        _staffClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    protected HttpClient CreateAuthenticatedClient(AppUser user, string role)
    {
        var client = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, role);
        return client;
    }

    protected async Task SeedEventWithParticipantsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create event
        _event = new Event
        {
            Name = "Kiosk Test Event",
            Location = "Test Location",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            Status = EventStatus.Draft,
            Faction = new Faction { CommanderId = _staffUser.Id, Name = "Test Faction" }
        };
        db.Events.Add(_event);
        await db.SaveChangesAsync();

        // Create event membership for staff
        db.EventMemberships.Add(new EventMembership
        {
            UserId = _staffUser.Id,
            EventId = _event.Id,
            Role = "faction_commander",
            JoinedAt = DateTime.UtcNow
        });

        // Create hierarchy: Platoon → Squad → Participants
        var platoon = new Platoon
        {
            FactionId = _event.Faction.Id,
            Name = "Alpha Platoon",
            Order = 1
        };
        var squad = new Squad { PlatoonId = platoon.Id, Name = "Alpha-1", Order = 1 };
        db.Platoons.Add(platoon);
        db.Squads.Add(squad);
        await db.SaveChangesAsync();

        // Create participants
        _participant1 = new EventPlayer
        {
            EventId = _event.Id,
            Name = "John Doe",
            Email = "john@test.com",
            PlatoonId = platoon.Id,
            SquadId = squad.Id
        };
        _participant2 = new EventPlayer
        {
            EventId = _event.Id,
            Name = "Jane Smith",
            Email = "jane@test.com",
            PlatoonId = platoon.Id,
            SquadId = squad.Id
        };
        db.EventPlayers.Add(_participant1);
        db.EventPlayers.Add(_participant2);
        await db.SaveChangesAsync();
    }
}

// ── AC-01, AC-02: Success Case ────────────────────────────────────────────────

[Trait("Category", "CheckIn_Success")]
public class CheckInSuccessTests : CheckInTestsBase
{
    public CheckInSuccessTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordScan_ValidQrCode_Returns201Created()
    {
        var request = new RecordScanRequest(_participant1.Id.ToString());
        var response = await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var dto = await response.Content.ReadFromJsonAsync<CheckInRecordDto>();
        dto.Should().NotBeNull();
        dto!.ParticipantId.Should().Be(_participant1.Id);
        dto.Name.Should().Be("John Doe");
        dto.Faction.Should().Be("Test Faction");
        dto.ScannedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordScan_CreatesCheckInRecord()
    {
        var request = new RecordScanRequest(_participant1.Id.ToString());
        await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);

        // Verify check-in was recorded in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var checkIn = await db.EventParticipantCheckIns
            .FirstOrDefaultAsync(c => c.EventId == _event.Id && c.ParticipantId == _participant1.Id);

        checkIn.Should().NotBeNull();
        checkIn!.QrCodeValue.Should().Be(_participant1.Id.ToString());
        checkIn.ScannedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

// ── AC-03: Duplicate Detection ────────────────────────────────────────────────

[Trait("Category", "CheckIn_Duplicate")]
public class CheckInDuplicateTests : CheckInTestsBase
{
    public CheckInDuplicateTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordScan_DuplicateCheckIn_Returns400()
    {
        var request = new RecordScanRequest(_participant1.Id.ToString());
        
        // First check-in succeeds
        var first = await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second check-in returns 400
        var second = await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await second.Content.ReadFromJsonAsync<dynamic>();
        error.Should().NotBeNull();
    }
}

// ── AC-04: Not Found Scenarios ────────────────────────────────────────────────

[Trait("Category", "CheckIn_NotFound")]
public class CheckInNotFoundTests : CheckInTestsBase
{
    public CheckInNotFoundTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordScan_NonExistentParticipant_Returns404()
    {
        var fakeParticipantId = Guid.NewGuid();
        var request = new RecordScanRequest(fakeParticipantId.ToString());
        
        var response = await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordScan_NonExistentEvent_Returns404()
    {
        var fakeEventId = Guid.NewGuid();
        var request = new RecordScanRequest(_participant1.Id.ToString());
        
        var response = await _staffClient.PostAsJsonAsync(
            $"/api/events/{fakeEventId}/check-in/record-scan",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── AC-05: Authorization ──────────────────────────────────────────────────────

[Trait("Category", "CheckIn_Authorization")]
public class CheckInAuthorizationTests : CheckInTestsBase
{
    public CheckInAuthorizationTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordScan_UnauthorizedUser_Returns403()
    {
        // Create a user not in the event
        var otherUser = await CreateUserAsync($"other-{Guid.NewGuid():N}@test.com", "faction_commander");
        var otherClient = CreateAuthenticatedClient(otherUser, "faction_commander");

        var request = new RecordScanRequest(_participant1.Id.ToString());
        var response = await otherClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── AC-01: Input Validation ───────────────────────────────────────────────────

[Trait("Category", "CheckIn_Validation")]
public class CheckInValidationTests : CheckInTestsBase
{
    public CheckInValidationTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RecordScan_EmptyQrCode_Returns400()
    {
        var request = new RecordScanRequest("");
        var response = await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecordScan_InvalidQrCodeFormat_Returns400()
    {
        var request = new RecordScanRequest("not-a-guid");
        var response = await _staffClient.PostAsJsonAsync(
            $"/api/events/{_event.Id}/check-in/record-scan",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
