namespace MilsimPlanning.Api.Models.Events;

public record CreateEventRequest(
    string Name,
    string? Location,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate
);
