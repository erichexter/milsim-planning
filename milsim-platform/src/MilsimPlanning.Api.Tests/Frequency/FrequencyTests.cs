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

namespace MilsimPlanning.Api.Tests.Frequency;

// ── FREQ-01 + FREQ-02: Squad frequencies ─────────────────────────────────────

[Trait("Category", "FREQ_Squad")]
public class SquadFrequencyTests : FrequencyTestsBase
{
    public SquadFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetSquadFrequency_AsPlayer_OwnSquad_Returns200()
    {
        var response = await _playerClient.GetAsync($"/api/squads/{_squadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("squadId").GetString().Should().Be(_squadId.ToString());
    }

    [Fact]
    public async Task GetSquadFrequency_AsPlayer_OtherSquad_Returns403()
    {
        var response = await _playerClient.GetAsync($"/api/squads/{_otherSquadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSquadFrequency_AsSquadLeader_OwnSquad_Returns200()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/squads/{_squadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSquadFrequency_AsSquadLeader_OtherSquad_Returns403()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/squads/{_otherSquadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSquadFrequency_AsPlatoonLeader_SquadInOwnPlatoon_Returns200()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/squads/{_squadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSquadFrequency_AsFactionCommander_Returns200()
    {
        var response = await _commanderClient.GetAsync($"/api/squads/{_squadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSquadFrequency_NonExistentSquad_Returns404()
    {
        var response = await _commanderClient.GetAsync($"/api/squads/{Guid.NewGuid()}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSquadFrequency_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync($"/api/squads/{_squadId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsSquadLeader_OwnSquad_Returns204AndPersists()
    {
        var patchResponse = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "45.500 MHz", backup = (string?)null });

        patchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _squadLeaderClient.GetAsync($"/api/squads/{_squadId}/frequencies");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("45.500 MHz");
        body.GetProperty("backup").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "45.500 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsSquadLeader_OtherSquad_Returns403()
    {
        var response = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_otherSquadId}/frequencies",
            new { primary = "45.500 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlatoonLeader_SquadInOwnPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "46.000 MHz", backup = "47.000 MHz" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsPlatoonLeader_SquadNotInOwnPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/squads/{_otherSquadId}/frequencies",
            new { primary = "46.000 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSquadFrequency_AsFactionCommander_Returns204()
    {
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "48.000 MHz", backup = "49.000 MHz" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSquadFrequency_OutsiderNotInEvent_Returns403()
    {
        var response = await _outsiderClient.PatchAsJsonAsync(
            $"/api/squads/{_squadId}/frequencies",
            new { primary = "45.500 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── FREQ-03 + FREQ-04: Platoon frequencies ───────────────────────────────────

[Trait("Category", "FREQ_Platoon")]
public class PlatoonFrequencyTests : FrequencyTestsBase
{
    public PlatoonFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetPlatoonFrequency_AsSquadLeader_OwnPlatoon_Returns200()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/platoons/{_platoonId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("platoonId").GetString().Should().Be(_platoonId.ToString());
    }

    [Fact]
    public async Task GetPlatoonFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.GetAsync($"/api/platoons/{_platoonId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatoonFrequency_AsPlatoonLeader_OwnPlatoon_Returns200()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/platoons/{_platoonId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPlatoonFrequency_AsFactionCommander_Returns200()
    {
        var response = await _commanderClient.GetAsync($"/api/platoons/{_platoonId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPlatoonFrequency_NonExistentPlatoon_Returns404()
    {
        var response = await _commanderClient.GetAsync($"/api/platoons/{Guid.NewGuid()}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsPlatoonLeader_OwnPlatoon_Returns204()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "46.750 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "46.750 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsPlatoonLeader_OtherPlatoon_Returns403()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/platoons/{_otherPlatoonId}/frequencies",
            new { primary = "46.750 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePlatoonFrequency_AsFactionCommander_Returns204()
    {
        var response = await _commanderClient.PatchAsJsonAsync(
            $"/api/platoons/{_platoonId}/frequencies",
            new { primary = "47.000 MHz", backup = "48.000 MHz" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

// ── FREQ-05 cross-faction IDOR regression ────────────────────────────────────
// Schema: one faction per event (IX_Factions_EventId unique). Cross-faction
// scenario: two events (A and B) each with their own faction. A platoon leader
// is a member of Event A but their EventPlayer.PlatoonId references a platoon
// that belongs to Faction B (Event B). Requesting GET /api/factions/F_A/frequencies
// must return 403 — not 200. Without the PlatoonBelongsToFactionAsync guard, the
// old code would return 200 because ep.PlatoonId != null.

[Trait("Category", "FREQ_CrossFactionIDOR")]
public class FactionFrequencyCrossFactionTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private Mock<IEmailService> _emailMock = null!;

    private HttpClient _crossFactionPlatoonLeaderClient = null!;
    private Guid _targetFactionId; // Faction A

    public FactionFrequencyCrossFactionTests(PostgreSqlFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "dev-placeholder-secret-32-chars!!",
                        ["Jwt:Issuer"] = "milsim-tests",
                        ["Jwt:Audience"] = "milsim-tests"
                    }));

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
                    services.AddAuthentication(o =>
                    {
                        o.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        o.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var commanderA = await CreateUser(userManager, "cf-cmdr-a", "faction_commander");
        var commanderB = await CreateUser(userManager, "cf-cmdr-b", "faction_commander");
        var crossPl    = await CreateUser(userManager, "cf-pl-b",   "platoon_leader");

        // IDs
        var eventAId   = Guid.NewGuid();
        var eventBId   = Guid.NewGuid();
        _targetFactionId = Guid.NewGuid(); // Faction A (lives in Event A)
        var factionBId   = Guid.NewGuid(); // Faction B (lives in Event B)
        var platoonBId   = Guid.NewGuid(); // Platoon in Faction B

        // Event A + Faction A  (one faction per event — circular FK resolved via Faction navigation)
        var factionA = new Faction { Id = _targetFactionId, Name = "CF Faction A", CommanderId = commanderA.Id, EventId = eventAId };
        var eventA   = new Event   { Id = eventAId, Name = "CF Event A", Status = EventStatus.Draft, FactionId = _targetFactionId, Faction = factionA };
        db.Events.Add(eventA);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Event B + Faction B  (separate event, separate faction)
        var factionB = new Faction { Id = factionBId, Name = "CF Faction B", CommanderId = commanderB.Id, EventId = eventBId };
        var eventB   = new Event   { Id = eventBId, Name = "CF Event B", Status = EventStatus.Draft, FactionId = factionBId, Faction = factionB };
        db.Events.Add(eventB);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Platoon belonging to Faction B
        db.Platoons.Add(new Platoon { Id = platoonBId, FactionId = factionBId, Name = "CF Platoon B", Order = 1 });

        // Cross-faction PL: event member of Event A, but EventPlayer.PlatoonId → platoon in Faction B
        // This is the data condition that triggers the FREQ-05 authorization bug in the old code.
        db.EventMemberships.Add(new EventMembership { UserId = crossPl.Id, EventId = eventAId, Role = "platoon_leader" });
        db.EventPlayers.Add(new EventPlayer
        {
            EventId = eventAId, Email = crossPl.Email!, Name = "CF PL B",
            UserId = crossPl.Id, PlatoonId = platoonBId
        });

        await db.SaveChangesAsync();

        _crossFactionPlatoonLeaderClient = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(_crossFactionPlatoonLeaderClient, crossPl.Id, "platoon_leader");
    }

    public Task DisposeAsync()
    {
        _crossFactionPlatoonLeaderClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetFactionFrequency_PlatoonLeaderWhosePlatoonBelongsToDifferentFaction_Returns403()
    {
        // crossPl is a platoon_leader in Event A with a platoon from Faction B.
        // Without PlatoonBelongsToFactionAsync guard, old code would return 200
        // because ep.PlatoonId != null passes the check. Fixed code must return 403.
        var response = await _crossFactionPlatoonLeaderClient.GetAsync($"/api/factions/{_targetFactionId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<AppUser> CreateUser(UserManager<AppUser> um, string prefix, string role)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@test.com";
        var user  = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        await um.CreateAsync(user, "TestPass123!");
        await um.AddToRoleAsync(user, role);
        user.Profile = new UserProfile { UserId = user.Id, Callsign = prefix, DisplayName = prefix, User = user };
        return user;
    }
}

// ── FREQ-05 + FREQ-06: Faction frequencies ───────────────────────────────────

[Trait("Category", "FREQ_Faction")]
public class FactionFrequencyTests : FrequencyTestsBase
{
    public FactionFrequencyTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFactionFrequency_AsFactionCommander_Returns200()
    {
        var response = await _commanderClient.GetAsync($"/api/factions/{_factionId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("factionId").GetString().Should().Be(_factionId.ToString());
    }

    [Fact]
    public async Task GetFactionFrequency_AsPlatoonLeader_Returns200()
    {
        var response = await _platoonLeaderClient.GetAsync($"/api/factions/{_factionId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFactionFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.GetAsync($"/api/factions/{_factionId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFactionFrequency_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.GetAsync($"/api/factions/{_factionId}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFactionFrequency_NonExistentFaction_Returns404()
    {
        var response = await _commanderClient.GetAsync($"/api/factions/{Guid.NewGuid()}/frequencies");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateFactionFrequency_AsFactionCommander_Returns204AndPersists()
    {
        var patchResponse = await _commanderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "47.000 MHz", backup = "48.250 MHz" });

        patchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _commanderClient.GetAsync($"/api/factions/{_factionId}/frequencies");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("primary").GetString().Should().Be("47.000 MHz");
        body.GetProperty("backup").GetString().Should().Be("48.250 MHz");
    }

    [Fact]
    public async Task UpdateFactionFrequency_AsPlatoonLeader_Returns403()
    {
        var response = await _platoonLeaderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "47.000 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFactionFrequency_AsSquadLeader_Returns403()
    {
        var response = await _squadLeaderClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "47.000 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFactionFrequency_AsPlayer_Returns403()
    {
        var response = await _playerClient.PatchAsJsonAsync(
            $"/api/factions/{_factionId}/frequencies",
            new { primary = "47.000 MHz", backup = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

}
