namespace MilsimPlanning.Api.Data.Entities;

public enum RosterChangeStatus
{
    Pending,
    Approved,
    Denied
}

public class RosterChangeRequest
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid EventPlayerId { get; set; }
    public string Note { get; set; } = null!;
    public RosterChangeStatus Status { get; set; } = RosterChangeStatus.Pending;
    public string? CommanderNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
    public EventPlayer EventPlayer { get; set; } = null!;
}
