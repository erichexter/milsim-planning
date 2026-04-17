using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

public class EventService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<AppUser> _userManager;

    public EventService(AppDbContext db, ICurrentUser currentUser, UserManager<AppUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
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

        // Auto-enroll the creator as EventOwner so ScopeGuard.AssertEventAccess passes for all
        // subsequent roster/hierarchy/content endpoints on this event.
        _db.EventMemberships.Add(new EventMembership
        {
            UserId = _currentUser.UserId,
            EventId = evt.Id,
            Role = AppRoles.EventOwner,
            JoinedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return ToDto(evt);
    }

    /// <summary>EVNT-03: List events the current user is a member of (commanders and players alike).</summary>
    public async Task<List<EventDto>> ListEventsAsync()
    {
        var userId = _currentUser.UserId;

        var events = await _db.Events
            .Include(e => e.Faction)
            .Where(e => _db.EventMemberships.Any(m => m.EventId == e.Id && m.UserId == userId))
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

    /// <summary>EVNT-01b: Update editable fields on an existing event.</summary>
    public async Task<EventDto> UpdateEventAsync(Guid eventId, UpdateEventRequest request)
    {
        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");

        evt.Name = request.Name;
        evt.Location = request.Location;
        evt.Description = request.Description;
        evt.StartDate = request.StartDate;
        evt.EndDate = request.EndDate;

        await _db.SaveChangesAsync();
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

    /// <summary>MEMB-01: List event members with optional pagination.</summary>
    public async Task<EventMembersListDto> GetEventMembersAsync(Guid eventId, int pageSize = 50, int pageNumber = 1)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Validate event exists
        var eventExists = await _db.Events.AnyAsync(e => e.Id == eventId);
        if (!eventExists)
            throw new KeyNotFoundException($"Event {eventId} not found");

        var query = _db.EventMemberships
            .Include(m => m.User)
            .Where(m => m.EventId == eventId);

        var totalCount = await query.CountAsync();
        var members = await query
            .OrderByDescending(m => m.JoinedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var memberDtos = members.Select(m => new EventMemberDto(
            m.UserId,
            m.User.Email ?? "unknown",
            m.Role,
            GetRoleLabel(m.Role),
            null, // FactionId — we don't track which faction a user commands in this design
            m.JoinedAt
        )).ToList();

        return new EventMembersListDto(memberDtos, totalCount, pageSize, pageNumber);
    }

    /// <summary>MEMB-02: Assign a user as Faction Commander within an event.</summary>
    public async Task<EventMemberDto> AssignFactionCommanderAsync(Guid eventId, string userId, Guid factionId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Only EventOwner (level 5) can assign faction commanders
        if (!_currentUser.EventMembershipIds.Contains(eventId))
            throw new ForbiddenException($"User does not have access to event {eventId}");

        var currentMembership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId);

        if (currentMembership?.Role != AppRoles.EventOwner && _currentUser.Role != AppRoles.SystemAdmin)
            throw new ForbiddenException("Only EventOwner or SystemAdmin can assign faction commanders");

        // Validate user exists
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found");

        // Validate event and faction exist
        var evt = await _db.Events.FindAsync(eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        var faction = await _db.Factions.FindAsync(factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found");

        if (faction.EventId != eventId)
            throw new ArgumentException("Faction does not belong to this event");

        // Check if user is already a faction commander in a different faction in the same event
        var existingFc = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == userId && m.Role == AppRoles.FactionCommander);

        if (existingFc != null)
            throw new InvalidOperationException($"User is already assigned as Faction Commander in this event");

        // Remove any existing EventMembership for this user in this event
        var existingMembership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == userId);

        if (existingMembership != null)
            _db.EventMemberships.Remove(existingMembership);

        // Create new faction commander membership
        var membership = new EventMembership
        {
            UserId = userId,
            EventId = eventId,
            Role = AppRoles.FactionCommander,
            JoinedAt = DateTime.UtcNow
        };

        _db.EventMemberships.Add(membership);

        // Update faction commander
        faction.CommanderId = userId;

        await _db.SaveChangesAsync();

        return new EventMemberDto(
            user.Id,
            user.Email ?? "unknown",
            AppRoles.FactionCommander,
            GetRoleLabel(AppRoles.FactionCommander),
            factionId,
            membership.JoinedAt
        );
    }

    /// <summary>MEMB-03: Remove a member from an event.</summary>
    public async Task RemoveMemberAsync(Guid eventId, string userId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Only EventOwner (level 5) can remove members
        var currentMembership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId);

        if (currentMembership?.Role != AppRoles.EventOwner && _currentUser.Role != AppRoles.SystemAdmin)
            throw new ForbiddenException("Only EventOwner or SystemAdmin can remove members");

        // Prevent self-removal
        if (userId == _currentUser.UserId)
            throw new InvalidOperationException("Cannot remove yourself from the event");

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == userId)
            ?? throw new KeyNotFoundException($"Member {userId} not found in event {eventId}");

        _db.EventMemberships.Remove(membership);
        await _db.SaveChangesAsync();
    }

    /// <summary>MEMB-04: Get event summary with statistics.</summary>
    public async Task<EventSummaryDto> GetEventSummaryAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
                .ThenInclude(f => f.Platoons)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        var creatorMembership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.Role == AppRoles.EventOwner);

        var creator = creatorMembership != null
            ? await _userManager.FindByIdAsync(creatorMembership.UserId)
            : null;

        // Count role breakdown
        var members = await _db.EventMemberships
            .Where(m => m.EventId == eventId)
            .ToListAsync();

        var roleBreakdown = new RoleBreakdownDto(
            members.Count(m => m.Role == AppRoles.Player),
            members.Count(m => m.Role == AppRoles.SquadLeader),
            members.Count(m => m.Role == AppRoles.PlatoonLeader),
            members.Count(m => m.Role == AppRoles.FactionCommander),
            members.Count(m => m.Role == AppRoles.EventOwner)
        );

        return new EventSummaryDto(
            evt.Id,
            evt.Name,
            evt.StartDate,
            evt.Location,
            new CreatedByDto(
                creator?.Id ?? "unknown",
                creator?.Email ?? "unknown",
                creator?.UserName
            ),
            DateTime.UtcNow,
            members.Count,
            evt.Faction?.Platoons?.Count ?? 0,
            roleBreakdown
        );
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

    /// <summary>Convert role constant to display label.</summary>
    private static string GetRoleLabel(string role) => role switch
    {
        AppRoles.Player => "Player",
        AppRoles.SquadLeader => "Squad Leader",
        AppRoles.PlatoonLeader => "Platoon Leader",
        AppRoles.FactionCommander => "Faction Commander",
        AppRoles.EventOwner => "Event Owner",
        AppRoles.SystemAdmin => "System Admin",
        _ => role
    };

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
