using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Models.RosterChangeRequests;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/roster-change-requests")]
public class RosterChangeRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationQueue _notificationQueue;

    public RosterChangeRequestsController(
        AppDbContext db,
        ICurrentUser currentUser,
        INotificationQueue notificationQueue)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationQueue = notificationQueue;
    }

    // ── Player Endpoints ───────────────────────────────────────────────────

    /// <summary>POST — player submits a change request</summary>
    [HttpPost]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> Submit(Guid eventId, [FromBody] SubmitChangeRequestDto request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Business rule: one pending request per player per event
        var existingPending = await _db.RosterChangeRequests
            .AnyAsync(r => r.EventPlayer.EventId == eventId
                        && r.EventPlayer.UserId == _currentUser.UserId
                        && r.Status == RosterChangeStatus.Pending);
        if (existingPending)
            return Conflict(new { error = "You already have a pending change request for this event." });

        // Find the EventPlayer for this user/event
        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);
        if (eventPlayer is null)
            return NotFound();

        var changeRequest = new RosterChangeRequest
        {
            EventId = eventId,
            EventPlayerId = eventPlayer.Id,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };
        _db.RosterChangeRequests.Add(changeRequest);
        await _db.SaveChangesAsync();

        return Created($"api/events/{eventId}/roster-change-requests/{changeRequest.Id}",
            new { id = changeRequest.Id, status = changeRequest.Status.ToString() });
    }

    /// <summary>GET mine — player views own most recent request</summary>
    [HttpGet("mine")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetMine(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var request = await _db.RosterChangeRequests
            .Where(r => r.EventPlayer.EventId == eventId
                     && r.EventPlayer.UserId == _currentUser.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Note,
                Status = r.Status.ToString(),
                r.CommanderNote,
                r.CreatedAt,
                r.ResolvedAt
            })
            .FirstOrDefaultAsync();

        return request is null ? NoContent() : Ok(request);
    }

    /// <summary>DELETE — player cancels pending request</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> Cancel(Guid eventId, Guid id)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var request = await _db.RosterChangeRequests
            .Include(r => r.EventPlayer)
            .FirstOrDefaultAsync(r => r.Id == id
                                   && r.EventPlayer.EventId == eventId
                                   && r.EventPlayer.UserId == _currentUser.UserId);
        if (request is null) return NotFound();
        if (request.Status != RosterChangeStatus.Pending)
            return UnprocessableEntity(new { error = "Only pending requests can be cancelled." });

        _db.RosterChangeRequests.Remove(request);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── Commander Endpoints ────────────────────────────────────────────────

    /// <summary>GET — commander lists all pending requests for event</summary>
    [HttpGet]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> ListPending(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var requests = await _db.RosterChangeRequests
            .Include(r => r.EventPlayer)
            .Where(r => r.EventId == eventId && r.Status == RosterChangeStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Note,
                r.CreatedAt,
                Player = new
                {
                    r.EventPlayer.Name,
                    r.EventPlayer.Callsign,
                    r.EventPlayer.PlatoonId,
                    r.EventPlayer.SquadId
                }
            })
            .ToListAsync();

        return Ok(requests);
    }

    /// <summary>POST approve — commander approves, updating EventPlayer assignment atomically</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> Approve(Guid eventId, Guid id, [FromBody] ApproveChangeRequestDto request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var changeRequest = await _db.RosterChangeRequests
            .Include(r => r.EventPlayer)
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == id && r.EventId == eventId);
        if (changeRequest is null) return NotFound();
        if (changeRequest.Status != RosterChangeStatus.Pending)
            return UnprocessableEntity(new { error = "Request is no longer pending." });

        // Pitfall 5: validate squad belongs to the specified platoon
        var squad = await _db.Squads
            .FirstOrDefaultAsync(s => s.Id == request.SquadId && s.PlatoonId == request.PlatoonId);
        if (squad is null)
            return BadRequest(new { error = "Squad does not belong to the specified platoon." });

        // RCHG-05: update EventPlayer assignment atomically
        changeRequest.EventPlayer.PlatoonId = request.PlatoonId;
        changeRequest.EventPlayer.SquadId = request.SquadId;

        // Mark resolved
        changeRequest.Status = RosterChangeStatus.Approved;
        changeRequest.CommanderNote = request.CommanderNote;
        changeRequest.ResolvedAt = DateTime.UtcNow;

        // Single SaveChangesAsync: EventPlayer update + RosterChangeRequest.Status = Approved
        await _db.SaveChangesAsync();

        // RCHG-04: enqueue notification AFTER DB save succeeds (mirrors Phase 3 pattern)
        if (changeRequest.EventPlayer.UserId is not null
            && !string.IsNullOrWhiteSpace(changeRequest.EventPlayer.Email))
        {
            await _notificationQueue.EnqueueAsync(new RosterChangeDecisionJob(
                RecipientEmail: changeRequest.EventPlayer.Email,
                RecipientName: changeRequest.EventPlayer.Name,
                EventName: changeRequest.Event.Name,
                Decision: "approved",
                RequestedChangeSummary: changeRequest.Note,
                CommanderNote: request.CommanderNote
            ));
        }

        return NoContent();
    }

    /// <summary>POST deny — commander denies</summary>
    [HttpPost("{id:guid}/deny")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> Deny(Guid eventId, Guid id, [FromBody] DenyChangeRequestDto request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var changeRequest = await _db.RosterChangeRequests
            .Include(r => r.EventPlayer)
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == id && r.EventId == eventId);
        if (changeRequest is null) return NotFound();
        if (changeRequest.Status != RosterChangeStatus.Pending)
            return UnprocessableEntity(new { error = "Request is no longer pending." });

        changeRequest.Status = RosterChangeStatus.Denied;
        changeRequest.CommanderNote = request.CommanderNote;
        changeRequest.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // RCHG-04: notify player
        if (changeRequest.EventPlayer.UserId is not null
            && !string.IsNullOrWhiteSpace(changeRequest.EventPlayer.Email))
        {
            await _notificationQueue.EnqueueAsync(new RosterChangeDecisionJob(
                RecipientEmail: changeRequest.EventPlayer.Email,
                RecipientName: changeRequest.EventPlayer.Name,
                EventName: changeRequest.Event.Name,
                Decision: "denied",
                RequestedChangeSummary: changeRequest.Note,
                CommanderNote: request.CommanderNote
            ));
        }

        return NoContent();
    }
}
