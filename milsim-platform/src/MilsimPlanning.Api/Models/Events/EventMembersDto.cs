namespace MilsimPlanning.Api.Models.Events;

/// <summary>
/// Paginated list of event members with total count.
/// </summary>
public record EventMembersDto(
    List<EventMemberDto> Members,
    int TotalCount,
    int PageSize,
    int PageNumber
);
