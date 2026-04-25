namespace MilsimPlanning.Api.Data.Entities;

public class RadioChannelAssignment
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }

    // Polymorphic unit reference — exactly one of these is non-null
    public Guid? SquadId { get; set; }
    public Guid? PlatoonId { get; set; }
    public Guid? FactionId { get; set; }

    public decimal? Primary { get; set; }   // MHz, decimal(7,3)
    public decimal? Alternate { get; set; } // MHz, decimal(7,3)
    public bool HasConflict { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public RadioChannel Channel { get; set; } = null!;
    public Squad? Squad { get; set; }
    public Platoon? Platoon { get; set; }
    public Faction? Faction { get; set; }
}
