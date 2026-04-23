namespace MilsimPlanning.Api.Models.CheckIn;

/// <summary>
/// Real-time check-in dashboard data for an event.
/// Provides participant count aggregation by faction.
/// </summary>
public class CheckInDashboardDto
{
    /// <summary>
    /// Total number of participants who have checked in for this event.
    /// </summary>
    public int CheckedInCount { get; set; }

    /// <summary>
    /// Target participant count for this event.
    /// </summary>
    public int TargetCount { get; set; }

    /// <summary>
    /// Check-in counts grouped by faction, sorted alphabetically by faction name.
    /// Includes all factions for the event, even if count is 0.
    /// </summary>
    public List<FactionCheckInCountDto> ByFaction { get; set; } = new();
}
