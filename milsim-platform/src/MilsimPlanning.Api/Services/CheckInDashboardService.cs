using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Models.CheckIn;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Service for providing real-time check-in dashboard data for events.
/// </summary>
public class CheckInDashboardService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CheckInDashboardService> _logger;

    public CheckInDashboardService(AppDbContext db, ILogger<CheckInDashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets real-time check-in dashboard for an event.
    /// Provides participant check-in counts by faction.
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="userId">Current user ID (for authorization)</param>
    /// <param name="since">Optional timestamp filter; returns only check-ins after this time</param>
    /// <returns>Dashboard DTO with checked-in counts by faction</returns>
    /// <exception cref="KeyNotFoundException">Event not found</exception>
    /// <exception cref="ForbiddenException">User lacks event access</exception>
    public async Task<CheckInDashboardDto> GetDashboard(
        Guid eventId,
        string userId,
        DateTime? since = null)
    {
        // Step 1: Authorization check (IDOR prevention)
        ScopeGuard.AssertEventAccess(eventId, userId, _db);

        // Step 2: Verify event exists, capture targetCount
        var @event = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        // Step 3: Get all factions for event (sorted for consistent ordering)
        var factions = await _db.Factions
            .Where(f => f.EventId == eventId)
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync();

        // Step 4: Query check-ins with optional timestamp filter
        var query = _db.EventParticipantCheckIns
            .Where(c => c.EventId == eventId);

        if (since.HasValue)
        {
            query = query.Where(c => c.ScannedAtUtc > since.Value);
        }

        // Step 5: Group by faction and count (one database round-trip)
        var checkInCounts = await query
            .GroupBy(c => c.Participant.SquadId != null ? c.Participant.Squad!.PlatoonId : (Guid?)null)
            .Select(g => new
            {
                PlatoonId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.PlatoonId, x => x.Count);

        // Actually, we need to group by faction. Let me reconsider the query.
        // The EventParticipantCheckIn needs to be joined with Participant -> Squad -> Platoon -> Faction
        // Let me rewrite this to group by FactionId properly

        // Step 4 (revised): Query check-ins with faction information
        var checkInsByFaction = await _db.EventParticipantCheckIns
            .Where(c => c.EventId == eventId)
            .Include(c => c.Participant)
            .ThenInclude(p => p.Squad)
            .ThenInclude(s => s!.Platoon)
            .ThenInclude(p => p.Faction)
            .ToListAsync();

        if (since.HasValue)
        {
            checkInsByFaction = checkInsByFaction
                .Where(c => c.ScannedAtUtc > since.Value)
                .ToList();
        }

        // Step 5 (revised): Group by faction and count
        var checkInCountsByFaction = checkInsByFaction
            .GroupBy(c => c.Participant.Squad?.Platoon?.FactionId)
            .Where(g => g.Key.HasValue)
            .ToDictionary(g => g.Key!.Value, g => g.Count());

        var totalCheckedIn = checkInCountsByFaction.Values.Sum();

        // Step 6: Build response DTO with left-join logic (include 0-count factions)
        var byFaction = factions.Select(f => new FactionCheckInCountDto
        {
            FactionName = f.Name,
            Count = checkInCountsByFaction.TryGetValue(f.Id, out var count) ? count : 0
        }).ToList();

        return new CheckInDashboardDto
        {
            CheckedInCount = totalCheckedIn,
            TargetCount = @event.TargetParticipantCount,
            ByFaction = byFaction
        };
    }
}
