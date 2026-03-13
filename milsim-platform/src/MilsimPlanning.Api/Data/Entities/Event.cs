namespace MilsimPlanning.Api.Data.Entities;

public enum EventStatus { Draft = 0, Published = 1 }

public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Location { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public Guid FactionId { get; set; }
    public Faction Faction { get; set; } = null!;
}
