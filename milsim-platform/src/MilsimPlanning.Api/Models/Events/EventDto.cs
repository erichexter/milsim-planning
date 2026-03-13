namespace MilsimPlanning.Api.Models.Events;

public record EventDto(
    Guid Id,
    string Name,
    string? Location,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status   // "Draft" | "Published"
);
