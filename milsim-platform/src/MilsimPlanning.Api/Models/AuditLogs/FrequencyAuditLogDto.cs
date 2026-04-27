namespace MilsimPlanning.Api.Models.AuditLogs;

public class FrequencyAuditLogDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string UnitName { get; set; } = null!;
    public string UnitType { get; set; } = null!;  // "Squad", "Platoon", or "Faction"
    public string? PrimaryFrequency { get; set; }
    public string? AlternateFrequency { get; set; }
    public string ActionType { get; set; } = null!;  // "created", "updated", "deleted", "conflict_detected", "conflict_overridden"
    public string UserName { get; set; } = null!;  // Display name from AppUser
    public string? ConflictingUnitName { get; set; }
}
