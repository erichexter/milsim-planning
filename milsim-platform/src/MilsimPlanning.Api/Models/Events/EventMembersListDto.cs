namespace MilsimPlanning.Api.Models.Events;

public record EventMembersListDto(
    List<EventMemberDto> Members,
    int TotalCount,
    int PageSize,
    int PageNumber
);
