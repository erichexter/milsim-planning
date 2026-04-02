using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Frequency;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class FrequenciesController : ControllerBase
{
    private readonly FrequencyService _frequencyService;

    public FrequenciesController(FrequencyService frequencyService)
        => _frequencyService = frequencyService;

    // ── GET /api/events/{eventId:guid}/frequencies ────────────────────────────

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<FrequenciesDto>> GetFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }

    // ── PATCH /api/squads/{squadId:guid}/frequencies ──────────────────────────

    [HttpPatch("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<IActionResult> PatchSquadFrequencies(Guid squadId, [FromBody] UpdateFrequencyRequest req)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequenciesAsync(squadId, req);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }

    // ── PATCH /api/platoons/{platoonId:guid}/frequencies ──────────────────────

    [HttpPatch("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<IActionResult> PatchPlatoonFrequencies(Guid platoonId, [FromBody] UpdateFrequencyRequest req)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequenciesAsync(platoonId, req);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }

    // ── PATCH /api/factions/{factionId:guid}/frequencies ──────────────────────

    [HttpPatch("api/factions/{factionId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> PatchFactionFrequencies(Guid factionId, [FromBody] UpdateFrequencyRequest req)
    {
        try
        {
            await _frequencyService.UpdateFactionFrequenciesAsync(factionId, req);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }
}
