using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Channels;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Orchestrates validation, conflict detection, and persistence for
/// RadioChannelAssignment (Story 4 — AC-01 through AC-07).
/// Writes audit log entries for all operations (Story 7).
/// </summary>
public class RadioChannelAssignmentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly NatoFrequencyValidationService _validator;
    private readonly RadioConflictDetectionService _conflictDetector;
    private readonly FrequencyAuditLogService _auditLog;

    public RadioChannelAssignmentService(
        AppDbContext db,
        ICurrentUser currentUser,
        NatoFrequencyValidationService validator,
        RadioConflictDetectionService conflictDetector,
        FrequencyAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _validator = validator;
        _conflictDetector = conflictDetector;
        _auditLog = auditLog;
    }

    // ── GET /api/radio-channels/{channelId}/assignments ───────────────────────

    public async Task<List<RadioChannelAssignmentDto>> GetAssignmentsAsync(Guid channelId)
    {
        var channel = await _db.RadioChannels
            .FirstOrDefaultAsync(rc => rc.Id == channelId)
            ?? throw new KeyNotFoundException($"RadioChannel {channelId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, channel.EventId);

        var assignments = await _db.RadioChannelAssignments
            .Include(a => a.Squad)
            .Include(a => a.Platoon)
            .Include(a => a.Faction)
            .Where(a => a.ChannelId == channelId)
            .ToListAsync();

        // Build conflict-with info for each assignment
        var dtos = new List<RadioChannelAssignmentDto>();
        foreach (var a in assignments)
        {
            var conflictWith = await BuildConflictWithAsync(a);
            dtos.Add(ToDto(a, conflictWith));
        }

        return dtos;
    }

    // ── PUT /api/radio-channels/{channelId}/assignments/{unitType}/{unitId} ───
    // AC-01/02: conflict detection for primary AND alternate
    // AC-04: advisory mode — throw InvalidOperationException if conflict and !override
    // AC-05: persist HasConflict flag

    public async Task<RadioChannelAssignmentDto> UpsertAssignmentAsync(
        Guid channelId,
        string unitType,
        Guid unitId,
        AssignFrequencyRequest request)
    {
        var channel = await _db.RadioChannels
            .Include(rc => rc.Event)
                .ThenInclude(e => e.Faction)
            .FirstOrDefaultAsync(rc => rc.Id == channelId)
            ?? throw new KeyNotFoundException($"RadioChannel {channelId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, channel.EventId);
        AssertCommanderAccess(channel.Event.Faction);

        // Resolve unit and validate it exists
        var (squadId, platoonId, factionId, unitName) =
            await ResolveUnitAsync(unitType, unitId, channel.EventId);

        // Validate frequencies (AC-01/02)
        if (request.Primary.HasValue)
            _validator.Validate(request.Primary.Value, channel.Scope, request.OverrideValidation);
        if (request.Alternate.HasValue)
            _validator.Validate(request.Alternate.Value, channel.Scope, request.OverrideValidation);

        // Find existing assignment for this unit on this channel
        var existing = await _db.RadioChannelAssignments
            .FirstOrDefaultAsync(a =>
                a.ChannelId == channelId
                && a.SquadId == squadId
                && a.PlatoonId == platoonId
                && a.FactionId == factionId);

        // Conflict detection (AC-01/02/03)
        var allConflicts = new List<ConflictInfo>();

        if (request.Primary.HasValue)
        {
            var conflicts = await _conflictDetector.DetectConflictsAsync(
                channelId, request.Primary.Value, existing?.Id);
            allConflicts.AddRange(conflicts);
        }

        if (request.Alternate.HasValue)
        {
            var conflicts = await _conflictDetector.DetectConflictsAsync(
                channelId, request.Alternate.Value, existing?.Id);
            allConflicts.AddRange(conflicts);
        }

        // AC-04: advisory — if conflicts and no override, throw 409
        if (allConflicts.Count > 0 && !request.OverrideValidation)
        {
            var detail = string.Join("; ", allConflicts.Select(c =>
                $"{c.UnitName} ({c.UnitType}) uses {c.Frequency} MHz as {c.ConflictType}"));
            throw new InvalidOperationException(
                $"Frequency conflict detected with: {detail}");
        }

        var hasConflict = allConflicts.Count > 0;
        var now = DateTime.UtcNow;
        var isCreate = existing is null;

        if (existing is null)
        {
            existing = new RadioChannelAssignment
            {
                Id = Guid.NewGuid(),
                ChannelId = channelId,
                SquadId = squadId,
                PlatoonId = platoonId,
                FactionId = factionId,
                Primary = request.Primary,
                Alternate = request.Alternate,
                HasConflict = hasConflict,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.RadioChannelAssignments.Add(existing);
        }
        else
        {
            existing.Primary = request.Primary;
            existing.Alternate = request.Alternate;
            existing.HasConflict = hasConflict;
            existing.UpdatedAt = now;
        }

        // AC-05: update HasConflict on all other conflicting assignments too
        if (hasConflict)
        {
            var conflictingIds = allConflicts.Select(c => c.AssignmentId).Distinct().ToList();
            var conflictingAssignments = await _db.RadioChannelAssignments
                .Where(a => conflictingIds.Contains(a.Id))
                .ToListAsync();

            foreach (var ca in conflictingAssignments)
            {
                ca.HasConflict = true;
                ca.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();

        // Write audit log entry (Story 7)
        var primaryFreqStr = request.Primary?.ToString("F3");
        var alternateFreqStr = request.Alternate?.ToString("F3");
        var actionType = isCreate ? "created" : "updated";
        var conflictingUnitName = allConflicts.FirstOrDefault()?.UnitName;

        await _auditLog.LogAssignmentActionAsync(
            channel.EventId,
            channel.Name,  // AC-03: include channel name
            unitType,
            unitId,
            unitName,
            primaryFreqStr,
            alternateFreqStr,
            actionType,
            conflictingUnitName
        );

        // Log conflict detection if applicable (AC-04)
        if (hasConflict)
        {
            await _auditLog.LogAssignmentActionAsync(
                channel.EventId,
                channel.Name,  // AC-03: include channel name
                unitType,
                unitId,
                unitName,
                primaryFreqStr,
                alternateFreqStr,
                "conflict_detected",
                conflictingUnitName
            );
        }

        // Reload with navigation properties
        await _db.Entry(existing).Reference(a => a.Channel).LoadAsync();
        if (existing.SquadId.HasValue)
            await _db.Entry(existing).Reference(a => a.Squad).LoadAsync();
        if (existing.PlatoonId.HasValue)
            await _db.Entry(existing).Reference(a => a.Platoon).LoadAsync();
        if (existing.FactionId.HasValue)
            await _db.Entry(existing).Reference(a => a.Faction).LoadAsync();

        var conflictWith = await BuildConflictWithAsync(existing);
        return ToDto(existing, conflictWith);
    }

    // ── DELETE /api/radio-channels/{channelId}/assignments/{unitType}/{unitId} ─

    public async Task DeleteAssignmentAsync(Guid channelId, string unitType, Guid unitId)
    {
        var channel = await _db.RadioChannels
            .Include(rc => rc.Event)
                .ThenInclude(e => e.Faction)
            .FirstOrDefaultAsync(rc => rc.Id == channelId)
            ?? throw new KeyNotFoundException($"RadioChannel {channelId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, channel.EventId);
        AssertCommanderAccess(channel.Event.Faction);

        var (squadId, platoonId, factionId, unitName) =
            await ResolveUnitAsync(unitType, unitId, channel.EventId);

        var assignment = await _db.RadioChannelAssignments
            .FirstOrDefaultAsync(a =>
                a.ChannelId == channelId
                && a.SquadId == squadId
                && a.PlatoonId == platoonId
                && a.FactionId == factionId)
            ?? throw new KeyNotFoundException(
                $"Assignment for {unitType} {unitId} on channel {channelId} not found.");

        // Capture frequency info before deletion (Story 7)
        var primaryFreqStr = assignment.Primary?.ToString("F3");
        var alternateFreqStr = assignment.Alternate?.ToString("F3");

        _db.RadioChannelAssignments.Remove(assignment);
        await _db.SaveChangesAsync();

        // Write audit log entry for deletion (Story 7)
        await _auditLog.LogAssignmentActionAsync(
            channel.EventId,
            channel.Name,  // AC-03: include channel name
            unitType,
            unitId,
            unitName,
            primaryFreqStr,
            alternateFreqStr,
            "deleted"
        );
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }

    private async Task<(Guid? squadId, Guid? platoonId, Guid? factionId, string unitName)>
        ResolveUnitAsync(string unitType, Guid unitId, Guid eventId)
    {
        return unitType.ToLowerInvariant() switch
        {
            "squad" => await ResolveSquadAsync(unitId),
            "platoon" => await ResolvePlatoonAsync(unitId),
            "faction" => await ResolveFactionAsync(unitId),
            _ => throw new ArgumentException($"Unknown unit type '{unitType}'. Must be squad, platoon, or faction.")
        };
    }

    private async Task<(Guid?, Guid?, Guid?, string)> ResolveSquadAsync(Guid id)
    {
        var squad = await _db.Squads.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Squad {id} not found.");
        return (squad.Id, null, null, squad.Name);
    }

    private async Task<(Guid?, Guid?, Guid?, string)> ResolvePlatoonAsync(Guid id)
    {
        var platoon = await _db.Platoons.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new KeyNotFoundException($"Platoon {id} not found.");
        return (null, platoon.Id, null, platoon.Name);
    }

    private async Task<(Guid?, Guid?, Guid?, string)> ResolveFactionAsync(Guid id)
    {
        var faction = await _db.Factions.FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new KeyNotFoundException($"Faction {id} not found.");
        return (null, null, faction.Id, faction.Name);
    }

    private async Task<List<string>?> BuildConflictWithAsync(RadioChannelAssignment a)
    {
        if (!a.HasConflict) return null;

        var conflicts = new List<string>();

        if (a.Primary.HasValue)
        {
            var c = await _conflictDetector.DetectConflictsAsync(a.ChannelId, a.Primary.Value, a.Id);
            conflicts.AddRange(c.Select(x => $"{x.UnitName} ({x.UnitType}) — {x.Frequency} MHz"));
        }

        if (a.Alternate.HasValue)
        {
            var c = await _conflictDetector.DetectConflictsAsync(a.ChannelId, a.Alternate.Value, a.Id);
            conflicts.AddRange(c.Select(x => $"{x.UnitName} ({x.UnitType}) — {x.Frequency} MHz"));
        }

        return conflicts.Count > 0 ? conflicts : null;
    }

    private static RadioChannelAssignmentDto ToDto(RadioChannelAssignment a, List<string>? conflictWith)
    {
        var (unitId, unitType, unitName) = a switch
        {
            { Squad: not null }   => (a.Squad.Id,   "squad",   a.Squad.Name),
            { Platoon: not null } => (a.Platoon.Id, "platoon", a.Platoon.Name),
            { Faction: not null } => (a.Faction.Id, "faction", a.Faction.Name),
            _ => (a.SquadId ?? a.PlatoonId ?? a.FactionId ?? Guid.Empty, "unknown", "unknown")
        };

        return new RadioChannelAssignmentDto(
            a.Id,
            a.ChannelId,
            unitId,
            unitType,
            unitName,
            a.Primary,
            a.Alternate,
            a.HasConflict,
            conflictWith,
            a.CreatedAt,
            a.UpdatedAt
        );
    }
}
