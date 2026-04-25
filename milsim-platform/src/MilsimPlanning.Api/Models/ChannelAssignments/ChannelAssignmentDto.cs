namespace MilsimPlanning.Api.Models.ChannelAssignments;

public record ChannelAssignmentDto
{
    public Guid Id { get; init; }
    public Guid RadioChannelId { get; init; }
    public string ChannelName { get; init; } = null!;
    public string ChannelScope { get; init; } = null!;
    public Guid SquadId { get; init; }
    public string SquadName { get; init; } = null!;
    public decimal PrimaryFrequency { get; init; }
    public decimal? AlternateFrequency { get; init; }
    public Guid EventId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
