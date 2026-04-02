using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Frequency;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class FrequencyController : ControllerBase
{
    private readonly FrequencyService _frequencyService;

    public FrequencyController(FrequencyService frequencyService)
        => _frequencyService = frequencyService;

    // ── GET /api/events/{eventId:guid}/frequencies ────────────────────────────

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<EventFrequenciesDto>> GetEventFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetEventFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return Problem(title: "Not Found", detail: $"Event {eventId} not found.", statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── PUT /api/squads/{squadId:guid}/frequencies ────────────────────────────

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<ActionResult<FrequencyLevelDto>> UpdateSquadFrequencies(Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            var result = await _frequencyService.UpdateSquadFrequenciesAsync(squadId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return Problem(title: "Not Found", detail: $"Squad {squadId} not found.", statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── PUT /api/platoons/{platoonId:guid}/frequencies ────────────────────────

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<ActionResult<FrequencyLevelDto>> UpdatePlatoonFrequencies(Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            var result = await _frequencyService.UpdatePlatoonFrequenciesAsync(platoonId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return Problem(title: "Not Found", detail: $"Platoon {platoonId} not found.", statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── PUT /api/factions/{factionId:guid}/frequencies ────────────────────────

    [HttpPut("api/factions/{factionId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> UpdateFactionFrequencies(Guid factionId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            var result = await _frequencyService.UpdateFactionFrequenciesAsync(factionId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return Problem(title: "Not Found", detail: $"Faction {factionId} not found.", statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }
}
