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

        // Build conflict-with lists in bulk (no N+1)
        var conflictedIds = items.Where(a => a.HasConflict).Select(a => a.Id).ToHashSet();
        var conflictWithMap = new Dictionary<Guid, List<string>>();

        if (conflictedIds.Count > 0)
        {
            var radioChannelIds = items.Where(a => a.HasConflict).Select(a => a.RadioChannelId).Distinct().ToList();
            var relatedAssignments = await _db.ChannelAssignments
                .Include(a => a.Squad)
                .Where(a => radioChannelIds.Contains(a.RadioChannelId) && !conflictedIds.Contains(a.Id))
                .ToListAsync();

            foreach (var a in items.Where(a => a.HasConflict))
            {
                var names = relatedAssignments
                    .Where(x => x.RadioChannelId == a.RadioChannelId
                             && (x.PrimaryFrequency == a.PrimaryFrequency
                                 || (a.AlternateFrequency.HasValue && x.PrimaryFrequency == a.AlternateFrequency.Value)
                                 || (a.AlternateFrequency.HasValue && x.AlternateFrequency.HasValue && x.AlternateFrequency.Value == a.AlternateFrequency.Value)
                                 || (x.AlternateFrequency.HasValue && x.AlternateFrequency.Value == a.PrimaryFrequency)))
                    .Select(x => x.Squad.Name)
                    .Distinct()
                    .ToList();

                if (names.Count > 0)
                    conflictWithMap[a.Id] = names;
            }
        }

        return new ChannelAssignmentListDto
        {
            Total = total,
            Items = items.Select(a => ToDto(a, conflictWithMap.GetValueOrDefault(a.Id))).ToList()
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

        // AC-01/02/03: Detect frequency conflicts within operation (channel-scoped = operation-scoped)
        var conflicts = await DetectFrequencyConflictsAsync(
            request.RadioChannelId, request.PrimaryFrequency, request.AlternateFrequency,
            excludeAssignmentId: null);

        // AC-04: Advisory mode — if conflicts and no override, return 409 with conflict details
        if (conflicts.Count > 0 && !request.OverrideConflict)
        {
            var first = conflicts[0];
            throw new FrequencyConflictException(first.ExistingSquadName, first.ConflictingFreq, first.FreqType);
        }

        var hasConflict = conflicts.Count > 0;
        var now = DateTime.UtcNow;

        var assignment = new ChannelAssignment
        {
            Id = Guid.NewGuid(),
            RadioChannelId = request.RadioChannelId,
            SquadId = request.SquadId,
            PrimaryFrequency = request.PrimaryFrequency,
            AlternateFrequency = request.AlternateFrequency,
            HasConflict = hasConflict,
            EventId = eventId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ChannelAssignments.Add(assignment);

        // AC-05: Persist conflict records and mark conflicting assignments as conflicted
        if (hasConflict)
        {
            await PersistConflictRecordsAsync(eventId, assignment.SquadId, squad.Name, conflicts);

            var conflictingIds = conflicts.Select(c => c.ExistingAssignmentId).Distinct().ToList();
            var conflictingAssignments = await _db.ChannelAssignments
                .Where(a => conflictingIds.Contains(a.Id))
                .ToListAsync();
            foreach (var ca in conflictingAssignments)
            {
                ca.HasConflict = true;
                ca.UpdatedAt = now;
            }
        }

        // AC-06: Write audit log entry
        await WriteAuditLogAsync(
            eventId, channel.Name, "squad", request.SquadId, squad.Name,
            request.PrimaryFrequency, request.AlternateFrequency,
            hasConflict ? "created_with_conflict" : "created",
            hasConflict ? string.Join(", ", conflicts.Select(c => c.ExistingSquadName).Distinct()) : null);

        await _db.SaveChangesAsync();

        // Reload with navigation properties for DTO mapping
        assignment.RadioChannel = channel;
        assignment.Squad = squad;

        var conflictWith = hasConflict
            ? conflicts.Select(c => c.ExistingSquadName).Distinct().ToList()
            : null;

        return ToDto(assignment, conflictWith);
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

        // AC-01/02/03: Detect conflicts (exclude self)
        var conflicts = await DetectFrequencyConflictsAsync(
            assignment.RadioChannelId, request.PrimaryFrequency, request.AlternateFrequency,
            excludeAssignmentId: assignmentId);

        // AC-04: Advisory mode
        if (conflicts.Count > 0 && !request.OverrideConflict)
        {
            var first = conflicts[0];
            throw new FrequencyConflictException(first.ExistingSquadName, first.ConflictingFreq, first.FreqType);
        }

        var hasConflict = conflicts.Count > 0;
        var now = DateTime.UtcNow;

        assignment.PrimaryFrequency = request.PrimaryFrequency;
        assignment.AlternateFrequency = request.AlternateFrequency;
        assignment.HasConflict = hasConflict;
        assignment.UpdatedAt = now;

        // AC-05: Persist conflict records and mark conflicting assignments
        if (hasConflict)
        {
            await PersistConflictRecordsAsync(eventId, assignment.SquadId, assignment.Squad.Name, conflicts);

            var conflictingIds = conflicts.Select(c => c.ExistingAssignmentId).Distinct().ToList();
            var conflictingAssignments = await _db.ChannelAssignments
                .Where(a => conflictingIds.Contains(a.Id))
                .ToListAsync();
            foreach (var ca in conflictingAssignments)
            {
                ca.HasConflict = true;
                ca.UpdatedAt = now;
            }
        }

        // AC-06: Write audit log entry
        await WriteAuditLogAsync(
            eventId, assignment.RadioChannel.Name, "squad", assignment.SquadId, assignment.Squad.Name,
            request.PrimaryFrequency, request.AlternateFrequency,
            hasConflict ? "updated_with_conflict" : "updated",
            hasConflict ? string.Join(", ", conflicts.Select(c => c.ExistingSquadName).Distinct()) : null);

        await _db.SaveChangesAsync();

        var conflictWith = hasConflict
            ? conflicts.Select(c => c.ExistingSquadName).Distinct().ToList()
            : null;

        return ToDto(assignment, conflictWith);
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
            .Include(a => a.Squad)
            .Include(a => a.RadioChannel)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.EventId == eventId)
            ?? throw new KeyNotFoundException($"ChannelAssignment {assignmentId} not found.");

        // AC-06: Write audit log before soft delete
        await WriteAuditLogAsync(
            eventId, assignment.RadioChannel.Name, "squad", assignment.SquadId, assignment.Squad.Name,
            assignment.PrimaryFrequency, assignment.AlternateFrequency,
            "deleted", null);

        // Soft delete
        assignment.IsDeleted = true;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── GET /api/events/{eventId}/channel-assignments/conflicts ───────────────

    /// <summary>
    /// AC-07: Returns conflict summary for all assignments in the operation.
    /// </summary>
    public async Task<ChannelAssignmentConflictSummaryDto> GetConflictsAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var conflicted = await _db.ChannelAssignments
            .Include(a => a.RadioChannel)
            .Include(a => a.Squad)
            .Where(a => a.EventId == eventId && a.HasConflict)
            .ToListAsync();

        if (conflicted.Count == 0)
            return new ChannelAssignmentConflictSummaryDto { EventId = eventId, ConflictCount = 0 };

        // Load all other assignments on same channels to identify counterparts
        var radioChannelIds = conflicted.Select(a => a.RadioChannelId).Distinct().ToList();
        var conflictedIdSet = conflicted.Select(a => a.Id).ToHashSet();

        var allOnChannels = await _db.ChannelAssignments
            .Include(a => a.Squad)
            .Include(a => a.RadioChannel)
            .Where(a => radioChannelIds.Contains(a.RadioChannelId) && !conflictedIdSet.Contains(a.Id))
            .ToListAsync();

        var items = new List<ChannelAssignmentConflictItemDto>();

        foreach (var a in conflicted)
        {
            foreach (var other in allOnChannels.Where(x => x.RadioChannelId == a.RadioChannelId))
            {
                decimal conflictingFreq;
                string freqType;

                if (other.PrimaryFrequency == a.PrimaryFrequency)
                {
                    conflictingFreq = a.PrimaryFrequency; freqType = "primary";
                }
                else if (a.AlternateFrequency.HasValue && other.PrimaryFrequency == a.AlternateFrequency.Value)
                {
                    conflictingFreq = a.AlternateFrequency.Value; freqType = "alternate";
                }
                else if (a.AlternateFrequency.HasValue && other.AlternateFrequency.HasValue
                         && other.AlternateFrequency.Value == a.AlternateFrequency.Value)
                {
                    conflictingFreq = a.AlternateFrequency.Value; freqType = "alternate";
                }
                else if (other.AlternateFrequency.HasValue && other.AlternateFrequency.Value == a.PrimaryFrequency)
                {
                    conflictingFreq = a.PrimaryFrequency; freqType = "primary";
                }
                else
                {
                    continue;
                }

                items.Add(new ChannelAssignmentConflictItemDto
                {
                    AssignmentId = a.Id,
                    SquadName = a.Squad.Name,
                    ChannelName = a.RadioChannel.Name,
                    ConflictingFrequency = conflictingFreq,
                    FrequencyType = freqType,
                    ConflictingSquadName = other.Squad.Name
                });
            }
        }

        return new ChannelAssignmentConflictSummaryDto
        {
            EventId = eventId,
            ConflictCount = items.Count,
            Conflicts = items
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private record ConflictMatch(
        Guid ExistingAssignmentId,
        string ExistingSquadName,
        decimal ConflictingFreq,
        string FreqType  // "primary" | "alternate"
    );

    /// <summary>
    /// AC-01/02/03: Detect exact-match frequency conflicts within the same radio channel.
    /// Channel-scoped == operation-scoped (each channel belongs to exactly one event).
    /// Checks both primary and alternate frequencies.
    /// </summary>
    private async Task<List<ConflictMatch>> DetectFrequencyConflictsAsync(
        Guid radioChannelId,
        decimal primaryFrequency,
        decimal? alternateFrequency,
        Guid? excludeAssignmentId)
    {
        var query = _db.ChannelAssignments
            .Include(a => a.Squad)
            .Where(a => a.RadioChannelId == radioChannelId);

        if (excludeAssignmentId.HasValue)
            query = query.Where(a => a.Id != excludeAssignmentId.Value);

        var existing = await query.ToListAsync();
        var results = new List<ConflictMatch>();

        foreach (var e in existing)
        {
            // Primary vs existing primary
            if (e.PrimaryFrequency == primaryFrequency)
                results.Add(new ConflictMatch(e.Id, e.Squad.Name, primaryFrequency, "primary"));

            // Primary vs existing alternate
            else if (e.AlternateFrequency.HasValue && e.AlternateFrequency.Value == primaryFrequency)
                results.Add(new ConflictMatch(e.Id, e.Squad.Name, primaryFrequency, "alternate"));

            // Alternate vs existing primary
            else if (alternateFrequency.HasValue && e.PrimaryFrequency == alternateFrequency.Value)
                results.Add(new ConflictMatch(e.Id, e.Squad.Name, alternateFrequency.Value, "primary"));

            // Alternate vs existing alternate
            else if (alternateFrequency.HasValue && e.AlternateFrequency.HasValue
                     && e.AlternateFrequency.Value == alternateFrequency.Value)
                results.Add(new ConflictMatch(e.Id, e.Squad.Name, alternateFrequency.Value, "alternate"));
        }

        return results;
    }

    /// <summary>
    /// AC-05: Persist FrequencyConflict records when a conflict override is accepted.
    /// ActionTaken = "overridden" (NOT NULL constraint satisfied).
    /// </summary>
    private Task PersistConflictRecordsAsync(
        Guid eventId,
        Guid unitAId,
        string unitAName,
        List<ConflictMatch> conflicts)
    {
        foreach (var conflict in conflicts)
        {
            _db.FrequencyConflicts.Add(new FrequencyConflict
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Frequency = conflict.ConflictingFreq.ToString("F3"),
                FrequencyType = conflict.FreqType,
                UnitAType = "squad",
                UnitAId = unitAId,
                UnitAName = unitAName,
                UnitBType = "squad",
                UnitBId = conflict.ExistingAssignmentId,  // use assignment ID as proxy; UnitB name is stored
                UnitBName = conflict.ExistingSquadName,
                Status = "Active",
                ActionTaken = "overridden",
                CreatedAt = DateTime.UtcNow
            });
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// AC-06: Write a frequency audit log entry for every create/update/delete.
    /// AC-03: Captures channel name for audit trail display.
    /// </summary>
    private async Task WriteAuditLogAsync(
        Guid eventId,
        string channelName,
        string unitType,
        Guid unitId,
        string unitName,
        decimal? primaryFrequency,
        decimal? alternateFrequency,
        string actionType,
        string? conflictingUnitName)
    {
        // Look up display name (best-effort — falls back to UserId string)
        var profile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == _currentUser.UserId);

        _db.FrequencyAuditLogs.Add(new FrequencyAuditLog
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ChannelName = channelName,
            UnitType = unitType,
            UnitId = unitId,
            UnitName = unitName,
            PrimaryFrequency = primaryFrequency?.ToString("F3"),
            AlternateFrequency = alternateFrequency?.ToString("F3"),
            ActionType = actionType,
            ConflictingUnitName = conflictingUnitName,
            PerformedByUserId = _currentUser.UserId,
            PerformedByDisplayName = profile?.DisplayName ?? _currentUser.UserId,
            OccurredAt = DateTime.UtcNow
        });
    }

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }

    private static ChannelAssignmentDto ToDto(ChannelAssignment a, List<string>? conflictWith = null) => new()
    {
        Id = a.Id,
        RadioChannelId = a.RadioChannelId,
        ChannelName = a.RadioChannel.Name,
        ChannelScope = a.RadioChannel.Scope.ToString(),
        SquadId = a.SquadId,
        SquadName = a.Squad.Name,
        PrimaryFrequency = a.PrimaryFrequency,
        AlternateFrequency = a.AlternateFrequency,
        HasConflict = a.HasConflict,
        ConflictWith = conflictWith,
        EventId = a.EventId,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt
    };
}
