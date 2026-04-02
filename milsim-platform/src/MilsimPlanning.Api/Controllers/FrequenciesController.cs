using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Frequency;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Authorize]
public class FrequenciesController : ControllerBase
{
    private readonly FrequencyService _frequencyService;

    public FrequenciesController(FrequencyService frequencyService)
    {
        _frequencyService = frequencyService;
    }

    // ── FREQ-01 ───────────────────────────────────────────────────────────────

    [HttpGet("api/squads/{squadId:guid}/frequencies")]
    public async Task<ActionResult<SquadFrequencyDto>> GetSquadFrequency(Guid squadId)
    {
        try
        {
            return Ok(await _frequencyService.GetSquadFrequencyAsync(squadId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = ex.Message, Status = 404 });
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-02 ───────────────────────────────────────────────────────────────

    [HttpPatch("api/squads/{squadId:guid}/frequencies")]
    public async Task<IActionResult> UpdateSquadFrequency(Guid squadId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateSquadFrequencyAsync(squadId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = ex.Message, Status = 404 });
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-03 ───────────────────────────────────────────────────────────────

    [HttpGet("api/platoons/{platoonId:guid}/frequencies")]
    public async Task<ActionResult<PlatoonFrequencyDto>> GetPlatoonFrequency(Guid platoonId)
    {
        try
        {
            return Ok(await _frequencyService.GetPlatoonFrequencyAsync(platoonId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = ex.Message, Status = 404 });
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-04 ───────────────────────────────────────────────────────────────

    [HttpPatch("api/platoons/{platoonId:guid}/frequencies")]
    public async Task<IActionResult> UpdatePlatoonFrequency(Guid platoonId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdatePlatoonFrequencyAsync(platoonId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = ex.Message, Status = 404 });
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-05 ───────────────────────────────────────────────────────────────

    [HttpGet("api/factions/{factionId:guid}/frequencies")]
    public async Task<ActionResult<FactionFrequencyDto>> GetFactionFrequency(Guid factionId)
    {
        try
        {
            return Ok(await _frequencyService.GetFactionFrequencyAsync(factionId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = ex.Message, Status = 404 });
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }

    // ── FREQ-06 ───────────────────────────────────────────────────────────────

    [HttpPatch("api/factions/{factionId:guid}/frequencies")]
    public async Task<IActionResult> UpdateFactionFrequency(Guid factionId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            await _frequencyService.UpdateFactionFrequencyAsync(factionId, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = ex.Message, Status = 404 });
        }
        catch (ForbiddenException)
        {
            return Problem(title: "Forbidden", detail: "Insufficient role to access this resource.", statusCode: 403);
        }
    }
}
