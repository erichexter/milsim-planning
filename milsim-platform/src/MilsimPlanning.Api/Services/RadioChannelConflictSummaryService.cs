using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Channels;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Builds the conflict summary for an operation (AC-07).
/// Returns all active RadioChannelAssignment conflicts scoped to the event.
/// </summary>
public class RadioChannelConflictSummaryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RadioChannelConflictSummaryService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ConflictSummaryDto> GetConflictSummaryAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Load all conflicted assignments for channels belonging to this event
        var conflictedAssignments = await _db.RadioChannelAssignments
            .Include(a => a.Channel)
            .Include(a => a.Squad)
            .Include(a => a.Platoon)
            .Include(a => a.Faction)
            .Where(a => a.HasConflict && a.Channel.EventId == eventId)
            .ToListAsync();

        var conflictItems = new List<ConflictItemDto>();

        foreach (var a in conflictedAssignments)
        {
            await AddConflictItemsAsync(a, conflictItems);
        }

        return new ConflictSummaryDto(eventId, conflictItems.Count, conflictItems);
    }

    private async Task AddConflictItemsAsync(
        RadioChannelAssignment assignment,
        List<ConflictItemDto> items)
    {
        var freqs = new List<(decimal freq, string type)>();
        if (assignment.Primary.HasValue) freqs.Add((assignment.Primary.Value, "primary"));
        if (assignment.Alternate.HasValue) freqs.Add((assignment.Alternate.Value, "alternate"));

        foreach (var (freq, freqType) in freqs)
        {
            var conflicts = await _db.RadioChannelAssignments
                .Include(a => a.Squad)
                .Include(a => a.Platoon)
                .Include(a => a.Faction)
                .Where(a =>
                    a.Id != assignment.Id
                    && a.ChannelId == assignment.ChannelId
                    && (a.Primary == freq || a.Alternate == freq))
                .ToListAsync();

            foreach (var conflicting in conflicts)
            {
                var (cUnitId, cUnitType, cUnitName) = ResolveUnit(conflicting);

                items.Add(new ConflictItemDto(
                    assignment.Id,
                    assignment.Channel.Name,
                    cUnitId,
                    cUnitType,
                    cUnitName,
                    freq,
                    freqType
                ));
            }
        }
    }

    private static (Guid unitId, string unitType, string unitName) ResolveUnit(RadioChannelAssignment a)
    {
        if (a.Squad != null) return (a.Squad.Id, "squad", a.Squad.Name);
        if (a.Platoon != null) return (a.Platoon.Id, "platoon", a.Platoon.Name);
        if (a.Faction != null) return (a.Faction.Id, "faction", a.Faction.Name);
        return (Guid.Empty, "unknown", "unknown");
    }
}
