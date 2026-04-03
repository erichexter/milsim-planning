namespace MilsimPlanning.Api.Data.Entities;

public class Platoon
{
    public Guid Id { get; set; }
    public Guid FactionId { get; set; }
    public string Name { get; set; } = null!;
    public int Order { get; set; }
    public bool IsCommandElement { get; set; }
    public string? PrimaryFrequency { get; set; }
    public string? BackupFrequency { get; set; }
    public Faction Faction { get; set; } = null!;
    public ICollection<Squad> Squads { get; set; } = [];
    public ICollection<EventPlayer> Players { get; set; } = [];
}
