namespace MilsimPlanning.Api.Data.Entities;

/// <summary>
/// Represents a detected frequency conflict between two units.
/// Populated by the conflict-detection service (Story 4+).
/// </summary>
public class FrequencyConflict
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Frequency { get; set; } = null!;
    public string FrequencyType { get; set; } = null!;

    // Unit A
    public string UnitAType { get; set; } = null!;
    public Guid UnitAId { get; set; }
    public string UnitAName { get; set; } = null!;

    // Unit B
    public string UnitBType { get; set; } = null!;
    public Guid UnitBId { get; set; }
    public string UnitBName { get; set; } = null!;

    public string Status { get; set; } = "Active";
    public string ActionTaken { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Event Event { get; set; } = null!;
}
