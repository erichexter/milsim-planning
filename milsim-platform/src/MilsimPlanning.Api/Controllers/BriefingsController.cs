using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Models.Briefings;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/v1/briefings")]
[Authorize]
public class BriefingsController : ControllerBase
{
    private readonly BriefingService _briefingService;

    public BriefingsController(BriefingService briefingService)
        => _briefingService = briefingService;

    // AC-01: POST /api/v1/briefings — create a new briefing channel
    [HttpPost]
    [Authorize(Policy = "BriefingAdmin")]
    public async Task<ActionResult<BriefingDto>> Create(CreateBriefingRequest request)
    {
        var dto = await _briefingService.CreateBriefingAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    // Helper route for CreatedAtAction
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "BriefingAdmin")]
    public async Task<ActionResult<BriefingDto>> GetById(Guid id)
    {
        var briefing = await _briefingService.GetByIdAsync(id);
        if (briefing is null)
            return Problem(
                title: "Not Found",
                detail: $"Briefing {id} not found.",
                statusCode: 404);

        return Ok(briefing);
    }
}
