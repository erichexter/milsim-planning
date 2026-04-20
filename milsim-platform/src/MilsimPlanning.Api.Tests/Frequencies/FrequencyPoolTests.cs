using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Frequencies;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Frequencies;

/// <summary>
/// Integration tests for Frequency Pool API.
/// AC-01 through AC-07 acceptance criteria coverage.
/// </summary>
public class FrequencyPoolTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;
    protected Guid _factionId;
    protected string _commanderUserId = string.Empty;

    public FrequencyPoolTestsBase(PostgreSqlFixture fixture)
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
        var cmdEmail = $"pool-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = cmdEmail, Email = cmdEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        // Create player
        var playerEmail = $"pool-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };

        // Seed event
        _eventId = Guid.NewGuid();
        _factionId = Guid.NewGuid();

        var faction = new Faction
        {
            Id = _factionId,
            Name = "Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId,
        };
        var testEvent = new Event
        {
            Id = _eventId,
            Name = "Pool Test Event",
            Status = EventStatus.Draft,
            FactionId = _factionId,
            Faction = faction
        };

        db.Events.Add(testEvent);
        await db.SaveChangesAsync();

        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Add("X-Test-UserId", commander.Id);
        _commanderClient.DefaultRequestHeaders.Add("X-Test-Role", "faction_commander");

        _playerClient = _factory.CreateClient();
        _playerClient.DefaultRequestHeaders.Add("X-Test-UserId", player.Id);
        _playerClient.DefaultRequestHeaders.Add("X-Test-Role", "player");
    }

    public async Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        await _fixture.ResetAsync();
    }
}

public class FrequencyPoolTests(PostgreSqlFixture fixture) : FrequencyPoolTestsBase(fixture)
{
    // ── AC-01: Organizer navigates to event settings and clicks "Configure Frequency Pool" ──
    // (This is implicit in the GET endpoint returning 404 initially)

    [Fact]
    public async Task GetFrequencyPool_WhenNoPoolExists_Returns404()
    {
        // Act
        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequency-pool");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── AC-02: Organizer enters frequencies as comma-separated or one per line ──
    // ── AC-04: System validates unique frequencies (no duplicates) ──
    // ── AC-05: Pool stored in database scoped to event ──

    [Fact]
    public async Task CreateFrequencyPool_WithValidFrequencies_Returns200WithPool()
    {
        // Arrange
        var request = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = false, ReservedRole = null },
                new() { Channel = "152.5 MHz", DisplayGroup = "VHF", SortOrder = 2, IsReserved = false, ReservedRole = null },
                new() { Channel = "154.2 MHz", DisplayGroup = "VHF", SortOrder = 3, IsReserved = false, ReservedRole = null }
            ]
        };

        // Act
        var response = await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);
        var pool = await response.Content.ReadAsAsync<FrequencyPoolDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pool.Should().NotBeNull();
        pool.EventId.Should().Be(_eventId);
        pool.Entries.Should().HaveCount(3);
        pool.Entries.Should().AllSatisfy(e => e.IsReserved.Should().BeFalse());
    }

    [Fact]
    public async Task CreateFrequencyPool_WithDuplicateChannels_Returns422()
    {
        // Arrange
        var request = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = false, ReservedRole = null },
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 2, IsReserved = false, ReservedRole = null } // Duplicate
            ]
        };

        // Act
        var response = await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── AC-03: Organizer designates 3 frequencies as reserved ──

    [Fact]
    public async Task CreateFrequencyPool_WithReservedFrequencies_Returns200WithReservedMarked()
    {
        // Arrange
        var request = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = true, ReservedRole = "Safety" },
                new() { Channel = "152.5 MHz", DisplayGroup = "VHF", SortOrder = 2, IsReserved = true, ReservedRole = "Medical" },
                new() { Channel = "154.2 MHz", DisplayGroup = "VHF", SortOrder = 3, IsReserved = true, ReservedRole = "Control" },
                new() { Channel = "154.3 MHz", DisplayGroup = "VHF", SortOrder = 4, IsReserved = false, ReservedRole = null }
            ]
        };

        // Act
        var response = await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);
        var pool = await response.Content.ReadAsAsync<FrequencyPoolDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pool.Entries.Where(e => e.IsReserved).Should().HaveCount(3);
        pool.Entries.Should().Contain(e => e.ReservedRole == "Safety");
        pool.Entries.Should().Contain(e => e.ReservedRole == "Medical");
        pool.Entries.Should().Contain(e => e.ReservedRole == "Control");
    }

    [Fact]
    public async Task CreateFrequencyPool_WithInvalidReservedRole_Returns422()
    {
        // Arrange
        var request = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = true, ReservedRole = "InvalidRole" }
            ]
        };

        // Act
        var response = await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── AC-07: Confirmation displays total available frequencies ──

    [Fact]
    public async Task CreateFrequencyPool_WithMixedReservedEntries_ConfirmationShowsAvailableCount()
    {
        // Arrange
        var request = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = true, ReservedRole = "Safety" },
                new() { Channel = "152.5 MHz", DisplayGroup = "VHF", SortOrder = 2, IsReserved = true, ReservedRole = "Medical" },
                new() { Channel = "154.2 MHz", DisplayGroup = "VHF", SortOrder = 3, IsReserved = true, ReservedRole = "Control" },
                new() { Channel = "154.3 MHz", DisplayGroup = "VHF", SortOrder = 4, IsReserved = false, ReservedRole = null },
                new() { Channel = "154.4 MHz", DisplayGroup = "VHF", SortOrder = 5, IsReserved = false, ReservedRole = null }
            ]
        };

        // Act
        var response = await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);
        var pool = await response.Content.ReadAsAsync<FrequencyPoolDto>();

        // Assert - AC-07: 5 total, 3 reserved, 2 available to factions
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pool.Entries.Should().HaveCount(5);
        pool.Entries.Count(e => e.IsReserved).Should().Be(3);
        pool.Entries.Count(e => !e.IsReserved).Should().Be(2);
    }

    // ── Channel normalization (lowercase, trimmed) ──

    [Fact]
    public async Task CreateFrequencyPool_WithMixedCaseAndWhitespace_NormalizesChannels()
    {
        // Arrange
        var request = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "  152.4 MHz  ", DisplayGroup = "VHF", SortOrder = 1, IsReserved = false, ReservedRole = null }
            ]
        };

        // Act
        var response = await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);
        var pool = await response.Content.ReadAsAsync<FrequencyPoolDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        pool.Entries[0].Channel.Should().Be("152.4 mhz"); // lowercase, trimmed
    }

    // ── GET after create ──

    [Fact]
    public async Task GetFrequencyPool_AfterCreate_ReturnsSavedPool()
    {
        // Arrange
        var createRequest = new CreateFrequencyPoolRequest
        {
            Entries =
            [
                new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = false, ReservedRole = null },
                new() { Channel = "152.5 MHz", DisplayGroup = "VHF", SortOrder = 2, IsReserved = true, ReservedRole = "Safety" }
            ]
        };
        await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", createRequest);

        // Act
        var getResponse = await _commanderClient.GetAsync($"/api/events/{_eventId}/frequency-pool");
        var pool = await getResponse.Content.ReadAsAsync<FrequencyPoolDto>();

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        pool.Entries.Should().HaveCount(2);
    }

    // ── Authorization: Player can read but not write ──

    [Fact]
    public async Task GetFrequencyPool_AsPlayer_Returns200()
    {
        // Arrange
        var createRequest = new CreateFrequencyPoolRequest
        {
            Entries = [new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = false, ReservedRole = null }]
        };
        await _commanderClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", createRequest);

        // Act
        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/frequency-pool");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateFrequencyPool_AsUnauthenticatedUser_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();
        var request = new CreateFrequencyPoolRequest
        {
            Entries = [new() { Channel = "152.4 MHz", DisplayGroup = "VHF", SortOrder = 1, IsReserved = false, ReservedRole = null }]
        };

        // Act
        var response = await unauthClient.PutAsJsonAsync($"/api/events/{_eventId}/frequency-pool", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
