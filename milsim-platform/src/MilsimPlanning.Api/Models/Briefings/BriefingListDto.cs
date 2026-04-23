namespace MilsimPlanning.Api.Models.Briefings;

public class BriefingListDto
{
    public IEnumerable<BriefingSummaryDto> Items { get; set; } = [];
    public PaginationDto Pagination { get; set; } = new();
}

public class BriefingSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string ChannelIdentifier { get; set; } = null!;
    public string PublicationState { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}

public class PaginationDto
{
    public int Limit { get; set; }
    public int Offset { get; set; }
    public int Total { get; set; }
}
