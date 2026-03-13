namespace MilsimPlanning.Api.Data.Entities;

public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "draft";   // draft | published | archived
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<EventMembership> Memberships { get; set; } = [];
}
