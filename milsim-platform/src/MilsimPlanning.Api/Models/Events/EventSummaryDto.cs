namespace MilsimPlanning.Api.Models.Events;

/// <summary>
/// Event summary with aggregated statistics: member counts, role breakdown, metadata.
/// Used by the Event Dashboard to display overview cards.
/// </summary>
public record EventSummaryDto(
    Guid EventId,
    string EventName,
    DateOnly? EventDate,
    string? Location,
    CreatorDto CreatedBy,
    DateTime CreatedAt,
    int MemberCount,
    int FactionCount,
    RoleBreakdownDto RoleBreakdown
);

/// <summary>
/// Event creator information.
/// </summary>
public record CreatorDto(
    string Id,
    string Email,
    string? Name
);

/// <summary>
/// Role distribution across event members.
/// </summary>
public record RoleBreakdownDto(
    int PlayerCount,
    int SquadLeaderCount,
    int PlatoonLeaderCount,
    int FactionCommanderCount,
    int EventOwnerCount
);
