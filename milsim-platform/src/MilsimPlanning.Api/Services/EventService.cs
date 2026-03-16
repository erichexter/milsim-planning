using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Events;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

public class EventService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public EventService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>EVNT-01: Create a new event in Draft status with a 1:1 Faction.</summary>
    public async Task<EventDto> CreateEventAsync(CreateEventRequest request)
    {
        var evt = new Event
        {
            Name = request.Name,
            Location = request.Location,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = EventStatus.Draft,
            Faction = new Faction
            {
                CommanderId = _currentUser.UserId,
                Name = request.Name   // faction name defaults to event name; can be changed later
            }
        };

        _db.Events.Add(evt);
        await _db.SaveChangesAsync();

        // Auto-enroll the commander so ScopeGuard.AssertEventAccess passes for all
        // subsequent roster/hierarchy/content endpoints on this event.
        _db.EventMemberships.Add(new EventMembership
        {
            UserId = _currentUser.UserId,
            EventId = evt.Id,
            Role = AppRoles.FactionCommander,
            JoinedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return ToDto(evt);
    }

    /// <summary>EVNT-03: List events scoped to the current commander.</summary>
    public async Task<List<EventDto>> ListEventsAsync()
    {
        var events = await _db.Events
            .Include(e => e.Faction)
            .Where(e => e.Faction.CommanderId == _currentUser.UserId)
            .OrderByDescending(e => e.Id)   // newest first (no CreatedAt in Phase 2 model)
            .ToListAsync();

        return events.Select(ToDto).ToList();
    }

    /// <summary>EVNT-01 helper: get a single event (scoped to commander).</summary>
    public async Task<EventDto?> GetEventAsync(Guid eventId)
    {
        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt is null) return null;

        AssertCommanderAccess(evt.Faction);
        return ToDto(evt);
    }

    /// <summary>EVNT-05: Publish event — status flip only, no email (EVNT-06).</summary>
    public async Task PublishEventAsync(Guid eventId)
    {
        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        if (evt.Status == EventStatus.Published)
            throw new InvalidOperationException("Event is already published");

        evt.Status = EventStatus.Published;
        await _db.SaveChangesAsync();
        // EVNT-06: DO NOT call any email service here. Notifications are a Phase 3 concern.
    }

    /// <summary>EVNT-02: Duplicate event — always copies Platoon/Squad structure; never copies roster or dates.</summary>
    public async Task<EventDto> DuplicateEventAsync(Guid sourceEventId, DuplicateEventRequest request)
    {
        var sourceEvent = await _db.Events
            .Include(e => e.Faction)
                .ThenInclude(f => f.Platoons)
                    .ThenInclude(p => p.Squads)
            .FirstOrDefaultAsync(e => e.Id == sourceEventId)
            ?? throw new KeyNotFoundException($"Event {sourceEventId} not found");

        AssertCommanderAccess(sourceEvent.Faction);

        var newEvent = new Event
        {
            Name = $"{sourceEvent.Name} (Copy)",
            Location = sourceEvent.Location,
            Description = sourceEvent.Description,
            StartDate = null,       // LOCKED: dates never copied (EVNT-02)
            EndDate = null,         // LOCKED: dates never copied (EVNT-02)
            Status = EventStatus.Draft,   // LOCKED: always Draft (EVNT-04)
            Faction = new Faction
            {
                CommanderId = sourceEvent.Faction.CommanderId,
                Name = sourceEvent.Faction.Name,
                Platoons = sourceEvent.Faction.Platoons.Select(p => new Platoon
                {
                    Name = p.Name,
                    Order = p.Order,
                    Squads = p.Squads.Select(s => new Squad
                    {
                        Name = s.Name,
                        Order = s.Order
                    }).ToList()
                }).ToList()
            }
        };

        // Phase 3: Copy selected InfoSections (+ their Attachments) when CopyInfoSectionIds is non-empty
        if (request.CopyInfoSectionIds.Any())
        {
            var sectionsToClone = await _db.InfoSections
                .Include(s => s.Attachments)
                .Where(s => request.CopyInfoSectionIds.Contains(s.Id) && s.EventId == sourceEventId)
                .ToListAsync();

            newEvent.InfoSections = sectionsToClone.Select(s => new InfoSection
            {
                Title = s.Title,
                BodyMarkdown = s.BodyMarkdown,
                Order = s.Order,
                Attachments = s.Attachments.Select(a => new InfoSectionAttachment
                {
                    R2Key = a.R2Key,          // shared R2 object — no R2 copy
                    FriendlyName = a.FriendlyName,
                    ContentType = a.ContentType,
                    FileSizeBytes = a.FileSizeBytes
                }).ToList()
            }).ToList();
        }

        _db.Events.Add(newEvent);
        await _db.SaveChangesAsync();
        return ToDto(newEvent);
    }

    /// <summary>
    /// Assert that the current user is the commander of this faction.
    /// Throws ForbiddenException (HTTP 403) if not.
    /// This is distinct from ScopeGuard.AssertEventAccess (which checks EventMembership).
    /// For event write operations, we check faction ownership (CommanderId).
    /// </summary>
    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException($"User is not the commander of this event's faction");
    }

    private static EventDto ToDto(Event evt) => new(
        evt.Id,
        evt.Name,
        evt.Location,
        evt.Description,
        evt.StartDate,
        evt.EndDate,
        evt.Status.ToString()
    );
}
