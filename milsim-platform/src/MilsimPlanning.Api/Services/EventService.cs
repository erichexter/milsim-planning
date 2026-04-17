using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Events;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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
            CreatedById = _currentUser.UserId,  // Track creator for EventOwner role assignment
            Faction = new Faction
            {
                CommanderId = _currentUser.UserId,
                Name = request.Name   // faction name defaults to event name; can be changed later
            }
        };

        _db.Events.Add(evt);
        await _db.SaveChangesAsync();

        // Auto-assign creator as EventOwner so they can manage the event and assign faction commanders.
        // EventOwner (level 5) inherits all FactionCommander permissions (level 4).
        _db.EventMemberships.Add(new EventMembership
        {
            UserId = _currentUser.UserId,
            EventId = evt.Id,
            Role = AppRoles.EventOwner,  // Event creator is the EventOwner
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

    /// <summary>
    /// Get all members of an event with pagination.
    /// EventOwner can see all members; FactionCommander can see their faction's members.
    /// </summary>
    public async Task<EventMembersDto> GetEventMembersAsync(Guid eventId, int pageNumber = 1, int pageSize = 50)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        var query = _db.EventMemberships
            .Where(m => m.EventId == eventId)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User.Email);

        var totalCount = await query.CountAsync();
        var members = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var memberDtos = members.Select(m => new EventMemberDto(
            m.Id,
            m.UserId,
            m.User.Email,
            m.Role,
            GetRoleLabel(m.Role),
            null,  // TODO: FactionId when Faction assignment is implemented
            m.JoinedAt
        )).ToList();

        return new EventMembersDto(memberDtos, totalCount, pageSize, pageNumber);
    }

    /// <summary>
    /// Get event summary with aggregated statistics and role breakdown.
    /// </summary>
    public async Task<EventSummaryDto> GetEventSummaryAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        var memberships = await _db.EventMemberships
            .Where(m => m.EventId == eventId)
            .Include(m => m.User)
            .ToListAsync();

        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == evt.CreatedById);

        var roleBreakdown = new RoleBreakdownDto(
            memberships.Count(m => m.Role == AppRoles.Player),
            memberships.Count(m => m.Role == AppRoles.SquadLeader),
            memberships.Count(m => m.Role == AppRoles.PlatoonLeader),
            memberships.Count(m => m.Role == AppRoles.FactionCommander),
            memberships.Count(m => m.Role == AppRoles.EventOwner)
        );

        return new EventSummaryDto(
            evt.Id,
            evt.Name,
            evt.StartDate,
            evt.Location,
            new CreatorDto(creator?.Id ?? evt.CreatedById, creator?.Email ?? "Unknown", creator?.UserName),
            DateTime.UtcNow,  // TODO: Add CreatedAt timestamp to Event entity
            memberships.Count,
            1,  // 1:1 Event-Faction in v1
            roleBreakdown
        );
    }

    /// <summary>
    /// Update a member's role within the event.
    /// Only EventOwner can invoke this; cannot promote user above EventOwner level.
    /// </summary>
    public async Task<EventMemberDto> UpdateMemberRoleAsync(Guid eventId, string userId, UpdateEventMemberRoleRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);
        AssertEventOwnerAccess(eventId);  // Only EventOwner can assign roles

        var evt = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        var membership = await _db.EventMemberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == userId)
            ?? throw new KeyNotFoundException($"Member not found in event");

        // Validate new role
        if (!AppRoles.Hierarchy.ContainsKey(request.NewRole))
            throw new ArgumentException($"Invalid role: {request.NewRole}");

        // Prevent assigning roles above EventOwner level
        if (AppRoles.Hierarchy[request.NewRole] > AppRoles.Hierarchy[AppRoles.EventOwner])
            throw new ArgumentException("Cannot assign role above EventOwner level");

        membership.Role = request.NewRole;
        await _db.SaveChangesAsync();

        return new EventMemberDto(
            membership.Id,
            membership.UserId,
            membership.User.Email,
            membership.Role,
            GetRoleLabel(membership.Role),
            null,
            membership.JoinedAt
        );
    }

    /// <summary>
    /// Remove a member from the event (delete EventMembership).
    /// Only EventOwner can invoke this; cannot remove themselves.
    /// </summary>
    public async Task RemoveMemberAsync(Guid eventId, string userId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);
        AssertEventOwnerAccess(eventId);  // Only EventOwner can remove members

        if (userId == _currentUser.UserId)
            throw new InvalidOperationException("Cannot remove yourself from the event");

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == userId)
            ?? throw new KeyNotFoundException($"Member not found in event");

        _db.EventMemberships.Remove(membership);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Assert that the current user has EventOwner role in the specified event.
    /// Throws ForbiddenException if not.
    /// </summary>
    private async Task AssertEventOwnerAccess(Guid eventId)
    {
        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId)
            ?? throw new ForbiddenException($"User does not have access to event {eventId}");

        if (membership.Role != AppRoles.EventOwner && _currentUser.Role != AppRoles.SystemAdmin)
            throw new ForbiddenException($"User must be EventOwner to perform this operation");
    }

    /// <summary>
    /// Get display label for a role (for UI rendering).
    /// </summary>
    private static string GetRoleLabel(string roleName) => roleName switch
    {
        AppRoles.Player => "Player",
        AppRoles.SquadLeader => "Squad Leader",
        AppRoles.PlatoonLeader => "Platoon Leader",
        AppRoles.FactionCommander => "Faction Commander",
        AppRoles.EventOwner => "Event Owner",
        AppRoles.SystemAdmin => "System Admin",
        _ => "Unknown"
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
