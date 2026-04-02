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
    public async Task<IActionResult> GetFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Problem(title: "Not Found", detail: $"Event {eventId} not found.", statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }

    [HttpPut("api/squads/{squadId:guid}/frequencies")]
    [Authorize(Policy = "RequireSquadLeader")]
    public async Task<IActionResult> UpdateSquadFrequency(Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequencyAsync(squadId, request.Primary, request.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Problem(title: "Not Found", detail: $"Squad {squadId} not found.", statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }

    [HttpPut("api/platoons/{platoonId:guid}/frequencies")]
    [Authorize(Policy = "RequirePlatoonLeader")]
    public async Task<IActionResult> UpdatePlatoonFrequency(Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequencyAsync(platoonId, request.Primary, request.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Problem(title: "Not Found", detail: $"Platoon {platoonId} not found.", statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }

    [HttpPut("api/events/{eventId:guid}/command-frequencies")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateCommandFrequency(Guid eventId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateCommandFrequencyAsync(eventId, request.Primary, request.Backup);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Problem(title: "Not Found", detail: $"Event {eventId} not found.", statusCode: 404);
        }
        catch (ForbiddenException ex)
        {
            return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403);
        }
    }
}
