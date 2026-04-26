namespace MilsimPlanning.Api.Models.ChannelAssignments;

public record ChannelAssignmentListDto
{
    public int Total { get; init; }
    public List<ChannelAssignmentDto> Items { get; init; } = [];
}
