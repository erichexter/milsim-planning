using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Events;
using MilsimPlanning.Api.Services;
using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly EventService _eventService;

    public EventsController(EventService eventService)
        => _eventService = eventService;

    // EVNT-01: Create event
    [HttpPost]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<EventDto>> Create(CreateEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        var dto = await _eventService.CreateEventAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    // EVNT-03: List events (scoped to commander)
    [HttpGet]
    public async Task<ActionResult<List<EventDto>>> List()
    {
        var events = await _eventService.ListEventsAsync();
        return Ok(events);
    }

    // Helper route for CreatedAtAction (also used in tests to fetch individual events)
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventDto>> GetById(Guid id)
    {
        var dto = await _eventService.GetEventAsync(id);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    // EVNT-01b: Update event
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<EventDto>> Update(Guid id, UpdateEventRequest request)
    {
        try
        {
            var dto = await _eventService.UpdateEventAsync(id, request);
            return Ok(dto);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // EVNT-05: Publish event
    [HttpPut("{id:guid}/publish")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> Publish(Guid id)
    {
        try
        {
            await _eventService.PublishEventAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException) { return Conflict(new { error = "Event is already published" }); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // EVNT-02: Duplicate event
    [HttpPost("{id:guid}/duplicate")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<ActionResult<EventDto>> Duplicate(Guid id, DuplicateEventRequest request)
    {
        try
        {
            var dto = await _eventService.DuplicateEventAsync(id, request);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // EventOwner: Get event summary with role breakdown
    [HttpGet("{id:guid}/summary")]
    public async Task<ActionResult<EventSummaryDto>> GetSummary(Guid id)
    {
        try
        {
            var dto = await _eventService.GetEventSummaryAsync(id);
            return Ok(dto);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // EventOwner: Get event members with pagination
    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<EventMembersDto>> GetMembers(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var dto = await _eventService.GetEventMembersAsync(id, pageNumber, pageSize);
            return Ok(dto);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // EventOwner: Update member role (assign FactionCommander, etc.)
    [HttpPatch("{id:guid}/members/{userId}/role")]
    [Authorize(Policy = "RequireEventOwner")]
    public async Task<ActionResult<EventMemberDto>> UpdateMemberRole(Guid id, string userId, UpdateEventMemberRoleRequest request)
    {
        try
        {
            var dto = await _eventService.UpdateMemberRoleAsync(id, userId, request);
            return Ok(dto);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // EventOwner: Remove member from event
    [HttpDelete("{id:guid}/members/{userId}")]
    [Authorize(Policy = "RequireEventOwner")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId)
    {
        try
        {
            await _eventService.RemoveMemberAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ForbiddenException) { return Forbid(); }
    }
}
