using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Models.Notifications;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/roster-change-decisions")]
public class RosterChangeDecisionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationQueue _notificationQueue;
    private readonly ICurrentUser _currentUser;

    public RosterChangeDecisionsController(AppDbContext db, INotificationQueue notificationQueue, ICurrentUser currentUser)
    {
        _db = db;
        _notificationQueue = notificationQueue;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> QueueRosterDecision(Guid eventId, [FromBody] QueueRosterDecisionRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        if (!string.Equals(request.Decision, "approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Decision, "denied", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Decision must be 'approved' or 'denied'." });
        }

        if (string.IsNullOrWhiteSpace(request.RequestedChangeSummary))
        {
            return BadRequest(new { error = "RequestedChangeSummary is required." });
        }

        var eventPlayer = await _db.EventPlayers
            .Include(ep => ep.Event)
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.Id == request.EventPlayerId);

        if (eventPlayer is null)
        {
            return NotFound();
        }

        if (eventPlayer.UserId is null || string.IsNullOrWhiteSpace(eventPlayer.Email))
        {
            return UnprocessableEntity(new
            {
                error = "Cannot send roster decision email because the player has not completed account registration."
            });
        }

        await _notificationQueue.EnqueueAsync(new RosterChangeDecisionJob(
            RecipientEmail: eventPlayer.Email,
            RecipientName: eventPlayer.Name,
            EventName: eventPlayer.Event.Name,
            Decision: request.Decision,
            RequestedChangeSummary: request.RequestedChangeSummary,
            CommanderNote: request.CommanderNote
        ));

        return Accepted(new { queued = true });
    }
}
