namespace MilsimPlanning.Api.Models.Events;

public record UpdateEventRequest(
    string Name,
    string? Location,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate
);
