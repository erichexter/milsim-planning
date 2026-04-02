using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Frequencies;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/frequencies")]
public class FrequencyController : ControllerBase
{
    private readonly FrequencyService _frequencyService;

    public FrequencyController(FrequencyService frequencyService)
        => _frequencyService = frequencyService;

    [HttpGet]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetFrequencies(Guid eventId)
    {
        try
        {
            var result = await _frequencyService.GetFrequenciesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    [HttpPut("command")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateCommandFrequencies(Guid eventId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateCommandFrequenciesAsync(eventId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    [HttpPut("platoons/{platoonId:guid}")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> UpdatePlatoonFrequencies(Guid eventId, Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequenciesAsync(eventId, platoonId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }

    [HttpPut("squads/{squadId:guid}")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> UpdateSquadFrequencies(Guid eventId, Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequenciesAsync(eventId, squadId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException) { return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403); }
    }
}
