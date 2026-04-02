namespace MilsimPlanning.Api.Data.Entities;

public class Faction
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string CommanderId { get; set; } = null!;   // FK to AppUser.Id (string)
    public string Name { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public AppUser Commander { get; set; } = null!;
    public string? PrimaryFrequency { get; set; }
    public string? BackupFrequency { get; set; }
    public ICollection<Platoon> Platoons { get; set; } = [];
    public ICollection<EventPlayer> Players { get; set; } = [];
}
