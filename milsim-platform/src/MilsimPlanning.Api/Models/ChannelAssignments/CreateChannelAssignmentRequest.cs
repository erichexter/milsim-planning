namespace MilsimPlanning.Api.Models.ChannelAssignments;

public record CreateChannelAssignmentRequest(
    Guid RadioChannelId,
    Guid SquadId,
    decimal PrimaryFrequency
);
