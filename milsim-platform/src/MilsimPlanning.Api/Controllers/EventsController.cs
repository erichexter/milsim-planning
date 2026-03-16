using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Events;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize(Policy = "RequireFactionCommander")]
public class EventsController : ControllerBase
{
    private readonly EventService _eventService;

    public EventsController(EventService eventService)
        => _eventService = eventService;

    // EVNT-01: Create event
    [HttpPost]
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
}
