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

        // Validate alternate frequency if provided
        if (request.AlternateFrequency.HasValue)
        {
            _validator.Validate(request.AlternateFrequency.Value, channel.Scope);
            if (request.AlternateFrequency.Value == request.PrimaryFrequency)
                throw new ArgumentException("Alternate frequency cannot match primary frequency");
        }

        // AC-04: Check for frequency conflicts with other units on the same channel
        await AssertNoFrequencyConflictsAsync(request.RadioChannelId, request.PrimaryFrequency, request.AlternateFrequency, excludeAssignmentId: null);

        var now = DateTime.UtcNow;
        var assignment = new ChannelAssignment
        {
            Id = Guid.NewGuid(),
            RadioChannelId = request.RadioChannelId,
            SquadId = request.SquadId,
            PrimaryFrequency = request.PrimaryFrequency,
            AlternateFrequency = request.AlternateFrequency,
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

        // Validate alternate frequency if provided
        if (request.AlternateFrequency.HasValue)
        {
            _validator.Validate(request.AlternateFrequency.Value, assignment.RadioChannel.Scope);
            if (request.AlternateFrequency.Value == request.PrimaryFrequency)
                throw new ArgumentException("Alternate frequency cannot match primary frequency");
        }

        // AC-04: Check for frequency conflicts with other units on the same channel (exclude self)
        await AssertNoFrequencyConflictsAsync(assignment.RadioChannelId, request.PrimaryFrequency, request.AlternateFrequency, excludeAssignmentId: assignmentId);

        assignment.PrimaryFrequency = request.PrimaryFrequency;
        assignment.AlternateFrequency = request.AlternateFrequency;
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

    /// <summary>
    /// AC-04: Checks if a primary or alternate frequency conflicts with any existing
    /// (non-deleted) assignment on the same radio channel. Excludes the calling
    /// assignment's own record (for updates).
    /// </summary>
    private async Task AssertNoFrequencyConflictsAsync(
        Guid radioChannelId,
        decimal primaryFrequency,
        decimal? alternateFrequency,
        Guid? excludeAssignmentId)
    {
        var existingAssignments = await _db.ChannelAssignments
            .Include(a => a.Squad)
            .Where(a => a.RadioChannelId == radioChannelId && !a.IsDeleted)
            .Where(a => excludeAssignmentId == null || a.Id != excludeAssignmentId.Value)
            .ToListAsync();

        foreach (var existing in existingAssignments)
        {
            // Check primary frequency against existing primary
            if (existing.PrimaryFrequency == primaryFrequency)
                throw new FrequencyConflictException(existing.Squad.Name, primaryFrequency, "primary");

            // Check primary frequency against existing alternate
            if (existing.AlternateFrequency.HasValue && existing.AlternateFrequency.Value == primaryFrequency)
                throw new FrequencyConflictException(existing.Squad.Name, primaryFrequency, "alternate");

            // Check alternate frequency (if provided) against existing primary
            if (alternateFrequency.HasValue && existing.PrimaryFrequency == alternateFrequency.Value)
                throw new FrequencyConflictException(existing.Squad.Name, alternateFrequency.Value, "primary");

            // Check alternate frequency (if provided) against existing alternate
            if (alternateFrequency.HasValue && existing.AlternateFrequency.HasValue
                && existing.AlternateFrequency.Value == alternateFrequency.Value)
                throw new FrequencyConflictException(existing.Squad.Name, alternateFrequency.Value, "alternate");
        }
    }

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
        AlternateFrequency = a.AlternateFrequency,
        EventId = a.EventId,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}
