using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/notification-blasts")]
public class NotificationBlastsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly INotificationQueue _notificationQueue;
    private readonly ICurrentUser _currentUser;

    public NotificationBlastsController(AppDbContext db, INotificationQueue notificationQueue, ICurrentUser currentUser)
    {
        _db = db;
        _notificationQueue = notificationQueue;
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> SendBlast(Guid eventId, [FromBody] SendNotificationBlastRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Subject and body are required" });

        var recipientEmails = await _db.EventPlayers
            .Where(ep => ep.EventId == eventId && ep.UserId != null && ep.Email != null)
            .Select(ep => ep.Email)
            .Distinct()
            .ToListAsync();

        var blast = new NotificationBlast
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Subject = request.Subject,
            Body = request.Body,
            SentAt = DateTime.UtcNow,
            RecipientCount = 0
        };

        _db.NotificationBlasts.Add(blast);
        await _db.SaveChangesAsync();

        await _notificationQueue.EnqueueAsync(new BlastNotificationJob(
            eventId,
            blast.Id,
            request.Subject,
            request.Body,
            recipientEmails));

        return Accepted(new { blastId = blast.Id, recipientCount = recipientEmails.Count });
    }

    [HttpGet]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetBlastLog(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var blasts = await _db.NotificationBlasts
            .Where(b => b.EventId == eventId)
            .OrderByDescending(b => b.SentAt)
            .Select(b => new
            {
                b.Id,
                b.Subject,
                b.SentAt,
                b.RecipientCount
            })
            .ToListAsync();

        return Ok(blasts);
    }
}

public class SendNotificationBlastRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
