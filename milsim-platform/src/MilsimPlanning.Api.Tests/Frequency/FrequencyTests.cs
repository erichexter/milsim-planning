using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MilsimPlanning.Api.Tests.Fixtures;
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
