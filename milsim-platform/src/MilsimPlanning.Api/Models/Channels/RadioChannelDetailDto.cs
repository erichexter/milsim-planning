namespace MilsimPlanning.Api.Models.Channels;

public record RadioChannelDetailDto(
    Guid Id,
    Guid EventId,
    string Name,
    string? CallSign,
    string Scope,
    List<RadioChannelAssignmentDto> Assignments,
    DateTime CreatedAt
);
