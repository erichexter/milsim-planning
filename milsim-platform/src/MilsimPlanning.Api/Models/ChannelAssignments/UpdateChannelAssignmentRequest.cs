namespace MilsimPlanning.Api.Models.ChannelAssignments;

public record UpdateChannelAssignmentRequest(
    decimal PrimaryFrequency,
    decimal? AlternateFrequency = null,
    bool OverrideConflict = false   // AC-04: advisory mode override flag
);
