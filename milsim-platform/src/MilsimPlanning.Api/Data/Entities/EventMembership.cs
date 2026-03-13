namespace MilsimPlanning.Api.Data.Entities;

public class EventMembership
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid EventId { get; set; }
    public string Role { get; set; } = null!;        // one of AppRoles constants
    public DateTime JoinedAt { get; set; }
    public AppUser User { get; set; } = null!;
    public Event Event { get; set; } = null!;
}
