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

    // ── FREQ-01: Read Frequencies (role-filtered) ───────────────────────────

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                title: "Not Found",
                detail: ex.Message,
                statusCode: 404
            );
        }
        catch (ForbiddenException)
        {
            return Problem(
                title: "Forbidden",
                detail: "Insufficient role to access this resource.",
                statusCode: 403
            );
        }
    }

    // ── FREQ-02: Update Squad Frequencies ───────────────────────────────────

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<IActionResult> UpdateSquadFrequencies(Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequenciesAsync(squadId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                title: "Not Found",
                detail: ex.Message,
                statusCode: 404
            );
        }
        catch (ForbiddenException)
        {
            return Problem(
                title: "Forbidden",
                detail: "Insufficient role to access this resource.",
                statusCode: 403
            );
        }
    }

    // ── FREQ-03: Update Platoon Frequencies ─────────────────────────────────

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<IActionResult> UpdatePlatoonFrequencies(Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequenciesAsync(platoonId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                title: "Not Found",
                detail: ex.Message,
                statusCode: 404
            );
        }
        catch (ForbiddenException)
        {
            return Problem(
                title: "Forbidden",
                detail: "Insufficient role to access this resource.",
                statusCode: 403
            );
        }
    }

    // ── FREQ-04: Update Command (Faction) Frequencies ───────────────────────

    [HttpPut("api/factions/{factionId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateCommandFrequencies(Guid factionId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateCommandFrequenciesAsync(factionId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                title: "Not Found",
                detail: ex.Message,
                statusCode: 404
            );
        }
        catch (ForbiddenException)
        {
            return Problem(
                title: "Forbidden",
                detail: "Insufficient role to access this resource.",
                statusCode: 403
            );
        }
    }
}
