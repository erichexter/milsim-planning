namespace MilsimPlanning.Api.Models.Channels;

public record FrequencyAuditLogDto(
    Guid Id,
    Guid EventId,
    string UnitType,           // "squad" | "platoon" | "faction"
    Guid UnitId,
    string UnitName,
    string? PrimaryFrequency,
    string? AlternateFrequency,
    string ActionType,         // "created" | "updated" | "deleted" | "conflict_detected" | "conflict_overridden"
    string? ConflictingUnitName,
    string PerformedByUserId,
    string PerformedByDisplayName,
    DateTime OccurredAt
);
