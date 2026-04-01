using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Frequencies;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class FrequencyController : ControllerBase
{
    private readonly FrequencyService _frequencyService;

    public FrequencyController(FrequencyService frequencyService)
        => _frequencyService = frequencyService;

    // ── GET /api/events/{eventId}/frequencies ─────────────────────────────────

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<EventFrequenciesDto>> GetEventFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetEventFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (ForbiddenException) { return Forbid(); }
    }

    // ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> SetSquadFrequencies(Guid squadId, [FromBody] SetFrequenciesRequest request)
    {
        try
        {
            var result = await _frequencyService.SetSquadFrequenciesAsync(squadId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> SetPlatoonFrequencies(Guid platoonId, [FromBody] SetFrequenciesRequest request)
    {
        try
        {
            var result = await _frequencyService.SetPlatoonFrequenciesAsync(platoonId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // ── PUT /api/factions/{factionId}/frequencies ─────────────────────────────

    [HttpPut("api/factions/{factionId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> SetFactionFrequencies(Guid factionId, [FromBody] SetFrequenciesRequest request)
    {
        try
        {
            var result = await _frequencyService.SetFactionFrequenciesAsync(factionId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }
}
