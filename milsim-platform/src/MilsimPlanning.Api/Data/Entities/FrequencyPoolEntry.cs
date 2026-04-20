namespace MilsimPlanning.Api.Data.Entities;

public class FrequencyPoolEntry
{
    public Guid Id { get; set; }
    public Guid FrequencyPoolId { get; set; }
    public string Channel { get; set; } = null!;  // canonical form: lowercase, trimmed
    public string? DisplayGroup { get; set; }      // e.g., "VHF", "GMRS", "PMR"
    public int SortOrder { get; set; }
    public bool IsReserved { get; set; }
    public string? ReservedRole { get; set; }      // "Safety" | "Medical" | "Control"

    public FrequencyPool Pool { get; set; } = null!;
}
