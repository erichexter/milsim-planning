namespace MilsimPlanning.Api.Data.Entities;

public class EventRsvp
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string UserId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime RespondedAt { get; set; }
    public Event Event { get; set; } = null!;
}
