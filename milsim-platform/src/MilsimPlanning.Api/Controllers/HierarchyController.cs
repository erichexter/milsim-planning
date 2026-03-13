using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Hierarchy;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class HierarchyController : ControllerBase
{
    private readonly HierarchyService _hierarchyService;

    public HierarchyController(HierarchyService hierarchyService)
        => _hierarchyService = hierarchyService;

    // ── HIER-01: Create Platoon ───────────────────────────────────────────────

    [HttpPost("api/events/{eventId:guid}/platoons")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> CreatePlatoon(Guid eventId, [FromBody] CreatePlatoonRequest request)
    {
        try
        {
            var platoon = await _hierarchyService.CreatePlatoonAsync(eventId, request.Name);
            return Created(
                $"/api/events/{eventId}/platoons/{platoon.Id}",
                new { id = platoon.Id, name = platoon.Name }
            );
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // ── HIER-02: Create Squad within Platoon ──────────────────────────────────

    [HttpPost("api/platoons/{platoonId:guid}/squads")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> CreateSquad(Guid platoonId, [FromBody] CreateSquadRequest request)
    {
        try
        {
            var squad = await _hierarchyService.CreateSquadAsync(platoonId, request.Name);
            return Created(
                $"/api/platoons/{platoonId}/squads/{squad.Id}",
                new { id = squad.Id, name = squad.Name }
            );
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // ── HIER-03 / HIER-04 / HIER-05: Assign/move player to squad ─────────────

    [HttpPut("api/event-players/{playerId:guid}/squad")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> AssignSquad(Guid playerId, [FromBody] AssignSquadRequest request)
    {
        try
        {
            await _hierarchyService.AssignSquadAsync(playerId, request.SquadId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // ── HIER-06: Get Roster Hierarchy (available to all faction members) ──────

    [HttpGet("api/events/{eventId:guid}/roster")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<ActionResult<RosterHierarchyDto>> GetRoster(Guid eventId)
    {
        try
        {
            var roster = await _hierarchyService.GetRosterHierarchyAsync(eventId);
            return Ok(roster);
        }
        catch (ForbiddenException) { return Forbid(); }
    }
}
