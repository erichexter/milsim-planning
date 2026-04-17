namespace MilsimPlanning.Api.Models.Events;

public record CreatedByDto(
    string Id,
    string Email,
    string? Name
);

public record RoleBreakdownDto(
    int PlayerCount,
    int SquadLeaderCount,
    int PlatoonLeaderCount,
    int FactionCommanderCount,
    int EventOwnerCount
);

public record EventSummaryDto(
    Guid EventId,
    string EventName,
    DateOnly? EventDate,
    string? Location,
    CreatedByDto CreatedBy,
    DateTime CreatedAt,
    int MemberCount,
    int FactionCount,
    RoleBreakdownDto RoleBreakdown
);
