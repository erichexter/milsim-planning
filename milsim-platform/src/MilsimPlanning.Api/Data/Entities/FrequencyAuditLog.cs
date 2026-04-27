namespace MilsimPlanning.Api.Data.Entities;

public class FrequencyAuditLog
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string UnitName { get; set; } = null!; // Squad/Platoon/Faction name
    public string UnitType { get; set; } = null!; // "Squad", "Platoon", or "Faction"
    public string? PrimaryFrequency { get; set; }
    public string? AlternateFrequency { get; set; } // AC-03 mentions "alternate frequency"
    public string ActionType { get; set; } = null!; // "created", "updated", "deleted", "conflict_detected", "conflict_overridden"
    public string UserId { get; set; } = null!; // FK to AppUser.Id (string)
    public string? ConflictingUnitName { get; set; } // AC-04: unit name showing the conflicting frequency holder
    public Event Event { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
