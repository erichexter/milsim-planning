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
using MilsimPlanning.Api.Models.AuditLogs;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.AuditLogs;

/// <summary>
/// Integration tests for Frequency Audit Log API.
/// Covers GET audit log with filtering, sorting, and pagination (AC-02, AC-07).
/// Covers audit log creation on frequency changes (AC-03, AC-06).
/// </summary>
public class AuditLogTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;
    protected Guid _factionId;
    protected Guid _platoonId;
    protected Guid _squadId;
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;

    public AuditLogTestsBase(PostgreSqlFixture fixture)
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
        var cmdEmail = $"audit-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Create player
        var playerEmail = $"audit-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        // Seed event + hierarchy
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();
        _platoonId = Guid.NewGuid();
        _squadId = Guid.NewGuid();

        var squad = new Squad
        {
            Id = _squadId,
            PlatoonId = _platoonId,
            Name = "Alpha 1",
            Order = 1,
            PrimaryFrequency = null,
            BackupFrequency = null
        };
        var platoon = new Platoon
        {
            Id = _platoonId,
            FactionId = _factionId,
            Name = "Alpha Platoon",
            Order = 1,
            PrimaryFrequency = null,
            BackupFrequency = null,
            Squads = [squad]
        };
        var faction = new Faction
        {
            Id = _factionId,
            Name = "Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
            PrimaryFrequency = null,
            BackupFrequency = null,
            Platoons = [platoon]
        };
        var testEvent = new Event
        {
            Id = _eventId,
            Name = "Audit Log Test Event",
            Status = EventStatus.Draft,
            FactionId = _factionId,
            Faction = faction
        };

        db.Events.Add(testEvent);
        await db.SaveChangesAsync();

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });

        db.EventPlayers.Add(new EventPlayer { EventId = _eventId, Email = playerEmail.ToLower(), Name = "Player 1", UserId = player.Id, SquadId = _squadId, PlatoonId = _platoonId });

        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_commanderClient, commander.Id, "faction_commander");
        _playerClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_playerClient, player.Id, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }
}

// ── Audit log query tests ────────────────────────────────────────────────────

[Trait("Category", "AuditLog_Get")]
public class AuditLogGetTests : AuditLogTestsBase
{
    public AuditLogGetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAuditLogs_EmptyEvent_ReturnsEmptyList()
    {
        // AC-06: Log entries exist and survive sessions
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuditLogResponse>();
        body.Should().NotBeNull();
        body!.Entries.Should().BeEmpty();
        body.Total.Should().Be(0);
    }

    [Fact]
    public async Task GetAuditLogs_WithFrequencyChanges_ReturnsChronologicalEntries()
    {
        // AC-02: Log displays chronological entries (newest first by default)
        // AC-03: Each log entry shows timestamp, unit name, frequencies, action type, user

        // Create a frequency assignment via the Frequency API
        var request = new { primaryFrequency = "143.500", backupFrequency = "144.500" };
        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Query audit log
        var auditResponse = await _commanderClient.GetAsync($"/api/events/{_eventId}/audit-logs");
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await auditResponse.Content.ReadFromJsonAsync<AuditLogResponse>();

        body.Should().NotBeNull();
        body!.Entries.Should().HaveCount(1);
        body.Total.Should().Be(1);

        var entry = body.Entries[0];
        entry.UnitName.Should().Be("Alpha 1");
        entry.UnitType.Should().Be("Squad");
        entry.PrimaryFrequency.Should().Be("143.500");
        entry.AlternateFrequency.Should().Be("144.500");
        entry.ActionType.Should().Be("created"); // First assignment
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAuditLogs_UpdatesFrequency_ShowsUpdatedAction()
    {
        // AC-03: Action type for updates
        // Create initial frequency
        var request1 = new { primaryFrequency = "143.500", backupFrequency = "144.500" };
        await _commanderClient.PutAsJsonAsync($"/api/squads/{_squadId}/frequencies", request1);

        // Update frequency
        var request2 = new { primaryFrequency = "143.600", backupFrequency = "144.600" };
        await _commanderClient.PutAsJsonAsync($"/api/squads/{_squadId}/frequencies", request2);

        // Query audit log
        var auditResponse = await _commanderClient.GetAsync($"/api/events/{_eventId}/audit-logs?sortOrder=asc");
        var body = await auditResponse.Content.ReadFromJsonAsync<AuditLogResponse>();

        body.Should().NotBeNull();
        body!.Entries.Should().HaveCount(2);
        body.Entries[0].ActionType.Should().Be("created");
        body.Entries[1].ActionType.Should().Be("updated");
        body.Entries[1].PrimaryFrequency.Should().Be("143.600");
    }

    [Fact]
    public async Task GetAuditLogs_FilterByUnitName_ReturnsMatchingEntries()
    {
        // AC-07: Log is filterable by unit
        // Create frequencies for multiple units
        await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "143.500", backupFrequency = "144.500" });

        await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primaryFrequency = "145.500", backupFrequency = "146.500" });

        // Filter by squad name
        var auditResponse = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/audit-logs?unitName=Alpha%201");
        var body = await auditResponse.Content.ReadFromJsonAsync<AuditLogResponse>();

        body.Should().NotBeNull();
        body!.Entries.Should().HaveCount(1);
        body.Entries[0].UnitName.Should().Be("Alpha 1");
    }

    [Fact]
    public async Task GetAuditLogs_SortByTimestampDesc_ReturnsNewestFirst()
    {
        // AC-02: User configurable sort (newest/oldest first)
        // Create frequencies with delays to ensure different timestamps
        await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "143.500", backupFrequency = "144.500" });

        await Task.Delay(100); // Small delay to ensure different timestamps

        await _commanderClient.PutAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primaryFrequency = "145.500", backupFrequency = "146.500" });

        // Default sort (desc) should return newest first
        var auditResponse = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/audit-logs");
        var body = await auditResponse.Content.ReadFromJsonAsync<AuditLogResponse>();

        body.Should().NotBeNull();
        body!.Entries.Should().HaveCount(2);
        body.Entries[0].UnitName.Should().Be("Alpha Platoon"); // Created second
        body.Entries[1].UnitName.Should().Be("Alpha 1");       // Created first
    }

    [Fact]
    public async Task GetAuditLogs_PlayerRole_CanAccessAuditLog()
    {
        // AC-06: Log is persistent and accessible to all event members
        // Create frequency as commander
        await _commanderClient.PutAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primaryFrequency = "143.500", backupFrequency = "144.500" });

        // Player should be able to read audit log
        var auditResponse = await _playerClient.GetAsync($"/api/events/{_eventId}/audit-logs");
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await auditResponse.Content.ReadFromJsonAsync<AuditLogResponse>();

        body.Should().NotBeNull();
        body!.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAuditLogs_Pagination_ReturnsCorrectPage()
    {
        // Create multiple audit entries
        for (int i = 0; i < 5; i++)
        {
            await _commanderClient.PutAsJsonAsync(
                $"/api/squads/{_squadId}/frequencies",
                new { primaryFrequency = $"143.{500 + i}", backupFrequency = $"144.{500 + i}" });
        }

        // Get first page (limit 2, offset 0)
        var page1 = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/audit-logs?limit=2&offset=0&sortOrder=asc");
        var body1 = await page1.Content.ReadFromJsonAsync<AuditLogResponse>();

        body1.Should().NotBeNull();
        body1!.Entries.Should().HaveCount(2);
        body1.Total.Should().Be(5);
        body1.Limit.Should().Be(2);
        body1.Offset.Should().Be(0);

        // Get second page (limit 2, offset 2)
        var page2 = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/audit-logs?limit=2&offset=2&sortOrder=asc");
        var body2 = await page2.Content.ReadFromJsonAsync<AuditLogResponse>();

        body2.Should().NotBeNull();
        body2!.Entries.Should().HaveCount(2);
    }
}
