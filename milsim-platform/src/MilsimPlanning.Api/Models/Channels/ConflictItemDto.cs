namespace MilsimPlanning.Api.Models.Channels;

public record ConflictItemDto(
    Guid AssignmentId,
    string ChannelName,
    Guid ConflictingUnitId,
    string ConflictingUnitType,
    string ConflictingUnitName,
    decimal ConflictingFrequency,
    string ConflictType   // "primary" | "alternate"
);
