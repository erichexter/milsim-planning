using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.CsvImport;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Roster;

/// <summary>
/// Integration tests for CSV roster import (ROST-01 through ROST-06).
/// Uses Testcontainers PostgreSQL + WebApplicationFactory.
/// Requires Docker Desktop to run.
/// </summary>
public class RosterImportTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _commanderClient = null!;
    private Mock<IEmailService> _emailMock = null!;
    private Guid _eventId;
    private Guid _seedSquadId;

    public RosterImportTests(PostgreSqlFixture fixture)
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
                    // Replace DB with Testcontainers DB
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    // Replace email service with mock
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
                    services.AddSingleton(_emailMock); // expose mock for Verify() calls
                });
            });

        // Apply migrations to test DB
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

        // Seed a faction_commander user + event
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var commanderEmail = $"commander-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser
        {
            UserName = commanderEmail,
            Email = commanderEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile
        {
            UserId = commander.Id,
            Callsign = "Commander",
            DisplayName = "Commander",
            User = commander
        };

        // Seed a test event
        _eventId = Guid.NewGuid();
        var faction = new Faction
        {
            Id = Guid.NewGuid(),
            Name = "Test Faction",
            CommanderId = commander.Id,
            EventId = _eventId
        };
        var testEvent = new Event
        {
            Id = _eventId,
            Name = "Test Event",
            Status = EventStatus.Draft,
            FactionId = faction.Id,
            Faction = faction
        };

        // Seed a platoon + squad for SquadId preservation test
        var platoon = new Platoon { Id = Guid.NewGuid(), FactionId = faction.Id, Name = "Alpha Platoon", Order = 1 };
        var squad = new Squad { Id = Guid.NewGuid(), PlatoonId = platoon.Id, Name = "Alpha 1", Order = 1 };
        _seedSquadId = squad.Id;

        db.Events.Add(testEvent);
        db.Platoons.Add(platoon);
        db.Squads.Add(squad);

        // Add event membership for commander
        db.EventMemberships.Add(new EventMembership
        {
            UserId = commander.Id,
            EventId = _eventId,
            Role = "faction_commander"
        });

        await db.SaveChangesAsync();

        // Build authenticated client for commander
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        var commanderJwt = authService.GenerateJwt(commander, "faction_commander");
        _commanderClient = _factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", commanderJwt);
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpContent BuildCsvContent(string csv)
    {
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        bytes.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        var form = new MultipartFormDataContent();
        form.Add(bytes, "file", "roster.csv");
        return form;
    }

    private AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private async Task SeedRegisteredPlayer(Guid eventId, string email)
    {
        using var db = GetDb();
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = eventId,
            Email = email.ToLowerInvariant(),
            Name = "Existing Player",
            UserId = "some-user-id-already-registered"  // non-null → registered
        });
        await db.SaveChangesAsync();
    }

    // ── ROST_Validate Tests ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ROST_Validate")]
    public async Task ValidateRoster_ValidCsv_ReturnsZeroErrors()
    {
        var csv = "name,email,callsign,team\nJohn Smith,john@test.com,GHOST,Alpha";

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/validate",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
        result!.ErrorCount.Should().Be(0);
        result.ValidCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "ROST_Validate")]
    public async Task ValidateRoster_MissingEmail_ReturnsRowError()
    {
        var csv = "name,email,callsign,team\nJohn Smith,,GHOST,Alpha";

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/validate",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
        result!.Errors.Should().ContainSingle(e => e.Field == "email");
        result.ErrorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "ROST_Validate")]
    public async Task ValidateRoster_MultipleErrors_ReturnsAllErrors()
    {
        // Two bad rows — ROST-03: ALL errors returned, not just first
        var csv = "name,email,callsign,team\n" +
                  ",invalid-email,,Alpha\n" +   // row 2: bad email + missing name
                  "Bob,,GHOST,Bravo";           // row 3: missing email

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/validate",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
        result!.ErrorCount.Should().BeGreaterThan(1, because: "both rows should be flagged");
    }

    [Fact]
    [Trait("Category", "ROST_Validate")]
    public async Task ValidateRoster_WithErrors_DoesNotPersistData()
    {
        // ROST-03: validate NEVER writes to DB
        var csv = "name,email,callsign,team\n,bad-email,,Team";

        await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/validate",
            BuildCsvContent(csv));

        using var db = GetDb();
        var count = await db.EventPlayers.CountAsync(ep => ep.EventId == _eventId);
        count.Should().Be(0, because: "validate must not persist any data");
    }

    [Fact]
    [Trait("Category", "ROST_Validate")]
    public async Task ValidateRoster_MissingCallsign_ReturnsWarningNotError()
    {
        // ROST-02: warnings do not block commit
        var csv = "name,email,callsign,team\nJohn Smith,john@test.com,,Alpha";

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/validate",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
        result!.ErrorCount.Should().Be(0, because: "missing callsign is a warning, not an error");
        result.WarningCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e =>
            e.Field == "callsign" && e.Severity == Severity.Warning);
    }

    [Fact]
    [Trait("Category", "ROST_Validate")]
    public async Task ValidateRoster_ParsedFields_ContainNameEmailCallsignTeam()
    {
        // ROST-04: all four fields parsed
        var csv = "name,email,callsign,team\nAlpha One,alpha@test.com,ALPHA,Bravo Squad";

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/validate",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CsvValidationResult>();
        result!.ErrorCount.Should().Be(0);
        result.ValidCount.Should().Be(1);
    }

    // ── ROST_Commit Tests ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "ROST_Commit")]
    public async Task CommitRoster_NewPlayers_UpsertsToDatabase()
    {
        var csv = "name,email,callsign,team\nJohn Smith,john-new@test.com,GHOST,Alpha";

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/commit",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var player = await db.EventPlayers
            .SingleOrDefaultAsync(ep => ep.Email == "john-new@test.com" && ep.EventId == _eventId);
        player.Should().NotBeNull();
        player!.Name.Should().Be("John Smith");
        player.Callsign.Should().Be("GHOST");
    }

    [Fact]
    [Trait("Category", "ROST_Commit")]
    public async Task CommitRoster_ExistingPlayer_UpdatesNameCallsignNotSquad()
    {
        // First import
        var csv1 = "name,email,callsign,team\nJohn Smith,john-upsert@test.com,OLD,Alpha";
        await _commanderClient.PostAsync($"/api/events/{_eventId}/roster/commit", BuildCsvContent(csv1));

        // Re-import with updated callsign — same email
        var csv2 = "name,email,callsign,team\nJohn Smith Updated,john-upsert@test.com,GHOST,Alpha";
        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/commit",
            BuildCsvContent(csv2));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDb();
        var players = await db.EventPlayers
            .Where(ep => ep.Email == "john-upsert@test.com" && ep.EventId == _eventId)
            .ToListAsync();
        players.Should().HaveCount(1, because: "upsert should not create a duplicate");
        players[0].Callsign.Should().Be("GHOST", because: "callsign updated on re-import");
        players[0].Name.Should().Be("John Smith Updated", because: "name updated on re-import");
    }

    [Fact]
    [Trait("Category", "ROST_Commit")]
    public async Task CommitRoster_ReimportPreservesSquadAssignments()
    {
        // ROST-05 critical: re-import must NOT overwrite squad assignment
        var uniqueEmail = $"john-squad-{Guid.NewGuid():N}@test.com";
        var csv = $"name,email,callsign,team\nJohn Smith,{uniqueEmail},OLD,Alpha";

        // First import
        await _commanderClient.PostAsync($"/api/events/{_eventId}/roster/commit", BuildCsvContent(csv));

        // Manually assign player to squad in DB
        using (var db = GetDb())
        {
            var player = await db.EventPlayers.SingleAsync(p => p.Email == uniqueEmail);
            player.SquadId = _seedSquadId;
            await db.SaveChangesAsync();
        }

        // Re-import with updated callsign
        var csv2 = $"name,email,callsign,team\nJohn Smith,{uniqueEmail},GHOST,Alpha";
        await _commanderClient.PostAsync($"/api/events/{_eventId}/roster/commit", BuildCsvContent(csv2));

        using var db2 = GetDb();
        var updated = await db2.EventPlayers.SingleAsync(p => p.Email == uniqueEmail);
        updated.Callsign.Should().Be("GHOST", because: "callsign updated from CSV");
        updated.SquadId.Should().Be(_seedSquadId, because: "squad assignment MUST be preserved (ROST-05)");
    }

    [Fact]
    [Trait("Category", "ROST_Commit")]
    public async Task CommitRoster_UnregisteredPlayers_ReceiveInvitationEmail()
    {
        // ROST-06: new player with no UserId should receive invite
        var uniqueEmail = $"new-invite-{Guid.NewGuid():N}@test.com";
        var csv = $"name,email,callsign,team\nNew Player,{uniqueEmail},ALPHA,Alpha";

        _emailMock.Invocations.Clear();

        await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/commit",
            BuildCsvContent(csv));

        _emailMock.Verify(
            e => e.SendAsync(uniqueEmail, It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "ROST_Commit")]
    public async Task CommitRoster_AlreadyRegisteredPlayers_DoNotReceiveInvite()
    {
        // ROST-06: registered players (UserId != null) must NOT receive another invite
        var registeredEmail = $"registered-{Guid.NewGuid():N}@test.com";
        await SeedRegisteredPlayer(_eventId, registeredEmail);

        _emailMock.Invocations.Clear();

        var csv = $"name,email,callsign,team\nExisting Player,{registeredEmail},ALPHA,Alpha";
        await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/commit",
            BuildCsvContent(csv));

        _emailMock.Verify(
            e => e.SendAsync(registeredEmail, It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "ROST_Commit")]
    public async Task CommitRoster_WithErrors_Returns422()
    {
        // CSV with errors → 422 Unprocessable Entity, NO new players saved
        var csv = "name,email,callsign,team\n,invalid-email,,";

        // Record count before request
        int countBefore;
        using (var db = GetDb())
            countBefore = await db.EventPlayers.CountAsync(ep => ep.EventId == _eventId);

        var response = await _commanderClient.PostAsync(
            $"/api/events/{_eventId}/roster/commit",
            BuildCsvContent(csv));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            because: "commit with errors returns 422");

        // Count should not have increased — no players added on invalid CSV
        using var db2 = GetDb();
        var countAfter = await db2.EventPlayers.CountAsync(ep => ep.EventId == _eventId);
        countAfter.Should().Be(countBefore, because: "no players saved when CSV has errors");
    }
}
