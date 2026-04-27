namespace MilsimPlanning.Api.Data.Entities;

/// <summary>
/// Audit log entry for frequency assignment changes.
/// Written by the assignment service on create/update/delete (Story 6+).
/// </summary>
public class FrequencyAuditLog
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    public string ChannelName { get; set; } = null!;  // AC-03: channel name
    public string UnitType { get; set; } = null!;
    public Guid UnitId { get; set; }
    public string UnitName { get; set; } = null!;

    public string? PrimaryFrequency { get; set; }
    public string? AlternateFrequency { get; set; }

    public string ActionType { get; set; } = null!;
    public string? ConflictingUnitName { get; set; }

    public string PerformedByUserId { get; set; } = null!;
    public string PerformedByDisplayName { get; set; } = null!;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Event Event { get; set; } = null!;
}
