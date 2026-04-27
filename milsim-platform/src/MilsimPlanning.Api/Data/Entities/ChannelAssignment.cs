namespace MilsimPlanning.Api.Data.Entities;

/// <summary>
/// Legacy squad-only channel assignment entity.
/// Supports basic frequency assignment per squad per radio channel.
/// Use RadioChannelAssignment for the polymorphic (squad/platoon/faction) model with conflict detection.
/// </summary>
public class ChannelAssignment
{
    public Guid Id { get; set; }
    public Guid RadioChannelId { get; set; }
    public Guid SquadId { get; set; }
    public Guid EventId { get; set; }
    public decimal PrimaryFrequency { get; set; }
    public decimal? AlternateFrequency { get; set; }
    public bool HasConflict { get; set; } = false;   // AC-05: conflict state persisted on record
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RadioChannel RadioChannel { get; set; } = null!;
    public Squad Squad { get; set; } = null!;
    public Event Event { get; set; } = null!;
}
