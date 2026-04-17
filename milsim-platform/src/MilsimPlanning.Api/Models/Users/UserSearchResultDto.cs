namespace MilsimPlanning.Api.Models.Users;

/// <summary>
/// DTO for user search results (used in async autocomplete for assigning roles).
/// </summary>
public record UserSearchResultDto(
    string Id,
    string Email,
    string? Name
);
