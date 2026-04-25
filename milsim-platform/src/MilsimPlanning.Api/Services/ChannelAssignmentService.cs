using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.ChannelAssignments;

namespace MilsimPlanning.Api.Services;

public class ChannelAssignmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly NatoFrequencyValidationService _validator;

    public ChannelAssignmentService(AppDbContext db, ICurrentUser currentUser, NatoFrequencyValidationService validator)
    {
        _db = db;
        _currentUser = currentUser;
        _validator = validator;
    }

    // ── GET /api/events/{eventId}/channel-assignments ─────────────────────────

    public async Task<ChannelAssignmentListDto> GetAssignmentsAsync(Guid eventId, int limit, int offset)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var query = _db.ChannelAssignments
            .Include(a => a.RadioChannel)
            .Include(a => a.Squad)
            .Where(a => a.EventId == eventId)
            .OrderBy(a => a.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip(offset).Take(limit).ToListAsync();

        return new ChannelAssignmentListDto
        {
            Total = total,
            Items = items.Select(ToDto).ToList()
        };
    }

    // ── POST /api/events/{eventId}/channel-assignments ────────────────────────

    public async Task<ChannelAssignmentDto> CreateAssignmentAsync(Guid eventId, CreateChannelAssignmentRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        AssertCommanderAccess(evt.Faction);

        // Verify RadioChannel belongs to this event
        var channel = await _db.RadioChannels
            .FirstOrDefaultAsync(c => c.Id == request.RadioChannelId && c.EventId == eventId)
            ?? throw new KeyNotFoundException($"RadioChannel {request.RadioChannelId} not found in event {eventId}.");

        // Verify Squad exists
        var squad = await _db.Squads
            .FirstOrDefaultAsync(s => s.Id == request.SquadId)
            ?? throw new KeyNotFoundException($"Squad {request.SquadId} not found.");

        // Validate frequency against channel scope
        _validator.Validate(request.PrimaryFrequency, channel.Scope);

        var now = DateTime.UtcNow;
        var assignment = new ChannelAssignment
        {
            Id = Guid.NewGuid(),
            RadioChannelId = request.RadioChannelId,
            SquadId = request.SquadId,
            PrimaryFrequency = request.PrimaryFrequency,
            EventId = eventId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ChannelAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        // Reload with navigation properties for DTO mapping
        assignment.RadioChannel = channel;
        assignment.Squad = squad;

        return ToDto(assignment);
    }

    // ── PUT /api/events/{eventId}/channel-assignments/{id} ───────────────────

    public async Task<ChannelAssignmentDto> UpdateAssignmentAsync(Guid eventId, Guid assignmentId, UpdateChannelAssignmentRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        AssertCommanderAccess(evt.Faction);

        var assignment = await _db.ChannelAssignments
            .Include(a => a.RadioChannel)
            .Include(a => a.Squad)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.EventId == eventId)
            ?? throw new KeyNotFoundException($"ChannelAssignment {assignmentId} not found.");

        // Validate frequency against channel scope
        _validator.Validate(request.PrimaryFrequency, assignment.RadioChannel.Scope);

        assignment.PrimaryFrequency = request.PrimaryFrequency;
        assignment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return ToDto(assignment);
    }

    // ── DELETE /api/events/{eventId}/channel-assignments/{id} ────────────────

    public async Task DeleteAssignmentAsync(Guid eventId, Guid assignmentId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        AssertCommanderAccess(evt.Faction);

        var assignment = await _db.ChannelAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.EventId == eventId)
            ?? throw new KeyNotFoundException($"ChannelAssignment {assignmentId} not found.");

        // Soft delete
        assignment.IsDeleted = true;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }

    private static ChannelAssignmentDto ToDto(ChannelAssignment a) => new()
    {
        Id = a.Id,
        RadioChannelId = a.RadioChannelId,
        ChannelName = a.RadioChannel.Name,
        ChannelScope = a.RadioChannel.Scope.ToString(),
        SquadId = a.SquadId,
        SquadName = a.Squad.Name,
        PrimaryFrequency = a.PrimaryFrequency,
        EventId = a.EventId,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}
