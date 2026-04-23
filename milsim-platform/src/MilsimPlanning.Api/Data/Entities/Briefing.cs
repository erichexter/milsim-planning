namespace MilsimPlanning.Api.Data.Entities;

public class Briefing
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string ChannelIdentifier { get; set; } = null!; // UUID, UNIQUE, immutable
    public string PublicationState { get; set; } = "Draft"; // Draft | Published | Archived
    public string VersionETag { get; set; } = "etag-v1";
    public bool IsDeleted { get; set; } = false; // soft-delete pattern
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
