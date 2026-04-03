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

    // ── FREQ-01: Get frequencies (role-filtered) ────────────────────────────────

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetFrequencies(Guid eventId)
    {
        try
        {
            var dto = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-02: Update squad frequencies ───────────────────────────────────────

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<IActionResult> UpdateSquadFrequency(Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequencyAsync(squadId, request.Primary, request.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-03: Update platoon frequencies ─────────────────────────────────────

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<IActionResult> UpdatePlatoonFrequency(Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequencyAsync(platoonId, request.Primary, request.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-04: Update command frequencies ─────────────────────────────────────

    [HttpPut("api/events/{eventId:guid}/command-frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateCommandFrequency(Guid eventId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateCommandFrequencyAsync(eventId, request.Primary, request.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }
}
