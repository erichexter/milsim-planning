namespace MilsimPlanning.Api.Models.Channels;

public record RadioChannelAssignmentDto(
    Guid Id,
    Guid ChannelId,
    Guid UnitId,
    string UnitType,       // "squad" | "platoon" | "faction"
    string UnitName,
    decimal? Primary,
    decimal? Alternate,
    bool HasConflict,
    List<string>? ConflictWith,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
