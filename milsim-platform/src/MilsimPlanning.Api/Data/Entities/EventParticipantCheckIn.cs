namespace MilsimPlanning.Api.Data.Entities;

public class EventParticipantCheckIn
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid ParticipantId { get; set; }
    public string? QrCodeValue { get; set; }
    public DateTime ScannedAtUtc { get; set; }
    public string? KioskId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Event Event { get; set; } = null!;
    public EventPlayer Participant { get; set; } = null!;
}
