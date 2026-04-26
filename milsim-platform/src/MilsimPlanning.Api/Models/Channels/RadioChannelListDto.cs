namespace MilsimPlanning.Api.Models.Channels;

public record RadioChannelListDto(
    Guid Id,
    string Name,
    string? CallSign,
    string Scope,
    int AssignmentCount,
    int ConflictCount
);
