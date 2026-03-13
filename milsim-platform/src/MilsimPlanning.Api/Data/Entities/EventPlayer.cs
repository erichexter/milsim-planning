namespace MilsimPlanning.Api.Data.Entities;

public class EventPlayer
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Email { get; set; } = null!;         // natural key; stored lowercase
    public string Name { get; set; } = null!;
    public string? Callsign { get; set; }
    public string? TeamAffiliation { get; set; }
    public string? UserId { get; set; }                // null until invite accepted; FK to AppUser.Id
    public Guid? PlatoonId { get; set; }
    public Guid? SquadId { get; set; }
    public Event Event { get; set; } = null!;
    public Platoon? Platoon { get; set; }
    public Squad? Squad { get; set; }
}
