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

    [HttpGet("api/events/{eventId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<FrequencyVisibilityDto>> GetFrequencies(Guid eventId)
    {
        try
        {
            var dto = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(dto);
        }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateSquadFrequencies(Guid squadId, [FromBody] UpdateFrequencyRequest body)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequencyAsync(squadId, body.Primary, body.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdatePlatoonFrequencies(Guid platoonId, [FromBody] UpdateFrequencyRequest body)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequencyAsync(platoonId, body.Primary, body.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("api/factions/{factionId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateFactionFrequencies(Guid factionId, [FromBody] UpdateFrequencyRequest body)
    {
        try
        {
            await _frequencyService.UpdateFactionFrequencyAsync(factionId, body.Primary, body.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }
}
