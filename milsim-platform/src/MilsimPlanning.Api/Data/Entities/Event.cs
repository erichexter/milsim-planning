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
    public string CreatedById { get; set; } = null!;  // FK to AppUser.Id; added for EventOwner role assignment
    public Faction Faction { get; set; } = null!;

    // Phase 3 navigation properties
    public ICollection<InfoSection> InfoSections { get; set; } = [];
    public ICollection<MapResource> MapResources { get; set; } = [];
    public ICollection<NotificationBlast> NotificationBlasts { get; set; } = [];
}
