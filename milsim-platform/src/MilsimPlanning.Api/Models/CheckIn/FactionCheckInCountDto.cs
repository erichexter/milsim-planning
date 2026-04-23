namespace MilsimPlanning.Api.Models.CheckIn;

/// <summary>
/// Check-in count for a single faction.
/// </summary>
public class FactionCheckInCountDto
{
    /// <summary>
    /// Name of the faction.
    /// </summary>
    public string FactionName { get; set; } = string.Empty;

    /// <summary>
    /// Number of participants from this faction who have checked in.
    /// May be 0 if no participants from this faction have checked in.
    /// </summary>
    public int Count { get; set; }
}
