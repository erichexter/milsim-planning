namespace MilsimPlanning.Api.Models.ChannelAssignments;

/// <summary>
/// AC-07: Summary of all active frequency conflicts within an operation's channel assignments.
/// </summary>
public record ChannelAssignmentConflictSummaryDto
{
    public Guid EventId { get; init; }
    public int ConflictCount { get; init; }
    public List<ChannelAssignmentConflictItemDto> Conflicts { get; init; } = [];
}

public record ChannelAssignmentConflictItemDto
{
    public Guid AssignmentId { get; init; }
    public string SquadName { get; init; } = null!;
    public string ChannelName { get; init; } = null!;
    public decimal ConflictingFrequency { get; init; }
    public string FrequencyType { get; init; } = null!;  // "primary" | "alternate"
    public string ConflictingSquadName { get; init; } = null!;
}
