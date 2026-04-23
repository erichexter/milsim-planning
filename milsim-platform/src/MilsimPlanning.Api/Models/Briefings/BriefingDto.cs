namespace MilsimPlanning.Api.Models.Briefings;

public record BriefingDto(
    Guid Id,
    string Title,
    string? Description,
    string ChannelIdentifier,
    string PublicationState,   // "Draft" | "Published" | "Archived"
    string VersionETag,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
