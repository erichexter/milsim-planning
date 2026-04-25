using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Detects exact-match frequency conflicts within the same channel (operation-scoped).
/// AC-01/02/03: checks both primary and alternate frequencies, within the same operation only.
/// </summary>
public class RadioConflictDetectionService
{
    private readonly AppDbContext _db;

    public RadioConflictDetectionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns all existing assignments on the given channel that share the specified
    /// frequency (primary or alternate), excluding the calling unit's own assignment.
    /// </summary>
    public async Task<List<ConflictInfo>> DetectConflictsAsync(
        Guid channelId,
        decimal frequency,
        Guid? excludeAssignmentId = null)
    {
        var query = _db.RadioChannelAssignments
            .Include(a => a.Squad)
            .Include(a => a.Platoon)
            .Include(a => a.Faction)
            .Include(a => a.Channel)
            .Where(a => a.ChannelId == channelId
                     && (a.Primary == frequency || a.Alternate == frequency));

        if (excludeAssignmentId.HasValue)
            query = query.Where(a => a.Id != excludeAssignmentId.Value);

        var matches = await query.ToListAsync();

        return matches.Select(a =>
        {
            var (unitId, unitType, unitName) = ResolveUnit(a);
            var conflictType = a.Primary == frequency ? "primary" : "alternate";
            return new ConflictInfo(a.Id, unitId, unitType, unitName, frequency, conflictType);
        }).ToList();
    }

    private static (Guid unitId, string unitType, string unitName) ResolveUnit(RadioChannelAssignment a)
    {
        if (a.SquadId.HasValue && a.Squad != null)
            return (a.Squad.Id, "squad", a.Squad.Name);
        if (a.PlatoonId.HasValue && a.Platoon != null)
            return (a.Platoon.Id, "platoon", a.Platoon.Name);
        if (a.FactionId.HasValue && a.Faction != null)
            return (a.Faction.Id, "faction", a.Faction.Name);

        // Fallback — should never happen in well-formed data
        return (Guid.Empty, "unknown", "unknown");
    }
}

public record ConflictInfo(
    Guid AssignmentId,
    Guid UnitId,
    string UnitType,
    string UnitName,
    decimal Frequency,
    string ConflictType   // "primary" | "alternate"
);
