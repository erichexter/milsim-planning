using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Channels;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class RadioChannelsController : ControllerBase
{
    private readonly RadioChannelAssignmentService _assignmentService;
    private readonly RadioChannelConflictSummaryService _conflictSummaryService;
    private readonly RadioChannelChannelService _channelService;

    public RadioChannelsController(
        RadioChannelAssignmentService assignmentService,
        RadioChannelConflictSummaryService conflictSummaryService,
        RadioChannelChannelService channelService)
    {
        _assignmentService = assignmentService;
        _conflictSummaryService = conflictSummaryService;
        _channelService = channelService;
    }

    // ── GET /api/events/{eventId}/radio-channels ──────────────────────────────

    [HttpGet("api/events/{eventId:guid}/radio-channels")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<List<RadioChannelListDto>>> GetEventChannels(Guid eventId)
    {
        try
        {
            var result = await _channelService.ListAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── POST /api/events/{eventId}/radio-channels ─────────────────────────────

    [HttpPost("api/events/{eventId:guid}/radio-channels")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<RadioChannelDetailDto>> CreateChannel(
        Guid eventId,
        [FromBody] CreateRadioChannelRequest request)
    {
        try
        {
            var result = await _channelService.CreateAsync(eventId, request);
            return CreatedAtAction(
                nameof(GetEventChannels),
                new { eventId },
                result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (InvalidOperationException ex) { return Problem(title: "Conflict", detail: ex.Message, statusCode: 409); }
        catch (ArgumentException ex) { return Problem(title: "Bad Request", detail: ex.Message, statusCode: 400); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── PATCH /api/radio-channels/{channelId} ────────────────────────────────
    // AC-07: Planner can edit channel name post-creation

    [HttpPatch("api/radio-channels/{channelId:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<RadioChannelDetailDto>> UpdateChannel(
        Guid channelId,
        [FromBody] UpdateRadioChannelRequest request)
    {
        try
        {
            var result = await _channelService.UpdateAsync(channelId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (InvalidOperationException ex) { return Problem(title: "Conflict", detail: ex.Message, statusCode: 409); }
        catch (ArgumentException ex) { return Problem(title: "Bad Request", detail: ex.Message, statusCode: 400); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── GET /api/radio-channels/{channelId}/assignments ───────────────────────

    [HttpGet("api/radio-channels/{channelId:guid}/assignments")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<List<RadioChannelAssignmentDto>>> GetAssignments(Guid channelId)
    {
        try
        {
            var result = await _assignmentService.GetAssignmentsAsync(channelId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── PUT /api/radio-channels/{channelId}/assignments/{unitType}/{unitId} ───
    // AC-04: returns 409 with conflict details when overrideValidation=false and conflicts exist

    [HttpPut("api/radio-channels/{channelId:guid}/assignments/{unitType}/{unitId:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<RadioChannelAssignmentDto>> UpsertAssignment(
        Guid channelId,
        string unitType,
        Guid unitId,
        [FromBody] AssignFrequencyRequest request)
    {
        try
        {
            var result = await _assignmentService.UpsertAssignmentAsync(channelId, unitType, unitId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (InvalidOperationException ex)
        {
            // AC-04: 409 with conflict detail in extensions
            return Problem(
                title: "Frequency Conflict",
                detail: ex.Message,
                statusCode: 409);
        }
        catch (ArgumentException ex) { return Problem(title: "Bad Request", detail: ex.Message, statusCode: 400); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── DELETE /api/radio-channels/{channelId}/assignments/{unitType}/{unitId} ─

    [HttpDelete("api/radio-channels/{channelId:guid}/assignments/{unitType}/{unitId:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> DeleteAssignment(
        Guid channelId,
        string unitType,
        Guid unitId)
    {
        try
        {
            await _assignmentService.DeleteAssignmentAsync(channelId, unitType, unitId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }

    // ── GET /api/events/{eventId}/radio-channels/conflicts ───────────────────
    // AC-07: planner can view conflict summary

    [HttpGet("api/events/{eventId:guid}/radio-channels/conflicts")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<ConflictSummaryDto>> GetConflictSummary(Guid eventId)
    {
        try
        {
            var result = await _conflictSummaryService.GetConflictSummaryAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }
}
