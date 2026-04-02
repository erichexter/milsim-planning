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

    // ── GET /api/events/{eventId}/frequencies — role-scoped overview ──────────

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<FrequencyViewDto>> GetEventFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetEventFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── GET /api/squads/{squadId}/frequencies ─────────────────────────────────

    [HttpGet("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<ActionResult<FrequencyLevelDto>> GetSquadFrequencies(Guid squadId)
    {
        try
        {
            var result = await _frequencyService.GetSquadFrequenciesAsync(squadId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<IActionResult> UpdateSquadFrequencies(Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequenciesAsync(squadId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── GET /api/platoons/{platoonId}/frequencies ─────────────────────────────

    [HttpGet("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<ActionResult<FrequencyLevelDto>> GetPlatoonFrequencies(Guid platoonId)
    {
        try
        {
            var result = await _frequencyService.GetPlatoonFrequenciesAsync(platoonId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<IActionResult> UpdatePlatoonFrequencies(Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequenciesAsync(platoonId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── GET /api/factions/{factionId}/command-frequencies ────────────────────

    [HttpGet("api/factions/{factionId:guid}/command-frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> GetFactionCommandFrequencies(Guid factionId)
    {
        try
        {
            var result = await _frequencyService.GetFactionCommandFrequenciesAsync(factionId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    // ── PUT /api/factions/{factionId}/command-frequencies ────────────────────

    [HttpPut("api/factions/{factionId:guid}/command-frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateFactionCommandFrequencies(Guid factionId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateFactionCommandFrequenciesAsync(factionId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }
}
