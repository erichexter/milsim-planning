using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.ChannelAssignments;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class ChannelAssignmentsController : ControllerBase
{
    private readonly ChannelAssignmentService _service;

    public ChannelAssignmentsController(ChannelAssignmentService service)
        => _service = service;

    // ── GET /api/events/{eventId}/channel-assignments ─────────────────────────

    [HttpGet("api/events/{eventId:guid}/channel-assignments")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<ChannelAssignmentListDto>> GetAssignments(
        Guid eventId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var result = await _service.GetAssignmentsAsync(eventId, limit, offset);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── POST /api/events/{eventId}/channel-assignments ────────────────────────

    [HttpPost("api/events/{eventId:guid}/channel-assignments")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<ChannelAssignmentDto>> CreateAssignment(
        Guid eventId,
        [FromBody] CreateChannelAssignmentRequest request)
    {
        try
        {
            var result = await _service.CreateAssignmentAsync(eventId, request);
            return StatusCode(201, result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ArgumentException ex) { return Problem(title: "Validation Error", detail: ex.Message, statusCode: 422); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── PUT /api/events/{eventId}/channel-assignments/{id} ───────────────────

    [HttpPut("api/events/{eventId:guid}/channel-assignments/{id:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<ChannelAssignmentDto>> UpdateAssignment(
        Guid eventId,
        Guid id,
        [FromBody] UpdateChannelAssignmentRequest request)
    {
        try
        {
            var result = await _service.UpdateAssignmentAsync(eventId, id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ArgumentException ex) { return Problem(title: "Validation Error", detail: ex.Message, statusCode: 422); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── DELETE /api/events/{eventId}/channel-assignments/{id} ────────────────

    [HttpDelete("api/events/{eventId:guid}/channel-assignments/{id:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> DeleteAssignment(Guid eventId, Guid id)
    {
        try
        {
            await _service.DeleteAssignmentAsync(eventId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }
}
