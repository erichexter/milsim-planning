using Microsoft.AspNetCore.Identity;

namespace MilsimPlanning.Api.Data.Entities;

public class AppUser : IdentityUser
{
    public UserProfile Profile { get; set; } = null!;
    public ICollection<EventMembership> EventMemberships { get; set; } = [];
}
