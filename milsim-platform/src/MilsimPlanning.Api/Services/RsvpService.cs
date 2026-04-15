using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Rsvp;

namespace MilsimPlanning.Api.Services;

public class RsvpService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "Attending", "NotAttending", "Maybe"
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RsvpService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RsvpDto> SetRsvpAsync(Guid eventId, SetRsvpRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var eventExists = await _db.Events.AnyAsync(e => e.Id == eventId);
        if (!eventExists)
            throw new KeyNotFoundException($"Event {eventId} not found.");

        var hasEventPlayer = await _db.EventPlayers
            .AnyAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);
        if (!hasEventPlayer)
            throw new ForbiddenException("Player is not assigned to a faction in this event.");

        if (!ValidStatuses.Contains(request.Status))
            throw new ArgumentException("Invalid RSVP status. Must be one of: Attending, NotAttending, Maybe.");

        var rsvp = await _db.EventRsvps
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == _currentUser.UserId);

        var now = DateTime.UtcNow;

        if (rsvp == null)
        {
            rsvp = new EventRsvp
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                UserId = _currentUser.UserId,
                Status = request.Status,
                RespondedAt = now
            };
            _db.EventRsvps.Add(rsvp);
        }
        else
        {
            rsvp.Status = request.Status;
            rsvp.RespondedAt = now;
        }

        await _db.SaveChangesAsync();

        return ToDto(rsvp);
    }

    public async Task<RsvpDto?> GetRsvpAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var eventExists = await _db.Events.AnyAsync(e => e.Id == eventId);
        if (!eventExists)
            throw new KeyNotFoundException($"Event {eventId} not found.");

        var rsvp = await _db.EventRsvps
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == _currentUser.UserId);

        return rsvp == null ? null : ToDto(rsvp);
    }

    private static RsvpDto ToDto(EventRsvp rsvp) => new()
    {
        EventId = rsvp.EventId,
        UserId = rsvp.UserId,
        Status = rsvp.Status,
        RespondedAt = rsvp.RespondedAt
    };
}
