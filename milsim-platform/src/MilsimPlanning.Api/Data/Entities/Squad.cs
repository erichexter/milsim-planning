namespace MilsimPlanning.Api.Data.Entities;

public class Squad
{
    public Guid Id { get; set; }
    public Guid PlatoonId { get; set; }
    public string Name { get; set; } = null!;
    public int Order { get; set; }
    public Platoon Platoon { get; set; } = null!;
    public string? SquadPrimaryFrequency { get; set; }
    public string? SquadBackupFrequency { get; set; }
    public ICollection<EventPlayer> Players { get; set; } = [];
}
