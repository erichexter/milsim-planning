using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Frequencies;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}")]
public class FrequenciesController : ControllerBase
{
    private readonly FrequencyService _frequencyService;

    public FrequenciesController(FrequencyService frequencyService)
        => _frequencyService = frequencyService;

    [HttpGet("frequencies")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<FrequencyDto>> GetFrequencies(Guid eventId)
    {
        try
        {
            var dto = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ForbiddenException ex)   { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut("squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> UpdateSquadFrequency(
        Guid eventId, Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            var dto = await _frequencyService.UpdateSquadFrequencyAsync(eventId, squadId, request.Primary, request.Backup);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ForbiddenException ex)   { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut("platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> UpdatePlatoonFrequency(
        Guid eventId, Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            var dto = await _frequencyService.UpdatePlatoonFrequencyAsync(eventId, platoonId, request.Primary, request.Backup);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ForbiddenException ex)   { return StatusCode(403, new { error = ex.Message }); }
    }

    [HttpPut("command-frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<FrequencyLevelDto>> UpdateCommandFrequency(
        Guid eventId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            var dto = await _frequencyService.UpdateCommandFrequencyAsync(eventId, request.Primary, request.Backup);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ForbiddenException ex)   { return StatusCode(403, new { error = ex.Message }); }
    }
}
