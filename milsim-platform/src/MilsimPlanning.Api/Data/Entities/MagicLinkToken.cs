namespace MilsimPlanning.Api.Data.Entities;

public class MagicLinkToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;   // SHA256 hex of raw token
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }            // null = unused
    public AppUser User { get; set; } = null!;
}
