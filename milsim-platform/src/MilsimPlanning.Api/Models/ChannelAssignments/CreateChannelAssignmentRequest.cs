namespace MilsimPlanning.Api.Models.ChannelAssignments;

public record CreateChannelAssignmentRequest(
    Guid RadioChannelId,
    Guid SquadId,
    decimal PrimaryFrequency,
    decimal? AlternateFrequency = null,
    bool OverrideConflict = false   // AC-04: advisory mode override flag
);
