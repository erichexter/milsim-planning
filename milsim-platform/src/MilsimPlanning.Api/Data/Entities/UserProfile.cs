namespace MilsimPlanning.Api.Data.Entities;

public class UserProfile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Callsign { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}
